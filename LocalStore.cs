using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CargoPlus;

internal sealed class LocalStore
{
    private readonly string _connectionString;
    private readonly object _gate = new();

    public LocalStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CargoPlus");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "cargoplus.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public void Init()
    {
        lock (_gate)
        {
            using var connection = Open();
            using (var journal = connection.CreateCommand())
            {
                journal.CommandText = "PRAGMA journal_mode = WAL;";
                journal.ExecuteScalar();
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS settings (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    mysql_host TEXT NOT NULL DEFAULT '',
                    mysql_port INTEGER NOT NULL DEFAULT 3306,
                    mysql_database TEXT NOT NULL DEFAULT '',
                    mysql_user TEXT NOT NULL DEFAULT '',
                    mysql_password BLOB NOT NULL DEFAULT x'',
                    use_ssl INTEGER NOT NULL DEFAULT 0,
                    label_printer TEXT NOT NULL DEFAULT '',
                    receipt_printer TEXT NOT NULL DEFAULT '',
                    bag_start INTEGER NOT NULL DEFAULT 200000,
                    bag_end INTEGER NOT NULL DEFAULT 600000,
                    bag_next INTEGER NOT NULL DEFAULT 200000
                );

                INSERT OR IGNORE INTO settings (id) VALUES (1);

                CREATE TABLE IF NOT EXISTS operators (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    access_level TEXT NOT NULL,
                    category TEXT NOT NULL,
                    pin_hash TEXT NOT NULL,
                    pin_salt TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sessions (
                    id TEXT PRIMARY KEY,
                    opened_at TEXT NOT NULL,
                    closed INTEGER NOT NULL DEFAULT 0,
                    kod INTEGER NOT NULL,
                    client_name TEXT NOT NULL,
                    client_surname TEXT NOT NULL,
                    client_phone TEXT NOT NULL,
                    store_phone TEXT NOT NULL DEFAULT '',
                    store_name TEXT NOT NULL DEFAULT '',
                    operator_name TEXT NOT NULL,
                    track_code TEXT NOT NULL DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS bags (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL,
                    kodid INTEGER NOT NULL,
                    kod INTEGER NOT NULL,
                    kilo TEXT NOT NULL,
                    teslim_eden TEXT NOT NULL,
                    sigorta INTEGER NOT NULL,
                    not1 TEXT NOT NULL,
                    tarih TEXT NOT NULL,
                    track_code TEXT NOT NULL,
                    bolum TEXT NOT NULL,
                    store_phone TEXT NOT NULL,
                    store_name TEXT NOT NULL,
                    synced INTEGER NOT NULL DEFAULT 0,
                    sync_error TEXT,
                    FOREIGN KEY (session_id) REFERENCES sessions(id)
                );
                """;
            command.ExecuteNonQuery();

            using (var alter = connection.CreateCommand())
            {
                alter.CommandText = "ALTER TABLE sessions ADD COLUMN track_code TEXT NOT NULL DEFAULT ''";
                try
                {
                    alter.ExecuteNonQuery();
                }
                catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
                {
                }
            }

            AddColumn(connection, "settings", "company_name", "TEXT NOT NULL DEFAULT 'Baraka Logistic Cargo'");
            AddColumn(connection, "settings", "branch_name", "TEXT NOT NULL DEFAULT 'Kuran Hotel'");
            AddColumn(connection, "settings", "language", "TEXT NOT NULL DEFAULT 'tr'");

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM operators";
            var operators = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (operators == 0)
                InsertOperator(connection, "Администратор", AccessLevels.Admin, "Администрация", "0000");
        }
    }

    public AppSettings GetSettings()
    {
        lock (_gate)
        {
            using var connection = Open();
            return ReadSettings(connection);
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE settings SET
                    mysql_host = $host,
                    mysql_port = $port,
                    mysql_database = $db,
                    mysql_user = $user,
                    mysql_password = $password,
                    use_ssl = $ssl,
                    label_printer = $label,
                    receipt_printer = $receipt,
                    company_name = $company,
                    branch_name = $branch,
                    language = $language,
                    bag_start = $start,
                    bag_end = $end,
                    bag_next = $next
                WHERE id = 1
                """;
            command.Parameters.AddWithValue("$host", settings.Host.Trim());
            command.Parameters.AddWithValue("$port", settings.Port);
            command.Parameters.AddWithValue("$db", settings.Database.Trim());
            command.Parameters.AddWithValue("$user", settings.User.Trim());
            command.Parameters.AddWithValue("$password", Protect(settings.Password));
            command.Parameters.AddWithValue("$ssl", settings.UseSsl ? 1 : 0);
            command.Parameters.AddWithValue("$label", settings.LabelPrinter.Trim());
            command.Parameters.AddWithValue("$receipt", settings.ReceiptPrinter.Trim());
            command.Parameters.AddWithValue("$company", settings.Company.Trim());
            command.Parameters.AddWithValue("$branch", settings.Branch.Trim());
            command.Parameters.AddWithValue("$language", settings.Language == "ru" ? "ru" : "tr");
            command.Parameters.AddWithValue("$start", settings.BagStart);
            command.Parameters.AddWithValue("$end", settings.BagEnd);
            command.Parameters.AddWithValue("$next", settings.BagNext);
            command.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<AppUser> Operators()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, name, access_level, category FROM operators ORDER BY name";
            using var reader = command.ExecuteReader();
            var list = new List<AppUser>();
            while (reader.Read())
            {
                list.Add(new AppUser
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    AccessLevel = reader.GetString(2),
                    Category = reader.GetString(3),
                });
            }

            return list;
        }
    }

    public AppUser? Authenticate(string name, string pin)
    {
        if (!CargoRules.IsPin(pin))
            return null;

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, name, access_level, category, pin_hash, pin_salt
                FROM operators WHERE name = $name
                """;
            command.Parameters.AddWithValue("$name", name.Trim());
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            if (!CargoRules.PinEquals(pin, reader.GetString(5), reader.GetString(4)))
                return null;
            return new AppUser
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                AccessLevel = reader.GetString(2),
                Category = reader.GetString(3),
            };
        }
    }

    public bool DefaultAdminPinStillSet()
    {
        var admin = Operators().FirstOrDefault(user => user.IsAdmin);
        return admin is not null && PinMatches(admin.Id, "0000");
    }

    public string? AddOperator(string name, string level, string category, string pin)
    {
        name = name.Trim();
        category = category.Trim();
        if (name.Length == 0)
            return "Введите имя оператора.";
        if (category.Length == 0)
            return "Введите категорию.";
        if (!AccessLevels.All.Contains(level))
            return "Неизвестный уровень доступа.";
        if (!CargoRules.IsPin(pin))
            return "PIN — четыре цифры.";

        lock (_gate)
        {
            using var connection = Open();
            try
            {
                InsertOperator(connection, name, level, category, pin);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return "Такое имя уже есть.";
            }
        }

        return null;
    }

    public string? ChangePin(long id, string pin)
    {
        if (!CargoRules.IsPin(pin))
            return "PIN — четыре цифры.";

        lock (_gate)
        {
            using var connection = Open();
            var hash = CargoRules.HashPin(pin, out var salt);
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE operators SET pin_hash = $hash, pin_salt = $salt WHERE id = $id";
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$salt", salt);
            command.Parameters.AddWithValue("$id", id);
            if (command.ExecuteNonQuery() == 0)
                return "Оператор не найден.";
        }

        return null;
    }

    public string? DeleteOperator(long id, long currentUserId)
    {
        if (id == currentUserId)
            return "Нельзя удалить оператора, который сейчас вошёл.";

        lock (_gate)
        {
            using var connection = Open();
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT access_level FROM operators WHERE id = $id";
            read.Parameters.AddWithValue("$id", id);
            var level = read.ExecuteScalar() as string;
            if (level is null)
                return "Оператор не найден.";

            if (level == AccessLevels.Admin)
            {
                using var count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM operators WHERE access_level = $level";
                count.Parameters.AddWithValue("$level", AccessLevels.Admin);
                if (Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture) <= 1)
                    return "Должен остаться хотя бы один главный администратор.";
            }

            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM operators WHERE id = $id";
            delete.Parameters.AddWithValue("$id", id);
            delete.ExecuteNonQuery();
        }

        return null;
    }

    public SessionInfo? OpenSession()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, kod, client_name, client_surname, client_phone, store_phone, store_name, operator_name, track_code
                FROM sessions WHERE closed = 0 ORDER BY opened_at DESC LIMIT 1
                """;
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadSession(reader) : null;
        }
    }

    public IReadOnlyList<BagRecord> GetBags(string sessionId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = BagSelect + " WHERE session_id = $id ORDER BY id";
            command.Parameters.AddWithValue("$id", sessionId);
            return ReadBags(command);
        }
    }

    public IReadOnlyList<BagRecord> GetPending(int limit)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = BagSelect + " WHERE synced = 0 ORDER BY id LIMIT $limit";
            command.Parameters.AddWithValue("$limit", limit);
            return ReadBags(command);
        }
    }

    public int PendingCount() => Count("SELECT COUNT(*) FROM bags WHERE synced = 0");

    public int SyncedCount() => Count("SELECT COUNT(*) FROM bags WHERE synced = 1");

    public IReadOnlyList<BagRecord> GetBagsSince(DateTime from)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = BagSelect + " WHERE tarih >= $from ORDER BY tarih DESC";
            command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            return ReadBags(command);
        }
    }

    public string? ArchiveOlderThan(int days)
    {
        var cutoff = DateTime.Now.AddDays(-days);
        var cutoffText = cutoff.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        lock (_gate)
        {
            using var connection = Open();
            using var read = connection.CreateCommand();
            read.CommandText = BagSelect + " WHERE synced = 1 AND tarih < $cutoff ORDER BY tarih";
            read.Parameters.AddWithValue("$cutoff", cutoffText);
            var old = ReadBags(read);
            if (old.Count == 0)
                return null;

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CargoPlus",
                "Backups");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"priem-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var lines = new List<string>
            {
                "номер;код;кг;магазин;страховка;примечание;дата;трекинг;тип;телефон;название;отправлен",
            };
            foreach (var bag in old)
            {
                lines.Add(string.Join(';',
                    bag.KodId,
                    bag.Kod,
                    bag.Kilo,
                    Csv(bag.TeslimEden),
                    bag.Sigorta,
                    Csv(bag.Not1),
                    bag.Tarih.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    bag.TrackCode,
                    bag.Bolum,
                    Csv(bag.StorePhone),
                    Csv(bag.StoreName),
                    bag.Synced ? "1" : "0"));
            }

            File.WriteAllLines(path, lines);

            using var deleteBags = connection.CreateCommand();
            deleteBags.CommandText = "DELETE FROM bags WHERE synced = 1 AND tarih < $cutoff";
            deleteBags.Parameters.AddWithValue("$cutoff", cutoffText);
            deleteBags.ExecuteNonQuery();

            using var deleteSessions = connection.CreateCommand();
            deleteSessions.CommandText = """
                DELETE FROM sessions
                WHERE closed = 1 AND opened_at < $cutoff
                  AND id NOT IN (SELECT session_id FROM bags)
                """;
            deleteSessions.Parameters.AddWithValue("$cutoff", cutoffText);
            deleteSessions.ExecuteNonQuery();
            return path;
        }
    }

    private int Count(string sql)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    private static string Csv(string value)
    {
        var text = value.Replace("\"", "\"\"");
        return text.Contains(';') || text.Contains('"') || text.Contains('\n') ? $"\"{text}\"" : text;
    }

    public void MarkSynced(long localId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE bags SET synced = 1, sync_error = NULL WHERE id = $id";
            command.Parameters.AddWithValue("$id", localId);
            command.ExecuteNonQuery();
        }
    }

    public void MarkError(long localId, string error)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE bags SET sync_error = $error WHERE id = $id AND synced = 0";
            command.Parameters.AddWithValue("$error", error);
            command.Parameters.AddWithValue("$id", localId);
            command.ExecuteNonQuery();
        }
    }

    public void CloseSession(string sessionId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE sessions SET closed = 1 WHERE id = $id";
            command.Parameters.AddWithValue("$id", sessionId);
            command.ExecuteNonQuery();
        }
    }

    public AddBagResult AddBag(
        SessionInfo? session,
        AppUser user,
        ClientInfo client,
        string storePhone,
        string storeName,
        string teslim,
        string bolum,
        int sigorta,
        string not1,
        string kilo)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var settings = ReadSettings(connection, transaction);
            if (settings.BagNext > settings.BagEnd)
                throw new InvalidOperationException("Номера мешков закончились. Позовите главного администратора, чтобы задать новый диапазон.");

            var kodId = settings.BagNext;
            var tarih = DateTime.Now;
            var tarihText = tarih.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var track = session?.TrackCode;
            SessionInfo sessionInfo;
            if (session is null || string.IsNullOrEmpty(track))
            {
                track = CargoRules.Track(kodId);
                sessionInfo = session is null
                    ? CreateSession(connection, transaction, user, client, storePhone, storeName, tarihText, track)
                    : session;
                if (session is not null)
                {
                    using var saveTrack = connection.CreateCommand();
                    saveTrack.Transaction = transaction;
                    saveTrack.CommandText = "UPDATE sessions SET track_code = $track WHERE id = $id";
                    saveTrack.Parameters.AddWithValue("$track", track);
                    saveTrack.Parameters.AddWithValue("$id", session.Id);
                    saveTrack.ExecuteNonQuery();
                    sessionInfo = new SessionInfo
                    {
                        Id = session.Id,
                        Kod = session.Kod,
                        ClientName = session.ClientName,
                        ClientSurname = session.ClientSurname,
                        ClientPhone = session.ClientPhone,
                        StorePhone = session.StorePhone,
                        StoreName = session.StoreName,
                        OperatorName = session.OperatorName,
                        TrackCode = track,
                    };
                }
            }
            else
            {
                sessionInfo = session;
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO bags (
                    session_id, kodid, kod, kilo, teslim_eden, sigorta, not1, tarih, track_code, bolum, store_phone, store_name
                ) VALUES (
                    $session, $kodid, $kod, $kilo, $teslim, $sigorta, $not1, $tarih, $track, $bolum, $phone, $store
                );
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$session", sessionInfo.Id);
            insert.Parameters.AddWithValue("$kodid", kodId);
            insert.Parameters.AddWithValue("$kod", client.Kod);
            insert.Parameters.AddWithValue("$kilo", kilo);
            insert.Parameters.AddWithValue("$teslim", teslim);
            insert.Parameters.AddWithValue("$sigorta", sigorta);
            insert.Parameters.AddWithValue("$not1", not1);
            insert.Parameters.AddWithValue("$tarih", tarihText);
            insert.Parameters.AddWithValue("$track", track);
            insert.Parameters.AddWithValue("$bolum", bolum);
            insert.Parameters.AddWithValue("$phone", storePhone.Trim());
            insert.Parameters.AddWithValue("$store", storeName.Trim());
            var localId = Convert.ToInt64(insert.ExecuteScalar(), CultureInfo.InvariantCulture);

            using var bump = connection.CreateCommand();
            bump.Transaction = transaction;
            bump.CommandText = "UPDATE settings SET bag_next = $next WHERE id = 1";
            bump.Parameters.AddWithValue("$next", kodId + 1);
            bump.ExecuteNonQuery();
            transaction.Commit();

            var bag = new BagRecord
            {
                LocalId = localId,
                SessionId = sessionInfo.Id,
                KodId = kodId,
                Kod = client.Kod,
                Kilo = kilo,
                TeslimEden = teslim,
                Sigorta = sigorta,
                Not1 = not1,
                Tarih = tarih,
                TrackCode = track,
                Bolum = bolum,
                StorePhone = storePhone.Trim(),
                StoreName = storeName.Trim(),
                Synced = false,
            };
            var remaining = kodId + 1 > settings.BagEnd ? 0 : settings.BagEnd - kodId;
            return new AddBagResult(sessionInfo, bag, remaining);
        }
    }

    private bool PinMatches(long id, string pin)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pin_hash, pin_salt FROM operators WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return false;
            return CargoRules.PinEquals(pin, reader.GetString(1), reader.GetString(0));
        }
    }

    private static SessionInfo CreateSession(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AppUser user,
        ClientInfo client,
        string storePhone,
        string storeName,
        string openedAt,
        string trackCode)
    {
        var id = Guid.NewGuid().ToString("N");
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO sessions (
                id, opened_at, kod, client_name, client_surname, client_phone, store_phone, store_name, operator_name, track_code
            ) VALUES (
                $id, $opened, $kod, $name, $surname, $phone, $storePhone, $storeName, $operator, $track
            )
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$opened", openedAt);
        command.Parameters.AddWithValue("$kod", client.Kod);
        command.Parameters.AddWithValue("$name", client.Name);
        command.Parameters.AddWithValue("$surname", client.Surname);
        command.Parameters.AddWithValue("$phone", client.Phone);
        command.Parameters.AddWithValue("$storePhone", storePhone.Trim());
        command.Parameters.AddWithValue("$storeName", storeName.Trim());
        command.Parameters.AddWithValue("$operator", user.Name);
        command.Parameters.AddWithValue("$track", trackCode);
        command.ExecuteNonQuery();
        return new SessionInfo
        {
            Id = id,
            Kod = client.Kod,
            ClientName = client.Name,
            ClientSurname = client.Surname,
            ClientPhone = client.Phone,
            StorePhone = storePhone.Trim(),
            StoreName = storeName.Trim(),
            OperatorName = user.Name,
            TrackCode = trackCode,
        };
    }

    private const string BagSelect = """
        SELECT id, session_id, kodid, kod, kilo, teslim_eden, sigorta, not1, tarih, track_code, bolum,
               store_phone, store_name, synced, sync_error
        FROM bags
        """;

    private static List<BagRecord> ReadBags(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var list = new List<BagRecord>();
        while (reader.Read())
        {
            list.Add(new BagRecord
            {
                LocalId = reader.GetInt64(0),
                SessionId = reader.GetString(1),
                KodId = reader.GetInt32(2),
                Kod = (uint)reader.GetInt64(3),
                Kilo = reader.GetString(4),
                TeslimEden = reader.GetString(5),
                Sigorta = reader.GetInt32(6),
                Not1 = reader.GetString(7),
                Tarih = DateTime.ParseExact(reader.GetString(8), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                TrackCode = reader.GetString(9),
                Bolum = reader.GetString(10),
                StorePhone = reader.GetString(11),
                StoreName = reader.GetString(12),
                Synced = reader.GetInt32(13) != 0,
                SyncError = reader.IsDBNull(14) ? null : reader.GetString(14),
            });
        }

        return list;
    }

    private static SessionInfo ReadSession(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Kod = (uint)reader.GetInt64(1),
        ClientName = reader.GetString(2),
        ClientSurname = reader.GetString(3),
        ClientPhone = reader.GetString(4),
        StorePhone = reader.GetString(5),
        StoreName = reader.GetString(6),
        OperatorName = reader.GetString(7),
        TrackCode = reader.FieldCount > 8 && !reader.IsDBNull(8) ? reader.GetString(8) : "",
    };

    private AppSettings ReadSettings(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT mysql_host, mysql_port, mysql_database, mysql_user, mysql_password, use_ssl,
                   label_printer, receipt_printer, bag_start, bag_end, bag_next,
                   company_name, branch_name, language
            FROM settings WHERE id = 1
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return new AppSettings();

        var password = "";
        if (!reader.IsDBNull(4))
        {
            try
            {
                password = Unprotect((byte[])reader.GetValue(4));
            }
            catch (CryptographicException)
            {
                password = "";
            }
        }

        return new AppSettings
        {
            Host = reader.GetString(0),
            Port = reader.GetInt32(1),
            Database = reader.GetString(2),
            User = reader.GetString(3),
            Password = password,
            UseSsl = reader.GetInt32(5) != 0,
            LabelPrinter = reader.GetString(6),
            ReceiptPrinter = reader.GetString(7),
            BagStart = reader.GetInt32(8),
            BagEnd = reader.GetInt32(9),
            BagNext = reader.GetInt32(10),
            Company = reader.FieldCount > 11 && !reader.IsDBNull(11) ? reader.GetString(11) : "Baraka Logistic Cargo",
            Branch = reader.FieldCount > 12 && !reader.IsDBNull(12) ? reader.GetString(12) : "Kuran Hotel",
            Language = reader.FieldCount > 13 && !reader.IsDBNull(13) ? reader.GetString(13) : "tr",
        };
    }

    private static void AddColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        try
        {
            alter.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static void InsertOperator(SqliteConnection connection, string name, string level, string category, string pin)
    {
        var hash = CargoRules.HashPin(pin, out var salt);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO operators (name, access_level, category, pin_hash, pin_salt)
            VALUES ($name, $level, $category, $hash, $salt)
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$level", level);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$salt", salt);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static byte[] Protect(string text)
    {
        if (text.Length == 0)
            return [];
        return ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser);
    }

    private static string Unprotect(byte[] data)
    {
        if (data.Length == 0)
            return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
    }
}
