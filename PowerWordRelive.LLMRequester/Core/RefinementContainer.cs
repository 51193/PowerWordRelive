using System.Text.RegularExpressions;
using PowerWordRelive.Infrastructure.Data;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public class RefinementContainer : IIncrementalContainer<string>
{
    private static readonly Regex SpeakerContentPattern = new(@"^([^：:]+)[：:](.+)$", RegexOptions.Compiled);

    private readonly LLMDatabase _db;
    private List<double> _ids = new();

    public RefinementContainer(LLMDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<string> Get(int count)
    {
        if (count <= 0)
            return Array.Empty<string>();

        var entries = _db.GetRefinementWindow(count);
        _ids = entries.ConvertAll(e => e.Id);

        return entries.ConvertAll(e => $"{e.Speaker}：{e.Content}").AsReadOnly();
    }

    public void Add(int displayIndex, string content)
    {
        if (_ids.Count == 0)
        {
            var (speaker, text) = SplitSpeakerContent(content);
            _db.InsertRefinement(1.0, speaker, text);
            return;
        }

        var idx = displayIndex - 1;
        if (idx >= _ids.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Insert index {displayIndex} exceeds window size {_ids.Count}");
            return;
        }

        var idAt = _ids[idx];
        double newId;

        if (idx + 1 < _ids.Count)
        {
            newId = (idAt + _ids[idx + 1]) / 2.0;
        }
        else
        {
            var nextInDb = _db.GetNextRefinementId(idAt);
            newId = nextInDb > 0 ? (idAt + nextInDb) / 2.0 : idAt + 1.0;
        }
        var (s, c) = SplitSpeakerContent(content);
        _db.InsertRefinement(newId, s, c);
    }

    public void Add(string content)
    {
        var maxId = _ids.Count > 0 ? _ids[^1] : _db.GetMaxRefinementId();
        var newId = maxId == 0.0 ? 1.0 : maxId + 1.0;
        var (speaker, text) = SplitSpeakerContent(content);
        _db.InsertRefinement(newId, speaker, text);
    }

    public void Remove(int displayIndex)
    {
        if (_ids.Count == 0)
            return;

        var idx = displayIndex - 1;
        if (idx >= _ids.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Remove index {displayIndex} exceeds window size {_ids.Count}");
            return;
        }

        _db.RemoveRefinement(_ids[idx]);
    }

    public void Edit(int displayIndex, string newContent)
    {
        if (_ids.Count == 0)
            return;

        var idx = displayIndex - 1;
        if (idx >= _ids.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Edit index {displayIndex} exceeds window size {_ids.Count}");
            return;
        }

        var (speaker, text) = SplitSpeakerContent(newContent);
        _db.UpdateRefinement(_ids[idx], speaker, text);
    }

    private static (string Speaker, string Content) SplitSpeakerContent(string content)
    {
        var match = SpeakerContentPattern.Match(content);
        if (match.Success)
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

        return (content, "");
    }
}
