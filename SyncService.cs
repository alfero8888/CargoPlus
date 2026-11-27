using System.Net.NetworkInformation;

namespace CargoPlus;

internal sealed class SyncService : IDisposable
{
    private readonly LocalStore _store;
    private readonly RemoteDb _remote;
    private readonly System.Threading.Timer _timer;
    private Action<SyncState>? _onStatus;
    private int _running;
    private DateTime? _lastOk;
    private int? _latencyMs;
    private double? _kiloBytesPerSecond;
    private DateTime _runStart;
    private long _bytesBefore;
    private DateOnly _archivedOn;
    private int _aligned;

    public string OperatorName { get; set; } = "";

    public SyncService(LocalStore store, RemoteDb remote)
    {
        _store = store;
        _remote = remote;
        _timer = new System.Threading.Timer(_ => Kick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(Action<SyncState> onStatus)
    {
        _onStatus = onStatus;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(4));
    }

    public void Kick() => _ = Task.Run(RunAsync);

    public void Dispose() => _timer.Dispose();

    private async Task RunAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;

        var started = DateTime.UtcNow;
        var bytesBefore = NicBytes();
        _runStart = started;
        _bytesBefore = bytesBefore;
        try
        {
            if (_archivedOn != DateOnly.FromDateTime(DateTime.Today))
            {
                _store.ArchiveOlderThan(20);
                _archivedOn = DateOnly.FromDateTime(DateTime.Today);
            }

            var settings = _store.GetSettings();
            if (settings.IsConfigured && Interlocked.Exchange(ref _aligned, 1) == 0)
            {
                try
                {
                    _store.AlignCounters(settings, OperatorName, (prefix, overflow) =>
                        _remote.LatestLetterAsync(settings, prefix, overflow).GetAwaiter().GetResult());
                }
                catch (Exception)
                {
                }
            }

            var pendingCount = _store.PendingCount();
            var synced = _store.SyncedCount();
            if (!settings.IsConfigured)
            {
                Report(new SyncState(false, false, pendingCount, synced, null, _kiloBytesPerSecond, UiText.SyncDb, _lastOk));
                return;
            }

            try
            {
                _latencyMs = await _remote.MeasureLatencyAsync(settings);
            }
            catch (Exception ex)
            {
                _latencyMs = null;
                Report(State(false, RemoteDb.Human(ex)));
                return;
            }

            var pending = _store.GetPending(30);
            try
            {
                await _remote.EnsureSchemaAsync(settings);
                var missing = new List<uint>();
                var looked = new Dictionary<uint, ClientInfo?>();
                string? rejected = null;
                foreach (var bag in pending)
                {
                    try
                    {
                        if (!_store.BagExists(bag.LocalId))
                            continue;
                        if (string.IsNullOrWhiteSpace(bag.KodId) || bag.KodId == "0" || bag.Kod == 0)
                        {
                            _store.MarkError(bag.LocalId, UiText.ZeroForbidden);
                            continue;
                        }
                        if (!looked.TryGetValue(bag.Kod, out var client))
                        {
                            client = await _remote.FindClientAsync(settings, bag.Kod);
                            looked[bag.Kod] = client;
                            if (client is not null)
                                _store.RememberClient(client);
                        }

                        if (client is null)
                        {
                            if (!missing.Contains(bag.Kod))
                                missing.Add(bag.Kod);
                            _store.MarkError(bag.LocalId, UiText.CodeMissingHold(bag.Kod));
                            continue;
                        }

                        await _remote.InsertBagAsync(settings, bag, _store.GetItems(bag.LocalId));
                        if (_store.BagExists(bag.LocalId))
                            _store.MarkSynced(bag.LocalId);
                    }
                    catch (Exception ex) when (RemoteDb.IsLinkDown(ex))
                    {
                        _lastOk = null;
                        Report(State(false, RemoteDb.Human(ex), missing));
                        return;
                    }
                    catch (Exception ex) when (RemoteDb.IsLetterRejected(ex) && bag.KodId.Any(char.IsLetter))
                    {
                        _store.MarkError(bag.LocalId, UiText.LettersNotAccepted);
                        continue;
                    }
                    catch (Exception ex) when (RemoteDb.IsDuplicate(ex))
                    {
                        var message = UiText.DuplicateBag(bag.KodId);
                        _store.MarkError(bag.LocalId, message);
                        rejected ??= bag.KodId;
                    }
                    catch (Exception ex)
                    {
                        var message = RemoteDb.Human(ex);
                        _store.MarkError(bag.LocalId, message);
                        _lastOk = null;
                        Report(State(false, message, missing, rejected));
                        return;
                    }
                }

                foreach (var bag in _store.BagsWithPendingItems())
                {
                    try
                    {
                        await _remote.ReplaceItemsAsync(settings, bag, _store.GetItems(bag.LocalId));
                        _store.MarkItemsSynced(bag.LocalId);
                    }
                    catch (Exception ex) when (RemoteDb.IsLinkDown(ex))
                    {
                        _lastOk = null;
                        Report(State(false, RemoteDb.Human(ex), missing));
                        return;
                    }
                    catch (Exception ex)
                    {
                        var message = RemoteDb.Human(ex);
                        _store.MarkError(bag.LocalId, message);
                        _lastOk = null;
                        Report(State(false, message, missing, rejected));
                        return;
                    }
                }

                foreach (var drop in _store.GetDrops(50))
                {
                    try
                    {
                        await _remote.DeleteBagAsync(settings, drop.KodId, drop.TrackCode);
                        _store.ForgetDrop(drop.Id);
                    }
                    catch (Exception ex)
                    {
                        var message = RemoteDb.Human(ex);
                        _lastOk = null;
                        Report(State(false, message, missing));
                        return;
                    }
                }

                // чеки приёма клиенту: только когда все мешки сессии уже на сервере (после офлайна — тоже)
                var kicked = false;
                foreach (var notice in _store.ReadyNotices(10))
                {
                    try
                    {
                        await _remote.WriteNoticeAsync(settings, notice);
                        _store.ForgetNotice(notice.Id);
                        kicked = true;
                    }
                    catch (Exception ex) when (RemoteDb.IsLinkDown(ex))
                    {
                        _lastOk = null;
                        Report(State(false, RemoteDb.Human(ex), missing));
                        return;
                    }
                    catch (Exception)
                    {
                        break;   // сервер не принял — попробуем на следующем круге, мешки это не держит
                    }
                }

                if (kicked)
                    _ = RemoteDb.KickAsync();

                _lastOk = DateTime.Now;
                Report(State(true, rejected is not null ? UiText.DuplicateBag(rejected) : null, missing, rejected));
            }
            catch (Exception ex)
            {
                Report(State(false, RemoteDb.Human(ex)));
            }
        }
        catch (Exception ex)
        {
            Report(new SyncState(true, false, 0, 0, _latencyMs, _kiloBytesPerSecond, ex.Message, _lastOk));
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private SyncState State(bool online, string? error, IReadOnlyList<uint>? missing = null, string? rejected = null) => new(
        true,
        online,
        _store.PendingCount(),
        _store.SyncedCount(),
        _latencyMs,
        _kiloBytesPerSecond,
        error,
        _lastOk,
        missing,
        rejected);

    private void Report(SyncState state)
    {
        var seconds = Math.Max(0.2, (DateTime.UtcNow - _runStart).TotalSeconds);
        var delta = NicBytes() - _bytesBefore;
        if (delta > 0)
            _kiloBytesPerSecond = delta / 1024d / seconds;
        _onStatus?.Invoke(state with { KiloBytesPerSecond = _kiloBytesPerSecond });
    }

    private static long NicBytes()
    {
        long total = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
                continue;
            if (adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            var stats = adapter.GetIPStatistics();
            total += stats.BytesReceived + stats.BytesSent;
        }

        return total;
    }
}
