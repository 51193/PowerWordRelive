using Microsoft.Data.Sqlite;
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

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}