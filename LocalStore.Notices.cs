using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CargoPlus;

/// <summary>
/// Чек приёма для клиента («Kargonuz teslim alındı»): хранится локально, пока мешки сессии не дошли до сервера
/// (работает и в офлайне), потом SyncService пишет его в kabul_notice на сервере и удаляет отсюда.
/// </summary>
internal sealed record ReceiptNotice(
    long Id,
    string SessionId,
    uint Kod,
    string Bags,
    int Count,
    double Kilo,
    string Store,
    string StorePhone,
    string Track,
    string Office,
    string Operator,
    string Channel,
    bool Manual,
    byte[] Receipt,
    byte[] Qr,
    DateTime Created);

internal sealed partial class LocalStore
{
    private static void InitNotices(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS receipt_notices (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                kod INTEGER NOT NULL,
                bags TEXT NOT NULL,
                n INTEGER NOT NULL,
                kilo REAL NOT NULL,
                store TEXT NOT NULL,
                store_phone TEXT NOT NULL,
                track TEXT NOT NULL,
                office TEXT NOT NULL,
                operator TEXT NOT NULL,
                channel TEXT NOT NULL,
                manual INTEGER NOT NULL DEFAULT 0,
                receipt BLOB,
                qr BLOB,
                created TEXT NOT NULL,
                error TEXT
            );
            """;
        command.ExecuteNonQuery();
        // индексы локальной базы: синхронизация каждые 4 с ищет мешки по сессии и по «не отправлен»
        using var index = connection.CreateCommand();
        index.CommandText = "CREATE INDEX IF NOT EXISTS ix_bags_session ON bags (session_id); CREATE INDEX IF NOT EXISTS ix_bags_synced ON bags (synced);";
        index.ExecuteNonQuery();
    }

    public void QueueNotice(SessionInfo session, IReadOnlyList<BagRecord> bags, string channel, bool manual, byte[] receipt, byte[] qr)
    {
        if (bags.Count == 0)
            return;
        double kilo = 0;
        foreach (var bag in bags)
        {
            if (double.TryParse(bag.Kilo.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                kilo += value;
        }

        var first = bags[0];
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO receipt_notices (session_id, kod, bags, n, kilo, store, store_phone, track, office, operator, channel, manual, receipt, qr, created)
                VALUES ($session, $kod, $bags, $n, $kilo, $store, $phone, $track, $office, $operator, $channel, $manual, $receipt, $qr, $created)
                """;
            command.Parameters.AddWithValue("$session", session.Id);
            command.Parameters.AddWithValue("$kod", (long)session.Kod);
            command.Parameters.AddWithValue("$bags", string.Join(' ', bags.Select(bag => bag.KodId)));
            command.Parameters.AddWithValue("$n", bags.Count);
            command.Parameters.AddWithValue("$kilo", Math.Round(kilo, 1));
            command.Parameters.AddWithValue("$store", string.IsNullOrWhiteSpace(session.StoreName) ? first.StoreName : session.StoreName);
            command.Parameters.AddWithValue("$phone", string.IsNullOrWhiteSpace(session.StorePhone) ? first.StorePhone : session.StorePhone);
            command.Parameters.AddWithValue("$track", string.IsNullOrWhiteSpace(session.TrackCode) ? first.TrackCode : session.TrackCode);
            command.Parameters.AddWithValue("$office", first.Office);
            command.Parameters.AddWithValue("$operator", session.OperatorName);
            command.Parameters.AddWithValue("$channel", channel);
            command.Parameters.AddWithValue("$manual", manual ? 1 : 0);
            command.Parameters.AddWithValue("$receipt", receipt);
            command.Parameters.AddWithValue("$qr", qr);
            command.Parameters.AddWithValue("$created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Чеки, у которых все мешки уже на сервере (несинхронизированных мешков сессии нет).</summary>
    public IReadOnlyList<ReceiptNotice> ReadyNotices(int limit)
    {
        lock (_gate)
        {
            using var connection = Open();
            using (var old = connection.CreateCommand())
            {
                // неделю не смогли отправить (мешки так и не ушли) — больше не держим
                old.CommandText = "DELETE FROM receipt_notices WHERE created < $edge";
                old.Parameters.AddWithValue("$edge", DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                old.ExecuteNonQuery();
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id, session_id, kod, bags, n, kilo, store, store_phone, track, office, operator, channel, manual, receipt, qr, created
                FROM receipt_notices n
                WHERE NOT EXISTS (SELECT 1 FROM bags b WHERE b.session_id = n.session_id AND b.synced = 0)
                ORDER BY id
                LIMIT $limit
                """;
            command.Parameters.AddWithValue("$limit", limit);
            using var reader = command.ExecuteReader();
            var list = new List<ReceiptNotice>();
            while (reader.Read())
            {
                list.Add(new ReceiptNotice(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    (uint)reader.GetInt64(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetDouble(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.GetString(11),
                    reader.GetInt64(12) == 1,
                    reader.IsDBNull(13) ? [] : (byte[])reader[13],
                    reader.IsDBNull(14) ? [] : (byte[])reader[14],
                    DateTime.TryParse(reader.GetString(15), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : DateTime.Now));
            }

            return list;
        }
    }

    public void ForgetNotice(long id)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM receipt_notices WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public int NoticeCount()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM receipt_notices";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }
}
