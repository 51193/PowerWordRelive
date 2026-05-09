using System.Text.RegularExpressions;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public class RefinementContainer : IncrementalListContainer
{
    private static readonly Regex SpeakerContentPattern = new(@"^([^：:]+)[：:](.+)$", RegexOptions.Compiled);

    private readonly LLMDatabase _db;

    public RefinementContainer(LLMDatabase db)
    {
        _db = db;
    }

    protected override List<(double Id, string Content)> GetWindowFromDb(int count)
    {
        return _db.GetRefinementWindow(count)
            .ConvertAll(e => (e.Id, $"{e.Speaker}：{e.Content}"));
    }

    protected override string FormatEntryForDisplay(double id, string content)
    {
        return content;
    }

    protected override double GetNextIdInDb(double afterId)
    {
        return _db.GetNextRefinementId(afterId);
    }

    protected override double GetMaxIdInDb()
    {
        return _db.GetMaxRefinementId();
    }

    protected override void InsertToDb(double id, string content)
    {
        var (speaker, text) = SplitSpeakerContent(content);
        _db.InsertRefinement(id, speaker, text);
    }

    protected override void UpdateToDb(double id, string content)
    {
        var (speaker, text) = SplitSpeakerContent(content);
        _db.UpdateRefinement(id, speaker, text);
    }

    protected override void DeleteFromDb(double id)
    {
        _db.RemoveRefinement(id);
    }

    private static (string Speaker, string Content) SplitSpeakerContent(string content)
    {
        var match = SpeakerContentPattern.Match(content);
        if (match.Success)
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

        return (content, "");
    }
}