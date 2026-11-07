using Microsoft.Data.Sqlite;
using TaskList.Models;

namespace TaskList.Data;

/// <summary>
/// Single-file SQLite storage. A single connection is kept open and guarded by a lock;
/// WAL + FULL sync keeps commits durable without risking database corruption.
/// </summary>
public sealed class TaskDatabase : IDisposable
{
    readonly SqliteConnection _connection;
    readonly object _gate = new();
    bool _disposed;

    public TaskDatabase(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        };
        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();
        Execute("PRAGMA journal_mode=WAL;");
        Execute("PRAGMA synchronous=FULL;");
        Execute("PRAGMA busy_timeout=5000;");
        Execute("PRAGMA foreign_keys=ON;");
        CreateSchema();
    }

    void CreateSchema()
    {
        Execute("""
            CREATE TABLE IF NOT EXISTS tasks (
                id                   INTEGER PRIMARY KEY AUTOINCREMENT,
                title                TEXT    NOT NULL,
                importance           INTEGER NOT NULL DEFAULT 5,
                repeat_kind          INTEGER NOT NULL DEFAULT 0,
                trigger_times        TEXT    NOT NULL DEFAULT '32400,64800',
                week_days            INTEGER NOT NULL DEFAULT 127,
                month_days           INTEGER NOT NULL DEFAULT 536870910,
                start_at             TEXT    NULL,
                due_at               TEXT    NULL,
                completed_count      INTEGER NOT NULL DEFAULT 0,
                last_completed_at    TEXT    NULL,
                archived             INTEGER NOT NULL DEFAULT 0,
                archive_reason       TEXT    NULL,
                created_at           TEXT    NOT NULL,
                updated_at           TEXT    NOT NULL,
                archived_at          TEXT    NULL,
                completed_at         TEXT    NULL
            );
            """);
        Execute("CREATE INDEX IF NOT EXISTS idx_tasks_archived ON tasks(archived);");

        // Older databases used a reset-time / cycle-index model. Keep their rows readable
        // by adding the new schedule columns in place; the old columns are simply unused.
        EnsureColumn("trigger_times", "TEXT NOT NULL DEFAULT '32400,64800'");
        EnsureColumn("week_days", "INTEGER NOT NULL DEFAULT 127");
        EnsureColumn("month_days", "INTEGER NOT NULL DEFAULT 536870910");
        EnsureColumn("last_completed_at", "TEXT NULL");
    }

    void EnsureColumn(string name, string definition)
    {
        if (HasColumn(name)) return;
        Execute($"ALTER TABLE tasks ADD COLUMN {name} {definition};");
    }

    bool HasColumn(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(tasks);";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    public List<TaskItem> GetAll()
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT id, title, importance, repeat_kind, trigger_times, week_days, month_days,
                       start_at, due_at, completed_count, last_completed_at,
                       archived, archive_reason, created_at, updated_at, archived_at, completed_at
                FROM tasks ORDER BY id;
                """;
            using var reader = cmd.ExecuteReader();
            var list = new List<TaskItem>();
            while (reader.Read())
            {
                var kind = (RepeatKind)reader.GetInt32(3);
                if (kind is not (RepeatKind.Once or RepeatKind.Daily or
                                 RepeatKind.Weekly or RepeatKind.Monthly))
                    kind = RepeatKind.Daily;

                list.Add(new TaskItem
                {
                    Id = reader.GetInt64(0),
                    Title = reader.GetString(1),
                    Importance = reader.GetInt32(2),
                    Repeat = kind,
                    TriggerSeconds = ParseTriggers(reader.IsDBNull(4) ? null : reader.GetString(4)),
                    WeekDays = reader.GetInt32(5),
                    MonthDays = reader.GetInt32(6),
                    StartAt = ReadDate(reader, 7),
                    DueAt = ReadDate(reader, 8),
                    CompletedCount = reader.GetInt32(9),
                    LastCompletedAt = ReadDate(reader, 10),
                    Archived = reader.GetInt32(11) != 0,
                    ArchiveReason = reader.IsDBNull(12) ? null : reader.GetString(12),
                    CreatedAt = ReadDate(reader, 13) ?? UtcTime.Now,
                    UpdatedAt = ReadDate(reader, 14) ?? UtcTime.Now,
                    ArchivedAt = ReadDate(reader, 15),
                    CompletedAt = ReadDate(reader, 16),
                });
            }
            return list;
        }
    }

    public void Insert(TaskItem t)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO tasks (title, importance, repeat_kind, trigger_times, week_days, month_days,
                                   start_at, due_at, completed_count, last_completed_at,
                                   archived, archive_reason, created_at, updated_at, archived_at, completed_at)
                VALUES ($title, $importance, $repeat, $triggers, $weekDays, $monthDays,
                        $start, $due, $completed, $lastCompleted,
                        $archived, $reason, $created, $updated, $archivedAt, $completedAt)
                RETURNING id;
                """;
            Bind(cmd, t);
            t.Id = (long)(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public void Update(TaskItem t)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                UPDATE tasks SET
                    title=$title, importance=$importance, repeat_kind=$repeat,
                    trigger_times=$triggers, week_days=$weekDays, month_days=$monthDays,
                    start_at=$start, due_at=$due, completed_count=$completed,
                    last_completed_at=$lastCompleted, archived=$archived,
                    archive_reason=$reason, created_at=$created, updated_at=$updated,
                    archived_at=$archivedAt, completed_at=$completedAt
                WHERE id=$id;
                """;
            Bind(cmd, t);
            cmd.Parameters.AddWithValue("$id", t.Id);
            cmd.ExecuteNonQuery();
        }
    }

    public void Delete(long id)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    static void Bind(SqliteCommand cmd, TaskItem t)
    {
        cmd.Parameters.AddWithValue("$title", t.Title);
        cmd.Parameters.AddWithValue("$importance", Math.Clamp(t.Importance, 0, 10));
        cmd.Parameters.AddWithValue("$repeat", (int)t.Repeat);
        cmd.Parameters.AddWithValue("$triggers",
            string.Join(',', t.EffectiveTriggers));
        cmd.Parameters.AddWithValue("$weekDays", t.WeekDays);
        cmd.Parameters.AddWithValue("$monthDays", t.MonthDays);
        cmd.Parameters.AddWithValue("$start", DateParam(t.StartAt));
        cmd.Parameters.AddWithValue("$due", DateParam(t.DueAt));
        cmd.Parameters.AddWithValue("$completed", t.CompletedCount);
        cmd.Parameters.AddWithValue("$lastCompleted", DateParam(t.LastCompletedAt));
        cmd.Parameters.AddWithValue("$archived", t.Archived ? 1 : 0);
        cmd.Parameters.AddWithValue("$reason", (object?)t.ArchiveReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", FormatDate(t.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", FormatDate(t.UpdatedAt));
        cmd.Parameters.AddWithValue("$archivedAt", DateParam(t.ArchivedAt));
        cmd.Parameters.AddWithValue("$completedAt", DateParam(t.CompletedAt));
    }

    static List<int> ParseTriggers(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var values = new List<int>();
        foreach (var part in text.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(part, out var seconds) && seconds is >= 0 and < 86400)
                values.Add(seconds);
        return values.Distinct().OrderBy(v => v).ToList();
    }

    static string FormatDate(DateTime value) => UtcTime.Iso(value);

    static object DateParam(DateTime? value) =>
        value.HasValue ? FormatDate(value.Value) : DBNull.Value;

    static DateTime? ReadDate(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return null;
        var text = reader.GetString(ordinal);
        return UtcTime.TryParse(text, out var utc) ? utc : null;
    }

    void Execute(string sql)
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Closing must never throw; WAL already guarantees consistency.
            }
            _connection.Dispose();
        }
    }
}
