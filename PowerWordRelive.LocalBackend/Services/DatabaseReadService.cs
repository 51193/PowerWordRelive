using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.LocalBackend.Services;

public class DatabaseReadService
{
    private readonly string _dbPath;
    private readonly IFileSystem _fs;

    public DatabaseReadService(string dbPath, IFileSystem fs)
    {
        _dbPath = dbPath;
        _fs = fs;
    }

    public async Task<(List<object> items, int total)> ListRefinementsAsync(int limit, int offset)
    {
        if (!_fs.FileExists(_dbPath))
            return (new List<object>(), 0);

        await using var conn = CreateReadOnlyConnection();
        await conn.OpenAsync();

        if (!await TableExists(conn, "refinement_results"))
            return (new List<object>(), 0);

        var items = new List<object>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT speaker, content FROM refinement_results ORDER BY id LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new { speaker = reader.GetString(0), content = reader.GetString(1) });

        var total = 0;
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM refinement_results";
        var countResult = await countCmd.ExecuteScalarAsync();
        if (countResult != null)
            total = Convert.ToInt32(countResult);

        return (items, total);
    }

    public async Task<(List<object> items, int total)> ListTranscriptionsAsync(int limit, int offset)
    {
        if (!_fs.FileExists(_dbPath))
            return (new List<object>(), 0);

        await using var conn = CreateReadOnlyConnection();
        await conn.OpenAsync();

        if (!await TableExists(conn, "transcriptions"))
            return (new List<object>(), 0);

        var speakerTableExists = await TableExists(conn, "speaker_mappings");

        var items = new List<object>();
        var query = speakerTableExists
            ? @"SELECT t.start_timestamp_ms, COALESCE(sm.role_name, t.speaker_id), t.text
               FROM transcriptions t
               LEFT JOIN speaker_mappings sm ON t.speaker_id = sm.speaker_id
               ORDER BY t.start_timestamp_ms
               LIMIT @limit OFFSET @offset"
            : @"SELECT t.start_timestamp_ms, t.speaker_id, t.text
               FROM transcriptions t
               ORDER BY t.start_timestamp_ms
               LIMIT @limit OFFSET @offset";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var ms = reader.GetInt64(0);
            var time = TimeSpan.FromMilliseconds(ms);
            var timeStr = $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
            items.Add(new { time = timeStr, speaker = reader.GetString(1), text = reader.GetString(2) });
        }

        var total = 0;
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM transcriptions";
        var countResult = await countCmd.ExecuteScalarAsync();
        if (countResult != null)
            total = Convert.ToInt32(countResult);

        return (items, total);
    }

    private SqliteConnection CreateReadOnlyConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
    }

    private static async Task<bool> TableExists(SqliteConnection conn, string tableName)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }
}