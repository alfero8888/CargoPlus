namespace CargoPlus;

internal static class AccessLevels
{
    public const string Admin = "Главный администратор";
    public const string Operator = "Оператор";

    public static readonly string[] All = [Admin, Operator];
}

internal sealed class AppUser
{
    public required long Id { get; init; }
    public required string Name { get; init; }
    public required string AccessLevel { get; init; }
    public required string Category { get; init; }
    public bool IsAdmin => AccessLevel == AccessLevels.Admin;
}

internal sealed class ClientInfo
{
    public required uint Kod { get; init; }
    public required string Name { get; init; }
    public required string Surname { get; init; }
    public required string Phone { get; init; }

    public string FullName
    {
        get
        {
            var text = $"{Name} {Surname}".Trim();
            return text.Length == 0 ? "—" : text;
        }
    }
}

internal sealed class SessionInfo
{
    public required string Id { get; init; }
    public required uint Kod { get; init; }
    public required string ClientName { get; init; }
    public required string ClientSurname { get; init; }
    public required string ClientPhone { get; init; }
    public required string StorePhone { get; init; }
    public required string StoreName { get; init; }
    public required string OperatorName { get; init; }
    public string TrackCode { get; init; } = "";

    public string ClientFullName
    {
        get
        {
            var text = $"{ClientName} {ClientSurname}".Trim();
            return text.Length == 0 ? "—" : text;
        }
    }
}

internal sealed class BagRecord
{
    public required long LocalId { get; init; }
    public required string SessionId { get; init; }
    public required int KodId { get; init; }
    public required uint Kod { get; init; }
    public required string Kilo { get; init; }
    public required string TeslimEden { get; init; }
    public required int Sigorta { get; init; }
    public required string Not1 { get; init; }
    public required DateTime Tarih { get; init; }
    public required string TrackCode { get; init; }
    public required string Bolum { get; init; }
    public required string StorePhone { get; init; }
    public required string StoreName { get; init; }
    public required bool Synced { get; init; }
    public string? SyncError { get; init; }
}

internal sealed class AppSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; }
    public string LabelPrinter { get; set; } = "";
    public string ReceiptPrinter { get; set; } = "";
    public string Company { get; set; } = "Baraka Logistic Cargo";
    public string Branch { get; set; } = "Kuran Hotel";
    public string Language { get; set; } = "tr";
    public int BagStart { get; set; } = 200_000;
    public int BagEnd { get; set; } = 600_000;
    public int BagNext { get; set; } = 200_000;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(Database)
        && !string.IsNullOrWhiteSpace(User);

    public int Remaining => BagNext > BagEnd ? 0 : BagEnd - BagNext + 1;
}

internal readonly record struct AddBagResult(SessionInfo Session, BagRecord Bag, int Remaining);

internal sealed record SyncState(
    bool Configured,
    bool Online,
    int Pending,
    int Synced,
    int? LatencyMs,
    double? KiloBytesPerSecond,
    string? Error,
    DateTime? LastOk);

internal sealed record SchemaStatus(bool TableExisted, bool CreatedTable, bool AddedTrack, bool ClientTableExists);
