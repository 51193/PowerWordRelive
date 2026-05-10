using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.LocalBackend.Services;

public class DatabaseReader
{
    private readonly string _dbPath;
    private readonly IFileSystem _fs;

    public DatabaseReader(string dbPath, IFileSystem fs)
    {
        _dbPath = dbPath;
        _fs = fs;
    }

    public int GetDataVersion()
    {
        if (!_fs.FileExists(_dbPath))
            return -1;

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA data_version";
        return Convert.ToInt32(cmd.ExecuteScalar()!);
    }

    public async Task<object> GetAllDataAsync()
    {
        var refinements = new List<object>();
        var storyProgress = new List<object>();
        var tasksByStatus = new Dictionary<string, List<object>>
        {
            ["in_progress"] = new(),
            ["complete"] = new(),
            ["fail"] = new(),
            ["discard"] = new()
        };
        var consistency = new List<object>();

        if (!_fs.FileExists(_dbPath))
            return BuildResult(refinements, storyProgress, tasksByStatus, consistency);

        await using var conn = CreateReadOnlyConnection();
        await conn.OpenAsync();

        if (await TableExists(conn, "refinement_results"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT speaker, content FROM refinement_results ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                refinements.Add(new { speaker = reader.GetString(0), content = reader.GetString(1) });
        }

        if (await TableExists(conn, "story_progress"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT content FROM story_progress ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                storyProgress.Add(new { content = reader.GetString(0) });
        }

        if (await TableExists(conn, "task_entries"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT summary, detail, status FROM task_entries ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var status = reader.GetString(2);
                if (tasksByStatus.ContainsKey(status))
                    tasksByStatus[status].Add(new { summary = reader.GetString(0), detail = reader.GetString(1) });
            }
        }

        if (await TableExists(conn, "consistency_entries"))
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name, detail, tag FROM consistency_entries WHERE deleted = 0 ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                consistency.Add(new
                    { name = reader.GetString(0), detail = reader.GetString(1), tag = reader.GetString(2) });
        }

        return BuildResult(refinements, storyProgress, tasksByStatus, consistency);
    }

    private static object BuildResult(
        List<object> refinements,
        List<object> storyProgress,
        Dictionary<string, List<object>> tasksByStatus,
        List<object> consistency)
    {
        return new
        {
            refinements,
            story_progress = storyProgress,
            tasks = new Dictionary<string, object>
            {
                ["in_progress"] = tasksByStatus["in_progress"],
                ["complete"] = tasksByStatus["complete"],
                ["fail"] = tasksByStatus["fail"],
                ["discard"] = tasksByStatus["discard"]
            },
            consistency
        };
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
        return await cmd.ExecuteScalarAsync() != null;
    }
}