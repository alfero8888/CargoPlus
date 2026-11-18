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

                CREATE TABLE IF NOT EXISTS bag_drops (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    kodid INTEGER NOT NULL,
                    track_code TEXT NOT NULL
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
            AddColumn(connection, "settings", "logo_path", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "enabled_bolum", "TEXT NOT NULL DEFAULT '1,2,3,4,6'");
            AddColumn(connection, "settings", "priem_table", "TEXT NOT NULL DEFAULT 'priem_ob'");
            AddColumn(connection, "settings", "last_level", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "last_category", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "last_name", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "numbers_ack_end", "INTEGER NOT NULL DEFAULT -1");
            AddColumn(connection, "settings", "print_hide", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "operator_rights", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "receipt_scale", "REAL NOT NULL DEFAULT 1");
            AddColumn(connection, "settings", "telemetry_minutes", "INTEGER NOT NULL DEFAULT 7");
            AddColumn(connection, "settings", "print_sizes", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "station_letter", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "settings", "station_letter_manual", "INTEGER NOT NULL DEFAULT 0");
            AddColumn(connection, "bags", "office", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "bags", "operator_name", "TEXT NOT NULL DEFAULT ''");
            using (var items = connection.CreateCommand())
            {
                items.CommandText = """
                    CREATE TABLE IF NOT EXISTS bag_items (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        bag_id INTEGER NOT NULL,
                        kodid TEXT NOT NULL,
                        line INTEGER NOT NULL,
                        name TEXT NOT NULL,
                        qty TEXT NOT NULL,
                        unit TEXT NOT NULL,
                        invoice_no TEXT NOT NULL DEFAULT '',
                        invoice_date TEXT NOT NULL DEFAULT '',
                        synced INTEGER NOT NULL DEFAULT 0
                    );
                    """;
                items.ExecuteNonQuery();
            }

            AddColumn(connection, "bag_items", "invoice_no", "TEXT NOT NULL DEFAULT ''");
            AddColumn(connection, "bag_items", "invoice_date", "TEXT NOT NULL DEFAULT ''");

            using (var counters = connection.CreateCommand())
            {
                counters.CommandText = """
                    CREATE TABLE IF NOT EXISTS bag_counters (
                        prefix TEXT PRIMARY KEY,
                        overflow TEXT NOT NULL DEFAULT '',
                        next INTEGER NOT NULL
                    );
                    """;
                counters.ExecuteNonQuery();
            }

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM operators";
            var operators = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (operators == 0)
                InsertOperator(connection, "Администратор", AccessLevels.Admin, "Администрация", "0000");

            KeepBagRange(connection);
            AlignUnlocked(connection, ReadSettings(connection), null);
        }
    }

    private void KeepBagRange(SqliteConnection connection)
    {
        var settings = ReadSettings(connection);
        var start = settings.BagStart;
        var end = settings.BagEnd;
        var next = settings.BagNext;
        if (BagRangeMemory.TryRead(out var remembered))
        {
            var freshDefaults = start == 200_000 && end == 600_000 && next == 200_000;
            var savedRange = remembered.Start != 200_000 || remembered.End != 600_000 || remembered.Next != 200_000;
            if (freshDefaults && savedRange)
            {
                start = remembered.Start;
                end = remembered.End;
                next = remembered.Next;
            }
            else
            {
                next = Math.Max(next, remembered.Next);
            }
        }

        if (start != settings.BagStart || end != settings.BagEnd || next != settings.BagNext)
            WriteBagRange(connection, null, start, end, next);
        BagRangeMemory.Remember(start, end, next);
    }

    private static void WriteBagRange(SqliteConnection connection, SqliteTransaction? transaction, int start, int end, int next)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE settings SET bag_start = $start, bag_end = $end, bag_next = $next WHERE id = 1";
        command.Parameters.AddWithValue("$start", start);
        command.Parameters.AddWithValue("$end", end);
        command.Parameters.AddWithValue("$next", next);
        command.ExecuteNonQuery();
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
                    logo_path = $logo,
                    enabled_bolum = $bolum,
                    priem_table = $table,
                    bag_start = $start,
                    bag_end = $end,
                    bag_next = $next,
                    print_hide = $hide,
                    operator_rights = $rights,
                    receipt_scale = $scale,
                    telemetry_minutes = $telemetry,
                    print_sizes = $sizes,
                    station_letter = $station,
                    station_letter_manual = $stationManual
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
            command.Parameters.AddWithValue("$logo", settings.LogoPath.Trim());
            command.Parameters.AddWithValue("$bolum", string.IsNullOrWhiteSpace(settings.EnabledBolum) ? "1,2,3,4,6" : settings.EnabledBolum.Trim());
            command.Parameters.AddWithValue("$table", RemoteDb.Table(settings));
            command.Parameters.AddWithValue("$start", settings.BagStart);
            command.Parameters.AddWithValue("$end", settings.BagEnd);
            command.Parameters.AddWithValue("$next", settings.BagNext);
            command.Parameters.AddWithValue("$hide", settings.PrintHide.Trim());
            command.Parameters.AddWithValue("$rights", settings.OperatorRights.Trim());
            command.Parameters.AddWithValue("$scale", settings.ReceiptScale);
            command.Parameters.AddWithValue("$sizes", settings.PrintSizes ?? "");
            command.Parameters.AddWithValue("$telemetry", settings.TelemetryMinutes);
            command.Parameters.AddWithValue("$station", (settings.StationLetter ?? "").Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("$stationManual", settings.StationLetterManual ? 1 : 0);
            command.ExecuteNonQuery();
            BagRangeMemory.Remember(settings.BagStart, settings.BagEnd, settings.BagNext);
            AlignUnlocked(connection, ReadSettings(connection), null);
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

    public (string Level, string Category, string Name) LastLogin()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT last_level, last_category, last_name FROM settings WHERE id = 1";
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return ("", "", "");
            return (
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2));
        }
    }

    public void RememberLogin(string level, string category, string name)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE settings
                SET last_level = $level, last_category = $category, last_name = $name
                WHERE id = 1
                """;
            command.Parameters.AddWithValue("$level", level.Trim());
            command.Parameters.AddWithValue("$category", category.Trim());
            command.Parameters.AddWithValue("$name", name.Trim());
            command.ExecuteNonQuery();
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
            return UiText.NeedOperatorName;
        if (!AccessLevels.All.Contains(level))
            return UiText.BadLevel;
        if (!CargoRules.IsPin(pin))
            return UiText.PinFour;

        lock (_gate)
        {
            using var connection = Open();
            try
            {
                InsertOperator(connection, name, level, category, pin);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return UiText.NameExists;
            }
        }

        return null;
    }

    public string? ChangePin(long id, string pin)
    {
        if (!CargoRules.IsPin(pin))
            return UiText.PinFour;

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
                return UiText.OperatorMissing;
        }

        return null;
    }

    public string? DeleteOperator(long id, long currentUserId)
    {
        if (id == currentUserId)
            return UiText.CannotDeleteSelf;

        lock (_gate)
        {
            using var connection = Open();
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT access_level FROM operators WHERE id = $id";
            read.Parameters.AddWithValue("$id", id);
            var level = read.ExecuteScalar() as string;
            if (level is null)
                return UiText.OperatorMissing;

            if (level == AccessLevels.Admin)
            {
                using var count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM operators WHERE access_level = $level";
                count.Parameters.AddWithValue("$level", AccessLevels.Admin);
                if (Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture) <= 1)
                    return UiText.NeedOneAdmin;
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
            if (!reader.Read())
                return null;
            var session = ReadSession(reader);
            reader.Close();
            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM bags WHERE session_id = $id";
            count.Parameters.AddWithValue("$id", session.Id);
            if (Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                return session;

            using var close = connection.CreateCommand();
            close.CommandText = "UPDATE sessions SET closed = 1 WHERE id = $id";
            close.Parameters.AddWithValue("$id", session.Id);
            close.ExecuteNonQuery();
            return null;
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

    public IReadOnlyList<BagRecord> UnsyncedBags()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = BagSelect + " WHERE synced = 0 ORDER BY tarih";
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

    public SessionInfo? GetSession(string sessionId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, kod, client_name, client_surname, client_phone, store_phone, store_name, operator_name, track_code
                FROM sessions WHERE id = $id
                """;
            command.Parameters.AddWithValue("$id", sessionId);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadSession(reader) : null;
        }
    }

    public IReadOnlyList<BagRecord> SearchBags(DateTime from, DateTime to, string store, string code)
    {
        var start = from.Date;
        var finish = to.Date;
        if (finish < start)
            (start, finish) = (finish, start);
        var end = finish.AddDays(1);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = BagSelect + """
                 WHERE tarih >= $from AND tarih < $to
                   AND ($store = '' OR store_name LIKE $store ESCAPE '\' OR store_phone LIKE $store ESCAPE '\' OR teslim_eden LIKE $store ESCAPE '\')
                   AND ($code = '' OR CAST(kod AS TEXT) LIKE $code ESCAPE '\')
                 ORDER BY tarih DESC
                """;
            command.Parameters.AddWithValue("$from", start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$to", end.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$store", Like(store));
            command.Parameters.AddWithValue("$code", Like(code));
            return ReadBags(command);
        }
    }

    private static string Like(string value)
    {
        var text = value.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return text.Length == 0 ? "" : $"%{text}%";
    }

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
            var kept = ReadSettings(connection);
            var lines = new List<string>
            {
                $"#range;{kept.BagStart};{kept.BagEnd};{kept.BagNext}",
            };
            foreach (var counter in ReadCounters(connection))
                lines.Add($"#counter;{counter.Prefix};{counter.Overflow};{counter.Next}");
            foreach (var bag in old)
            {
                foreach (var item in ReadItems(connection, null, bag.LocalId))
                    lines.Add(string.Join(';', "#item", bag.KodId, item.Line.ToString(CultureInfo.InvariantCulture), Csv(item.Name), item.Qty, item.Unit));
            }
            lines.Add("номер;код;кг;магазин;страховка;примечание;дата;трекинг;тип;телефон;название;отделение;оператор;отправлен");
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
                    Csv(bag.Office),
                    Csv(bag.OperatorName),
                    bag.Synced ? "1" : "0"));
            }

            File.WriteAllLines(path, lines);

            using var deleteItems = connection.CreateCommand();
            deleteItems.CommandText = """
                DELETE FROM bag_items
                WHERE bag_id IN (SELECT id FROM bags WHERE synced = 1 AND tarih < $cutoff)
                """;
            deleteItems.Parameters.AddWithValue("$cutoff", cutoffText);
            deleteItems.ExecuteNonQuery();

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

    public bool BagExists(long localId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM bags WHERE id = $id";
            command.Parameters.AddWithValue("$id", localId);
            return command.ExecuteScalar() is not null;
        }
    }

    public void RemoveBags(IReadOnlyList<BagRecord> bags)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            foreach (var bag in bags)
            {
                using var drop = connection.CreateCommand();
                drop.Transaction = transaction;
                drop.CommandText = "INSERT INTO bag_drops (kodid, track_code) VALUES ($kodid, $track)";
                drop.Parameters.AddWithValue("$kodid", bag.KodId);
                drop.Parameters.AddWithValue("$track", bag.TrackCode);
                drop.ExecuteNonQuery();

                using var deleteItems = connection.CreateCommand();
                deleteItems.Transaction = transaction;
                deleteItems.CommandText = "DELETE FROM bag_items WHERE bag_id = $id";
                deleteItems.Parameters.AddWithValue("$id", bag.LocalId);
                deleteItems.ExecuteNonQuery();

                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM bags WHERE id = $id";
                delete.Parameters.AddWithValue("$id", bag.LocalId);
                delete.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    public IReadOnlyList<BagDrop> GetDrops(int limit)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, kodid, track_code FROM bag_drops ORDER BY id LIMIT $limit";
            command.Parameters.AddWithValue("$limit", limit);
            using var reader = command.ExecuteReader();
            var list = new List<BagDrop>();
            while (reader.Read())
            {
                list.Add(new BagDrop(reader.GetInt64(0), Cell(reader, 1), reader.GetString(2)));
            }

            return list;
        }
    }

    public void ForgetDrop(long id)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM bag_drops WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
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
            using var items = connection.CreateCommand();
            items.CommandText = "UPDATE bag_items SET synced = 1 WHERE bag_id = $id";
            items.Parameters.AddWithValue("$id", localId);
            items.ExecuteNonQuery();
        }
    }

    public void MarkItemsSynced(long localId)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE bag_items SET synced = 1 WHERE bag_id = $id";
            command.Parameters.AddWithValue("$id", localId);
            command.ExecuteNonQuery();
            using var clear = connection.CreateCommand();
            clear.CommandText = "UPDATE bags SET sync_error = NULL WHERE id = $id AND synced = 1";
            clear.Parameters.AddWithValue("$id", localId);
            clear.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<BagItem> GetItems(long bagId)
    {
        lock (_gate)
        {
            using var connection = Open();
            return ReadItems(connection, null, bagId);
        }
    }

    public IReadOnlyList<string[]> ExportItemRows()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT b.kodid, i.line, i.name, i.qty, i.unit, i.invoice_no, i.invoice_date, b.track_code, b.tarih, b.bolum
                FROM bag_items i
                JOIN bags b ON b.id = i.bag_id
                ORDER BY b.tarih, b.kodid, i.line
                """;
            using var reader = command.ExecuteReader();
            var rows = new List<string[]>();
            while (reader.Read())
            {
                rows.Add(
                [
                    Cell(reader, 0),
                    reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    CargoRules.InvoiceDateText(reader.GetString(6)),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetString(9),
                ]);
            }

            return rows;
        }
    }

    public IReadOnlyList<string> RecentItemNames()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT name FROM bag_items
                GROUP BY name COLLATE NOCASE
                ORDER BY MAX(id) DESC
                LIMIT 500
                """;
            using var reader = command.ExecuteReader();
            var names = new List<string>();
            while (reader.Read())
                names.Add(reader.GetString(0));
            return names;
        }
    }

    public void ReplaceItems(long bagId, string kodId, IReadOnlyList<BagItem> items)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM bag_items WHERE bag_id = $id";
            delete.Parameters.AddWithValue("$id", bagId);
            delete.ExecuteNonQuery();
            WriteItems(connection, transaction, bagId, kodId, items);
            transaction.Commit();
        }
    }

    public IReadOnlyList<BagRecord> BagsWithPendingItems()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = BagSelect + """
                 WHERE synced = 1 AND id IN (SELECT bag_id FROM bag_items WHERE synced = 0)
                 ORDER BY id
                 LIMIT 30
                """;
            return ReadBags(command);
        }
    }

    private static void WriteItems(SqliteConnection connection, SqliteTransaction transaction, long bagId, string kodId, IReadOnlyList<BagItem>? items)
    {
        if (items is null || items.Count == 0)
            return;
        var line = 1;
        foreach (var item in items.Take(CargoRules.ItemLineMax))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO bag_items (bag_id, kodid, line, name, qty, unit, invoice_no, invoice_date, synced)
                VALUES ($bag, $kodid, $line, $name, $qty, $unit, $invoice, $idate, 0)
                """;
            command.Parameters.AddWithValue("$bag", bagId);
            command.Parameters.AddWithValue("$kodid", kodId);
            command.Parameters.AddWithValue("$line", line);
            command.Parameters.AddWithValue("$name", item.Name.Trim());
            command.Parameters.AddWithValue("$qty", item.Qty);
            command.Parameters.AddWithValue("$unit", CargoRules.ItemUnit(item.Unit));
            command.Parameters.AddWithValue("$invoice", item.InvoiceNo);
            command.Parameters.AddWithValue("$idate", item.InvoiceDate);
            command.ExecuteNonQuery();
            line++;
        }
    }

    private static List<BagItem> ReadItems(SqliteConnection connection, SqliteTransaction? transaction, long bagId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT line, name, qty, unit, invoice_no, invoice_date FROM bag_items WHERE bag_id = $id ORDER BY line";
        command.Parameters.AddWithValue("$id", bagId);
        using var reader = command.ExecuteReader();
        var list = new List<BagItem>();
        while (reader.Read())
        {
            list.Add(new BagItem
            {
                Line = reader.GetInt32(0),
                Name = reader.GetString(1),
                Qty = reader.GetString(2),
                Unit = reader.GetString(3),
                InvoiceNo = reader.IsDBNull(4) ? "" : reader.GetString(4),
                InvoiceDate = reader.IsDBNull(5) ? "" : reader.GetString(5),
            });
        }

        return list;
    }

    public void AcknowledgeNumbers(int bagEnd)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE settings SET numbers_ack_end = $end WHERE id = 1";
            command.Parameters.AddWithValue("$end", bagEnd);
            command.ExecuteNonQuery();
        }
    }

    public void RememberClient(ClientInfo client)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE sessions
                SET client_name = $name, client_surname = $surname, client_phone = $phone
                WHERE kod = $kod AND TRIM(client_name) = ''
                """;
            command.Parameters.AddWithValue("$name", client.Name);
            command.Parameters.AddWithValue("$surname", client.Surname);
            command.Parameters.AddWithValue("$phone", client.Phone);
            command.Parameters.AddWithValue("$kod", client.Kod);
            command.ExecuteNonQuery();
        }
    }

    public void ReplaceUnsyncedCode(uint oldKod, ClientInfo client, string note)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            using var sessions = connection.CreateCommand();
            sessions.Transaction = transaction;
            sessions.CommandText = """
                UPDATE sessions
                SET kod = $kod, client_name = $name, client_surname = $surname, client_phone = $phone
                WHERE kod = $old AND id IN (SELECT session_id FROM bags WHERE synced = 0 AND kod = $old)
                """;
            sessions.Parameters.AddWithValue("$kod", client.Kod);
            sessions.Parameters.AddWithValue("$name", client.Name);
            sessions.Parameters.AddWithValue("$surname", client.Surname);
            sessions.Parameters.AddWithValue("$phone", client.Phone);
            sessions.Parameters.AddWithValue("$old", oldKod);
            sessions.ExecuteNonQuery();

            using var bags = connection.CreateCommand();
            bags.Transaction = transaction;
            bags.CommandText = """
                UPDATE bags
                SET kod = $kod,
                    sync_error = NULL,
                    not1 = CASE
                        WHEN not1 IS NULL OR TRIM(not1) = '' OR not1 = '0' THEN $note
                        WHEN INSTR(not1, $note) > 0 THEN not1
                        WHEN LENGTH(not1) + 3 + LENGTH($note) <= 100 THEN not1 || ' | ' || $note
                        ELSE $note
                    END
                WHERE synced = 0 AND kod = $old
                """;
            bags.Parameters.AddWithValue("$kod", client.Kod);
            bags.Parameters.AddWithValue("$note", note);
            bags.Parameters.AddWithValue("$old", oldKod);
            bags.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    public void MarkError(long localId, string error)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE bags SET sync_error = $error WHERE id = $id";
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
        string kilo,
        IReadOnlyList<BagItem>? items = null)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();
            var settings = ReadSettings(connection, transaction);
            if (client.Kod == 0)
                throw new InvalidOperationException(UiText.ZeroForbidden);

            var station = CargoRules.FirstStationLetter(settings.Branch);
            var written = (settings.StationLetter ?? "").Trim().ToUpperInvariant();
            if (written.Length is 1 or 2)
                station = written;
            var kodId = TakeLetterNumber(connection, transaction, station, user.Name, bolum);
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
                    session_id, kodid, kod, kilo, teslim_eden, sigorta, not1, tarih, track_code, bolum, store_phone, store_name, office, operator_name
                ) VALUES (
                    $session, $kodid, $kod, $kilo, $teslim, $sigorta, $not1, $tarih, $track, $bolum, $phone, $store, $office, $operator
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
            insert.Parameters.AddWithValue("$office", settings.Branch.Trim());
            insert.Parameters.AddWithValue("$operator", user.Name.Trim());
            var localId = Convert.ToInt64(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
            WriteItems(connection, transaction, localId, kodId, items);

            transaction.Commit();
            BagCounterMemory.Remember(ReadCounters(connection));

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
                Office = settings.Branch.Trim(),
                OperatorName = user.Name.Trim(),
                Synced = false,
            };
            var remaining = settings.Remaining;
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
               store_phone, store_name, synced, sync_error, office, operator_name
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
                KodId = Cell(reader, 2),
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
                Office = reader.FieldCount > 15 && !reader.IsDBNull(15) ? reader.GetString(15) : "",
                OperatorName = reader.FieldCount > 16 && !reader.IsDBNull(16) ? reader.GetString(16) : "",
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
                   company_name, branch_name, language, logo_path, enabled_bolum, priem_table, numbers_ack_end,
                   print_hide, operator_rights, receipt_scale, telemetry_minutes, print_sizes, station_letter,
                   station_letter_manual
            FROM settings WHERE id = 1
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            var fresh = new AppSettings();
            BuiltInConnection.Apply(fresh);
            return fresh;
        }

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

        var settings = new AppSettings
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
            LogoPath = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetString(14) : "",
            EnabledBolum = reader.FieldCount > 15 && !reader.IsDBNull(15) ? reader.GetString(15) : "1,2,3,4,6",
            PriemTable = reader.FieldCount > 16 && !reader.IsDBNull(16) && reader.GetString(16).Trim().Length > 0
                ? reader.GetString(16).Trim()
                : "priem_ob",
            NumbersAckEnd = reader.FieldCount > 17 && !reader.IsDBNull(17) ? reader.GetInt32(17) : -1,
            PrintHide = reader.FieldCount > 18 && !reader.IsDBNull(18) ? reader.GetString(18) : "",
            OperatorRights = reader.FieldCount > 19 && !reader.IsDBNull(19) ? reader.GetString(19) : "",
            ReceiptScale = reader.FieldCount > 20 && !reader.IsDBNull(20) ? (float)reader.GetDouble(20) : 1f,
            TelemetryMinutes = reader.FieldCount > 21 && !reader.IsDBNull(21) ? reader.GetInt32(21) : 7,
            PrintSizes = reader.FieldCount > 22 && !reader.IsDBNull(22) ? reader.GetString(22) : "",
            StationLetter = reader.FieldCount > 23 && !reader.IsDBNull(23) ? reader.GetString(23).Trim().ToUpperInvariant() : "",
            StationLetterManual = reader.FieldCount > 24 && !reader.IsDBNull(24) && reader.GetInt32(24) != 0,
        };
        var autoLetter = CargoRules.FirstStationLetter(settings.Branch);
        if (settings.StationLetter.Length is < 1 or > 2)
        {
            settings.StationLetter = autoLetter;
            settings.StationLetterManual = false;
        }
        else if (!settings.StationLetterManual && settings.StationLetter != autoLetter)
        {
            settings.StationLetterManual = true;
        }
        else if (!settings.StationLetterManual)
        {
            settings.StationLetter = autoLetter;
        }

        BuiltInConnection.Apply(settings);
        return settings;
    }

    public void AlignCounters(AppSettings settings, string operatorName, Func<string, string, string?>? serverLatest)
    {
        lock (_gate)
        {
            using var connection = Open();
            AlignUnlocked(connection, settings, serverLatest, operatorName);
        }
    }

    private static void AlignUnlocked(SqliteConnection connection, AppSettings settings, Func<string, string, string?>? serverLatest, string operatorName = "")
    {
        var letter = (settings.StationLetter ?? "").Trim().ToUpperInvariant();
        if (letter.Length is < 1 or > 2)
            letter = CargoRules.FirstStationLetter(settings.Branch);

        using (var transaction = connection.BeginTransaction())
        {
            foreach (var choice in BolumChoice.All)
            {
                var prefix = CargoRules.NumberPrefix(letter, operatorName, choice.Code);
                var best = Best(ReadOne(connection, transaction, prefix), FromBags(connection, transaction, prefix), FromFile(prefix));
                if (serverLatest is not null)
                {
                    var remote = ParseIssued(prefix, serverLatest(prefix, best.Overflow));
                    best = Higher(best, remote);
                    if (best.Next > 999999)
                    {
                        var rolled = Roll(best);
                        var nextRemote = ParseIssued(prefix, serverLatest(prefix, rolled.Overflow));
                        best = Higher(rolled, nextRemote);
                    }
                }

                if (best.Next > 999999)
                    best = Roll(best);
                WriteCounter(connection, transaction, best);
            }

            transaction.Commit();
            BagCounterMemory.Remember(ReadCounters(connection));
        }
    }

    private static string TakeLetterNumber(SqliteConnection connection, SqliteTransaction transaction, string station, string operatorName, string bolum)
    {
        var prefix = CargoRules.NumberPrefix(station, operatorName, bolum);
        var current = ReadOne(connection, transaction, prefix);
        if (current.Next < 1)
            current = current with { Next = 1 };
        if (current.Next > 999999)
            current = Roll(current);
        var issued = prefix + current.Overflow + current.Next.ToString(CultureInfo.InvariantCulture);
        WriteCounter(connection, transaction, current with { Next = current.Next + 1 });
        return issued;
    }

    private static BagCounterMemory.Counter Best(params BagCounterMemory.Counter[] items)
    {
        var best = items[0];
        for (var i = 1; i < items.Length; i++)
            best = Higher(best, items[i]);
        return best;
    }

    private static BagCounterMemory.Counter Higher(BagCounterMemory.Counter left, BagCounterMemory.Counter right)
    {
        var overflow = string.Compare(left.Overflow, right.Overflow, StringComparison.Ordinal);
        if (overflow > 0 || (overflow == 0 && left.Next >= right.Next))
            return left;
        return right;
    }

    private static BagCounterMemory.Counter Roll(BagCounterMemory.Counter counter)
    {
        var overflow = counter.Overflow ?? "";
        var mark = overflow.Length == 0 ? 'A' : (char)(overflow[0] + 1);
        if (mark > 'Z')
            throw new InvalidOperationException(UiText.NumbersEnded);
        return counter with { Overflow = mark.ToString(), Next = 1 };
    }

    private static BagCounterMemory.Counter FromFile(string prefix)
    {
        foreach (var counter in BagCounterMemory.Read())
        {
            if (string.Equals(counter.Prefix, prefix, StringComparison.Ordinal))
                return counter.Next < 1 ? counter with { Next = 1, Overflow = counter.Overflow ?? "" } : counter with { Overflow = counter.Overflow ?? "" };
        }

        return new BagCounterMemory.Counter(prefix, "", 1);
    }

    private static BagCounterMemory.Counter FromBags(SqliteConnection connection, SqliteTransaction transaction, string prefix)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT kodid FROM bags WHERE CAST(kodid AS TEXT) LIKE $prefix";
        command.Parameters.AddWithValue("$prefix", prefix + "%");
        using var reader = command.ExecuteReader();
        var best = new BagCounterMemory.Counter(prefix, "", 1);
        while (reader.Read())
            best = Higher(best, ParseIssued(prefix, Cell(reader, 0)));
        return best;
    }

    private static BagCounterMemory.Counter ParseIssued(string prefix, string? kodId)
    {
        var fresh = new BagCounterMemory.Counter(prefix, "", 1);
        var text = (kodId ?? "").Trim().ToUpperInvariant();
        if (!text.StartsWith(prefix, StringComparison.Ordinal) || text.Length <= prefix.Length)
            return fresh;
        var tail = text[prefix.Length..];
        var overflow = "";
        if (char.IsLetter(tail[0]))
        {
            overflow = tail[..1];
            tail = tail[1..];
        }

        if (tail.Length is < 1 or > 6 || tail.Any(ch => !char.IsDigit(ch)))
            return fresh;
        if (!int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 999999)
            return fresh;
        return new BagCounterMemory.Counter(prefix, overflow, number + 1);
    }

    private static BagCounterMemory.Counter ReadOne(SqliteConnection connection, SqliteTransaction? transaction, string prefix)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT prefix, overflow, next FROM bag_counters WHERE prefix = $prefix";
        command.Parameters.AddWithValue("$prefix", prefix);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return new BagCounterMemory.Counter(prefix, "", 1);
        return new BagCounterMemory.Counter(reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
    }

    private static void WriteCounter(SqliteConnection connection, SqliteTransaction? transaction, BagCounterMemory.Counter counter)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO bag_counters (prefix, overflow, next) VALUES ($prefix, $overflow, $next)
            ON CONFLICT(prefix) DO UPDATE SET overflow = $overflow, next = $next
            """;
        command.Parameters.AddWithValue("$prefix", counter.Prefix);
        command.Parameters.AddWithValue("$overflow", counter.Overflow);
        command.Parameters.AddWithValue("$next", counter.Next);
        command.ExecuteNonQuery();
    }

    private static List<BagCounterMemory.Counter> ReadCounters(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT prefix, overflow, next FROM bag_counters ORDER BY prefix";
        using var reader = command.ExecuteReader();
        var list = new List<BagCounterMemory.Counter>();
        while (reader.Read())
            list.Add(new BagCounterMemory.Counter(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        return list;
    }

    private static string Cell(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
            return "";
        return reader.GetValue(index)?.ToString() ?? "";
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
