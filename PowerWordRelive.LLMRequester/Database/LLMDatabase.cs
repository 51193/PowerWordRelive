using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Models;

namespace PowerWordRelive.LLMRequester.Database;

public class LLMDatabase : IDisposable
{
    private const string UnassignedValue = "__UNASSIGNED__";
    private const string UnknownValue = "__UNKNOWN__";

    private readonly SqliteConnection _connection;

    public LLMDatabase(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL";
        pragma.ExecuteNonQuery();
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

    public void UpdateSpeakerRole(string speakerId, string roleName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE speaker_mappings SET role_name = @role WHERE speaker_id = @speaker";
        cmd.Parameters.AddWithValue("@role", roleName);
        cmd.Parameters.AddWithValue("@speaker", speakerId);
        cmd.ExecuteNonQuery();
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

    public List<(double Id, string Speaker, string Content)> GetAllRefinementEntries()
    {
        var list = new List<(double, string, string)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, speaker, content FROM refinement_results ORDER BY id ASC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add((reader.GetDouble(0), reader.GetString(1), reader.GetString(2)));

        return list;
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

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}