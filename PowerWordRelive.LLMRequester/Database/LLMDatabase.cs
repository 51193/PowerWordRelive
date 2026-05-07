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

    public List<string> GetDialogueForSpeaker(string speakerId)
    {
        var lines = new List<string>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT text FROM transcriptions
            WHERE speaker_id = @speaker
            ORDER BY start_timestamp_ms";
        cmd.Parameters.AddWithValue("@speaker", speakerId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            lines.Add(reader.GetString(0));

        return lines;
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