using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public class StoryProgressContainer : IncrementalListContainer
{
    private readonly LLMDatabase _db;

    public StoryProgressContainer(LLMDatabase db)
    {
        _db = db;
    }

    protected override List<(double Id, string Content)> GetWindowFromDb(int count)
    {
        return _db.GetStoryProgressWindow(count);
    }

    protected override string FormatEntryForDisplay(double id, string content)
    {
        return content;
    }

    protected override double GetNextIdInDb(double afterId)
    {
        return _db.GetNextStoryProgressId(afterId);
    }

    protected override double GetMaxIdInDb()
    {
        return _db.GetMaxStoryProgressId();
    }

    protected override void InsertToDb(double id, string content)
    {
        _db.InsertStoryProgress(id, content);
    }

    protected override void UpdateToDb(double id, string content)
    {
        _db.UpdateStoryProgress(id, content);
    }

    protected override void DeleteFromDb(double id)
    {
        _db.RemoveStoryProgress(id);
    }
}