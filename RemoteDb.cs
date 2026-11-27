using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CargoPlus;

/// <summary>Ошибка, которую вернул API сайта (логин, таблица, данные). Не «нет связи».</summary>
internal sealed class ApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Хостинг / база не ответили (нет связи, 5xx, «занято»). Данные остаются в программе, отправка позже.</summary>
internal sealed class HostDownException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Результат по одному мешку из пачки: ok | dup | letters | noclient | zero | err.</summary>
internal sealed record BagResult(long LocalId, string Status, string? Error, string? Code);

/// <summary>
/// Связь с сервером — через HTTPS API сайта (cargoplus_api.php), а не прямым подключением к MySQL.
/// Подтверждение — логин/пароль базы из настроек программы (сайт подключается ими к базе на localhost).
/// Каждый вызов — один короткий HTTP-запрос; мешки — пачками. Номера мешков считает программа сама.
/// </summary>
internal sealed partial class RemoteDb
{
    public const string DefaultTable = "priem_ob";
    public const string ApiUrl = "https://ozbekcargo.com/gen/bx/cargoplus_api.php";

    public static readonly string[] WrittenColumns =
    [
        "KODID", "KOD", "KILO", "TESLIM_EDEN", "SIGORTA", "NOT1", "TARIH", "track_code", "BOLUM", "sube", "kabul_eden",
    ];

    private static readonly HttpClient Api = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        })
        {
            Timeout = TimeSpan.FromSeconds(40),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CargoPlus", AppRelease.Current.ToString()));
        return client;
    }

    /// <summary>Один запрос к API. Возвращает JSON-ответ с ok=true или бросает ApiException / HostDownException.</summary>
    private static async Task<JsonObject> CallAsync(AppSettings settings, string action, JsonObject body, CancellationToken cancellationToken = default)
    {
        body["a"] = action;
        body["u"] = settings.User.Trim();
        body["p"] = settings.Password;
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await Api.PostAsync(ApiUrl, content, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException or IOException)
        {
            throw new HostDownException(UiText.HostDown, ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonObject? json = null;
            try
            {
                json = JsonNode.Parse(text) as JsonObject;
            }
            catch (JsonException)
            {
            }

            var status = (int)response.StatusCode;
            if (json is null)
            {
                // не JSON: страница ошибки хостинга / защиты — считаем «хостинг не ответил», повторим позже
                throw new HostDownException(UiText.HostDown + " (HTTP " + status + ")");
            }

            if (json["ok"]?.GetValue<bool>() == true)
                return json;

            var code = json["code"]?.GetValue<string>() ?? "err";
            var message = json["e"]?.GetValue<string>() ?? ("HTTP " + status);
            if (code is "busy" or "locked" || status is 429 or >= 500)
                throw new HostDownException(UiText.HostDown + " — " + message);
            throw new ApiException(code, message);
        }
    }

    private static string Stamp(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public async Task<SchemaStatus> EnsureSchemaAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var json = await CallAsync(settings, "schema", new JsonObject { ["table"] = Table(settings) }, cancellationToken);
        if (json["ready"]?.GetValue<bool>() != true)
            throw new InvalidOperationException(UiText.TableMissing(Table(settings)));
        return new SchemaStatus(true, false, false, json["client"]?.GetValue<bool>() == true);
    }

    public async Task<ClientInfo?> FindClientAsync(AppSettings settings, uint kod, CancellationToken cancellationToken = default)
    {
        var json = await CallAsync(settings, "client", new JsonObject { ["kod"] = kod }, cancellationToken);
        return json["c"] is JsonObject c ? ReadClient(c) : null;
    }

    private static ClientInfo ReadClient(JsonObject c) => new()
    {
        Kod = (uint)(c["kod"]?.GetValue<long>() ?? 0),
        Name = c["ad"]?.GetValue<string>()?.Trim() ?? "",
        Surname = c["soyad"]?.GetValue<string>()?.Trim() ?? "",
        Phone = c["tel"]?.GetValue<string>()?.Trim() ?? "",
    };

    private static JsonObject BagJson(BagRecord bag, IReadOnlyList<BagItem> items) => new()
    {
        ["lid"] = bag.LocalId,
        ["kodid"] = bag.KodId,
        ["kod"] = bag.Kod,
        ["kilo"] = bag.Kilo,
        ["teslim"] = bag.TeslimEden,
        ["sigorta"] = bag.Sigorta,
        ["not1"] = bag.Not1,
        ["tarih"] = Stamp(bag.Tarih),
        ["track"] = bag.TrackCode,
        ["bolum"] = bag.Bolum,
        ["sube"] = Clip(bag.Office, 50),
        ["kabul"] = Clip(bag.OperatorName, 50),
        ["items"] = new JsonArray(items.Select(item => (JsonNode)new JsonObject
        {
            ["name"] = item.Name,
            ["qty"] = item.Qty,
            ["unit"] = CargoRules.ItemUnit(item.Unit),
            ["inv"] = item.InvoiceNo,
            ["idate"] = item.InvoiceDate,
        }).ToArray()),
    };

    private static List<BagResult> ReadResults(JsonObject json)
    {
        var list = new List<BagResult>();
        if (json["r"] is not JsonArray results)
            return list;
        foreach (var node in results.OfType<JsonObject>())
        {
            list.Add(new BagResult(
                node["lid"]?.GetValue<long>() ?? 0,
                node["st"]?.GetValue<string>() ?? "err",
                node["e"]?.GetValue<string>(),
                node["code"]?.GetValue<string>()));
        }

        return list;
    }

    /// <summary>Пачка мешков (с составом) одним запросом. Сервер: свой номер + трек уже есть — только состав (повтор безопасен).</summary>
    public async Task<(List<BagResult> Results, List<ClientInfo> Clients)> InsertBagsAsync(
        AppSettings settings, IReadOnlyList<(BagRecord Bag, IReadOnlyList<BagItem> Items)> bags, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["table"] = Table(settings),
            ["bags"] = new JsonArray(bags.Select(x => (JsonNode)BagJson(x.Bag, x.Items)).ToArray()),
        };
        var json = await CallAsync(settings, "bags", body, cancellationToken);
        var clients = new List<ClientInfo>();
        if (json["clients"] is JsonArray array)
            clients.AddRange(array.OfType<JsonObject>().Select(ReadClient));
        return (ReadResults(json), clients);
    }

    /// <summary>Изменённый состав уже отправленных мешков — пачкой.</summary>
    public async Task<List<BagResult>> ReplaceItemsAsync(
        AppSettings settings, IReadOnlyList<(BagRecord Bag, IReadOnlyList<BagItem> Items)> bags, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["bags"] = new JsonArray(bags.Select(x => (JsonNode)BagJson(x.Bag, x.Items)).ToArray()) };
        return ReadResults(await CallAsync(settings, "items", body, cancellationToken));
    }

    public async Task DeleteBagAsync(AppSettings settings, string kodId, string trackCode, CancellationToken cancellationToken = default)
    {
        await CallAsync(settings, "delete", new JsonObject { ["table"] = Table(settings), ["kodid"] = kodId, ["track"] = trackCode }, cancellationToken);
    }

    /// <summary>Последний номер на сервере с этим началом (только подсказка для выравнивания счётчика вверх).</summary>
    public async Task<string?> LatestLetterAsync(AppSettings settings, string prefix, string overflow, CancellationToken cancellationToken = default)
    {
        var json = await CallAsync(settings, "latest_letter", new JsonObject { ["table"] = Table(settings), ["head"] = prefix + overflow }, cancellationToken);
        return json["v"]?.GetValue<string>();
    }

    public async Task<int?> MaxBagNumberAsync(AppSettings settings, int start, int end, CancellationToken cancellationToken = default)
    {
        var json = await CallAsync(settings, "max_number", new JsonObject { ["table"] = Table(settings), ["start"] = start, ["end"] = end }, cancellationToken);
        var value = json["v"];
        return value is null ? null : (int)value.GetValue<long>();
    }

    public async Task WriteTelemetryAsync(AppSettings settings, string json, CancellationToken cancellationToken = default)
    {
        await CallAsync(settings, "telemetry", new JsonObject { ["x"] = JsonNode.Parse(json) }, cancellationToken);
    }

    public static bool IsLinkDown(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HostDownException or TimeoutException or SocketException or TaskCanceledException or HttpRequestException)
                return true;
        }

        return false;
    }

    public static string Human(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is HostDownException)
                return current.Message;
            if (current is ApiException api)
            {
                return api.Code switch
                {
                    "auth" => UiText.BadLogin,
                    "items_table" => UiText.ItemsTableMissing,
                    "invoice_cols" => UiText.InvoiceColumnsMissing,
                    "table" => UiText.TableNameBad,
                    _ => api.Message,
                };
            }
        }

        return exception.Message;
    }

    public static string Table(AppSettings settings)
    {
        var name = string.IsNullOrWhiteSpace(settings.PriemTable) ? DefaultTable : settings.PriemTable.Trim();
        if (name.Length is < 1 or > 64 || name.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '_'))
            throw new InvalidOperationException(UiText.TableNameBad);

        return name;
    }

    private static string Clip(string value, int max)
    {
        value ??= "";
        return value.Length <= max ? value : value[..max];
    }
}
