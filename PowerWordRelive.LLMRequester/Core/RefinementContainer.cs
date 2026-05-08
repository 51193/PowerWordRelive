using System.Text.RegularExpressions;
using PowerWordRelive.Infrastructure.Data;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public class RefinementContainer : IIncrementalContainer<string>
{
    private static readonly Regex SpeakerContentPattern = new(@"^([^：:]+)[：:](.+)$", RegexOptions.Compiled);

    private readonly LLMDatabase _db;
    private List<double> _floatIds = new();
    private int _lastWindowSize;
    private int _windowStart;

    public RefinementContainer(LLMDatabase db)
    {
        _db = db;
    }

    public IReadOnlyList<string> Get(int count)
    {
        _lastWindowSize = count;

        var entries = _db.GetAllRefinementEntries();
        _floatIds = entries.ConvertAll(e => e.Id);
        _windowStart = Math.Max(0, _floatIds.Count - count);

        var window = entries.GetRange(_windowStart, entries.Count - _windowStart);
        return window.ConvertAll(e => $"{e.Speaker}：{e.Content}").AsReadOnly();
    }

    public void Add(int displayIndex, string content)
    {
        if (_floatIds.Count == 0)
        {
            var (speaker, text) = SplitSpeakerContent(content);
            _db.InsertRefinement(1.0, speaker, text);
            _floatIds.Add(1.0);
            _windowStart = 0;
            return;
        }

        var realIdx = _windowStart + displayIndex - 1;
        if (realIdx >= _floatIds.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Refinement insert index {displayIndex} exceeds snapshot range");
            return;
        }

        var idAt = _floatIds[realIdx];
        var idNext = realIdx + 1 < _floatIds.Count
            ? _floatIds[realIdx + 1]
            : idAt + 1.0;

        var newId = (idAt + idNext) / 2.0;
        var (s, c) = SplitSpeakerContent(content);
        _db.InsertRefinement(newId, s, c);
        _floatIds.Insert(realIdx + 1, newId);
    }

    public void Add(string content)
    {
        var maxId = _floatIds.Count > 0 ? _floatIds[^1] : 0.0;
        var newId = maxId == 0.0 ? 1.0 : maxId + 1.0;
        var (speaker, text) = SplitSpeakerContent(content);
        _db.InsertRefinement(newId, speaker, text);
        _floatIds.Add(newId);
    }

    public void Remove(int displayIndex)
    {
        if (_floatIds.Count == 0)
            return;

        var realIdx = _windowStart + displayIndex - 1;
        if (realIdx >= _floatIds.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Refinement remove index {displayIndex} exceeds snapshot range");
            return;
        }

        var floatId = _floatIds[realIdx];
        _db.RemoveRefinement(floatId);
        _floatIds.RemoveAt(realIdx);
    }

    public void Edit(int displayIndex, string newContent)
    {
        if (_floatIds.Count == 0)
            return;

        var realIdx = _windowStart + displayIndex - 1;
        if (realIdx >= _floatIds.Count)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Refinement edit index {displayIndex} exceeds snapshot range");
            return;
        }

        var floatId = _floatIds[realIdx];
        var (speaker, text) = SplitSpeakerContent(newContent);
        _db.UpdateRefinement(floatId, speaker, text);
    }

    private static (string Speaker, string Content) SplitSpeakerContent(string content)
    {
        var match = SpeakerContentPattern.Match(content);
        if (match.Success)
            return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

        return (content, "");
    }
}