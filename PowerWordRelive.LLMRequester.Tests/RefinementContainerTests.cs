using Microsoft.Data.Sqlite;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;
using Xunit;

namespace PowerWordRelive.LLMRequester.Tests;

public class RefinementContainerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly LLMDatabase _db;
    private readonly RefinementContainer _container;

    public RefinementContainerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_container_{Guid.NewGuid()}.db");
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

        _db = new LLMDatabase(_dbPath);
        _db.TryEnsureRefinementTable();
        _container = new RefinementContainer(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
        }
    }

    [Fact]
    public void Get_EmptyTable_ReturnsEmpty()
    {
        Assert.Empty(_container.Get(20));
    }

    [Fact]
    public void Append_CreatesSequentialFloatIds()
    {
        _container.Add("主持人：A");
        _container.Add("[场景]：B");

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(2, entries.Count);
        Assert.Equal(1.0, entries[0].Id);
        Assert.Equal(2.0, entries[1].Id);
    }

    [Fact]
    public void Append_SplitsSpeakerFromContent()
    {
        _container.Add("主持人：欢迎各位");

        var entries = _db.GetRefinementWindow(1);
        Assert.Equal("主持人", entries[0].Speaker);
        Assert.Equal("欢迎各位", entries[0].Content);
    }

    [Fact]
    public void Insert_BetweenTwo_Midpoint()
    {
        _container.Add("S0：c0"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Get(2);

        _container.Add(1, "[场景]：插入");

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(3, entries.Count);
        Assert.Equal(1.0, entries[0].Id);
        Assert.Equal(1.5, entries[1].Id);
        Assert.Equal(2.0, entries[2].Id);
    }

    [Fact]
    public void Insert_AtWindowEnd_QueriesDbForNextId()
    {
        // 5 entries, window size 3 → LLM sees entries 3,4,5 as display 1,2,3
        _container.Add("S0：c0"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Add("S2：c2"); // 3.0
        _container.Add("S3：c3"); // 4.0
        _container.Add("S4：c4"); // 5.0
        _container.Get(3); // window → ids [3.0, 4.0, 5.0]

        _container.Add(3, "inserted"); // insert after display 3 → after 5.0

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(6, entries.Count);
        Assert.Equal(5.0, entries[4].Id);
        Assert.Equal(6.0, entries[5].Id); // 5.0 + 1.0 = 6.0
    }

    [Fact]
    public void Insert_PastWindowEnd_QueriesDbCorrectly()
    {
        // 5 entries, window 3, but window has all 5 because count > total
        _container.Add("S0：c0"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Get(2); // window → [1.0, 2.0]

        _container.Add(2, "after last"); // insert after display 2 → after 2.0, DB has no more entries → 3.0

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(3, entries.Count);
        Assert.Equal(3.0, entries[2].Id);
    }

    [Fact]
    public void Remove_UsesIdsArrayIndex()
    {
        _container.Add("S0：c0"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Add("S2：c2"); // 3.0
        _container.Get(3); // ids [1.0, 2.0, 3.0]

        _container.Remove(2); // display 2 → ids[1] = 2.0

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(2, entries.Count);
        Assert.Equal(1.0, entries[0].Id);
        Assert.Equal(3.0, entries[1].Id);
    }

    [Fact]
    public void Edit_UsesIdsArrayIndex()
    {
        _container.Add("S0：old"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Get(2); // ids [1.0, 2.0]

        _container.Edit(1, "S0：new");

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal("new", entries[0].Content);
        Assert.Equal("c1", entries[1].Content);
    }

    [Fact]
    public void RemoveThenEdit_DifferentPositions_FixedSnapshot()
    {
        _container.Add("S0：c0"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Add("S2：c2"); // 3.0
        _container.Get(3); // ids [1.0, 2.0, 3.0]

        _container.Remove(2); // delete ids[1] = 2.0
        _container.Edit(3, "S2：modified"); // edit ids[2] = 3.0

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(2, entries.Count);
        Assert.Equal(1.0, entries[0].Id);
        Assert.Equal("c0", entries[0].Content);
        Assert.Equal(3.0, entries[1].Id);
        Assert.Equal("modified", entries[1].Content);
    }

    [Fact]
    public void InsertThenEdit_EditTargetsOriginalPosition()
    {
        _container.Add("S0：A"); // 1.0
        _container.Add("S1：B"); // 2.0
        _container.Get(2); // ids [1.0, 2.0]

        _container.Add(1, "inserted"); // insert after display 1 → 1.5
        _container.Edit(2, "S1：modified"); // edit display 2 → ids[1] = 2.0

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(3, entries.Count);
        Assert.Equal(1.0, entries[0].Id);
        Assert.Equal(1.5, entries[1].Id);
        Assert.Equal(2.0, entries[2].Id);
        Assert.Equal("modified", entries[2].Content);
    }

    [Fact]
    public void Get_ReturnsWindowSubset()
    {
        for (var i = 0; i < 5; i++)
            _container.Add($"Speaker{i}：内容{i}");

        var result = _container.Get(3);

        Assert.Equal(3, result.Count);
        Assert.Equal("Speaker2：内容2", result[0]);
        Assert.Equal("Speaker3：内容3", result[1]);
        Assert.Equal("Speaker4：内容4", result[2]);
    }

    [Fact]
    public void Append_AfterGet_SeedsFromMaxWindowId()
    {
        _container.Add("S0：c0"); // 1.0
        _container.Add("S1：c1"); // 2.0
        _container.Get(1); // ids [2.0]

        _container.Add("new"); // max from _ids = 2.0 → 3.0

        var entries = _db.GetRefinementWindow(10);
        Assert.Equal(3, entries.Count);
        Assert.Equal(3.0, entries[2].Id);
    }

    [Fact]
    public void Insert_OutOfRange_Noop()
    {
        _container.Add("S0：c0");
        _container.Get(1); // ids [1.0]

        _container.Add(5, "should not insert"); // display 5 > 1

        var entries = _db.GetRefinementWindow(10);
        Assert.Single(entries);
    }
}