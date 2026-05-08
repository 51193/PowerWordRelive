using Microsoft.Data.Sqlite;
using PowerWordRelive.LLMRequester.Database;
using Xunit;

namespace PowerWordRelive.LLMRequester.Tests;

public class RefinementDatabaseTests : IDisposable
{
    private readonly string _dbPath;

    public RefinementDatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_refine_{Guid.NewGuid()}.db");

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                              CREATE TABLE IF NOT EXISTS transcriptions (
                                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                                  start_timestamp_ms INTEGER NOT NULL,
                                  end_timestamp_ms INTEGER NOT NULL,
                                  speaker_id TEXT NOT NULL,
                                  text TEXT NOT NULL,
                                  source_file TEXT NOT NULL
                              );
                              CREATE TABLE IF NOT EXISTS speaker_mappings (
                                  speaker_id TEXT PRIMARY KEY NOT NULL,
                                  role_name TEXT NOT NULL
                              );
                          """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
        }
    }

    [Fact]
    public void TryEnsureRefinementTable_CreatesTable()
    {
        using var db = new LLMDatabase(_dbPath);
        var ok = db.TryEnsureRefinementTable();
        Assert.True(ok);
    }

    [Fact]
    public void TryEnsureRefinementTable_Idempotent()
    {
        using var db = new LLMDatabase(_dbPath);
        Assert.True(db.TryEnsureRefinementTable());
        Assert.True(db.TryEnsureRefinementTable());
    }

    [Fact]
    public void GetRefinementWindow_ReturnsInAscOrder()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        db.InsertRefinement(3.0, "梅琳", "我是梅琳");
        db.InsertRefinement(1.0, "主持人", "欢迎冒险者");
        db.InsertRefinement(2.0, "[场景]", "冰风谷");

        var window = db.GetRefinementWindow(2);
        Assert.Equal(2, window.Count);
        Assert.Equal(2.0, window[0].Id);
        Assert.Equal(3.0, window[1].Id);
    }

    [Fact]
    public void GetRefinementWindow_LessThanCount_ReturnsAll()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        db.InsertRefinement(1.0, "主持人", "欢迎");

        var window = db.GetRefinementWindow(10);
        Assert.Single(window);
    }

    [Fact]
    public void RemoveRefinement_DeletesByFloatId()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        db.InsertRefinement(1.0, "主持人", "欢迎");
        db.InsertRefinement(2.0, "梅琳", "我是梅琳");

        db.RemoveRefinement(1.0);

        var window = db.GetRefinementWindow(10);
        Assert.Single(window);
        Assert.Equal(2.0, window[0].Id);
    }

    [Fact]
    public void UpdateRefinement_ModifiesByFloatId()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        db.InsertRefinement(1.0, "梅琳", "旧");

        db.UpdateRefinement(1.0, "梅琳", "新");

        var window = db.GetRefinementWindow(1);
        Assert.Equal("新", window[0].Content);
    }

    [Fact]
    public void GetMaxRefinementId_Empty_ReturnsZero()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        Assert.Equal(0.0, db.GetMaxRefinementId());
    }

    [Fact]
    public void GetMaxRefinementId_ReturnsHighest()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        db.InsertRefinement(1.0, "A", "a");
        db.InsertRefinement(2.5, "B", "b");
        Assert.Equal(2.5, db.GetMaxRefinementId());
    }

    [Fact]
    public void GetNextRefinementId_ReturnsNext()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();
        db.InsertRefinement(1.0, "A", "a");
        db.InsertRefinement(2.0, "B", "b");
        db.InsertRefinement(3.5, "C", "c");

        Assert.Equal(2.0, db.GetNextRefinementId(1.0));
        Assert.Equal(3.5, db.GetNextRefinementId(2.0));
        Assert.Equal(-1.0, db.GetNextRefinementId(3.5));
    }

    [Fact]
    public void GetLatestDialogues_ReturnsMappedEntries()
    {
        using var db = new LLMDatabase(_dbPath);
        db.TryEnsureRefinementTable();

        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO speaker_mappings VALUES ('spk_a', '梅琳')";
            cmd.ExecuteNonQuery();
            cmd.CommandText =
                "INSERT INTO transcriptions (start_timestamp_ms, end_timestamp_ms, speaker_id, text, source_file) VALUES (0,1000,'spk_a','大家好','test.srt')";
            cmd.ExecuteNonQuery();
        }

        var dialogues = db.GetLatestDialogues(10);
        Assert.Single(dialogues);
        Assert.Equal("spk_a", dialogues[0].SpeakerId);
        Assert.Equal("梅琳", dialogues[0].RoleName);
    }
}