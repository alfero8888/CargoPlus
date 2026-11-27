using System.Globalization;
using System.Net.Sockets;
using MySqlConnector;

namespace CargoPlus;

internal sealed partial class RemoteDb
{
    public const string DefaultTable = "priem_ob";

    public static readonly string[] WrittenColumns =
    [
        "KODID", "KOD", "KILO", "TESLIM_EDEN", "SIGORTA", "NOT1", "TARIH", "track_code", "BOLUM", "sube", "kabul_eden",
    ];

    private string? _schemaKey;

    public async Task<SchemaStatus> EnsureSchemaAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        var status = await EnsureSchemaAsync(connection, settings, cancellationToken);
        _schemaKey = Key(settings);
        return status;
    }

    public async Task<int> MeasureLatencyAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
        watch.Stop();
        return (int)watch.ElapsedMilliseconds;
    }

    public async Task<ClientInfo?> FindClientAsync(AppSettings settings, uint kod, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CAST(id AS CHAR), IFNULL(ad, ''), IFNULL(soyad, ''), IFNULL(tel_1, '')
            FROM client
            WHERE id = @id
            LIMIT 1
            """;
        command.Parameters.Add("@id", MySqlDbType.UInt32).Value = kod;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ClientInfo
        {
            Kod = kod,
            Name = reader.GetString(1).Trim(),
            Surname = reader.GetString(2).Trim(),
            Phone = reader.GetString(3).Trim(),
        };
    }

    public async Task DeleteBagAsync(AppSettings settings, string kodId, string trackCode, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var items = connection.CreateCommand())
            {
                items.Transaction = transaction;
                items.CommandText = "DELETE FROM bag_items WHERE kodid = @kodid";
                items.Parameters.Add("@kodid", MySqlDbType.VarChar, 12).Value = kodId;
                await items.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = $"""
                    DELETE FROM `{Table(settings)}`
                    WHERE KODID = @kodid AND track_code = @track
                    """;
                command.Parameters.Add("@kodid", MySqlDbType.VarChar, 12).Value = kodId;
                command.Parameters.Add("@track", MySqlDbType.VarChar).Value = trackCode;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ItemsSchemaMessage(ex) is string message)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(message);
        }
    }

    public async Task InsertBagAsync(AppSettings settings, BagRecord bag, IReadOnlyList<BagItem> items, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bag.KodId) || bag.KodId == "0" || bag.Kod == 0)
            throw new InvalidOperationException(UiText.ZeroForbidden);

        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        if (_schemaKey != Key(settings))
            await EnsureSchemaAsync(connection, settings, cancellationToken);

        var table = Table(settings);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var exists = false;
            try
            {
                await using var probe = connection.CreateCommand();
                probe.Transaction = transaction;
                probe.CommandText = $"""
                    SELECT 1 FROM `{table}` WHERE KODID = @kodid AND track_code = @track LIMIT 1
                    """;
                probe.Parameters.Add("@kodid", MySqlDbType.VarChar, 12).Value = bag.KodId;
                probe.Parameters.Add("@track", MySqlDbType.VarChar).Value = bag.TrackCode;
                exists = await probe.ExecuteScalarAsync(cancellationToken) is not null;
            }
            catch (Exception ex) when (IsLetterRejected(ex))
            {
            }

            if (!exists)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"""
                    INSERT INTO `{table}` (KODID, KOD, KILO, TESLIM_EDEN, SIGORTA, NOT1, TARIH, track_code, BOLUM, sube, kabul_eden)
                    VALUES (@kodid, @kod, @kilo, @teslim, @sigorta, @not1, @tarih, @track, @bolum, @sube, @kabul)
                    """;
                command.Parameters.Add("@kodid", MySqlDbType.VarChar, 12).Value = bag.KodId;
                command.Parameters.Add("@kod", MySqlDbType.UInt32).Value = bag.Kod;
                command.Parameters.Add("@kilo", MySqlDbType.VarChar).Value = bag.Kilo;
                command.Parameters.Add("@teslim", MySqlDbType.VarChar).Value = bag.TeslimEden;
                command.Parameters.Add("@sigorta", MySqlDbType.Int32).Value = bag.Sigorta;
                command.Parameters.Add("@not1", MySqlDbType.VarChar).Value = bag.Not1;
                command.Parameters.Add("@tarih", MySqlDbType.DateTime).Value = DateTime.SpecifyKind(bag.Tarih, DateTimeKind.Unspecified);
                command.Parameters.Add("@track", MySqlDbType.VarChar).Value = bag.TrackCode;
                command.Parameters.Add("@bolum", MySqlDbType.VarChar).Value = bag.Bolum;
                command.Parameters.Add("@sube", MySqlDbType.VarChar).Value = Clip(bag.Office, 50);
                command.Parameters.Add("@kabul", MySqlDbType.VarChar).Value = Clip(bag.OperatorName, 50);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await WriteItemsAsync(connection, transaction, bag, items, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _schemaKey = Key(settings);
        }
        catch (Exception ex) when (ItemsSchemaMessage(ex) is string message)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(message);
        }
    }

    public async Task ReplaceItemsAsync(AppSettings settings, BagRecord bag, IReadOnlyList<BagItem> items, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await WriteItemsAsync(connection, transaction, bag, items, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ItemsSchemaMessage(ex) is string message)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(message);
        }
    }

    private static async Task WriteItemsAsync(MySqlConnection connection, MySqlTransaction transaction, BagRecord bag, IReadOnlyList<BagItem> items, CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM bag_items WHERE kodid = @kodid";
            delete.Parameters.Add("@kodid", MySqlDbType.VarChar, 12).Value = bag.KodId;
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        var line = 1;
        foreach (var item in items)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO bag_items (kodid, line, name, qty, unit, invoice_no, invoice_date, track_code, created_at, created_by)
                VALUES (@kodid, @line, @name, @qty, @unit, @invoice, @idate, @track, @created, @by)
                """;
            insert.Parameters.Add("@kodid", MySqlDbType.VarChar, 12).Value = bag.KodId;
            insert.Parameters.Add("@line", MySqlDbType.Int16).Value = line;
            insert.Parameters.Add("@name", MySqlDbType.VarChar, 120).Value = item.Name;
            insert.Parameters.Add("@qty", MySqlDbType.Decimal).Value = decimal.Parse(item.Qty, CultureInfo.InvariantCulture);
            insert.Parameters.Add("@unit", MySqlDbType.VarChar, 10).Value = CargoRules.ItemUnit(item.Unit);
            insert.Parameters.Add("@invoice", MySqlDbType.VarChar, 40).Value = item.InvoiceNo.Length == 0 ? DBNull.Value : item.InvoiceNo;
            insert.Parameters.Add("@idate", MySqlDbType.Date).Value = item.InvoiceDate.Length == 0
                ? DBNull.Value
                : DateTime.ParseExact(item.InvoiceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            insert.Parameters.Add("@track", MySqlDbType.VarChar, 20).Value = bag.TrackCode;
            insert.Parameters.Add("@created", MySqlDbType.DateTime).Value = DateTime.SpecifyKind(bag.Tarih, DateTimeKind.Unspecified);
            insert.Parameters.Add("@by", MySqlDbType.VarChar, 50).Value = Clip(bag.OperatorName, 50);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            line++;
        }
    }

    private static string? ItemsSchemaMessage(Exception exception)
    {
        if (IsMissingItemsTable(exception))
            return UiText.ItemsTableMissing;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql
                && mysql.ErrorCode == MySqlErrorCode.BadFieldError
                && (mysql.Message.Contains("invoice_no", StringComparison.OrdinalIgnoreCase)
                    || mysql.Message.Contains("invoice_date", StringComparison.OrdinalIgnoreCase)))
                return UiText.InvoiceColumnsMissing;
        }

        return null;
    }

    public static bool IsMissingItemsTable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql
                && mysql.ErrorCode == MySqlErrorCode.NoSuchTable
                && mysql.Message.Contains("bag_items", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public async Task<string?> LatestLetterAsync(AppSettings settings, string prefix, string overflow, CancellationToken cancellationToken = default)
    {
        var head = prefix + overflow;
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 4;
        command.CommandText = $"""
            SELECT MAX(CAST(SUBSTRING(KODID, @from) AS UNSIGNED))
            FROM `{Table(settings)}`
            WHERE KODID LIKE @p
              AND SUBSTRING(KODID, @from) REGEXP '^[0-9]+$'
            """;
        command.Parameters.Add("@p", MySqlDbType.VarChar, 12).Value = head + "%";
        command.Parameters.Add("@from", MySqlDbType.Int32).Value = head.Length + 1;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
            return null;
        var number = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        return number < 1 ? null : head + number.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<int?> MaxBagNumberAsync(AppSettings settings, int start, int end, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT MAX(KODID) FROM `{Table(settings)}` WHERE KODID BETWEEN @start AND @end";
        command.Parameters.Add("@start", MySqlDbType.Int32).Value = start;
        command.Parameters.Add("@end", MySqlDbType.Int32).Value = end;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
            return null;
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static bool IsLinkDown(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException or SocketException or TaskCanceledException)
                return true;
            if (current is MySqlException mysql)
            {
                if (mysql.ErrorCode is MySqlErrorCode.UnableToConnectToHost or MySqlErrorCode.ConnectionCountError or MySqlErrorCode.AbortingConnection)
                    return true;
                if (mysql.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                    || mysql.Message.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }
        }

        return false;
    }

    public static bool IsLetterRejected(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("greater than zero", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Incorrect integer", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Truncated incorrect", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Data truncated", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsDuplicate(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql && mysql.ErrorCode is MySqlErrorCode.DuplicateKeyEntry or MySqlErrorCode.DuplicateKey)
                return true;
        }

        return false;
    }

    public static string Human(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql)
            {
                return mysql.ErrorCode switch
                {
                    MySqlErrorCode.UnableToConnectToHost => UiText.NoServer,
                    MySqlErrorCode.AccessDenied => UiText.BadLogin,
                    MySqlErrorCode.UnknownDatabase => UiText.BadDatabase,
                    MySqlErrorCode.NoSuchTable => UiText.BadTable,
                    _ when mysql.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => UiText.Timeout,
                    _ => mysql.Message,
                };
            }

            if (current is TimeoutException)
                return UiText.Timeout;
            if (current is SocketException)
                return UiText.NoServer;
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

    private async Task<SchemaStatus> EnsureSchemaAsync(MySqlConnection connection, AppSettings settings, CancellationToken cancellationToken)
    {
        var table = Table(settings);
        var ready = true;
        try
        {
            await using var probe = connection.CreateCommand();
            var columns = string.Join(", ", WrittenColumns.Select(column => $"`{column}`"));
            probe.CommandText = $"SELECT {columns} FROM `{table}` LIMIT 0";
            await probe.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.ErrorCode is MySqlErrorCode.BadFieldError or MySqlErrorCode.NoSuchTable)
        {
            ready = false;
        }

        var clientExists = true;
        try
        {
            await using var client = connection.CreateCommand();
            client.CommandText = "SELECT id, ad, soyad, tel_1 FROM client LIMIT 0";
            await client.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            clientExists = false;
        }

        if (!ready)
            throw new InvalidOperationException(UiText.TableMissing(table));

        return new SchemaStatus(true, false, false, clientExists);
    }

    public async Task WriteTelemetryAsync(AppSettings settings, string json, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS cargo_telemetry_logs (
                    id INT UNSIGNED NOT NULL AUTO_INCREMENT,
                    branch_name VARCHAR(120) NOT NULL DEFAULT '',
                    hostname VARCHAR(120) NOT NULL DEFAULT '',
                    ip_address VARCHAR(64) NOT NULL DEFAULT '',
                    app_version VARCHAR(32) NOT NULL DEFAULT '',
                    cpu_usage DECIMAL(5,1) NOT NULL DEFAULT 0,
                    ram_usage DECIMAL(8,1) NOT NULL DEFAULT 0,
                    sys_cpu DECIMAL(5,1) NOT NULL DEFAULT 0,
                    free_ram_mb INT NOT NULL DEFAULT 0,
                    free_disk_gb DECIMAL(8,1) NOT NULL DEFAULT 0,
                    top_cpu_processes TEXT NULL,
                    top_ram_processes TEXT NULL,
                    errors_log TEXT NULL,
                    created_at DATETIME NOT NULL,
                    PRIMARY KEY (id),
                    KEY branch_time (branch_name, created_at)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var packet = document.RootElement;
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO cargo_telemetry_logs (
                branch_name, hostname, ip_address, app_version, cpu_usage, ram_usage,
                sys_cpu, free_ram_mb, free_disk_gb, top_cpu_processes, top_ram_processes, errors_log, created_at
            ) VALUES (
                @branch, @host, @ip, @version, @cpu, @ram, @sys, @freeRam, @disk, @topCpu, @topRam, @errors, NOW()
            )
            """;
        insert.Parameters.Add("@branch", MySqlDbType.VarChar).Value = Text(packet, "branch", 120);
        insert.Parameters.Add("@host", MySqlDbType.VarChar).Value = Text(packet, "hostname", 120);
        insert.Parameters.Add("@ip", MySqlDbType.VarChar).Value = Text(packet, "ip", 64);
        insert.Parameters.Add("@version", MySqlDbType.VarChar).Value = Text(packet, "version", 32);
        insert.Parameters.Add("@cpu", MySqlDbType.Double).Value = Num(packet, "cpu");
        insert.Parameters.Add("@ram", MySqlDbType.Double).Value = Num(packet, "ram");
        insert.Parameters.Add("@sys", MySqlDbType.Double).Value = Num(packet, "sysCpu");
        insert.Parameters.Add("@freeRam", MySqlDbType.Int32).Value = (int)Num(packet, "freeRam");
        insert.Parameters.Add("@disk", MySqlDbType.Double).Value = Num(packet, "disk");
        insert.Parameters.Add("@topCpu", MySqlDbType.Text).Value = Raw(packet, "topCpu");
        insert.Parameters.Add("@topRam", MySqlDbType.Text).Value = Raw(packet, "topRam");
        insert.Parameters.Add("@errors", MySqlDbType.Text).Value = Raw(packet, "errors");
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Text(System.Text.Json.JsonElement packet, string name, int max)
    {
        var text = packet.TryGetProperty(name, out var value) ? value.ToString() : "";
        text = text.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static double Num(System.Text.Json.JsonElement packet, string name)
    {
        if (!packet.TryGetProperty(name, out var value))
            return 0;
        return value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDouble(out var number) ? number : 0;
    }

    private static string Raw(System.Text.Json.JsonElement packet, string name) =>
        packet.TryGetProperty(name, out var value) ? value.GetRawText() : "[]";

    private static string Clip(string value, int max)
    {
        var text = value.Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static MySqlConnection Create(AppSettings settings)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = settings.Host.Trim(),
            Port = (uint)settings.Port,
            Database = settings.Database.Trim(),
            UserID = settings.User.Trim(),
            Password = settings.Password,
            SslMode = settings.UseSsl ? MySqlSslMode.Preferred : MySqlSslMode.None,
            ConnectionTimeout = 8,
            DefaultCommandTimeout = 15,
            CharacterSet = "utf8mb4",
            AllowUserVariables = true,
            Pooling = true,
        };
        if (!settings.UseSsl)
            builder.AllowPublicKeyRetrieval = true;
        return new MySqlConnection(builder.ConnectionString);
    }

    private static string Key(AppSettings settings) =>
        $"{settings.Host.Trim()}|{settings.Port}|{settings.Database.Trim()}|{settings.User.Trim()}|{Table(settings)}";
}
