using System.Globalization;
using System.Net.Sockets;
using MySqlConnector;

namespace CargoPlus;

internal sealed class RemoteDb
{
    internal const string PriemTable = "c_priem_ob";

    private string? _schemaKey;

    public async Task<SchemaStatus> EnsureSchemaAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        var status = await EnsureSchemaAsync(connection, cancellationToken);
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

    public async Task InsertBagAsync(AppSettings settings, BagRecord bag, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        if (_schemaKey != Key(settings))
            await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO `{PriemTable}` (KODID, KOD, KILO, TESLIM_EDEN, SIGORTA, NOT1, TARIH, track_code, BOLUM)
            VALUES (@kodid, @kod, @kilo, @teslim, @sigorta, @not1, @tarih, @track, @bolum)
            """;
        command.Parameters.Add("@kodid", MySqlDbType.Int32).Value = bag.KodId;
        command.Parameters.Add("@kod", MySqlDbType.UInt32).Value = bag.Kod;
        command.Parameters.Add("@kilo", MySqlDbType.VarChar).Value = bag.Kilo;
        command.Parameters.Add("@teslim", MySqlDbType.VarChar).Value = bag.TeslimEden;
        command.Parameters.Add("@sigorta", MySqlDbType.Int32).Value = bag.Sigorta;
        command.Parameters.Add("@not1", MySqlDbType.VarChar).Value = bag.Not1;
        command.Parameters.Add("@tarih", MySqlDbType.DateTime).Value = DateTime.SpecifyKind(bag.Tarih, DateTimeKind.Unspecified);
        command.Parameters.Add("@track", MySqlDbType.VarChar).Value = bag.TrackCode;
        command.Parameters.Add("@bolum", MySqlDbType.VarChar).Value = bag.Bolum;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _schemaKey = Key(settings);
    }

    public async Task<int?> MaxBagNumberAsync(AppSettings settings, int start, int end, CancellationToken cancellationToken = default)
    {
        await using var connection = Create(settings);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT MAX(KODID) FROM `{PriemTable}` WHERE KODID BETWEEN @start AND @end";
        command.Parameters.Add("@start", MySqlDbType.Int32).Value = start;
        command.Parameters.Add("@end", MySqlDbType.Int32).Value = end;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
            return null;
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static string Human(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException mysql)
            {
                return mysql.ErrorCode switch
                {
                    MySqlErrorCode.UnableToConnectToHost => "Нет связи с сервером.",
                    MySqlErrorCode.AccessDenied => "Сервер отклонил логин или пароль.",
                    MySqlErrorCode.UnknownDatabase => "Такой базы на сервере нет.",
                    MySqlErrorCode.NoSuchTable => "На сервере нет нужной таблицы.",
                    _ when mysql.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => "Сервер не ответил вовремя.",
                    _ => mysql.Message,
                };
            }

            if (current is TimeoutException)
                return "Сервер не ответил вовремя.";
            if (current is SocketException)
                return "Нет связи с сервером.";
        }

        return exception.Message;
    }

    private async Task<SchemaStatus> EnsureSchemaAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var tableExists = true;
        var trackExists = true;
        try
        {
            await using var probe = connection.CreateCommand();
            probe.CommandText = $"SELECT track_code FROM `{PriemTable}` LIMIT 0";
            await probe.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.BadFieldError)
        {
            trackExists = false;
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            tableExists = false;
            trackExists = false;
        }

        var created = false;
        var addedTrack = false;
        if (!tableExists)
        {
            await using var create = connection.CreateCommand();
            create.CommandText = $"""
                CREATE TABLE IF NOT EXISTS `{PriemTable}` (
                  `ID` int(11) NOT NULL AUTO_INCREMENT,
                  `KODID` int(11) DEFAULT NULL,
                  `KOD` int(11) UNSIGNED DEFAULT NULL,
                  `KILO` varchar(11) DEFAULT NULL,
                  `TESLIM_EDEN` varchar(25) DEFAULT NULL,
                  `SIGORTA` int(11) DEFAULT NULL,
                  `NOT1` varchar(100) DEFAULT NULL,
                  `TARIH` timestamp NULL DEFAULT NULL,
                  `rep` varchar(30) DEFAULT NULL,
                  `per_id` varchar(20) DEFAULT NULL,
                  `track_code` varchar(20) DEFAULT NULL,
                  `BOLUM` varchar(4) DEFAULT NULL,
                  `fat_no` varchar(25) DEFAULT NULL,
                  `mal_cin` text,
                  `mal_ad` varchar(30) DEFAULT NULL,
                  `ad_bir` varchar(11) DEFAULT NULL,
                  `status` varchar(11) DEFAULT NULL,
                  `pucak` varchar(10) DEFAULT NULL,
                  `prazvoz` int(5) DEFAULT NULL,
                  `prazvozdata` varchar(40) DEFAULT NULL,
                  `psom` decimal(20,1) DEFAULT NULL,
                  `pdol` decimal(20,1) DEFAULT NULL,
                  `pzamodin` varchar(25) DEFAULT NULL,
                  `pzamdva` varchar(25) DEFAULT NULL,
                  `pcasid` varchar(11) DEFAULT NULL,
                  `popldat` varchar(20) DEFAULT NULL,
                  `opl_status` varchar(11) DEFAULT NULL,
                  `workerid` int(11) DEFAULT NULL,
                  `car_name` varchar(30) DEFAULT NULL,
                  `lot_data` datetime DEFAULT NULL,
                  `worker_id` int(11) DEFAULT NULL,
                  `ucno` int(11) DEFAULT NULL,
                  `cgo` varchar(50) DEFAULT NULL,
                  `zametki` text,
                  `ucaktarihi` varchar(20) DEFAULT NULL,
                  `razdstat` int(1) DEFAULT NULL,
                  `dpriem` varchar(1000) DEFAULT NULL,
                  `dpriemop` int(3) DEFAULT NULL,
                  `dpdate` varchar(100) DEFAULT NULL,
                  `dpozname` varchar(1000) DEFAULT NULL,
                  `dpozopid` int(3) DEFAULT NULL,
                  `dpozdate` varchar(100) DEFAULT NULL,
                  `mesh` varchar(30) DEFAULT NULL,
                  `meshdata` varchar(20) DEFAULT NULL,
                  `dcontrolname` varchar(1000) DEFAULT NULL,
                  `dcontrolopid` int(11) DEFAULT NULL,
                  `dcontroldate` varchar(100) DEFAULT NULL,
                  PRIMARY KEY (`ID`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """;
            await create.ExecuteNonQueryAsync(cancellationToken);
            created = true;
            trackExists = true;
        }
        else if (!trackExists)
        {
            await using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE `{PriemTable}` ADD COLUMN track_code varchar(20) NULL";
            await alter.ExecuteNonQueryAsync(cancellationToken);
            addedTrack = true;
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

        return new SchemaStatus(tableExists, created, addedTrack, clientExists);
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
        $"{settings.Host.Trim()}|{settings.Port}|{settings.Database.Trim()}|{settings.User.Trim()}";
}
