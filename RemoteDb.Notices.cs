using MySqlConnector;

namespace CargoPlus;

/// <summary>
/// Чек приёма клиенту: одна строка в kabul_notice на сервере (сайт сам отправит Telegram / e-posta на трёх языках)
/// и короткий сигнал kabul_kick.php. Каналы клиента — для ручной кнопки «Fişi müşteriye gönder».
/// </summary>
internal sealed partial class RemoteDb
{
    public const string KickUrl = "https://ozbekcargo.com/gen/bx/kabul_kick.php";
    private static bool _noticeTable;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task WriteNoticeAsync(AppSettings settings, ReceiptNotice notice, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        if (!_noticeTable)
        {
            await using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS kabul_notice (
                    id INT UNSIGNED NOT NULL AUTO_INCREMENT,
                    kod INT UNSIGNED NOT NULL,
                    session_id VARCHAR(64) NOT NULL DEFAULT '',
                    bags TEXT NULL,
                    n INT NOT NULL DEFAULT 0,
                    kg DECIMAL(12,1) NOT NULL DEFAULT 0,
                    store VARCHAR(160) NOT NULL DEFAULT '',
                    store_phone VARCHAR(40) NOT NULL DEFAULT '',
                    track VARCHAR(40) NOT NULL DEFAULT '',
                    office VARCHAR(80) NOT NULL DEFAULT '',
                    operator VARCHAR(80) NOT NULL DEFAULT '',
                    ch VARCHAR(8) NOT NULL DEFAULT 'all',
                    manual TINYINT NOT NULL DEFAULT 0,
                    receipt MEDIUMBLOB NULL,
                    qr MEDIUMBLOB NULL,
                    created DATETIME NOT NULL,
                    queued TINYINT NOT NULL DEFAULT 0,
                    PRIMARY KEY (id),
                    KEY ix_queued (queued),
                    KEY ix_created (created)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
            _noticeTable = true;
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO kabul_notice (kod, session_id, bags, n, kg, store, store_phone, track, office, operator, ch, manual, receipt, qr, created)
            VALUES (@kod, @session, @bags, @n, @kg, @store, @phone, @track, @office, @operator, @ch, @manual, @receipt, @qr, @created)
            """;
        insert.Parameters.AddWithValue("@kod", notice.Kod);
        insert.Parameters.AddWithValue("@session", Cut(notice.SessionId, 64));
        insert.Parameters.AddWithValue("@bags", notice.Bags);
        insert.Parameters.AddWithValue("@n", notice.Count);
        insert.Parameters.AddWithValue("@kg", notice.Kilo);
        insert.Parameters.AddWithValue("@store", Cut(notice.Store, 160));
        insert.Parameters.AddWithValue("@phone", Cut(notice.StorePhone, 40));
        insert.Parameters.AddWithValue("@track", Cut(notice.Track, 40));
        insert.Parameters.AddWithValue("@office", Cut(notice.Office, 80));
        insert.Parameters.AddWithValue("@operator", Cut(notice.Operator, 80));
        insert.Parameters.AddWithValue("@ch", notice.Channel is "tg" or "mail" ? notice.Channel : "all");
        insert.Parameters.AddWithValue("@manual", notice.Manual ? 1 : 0);
        insert.Parameters.Add("@receipt", MySqlDbType.MediumBlob).Value = notice.Receipt.Length > 0 ? notice.Receipt : DBNull.Value;
        insert.Parameters.Add("@qr", MySqlDbType.MediumBlob).Value = notice.Qr.Length > 0 ? notice.Qr : DBNull.Value;
        insert.Parameters.AddWithValue("@created", notice.Created);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Сигнал сайту: «есть новый чек». Ошибка не важна — чек уйдёт со следующим сигналом.</summary>
    public static async Task KickAsync()
    {
        try
        {
            using var response = await Http.GetAsync(KickUrl);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Есть ли у клиента подтверждённый Telegram и подтверждённая e-posta.</summary>
    public async Task<(bool Telegram, bool Mail)> ClientChannelsAsync(AppSettings settings, uint kod, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        var telegram = false;
        var mail = false;
        try
        {
            await using var tg = connection.CreateCommand();
            tg.CommandText = """
                SELECT 1 FROM tg_clients tc JOIN tg_links tl ON tl.id = tc.link_id AND tl.blocked = 0 AND tl.chat_id > 0
                WHERE tc.client_id = @kod LIMIT 1
                """;
            tg.Parameters.AddWithValue("@kod", kod);
            telegram = await tg.ExecuteScalarAsync(cancellationToken) is not null;
        }
        catch (MySqlException)
        {
        }

        try
        {
            await using var em = connection.CreateCommand();
            em.CommandText = """
                SELECT 1 FROM client c JOIN client_email_verify v ON v.client_id = c.id AND v.verified_at IS NOT NULL
                WHERE c.id = @kod AND c.email LIKE '%_@_%._%' LIMIT 1
                """;
            em.Parameters.AddWithValue("@kod", kod);
            mail = await em.ExecuteScalarAsync(cancellationToken) is not null;
        }
        catch (MySqlException)
        {
        }

        return (telegram, mail);
    }

    private static string Cut(string value, int max)
    {
        value ??= "";
        return value.Length <= max ? value : value[..max];
    }
}
