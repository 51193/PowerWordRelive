using PowerWordRelive.Infrastructure.Data;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Core;

public abstract class IncrementalListContainer : IIncrementalContainer<string>
{
    protected readonly List<double> Ids = new();

    protected abstract List<(double Id, string Content)> GetWindowFromDb(int count);
    protected abstract string FormatEntryForDisplay(double id, string content);
    protected abstract double GetNextIdInDb(double afterId);
    protected abstract double GetMaxIdInDb();
    protected abstract void InsertToDb(double id, string content);
    protected abstract void UpdateToDb(double id, string content);
    protected abstract void DeleteFromDb(double id);

    public IReadOnlyList<string> Get(int count)
    {
        if (count <= 0)
            return Array.Empty<string>();

        var entries = GetWindowFromDb(count);
        Ids.Clear();
        Ids.AddRange(entries.ConvertAll(e => e.Id));

        return entries.ConvertAll(e => FormatEntryForDisplay(e.Id, e.Content)).AsReadOnly();
    }

    public void Add(string content)
    {
        var maxId = GetMaxIdInDb();
        var newId = maxId == 0.0 ? 1.0 : maxId + 1.0;
        InsertToDb(newId, content);
    }

    public void Add(int displayIndex, string content)
    {
        if (Ids.Count == 0)
        {
            InsertToDb(1.0, content);
            return;
        }

        var idx = displayIndex - 1;
        if (idx >= Ids.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Insert index {displayIndex} exceeds window size {Ids.Count}");
            return;
        }

        var idAt = Ids[idx];
        double newId;

        if (idx + 1 < Ids.Count)
        {
            newId = (idAt + Ids[idx + 1]) / 2.0;
        }
        else
        {
            var nextInDb = GetNextIdInDb(idAt);
            newId = nextInDb > 0 ? (idAt + nextInDb) / 2.0 : idAt + 1.0;
        }

        InsertToDb(newId, content);
    }

    public void Remove(int displayIndex)
    {
        if (Ids.Count == 0)
            return;

        var idx = displayIndex - 1;
        if (idx >= Ids.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Remove index {displayIndex} exceeds window size {Ids.Count}");
            return;
        }

        DeleteFromDb(Ids[idx]);
    }

    public void Edit(int displayIndex, string newContent)
    {
        if (Ids.Count == 0)
            return;

        var idx = displayIndex - 1;
        if (idx >= Ids.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Edit index {displayIndex} exceeds window size {Ids.Count}");
            return;
        }

        UpdateToDb(Ids[idx], newContent);
    }
}