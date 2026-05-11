using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Models;

namespace PowerWordRelive.LLMRequester.Database;

public class LLMDatabase : IDisposable
{
    private const string UnassignedValue = "__UNASSIGNED__";

    private readonly SqliteConnection _connection;

    public LLMDatabase(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL";
        pragma.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    public List<SpeakerMapping> GetUnassignedSpeakers()
    {
        var list = new List<SpeakerMapping>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT speaker_id, role_name FROM speaker_mappings WHERE role_name = @role";
        cmd.Parameters.AddWithValue("@role", UnassignedValue);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new SpeakerMapping(
                reader.GetString(0),
                reader.GetString(1)));

        return list;
    }

    public List<long> GetTranscriptionIdsForSpeaker(string speakerId)
    {
        var ids = new List<long>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM transcriptions WHERE speaker_id = @speaker ORDER BY id";
        cmd.Parameters.AddWithValue("@speaker", speakerId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetInt64(0));

        return ids;
    }

    public List<DialogueEntry> GetDialogueRange(long minId, long maxId)
    {
        var entries = new List<DialogueEntry>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, speaker_id, text FROM transcriptions
            WHERE id BETWEEN @min AND @max
            ORDER BY id";
        cmd.Parameters.AddWithValue("@min", minId);
        cmd.Parameters.AddWithValue("@max", maxId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            entries.Add(new DialogueEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));

        return entries;
    }

    public Dictionary<string, string> GetSpeakerNameMap()
    {
        var map = new Dictionary<string, string>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT speaker_id, role_name FROM speaker_mappings";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            map[reader.GetString(0)] = reader.GetString(1);

        return map;
    }

    public bool TryUpdateSpeakerRole(string speakerId, string newRole, out string? currentRole)
    {
        using var txn = _connection.BeginTransaction();

        using var updateCmd = _connection.CreateCommand();
        updateCmd.CommandText = @"
            UPDATE speaker_mappings
            SET role_name = @new
            WHERE speaker_id = @speaker AND role_name = @unassigned";
        updateCmd.Parameters.AddWithValue("@new", newRole);
        updateCmd.Parameters.AddWithValue("@speaker", speakerId);
        updateCmd.Parameters.AddWithValue("@unassigned", UnassignedValue);
        var rows = updateCmd.ExecuteNonQuery();

        if (rows > 0)
        {
            txn.Commit();
            currentRole = null;
            return true;
        }

        using var readCmd = _connection.CreateCommand();
        readCmd.CommandText = "SELECT role_name FROM speaker_mappings WHERE speaker_id = @speaker";
        readCmd.Parameters.AddWithValue("@speaker", speakerId);
        var result = readCmd.ExecuteScalar();
        currentRole = result as string;

        txn.Commit();
        return false;
    }

    public bool TryEnsureRefinementTable()
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS refinement_results (
                                      id       REAL PRIMARY KEY NOT NULL,
                                      speaker  TEXT NOT NULL,
                                      content  TEXT NOT NULL
                                  );
                              """;
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Refinement table not ready: {ex.Message}");
            return false;
        }
    }

    public List<(double Id, string Speaker, string Content)> GetRefinementWindow(int count)
    {
        var list = new List<(double, string, string)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              SELECT id, speaker, content FROM refinement_results
                              ORDER BY id DESC
                              LIMIT @count
                          """;
        cmd.Parameters.AddWithValue("@count", count);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetDouble(0), reader.GetString(1), reader.GetString(2)));

        list.Reverse();
        return list;
    }

    public double GetNextRefinementId(double afterId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MIN(id), -1.0) FROM refinement_results WHERE id > @after";
        cmd.Parameters.AddWithValue("@after", afterId);

        var result = cmd.ExecuteScalar();
        var next = result is double d ? d : Convert.ToDouble(result!);
        return next > 0 ? next : -1.0;
    }

    public void InsertRefinement(double id, string speaker, string content)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO refinement_results (id, speaker, content) VALUES (@id, @speaker, @content)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@speaker", speaker);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.ExecuteNonQuery();
    }

    public void RemoveRefinement(double id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM refinement_results WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateRefinement(double id, string speaker, string content)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE refinement_results SET speaker = @speaker, content = @content WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@speaker", speaker);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.ExecuteNonQuery();
    }

    public double GetMaxRefinementId()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(id), 0.0) FROM refinement_results";

        var result = cmd.ExecuteScalar();
        return result is double d ? d : Convert.ToDouble(result!);
    }

    public bool TryEnsureStoryProgressTable()
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS story_progress (
                                      id       REAL PRIMARY KEY NOT NULL,
                                      content  TEXT NOT NULL
                                  );
                              """;
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Story progress table not ready: {ex.Message}");
            return false;
        }
    }

    public List<(double Id, string Content)> GetStoryProgressWindow(int count)
    {
        var list = new List<(double, string)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              SELECT id, content FROM story_progress
                              ORDER BY id DESC
                              LIMIT @count
                          """;
        cmd.Parameters.AddWithValue("@count", count);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetDouble(0), reader.GetString(1)));

        list.Reverse();
        return list;
    }

    public double GetNextStoryProgressId(double afterId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MIN(id), -1.0) FROM story_progress WHERE id > @after";
        cmd.Parameters.AddWithValue("@after", afterId);

        var result = cmd.ExecuteScalar();
        var next = result is double d ? d : Convert.ToDouble(result!);
        return next > 0 ? next : -1.0;
    }

    public double GetMaxStoryProgressId()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(id), 0.0) FROM story_progress";

        var result = cmd.ExecuteScalar();
        return result is double d ? d : Convert.ToDouble(result!);
    }

    public void InsertStoryProgress(double id, string content)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO story_progress (id, content) VALUES (@id, @content)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.ExecuteNonQuery();
    }

    public void UpdateStoryProgress(double id, string content)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE story_progress SET content = @content WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.ExecuteNonQuery();
    }

    public void RemoveStoryProgress(double id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM story_progress WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public List<DialogueWindowEntry> GetLatestDialogues(int count)
    {
        var list = new List<DialogueWindowEntry>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              SELECT t.id, t.speaker_id, t.text, sm.role_name
                              FROM transcriptions t
                              LEFT JOIN speaker_mappings sm ON t.speaker_id = sm.speaker_id
                              ORDER BY t.id DESC
                              LIMIT @count
                          """;
        cmd.Parameters.AddWithValue("@count", count);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var speakerId = reader.GetString(1);
            var roleName = reader.IsDBNull(3) ? null : reader.GetString(3);
            list.Add(new DialogueWindowEntry(
                reader.GetInt64(0),
                speakerId,
                reader.GetString(2),
                roleName));
        }

        list.Reverse();
        return list;
    }

    public bool TryEnsureTaskTable()
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS task_entries (
                                      id       INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                                      summary  TEXT NOT NULL,
                                      detail   TEXT NOT NULL,
                                      status   TEXT NOT NULL DEFAULT 'in_progress'
                                  );
                              """;
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Task table not ready: {ex.Message}");
            return false;
        }
    }

    public bool TryEnsureTaskFinishLogTable()
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS task_finish_log (
                                      id       INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                                      task_id  INTEGER NOT NULL
                                  );
                              """;
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Task finish log table not ready: {ex.Message}");
            return false;
        }
    }

    public List<(int Id, string Summary, string Detail)> GetActiveTasks(int limit)
    {
        var list = new List<(int, string, string)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              SELECT id, summary, detail FROM task_entries
                              WHERE status = 'in_progress'
                              ORDER BY id
                              LIMIT @limit
                          """;
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

        return list;
    }

    public int CountActiveTasks()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM task_entries WHERE status = 'in_progress'";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public List<(int Id, string Summary, string Detail, string Status)> GetRecentFinishedTasks(int limit)
    {
        var list = new List<(int, string, string, string)>();

        if (limit <= 0)
            return list;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              SELECT te.id, te.summary, te.detail, te.status
                              FROM task_entries te
                              INNER JOIN task_finish_log tfl ON te.id = tfl.task_id
                              ORDER BY tfl.id DESC
                              LIMIT @limit
                          """;
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

        return list;
    }

    public void InsertTask(string summary, string detail)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO task_entries (summary, detail, status) VALUES (@summary, @detail, 'in_progress')";
        cmd.Parameters.AddWithValue("@summary", summary);
        cmd.Parameters.AddWithValue("@detail", detail);
        cmd.ExecuteNonQuery();
    }

    public void UpdateTaskDetail(int id, string summary, string detail)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE task_entries SET summary = @summary, detail = @detail WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@summary", summary);
        cmd.Parameters.AddWithValue("@detail", detail);
        cmd.ExecuteNonQuery();
    }

    public void DeleteTask(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM task_entries WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void SetTaskStatus(int id, string status)
    {
        using var txn = _connection.BeginTransaction();
        try
        {
            using var updateCmd = _connection.CreateCommand();
            updateCmd.Transaction = txn;
            updateCmd.CommandText = "UPDATE task_entries SET status = @status WHERE id = @id";
            updateCmd.Parameters.AddWithValue("@status", status);
            updateCmd.Parameters.AddWithValue("@id", id);
            updateCmd.ExecuteNonQuery();

            using var logCmd = _connection.CreateCommand();
            logCmd.Transaction = txn;
            logCmd.CommandText = "INSERT INTO task_finish_log (task_id) VALUES (@task_id)";
            logCmd.Parameters.AddWithValue("@task_id", id);
            logCmd.ExecuteNonQuery();

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
            throw;
        }
    }

    public int? FindActiveTaskIdByKey(string summary)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM task_entries WHERE summary = @summary AND status = 'in_progress' LIMIT 1";
        cmd.Parameters.AddWithValue("@summary", summary);

        var result = cmd.ExecuteScalar();
        return result is DBNull or null ? null : Convert.ToInt32(result);
    }

    public bool TryEnsureConsistencyTable()
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS consistency_entries (
                                      id      INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                                      name    TEXT NOT NULL,
                                      detail  TEXT NOT NULL,
                                      tag     TEXT NOT NULL DEFAULT 'null',
                                      deleted INTEGER NOT NULL DEFAULT 0
                                  );
                              """;
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Consistency table not ready: {ex.Message}");
            return false;
        }
    }

    public List<(int Id, string Name, string Detail, string Tag)> GetActiveConsistencyEntries(int limit)
    {
        var list = new List<(int, string, string, string)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              SELECT id, name, detail, tag FROM consistency_entries
                              WHERE deleted = 0
                              ORDER BY id
                              LIMIT @limit
                          """;
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

        return list;
    }

    public int CountActiveConsistencyEntries()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM consistency_entries WHERE deleted = 0";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void InsertConsistencyEntry(string name, string detail, string tag)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO consistency_entries (name, detail, tag) VALUES (@name, @detail, @tag)";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@detail", detail);
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.ExecuteNonQuery();
    }

    public void UpdateConsistencyEntry(int id, string name, string detail, string tag)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE consistency_entries SET name = @name, detail = @detail, tag = @tag WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@detail", detail);
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.ExecuteNonQuery();
    }

    public void UpdateConsistencyEntryTag(int id, string tag)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE consistency_entries SET tag = @tag WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@tag", tag);
        cmd.ExecuteNonQuery();
    }

    public void SoftDeleteConsistencyEntry(int id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE consistency_entries SET deleted = 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public int? FindActiveConsistencyIdByName(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM consistency_entries WHERE name = @name AND deleted = 0 LIMIT 1";
        cmd.Parameters.AddWithValue("@name", name);

        var result = cmd.ExecuteScalar();
        return result is DBNull or null ? null : Convert.ToInt32(result);
    }

    public bool TryEnsureTokenUsageTable()
    {
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS token_usage (
                                      id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                                      request_key         TEXT NOT NULL,
                                      created_at          TEXT NOT NULL DEFAULT (datetime('now')),
                                      output_tokens       INTEGER NOT NULL DEFAULT 0,
                                      cached_input_tokens INTEGER NOT NULL DEFAULT 0,
                                      miss_input_tokens   INTEGER NOT NULL DEFAULT 0
                                  );
                              """;
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Token usage table not ready: {ex.Message}");
            return false;
        }
    }

    public void InsertTokenUsage(string requestKey, int outputTokens, int cachedInputTokens, int missInputTokens)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO token_usage (request_key, output_tokens, cached_input_tokens, miss_input_tokens)
            VALUES (@key, @output, @cached, @miss)";
        cmd.Parameters.AddWithValue("@key", requestKey);
        cmd.Parameters.AddWithValue("@output", outputTokens);
        cmd.Parameters.AddWithValue("@cached", cachedInputTokens);
        cmd.Parameters.AddWithValue("@miss", missInputTokens);
        cmd.ExecuteNonQuery();
    }
}