using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Parsing;

public class StoryProgressParser : CommandParser<IncrementalOperation>
{
    private const string SpPrefix = "story_progress|";

    protected override string Prefix => SpPrefix;

    protected override IncrementalOperation? ParseLine(string line)
    {
        var body = line[SpPrefix.Length..];
        var parts = body.Split('|');

        if (parts.Length < 2)
            return null;

        var command = parts[0].Trim();

        return command switch
        {
            "append" => ParseAppend(parts),
            "insert" => ParseInsert(parts),
            "edit" => ParseEdit(parts),
            "remove" => ParseRemove(parts),
            _ => null
        };
    }

    private IncrementalOperation? ParseAppend(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        var content = string.Join("|", parts.Skip(1)).Trim();
        if (string.IsNullOrEmpty(content))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Story progress append has empty content");
            return null;
        }

        return IncrementalOperation.Append(content);
    }

    private IncrementalOperation? ParseInsert(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var indexStr = parts[1].Trim();
        var content = string.Join("|", parts.Skip(2)).Trim();

        if (!int.TryParse(indexStr, out var index) || index < 1)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Story progress insert has invalid index: {indexStr}");
            return null;
        }

        if (string.IsNullOrEmpty(content))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Story progress insert has empty content");
            return null;
        }

        return IncrementalOperation.Insert(index, content);
    }

    private IncrementalOperation? ParseEdit(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var indexStr = parts[1].Trim();
        var content = string.Join("|", parts.Skip(2)).Trim();

        if (!int.TryParse(indexStr, out var index) || index < 1)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Story progress edit has invalid index: {indexStr}");
            return null;
        }

        if (string.IsNullOrEmpty(content))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Story progress edit has empty content");
            return null;
        }

        return IncrementalOperation.Edit(index, content);
    }

    private IncrementalOperation? ParseRemove(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        var indexStr = parts[1].Trim();
        if (!int.TryParse(indexStr, out var index) || index < 1)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Story progress remove has invalid index: {indexStr}");
            return null;
        }

        return IncrementalOperation.Remove(index);
    }
}