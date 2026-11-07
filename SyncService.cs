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

    public SyncService(LocalStore store, RemoteDb remote)
    {
        _store = store;
        _remote = remote;
        _timer = new System.Threading.Timer(_ => _ = RunAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start(Action<SyncState> onStatus)
    {
        _onStatus = onStatus;
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(4));
    }

    public void Kick() => _ = RunAsync();

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
                foreach (var bag in pending)
                {
                    try
                    {
                        await _remote.InsertBagAsync(settings, bag);
                        _store.MarkSynced(bag.LocalId);
                    }
                    catch (Exception ex)
                    {
                        var message = RemoteDb.Human(ex);
                        _store.MarkError(bag.LocalId, message);
                        _lastOk = null;
                        Report(State(false, message));
                        return;
                    }
                }

                _lastOk = DateTime.Now;
                Report(State(true, null));
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

    private SyncState State(bool online, string? error) => new(
        true,
        online,
        _store.PendingCount(),
        _store.SyncedCount(),
        _latencyMs,
        _kiloBytesPerSecond,
        error,
        _lastOk);

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
