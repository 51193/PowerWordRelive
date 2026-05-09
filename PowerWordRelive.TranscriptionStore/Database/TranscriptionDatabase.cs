using Microsoft.Data.Sqlite;
using PowerWordRelive.TranscriptionStore.Models;

namespace PowerWordRelive.TranscriptionStore.Database;

internal class TranscriptionDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TranscriptionDatabase(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        using var pragmaCmd = _connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL";
        pragmaCmd.ExecuteNonQuery();

        EnsureSchema();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS transcriptions (
                                  id                   INTEGER PRIMARY KEY AUTOINCREMENT,
                                  start_timestamp_ms   INTEGER NOT NULL,
                                  end_timestamp_ms     INTEGER NOT NULL,
                                  speaker_id           TEXT    NOT NULL,
                                  text                 TEXT    NOT NULL,
                                  source_file          TEXT    NOT NULL
                              );
                          """;
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            "CREATE INDEX IF NOT EXISTS idx_transcriptions_start_time ON transcriptions(start_timestamp_ms)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS speaker_mappings (
                                  speaker_id TEXT PRIMARY KEY NOT NULL,
                                  role_name  TEXT NOT NULL
                              );
                          """;
        cmd.ExecuteNonQuery();
    }

    public void EnsureSpeakerExists(string speakerId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO speaker_mappings (speaker_id, role_name) VALUES (@speaker, @role)";
        cmd.Parameters.AddWithValue("@speaker", speakerId);
        cmd.Parameters.AddWithValue("@role", "__UNASSIGNED__");
        cmd.ExecuteNonQuery();
    }

    public void Insert(IReadOnlyList<TranscriptionEntry> entries)
    {
        if (entries.Count == 0)
            return;

        using var tx = _connection.BeginTransaction();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
                              INSERT INTO transcriptions (start_timestamp_ms, end_timestamp_ms, speaker_id, text, source_file)
                              VALUES (@start, @end, @speaker, @text, @source)
                          """;

        var startParam = cmd.Parameters.Add("@start", SqliteType.Integer);
        var endParam = cmd.Parameters.Add("@end", SqliteType.Integer);
        var speakerParam = cmd.Parameters.Add("@speaker", SqliteType.Text);
        var textParam = cmd.Parameters.Add("@text", SqliteType.Text);
        var sourceParam = cmd.Parameters.Add("@source", SqliteType.Text);

        foreach (var e in entries)
        {
            startParam.Value = e.StartTimestampMs;
            endParam.Value = e.EndTimestampMs;
            speakerParam.Value = e.SpeakerId;
            textParam.Value = e.Text;
            sourceParam.Value = e.SourceFile;
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}