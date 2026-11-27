using System.Diagnostics;
using System.Net.NetworkInformation;

namespace CargoPlus;

/// <summary>
/// Отправка на сервер (через API сайта) — только когда есть что отправлять.
///  • Интернет проверяется на самом компьютере и запросом к Google (не к нашему хостингу), не чаще раза в минуту.
///  • Хостинг трогаем только с работой: пачка до 25 мешков с составом за запрос, запросы строго по одному.
///  • Хостинг / база не ответили — повтор через 10 с, на экране «сервер не отвечает», программа работает сама.
///  • Мешок с ошибкой (дубль, нет клиента…) — повтор через 10 с. Нет интернета — проверка каждые 4 с, связь вернулась — сразу отправка.
/// Номера мешков здесь не меняются: только отправка готовых записей и выравнивание счётчика вверх при старте.
/// </summary>
internal sealed class SyncService : IDisposable
{
    private const int Batch = 25;
    private static readonly TimeSpan NetCheckEvery = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BagRetry = TimeSpan.FromSeconds(10);
    private static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromSeconds(6) };

    private readonly LocalStore _store;
    private readonly RemoteDb _remote;
    private readonly System.Threading.Timer _timer;
    private readonly Dictionary<long, DateTime> _heldBags = [];
    private Action<SyncState>? _onStatus;
    private int _running;
    private DateTime? _lastOk;
    private int? _latencyMs;
    private double? _kiloBytesPerSecond;
    private DateTime _runStart;
    private long _bytesBefore;
    private DateOnly _archivedOn;
    private int _aligned;
    private bool _internet;
    private DateTime _netCheckedAt = DateTime.MinValue;
    private DateTime _hostHoldUntil = DateTime.MinValue;
    private int _hostFails;
    private string? _hostError;

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

    /// <summary>Новый мешок / чек — попробовать отправить сразу (пауза после сбоя хостинга сохраняется).</summary>
    public void Kick() => _ = Task.Run(RunAsync);

    public void Dispose() => _timer.Dispose();

    private async Task<bool> InternetAsync()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            _internet = false;
            _latencyMs = null;
            return false;
        }

        if (_internet && DateTime.UtcNow - _netCheckedAt < NetCheckEvery)
            return true;
        _netCheckedAt = DateTime.UtcNow;
        try
        {
            var watch = Stopwatch.StartNew();
            using var response = await Probe.GetAsync("http://clients3.google.com/generate_204");
            _latencyMs = (int)watch.ElapsedMilliseconds;
            _internet = true;
        }
        catch (Exception)
        {
            _internet = false;
            _latencyMs = null;
        }

        return _internet;
    }

    private void HostFailed(Exception ex)
    {
        _hostFails++;
        _hostHoldUntil = DateTime.UtcNow.AddSeconds(10);   // сервер не ответил — снова через 10 с
        _hostError = RemoteDb.Human(ex);
        _lastOk = null;
        _netCheckedAt = DateTime.MinValue;   // заодно перепроверить сам интернет
    }

    private void HostOk()
    {
        _hostFails = 0;
        _hostError = null;
        _hostHoldUntil = DateTime.MinValue;
    }

    private bool Held(long localId) => _heldBags.TryGetValue(localId, out var until) && until > DateTime.UtcNow;

    private void Hold(long localId) => _heldBags[localId] = DateTime.UtcNow + BagRetry;

    private async Task RunAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            return;

        _runStart = DateTime.UtcNow;
        _bytesBefore = NicBytes();
        try
        {
            if (_archivedOn != DateOnly.FromDateTime(DateTime.Today))
            {
                _store.ArchiveOlderThan(20);
                _archivedOn = DateOnly.FromDateTime(DateTime.Today);
            }

            var settings = _store.GetSettings();
            if (!settings.IsConfigured)
            {
                Report(new SyncState(false, false, _store.PendingCount(), _store.SyncedCount(), null, _kiloBytesPerSecond, UiText.SyncDb, _lastOk));
                return;
            }

            if (!await InternetAsync())
            {
                Report(State(false, UiText.NoInternet));
                return;
            }

            if (DateTime.UtcNow < _hostHoldUntil)
            {
                Report(State(false, _hostError));
                return;
            }

            // счётчики номеров: один раз за запуск программы поднять до последнего номера на сервере (вниз не опускаются)
            if (Interlocked.Exchange(ref _aligned, 1) == 0)
            {
                try
                {
                    _store.AlignCounters(settings, OperatorName, (prefix, overflow) =>
                        _remote.LatestLetterAsync(settings, prefix, overflow, new CancellationTokenSource(TimeSpan.FromSeconds(6)).Token).GetAwaiter().GetResult());
                }
                catch (Exception ex) when (RemoteDb.IsLinkDown(ex))
                {
                    Interlocked.Exchange(ref _aligned, 0);   // сервер не ответил — выровняем при следующей связи
                }
                catch (Exception)
                {
                }
            }

            var missing = new List<uint>();
            string? rejected = null;
            string? error = null;
            var deadline = DateTime.UtcNow.AddSeconds(45);
            try
            {
                // 1) новые мешки — пачками, пока есть (не дольше 45 с за круг)
                while (DateTime.UtcNow < deadline)
                {
                    var batch = new List<(BagRecord Bag, IReadOnlyList<BagItem> Items)>();
                    foreach (var bag in _store.GetPending(500))
                    {
                        if (batch.Count >= Batch)
                            break;
                        if (Held(bag.LocalId) || !_store.BagExists(bag.LocalId))
                            continue;
                        if (string.IsNullOrWhiteSpace(bag.KodId) || bag.KodId == "0" || bag.Kod == 0)
                        {
                            _store.MarkError(bag.LocalId, UiText.ZeroForbidden);
                            Hold(bag.LocalId);
                            continue;
                        }

                        batch.Add((bag, _store.GetItems(bag.LocalId)));
                    }

                    if (batch.Count == 0)
                        break;

                    var (results, clients) = await _remote.InsertBagsAsync(settings, batch);
                    HostOk();
                    foreach (var client in clients)
                        _store.RememberClient(client);
                    var byId = batch.ToDictionary(x => x.Bag.LocalId, x => x.Bag);
                    foreach (var result in results)
                    {
                        if (!byId.TryGetValue(result.LocalId, out var bag))
                            continue;
                        switch (result.Status)
                        {
                            case "ok":
                                if (_store.BagExists(bag.LocalId))
                                    _store.MarkSynced(bag.LocalId);
                                _heldBags.Remove(bag.LocalId);
                                break;
                            case "noclient":
                                if (!missing.Contains(bag.Kod))
                                    missing.Add(bag.Kod);
                                _store.MarkError(bag.LocalId, UiText.CodeMissingHold(bag.Kod));
                                Hold(bag.LocalId);
                                break;
                            case "dup":
                                _store.MarkError(bag.LocalId, UiText.DuplicateBag(bag.KodId));
                                rejected ??= bag.KodId;
                                Hold(bag.LocalId);
                                break;
                            case "letters":
                                _store.MarkError(bag.LocalId, UiText.LettersNotAccepted);
                                Hold(bag.LocalId);
                                break;
                            case "zero":
                                _store.MarkError(bag.LocalId, UiText.ZeroForbidden);
                                Hold(bag.LocalId);
                                break;
                            default:
                                var message = Message(result);
                                _store.MarkError(bag.LocalId, message);
                                error ??= message;
                                Hold(bag.LocalId);
                                break;
                        }
                    }

                    // мешок без ответа сервера — не крутим его в этом круге, повтор позже
                    foreach (var id in byId.Keys.Where(id => results.All(result => result.LocalId != id)))
                        Hold(id);
                }

                // 2) изменённый состав уже отправленных мешков — пачками
                var changed = _store.BagsWithPendingItems().Where(bag => !Held(bag.LocalId)).Take(Batch)
                    .Select(bag => (bag, (IReadOnlyList<BagItem>)_store.GetItems(bag.LocalId))).ToList();
                if (changed.Count > 0)
                {
                    var results = await _remote.ReplaceItemsAsync(settings, changed);
                    HostOk();
                    foreach (var result in results)
                    {
                        if (result.Status == "ok")
                        {
                            _store.MarkItemsSynced(result.LocalId);
                        }
                        else
                        {
                            var message = Message(result);
                            _store.MarkError(result.LocalId, message);
                            error ??= message;
                            Hold(result.LocalId);
                        }
                    }
                }

                // 3) удалённые мешки
                foreach (var drop in _store.GetDrops(50))
                {
                    await _remote.DeleteBagAsync(settings, drop.KodId, drop.TrackCode);
                    HostOk();
                    _store.ForgetDrop(drop.Id);
                }

                // 4) чеки клиенту — когда все мешки сессии уже на сервере; сайт сам отправит Telegram / e-posta
                foreach (var notice in _store.ReadyNotices(10))
                {
                    try
                    {
                        await _remote.WriteNoticeAsync(settings, notice);
                        HostOk();
                        _store.ForgetNotice(notice.Id);
                    }
                    catch (ApiException)
                    {
                        break;   // сервер не принял — попробуем на следующем круге; мешки это не держит
                    }
                }
            }
            catch (Exception ex) when (RemoteDb.IsLinkDown(ex))
            {
                HostFailed(ex);
                Report(State(false, _hostError, missing));
                return;
            }
            catch (Exception ex)
            {
                error = RemoteDb.Human(ex);
            }

            if (error is null)
                _lastOk = DateTime.Now;
            Report(State(true, error ?? (rejected is not null ? UiText.DuplicateBag(rejected) : null), missing, rejected));
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

    private static string Message(BagResult result) => result.Code switch
    {
        "items_table" => UiText.ItemsTableMissing,
        "invoice_cols" => UiText.InvoiceColumnsMissing,
        _ => result.Error ?? "Hata",
    };

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
