using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Parsing;

public class RefinementParser : CommandParser<RefinementOperation>
{
    private const string RefinePrefix = "refine|";

    protected override string Prefix => RefinePrefix;

    protected override RefinementOperation? ParseLine(string line)
    {
        var body = line[RefinePrefix.Length..];
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

    private RefinementOperation? ParseAppend(string[] parts)
    {
        // parts[0] == "append", parts[1..] == content
        if (parts.Length < 2)
            return null;

        var content = string.Join("|", parts.Skip(1)).Trim();
        if (string.IsNullOrEmpty(content))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Refine append has empty content");
            return null;
        }

        return RefinementOperation.Append(content);
    }

    private RefinementOperation? ParseInsert(string[] parts)
    {
        // parts[0] == "insert", parts[1] == index, parts[2..] == content
        if (parts.Length < 3)
            return null;

        var indexStr = parts[1].Trim();
        var content = string.Join("|", parts.Skip(2)).Trim();

        if (!int.TryParse(indexStr, out var index) || index < 1)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Refine insert has invalid index: {indexStr}");
            return null;
        }

        if (string.IsNullOrEmpty(content))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Refine insert has empty content");
            return null;
        }

        return RefinementOperation.Insert(index, content);
    }

    private RefinementOperation? ParseEdit(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var indexStr = parts[1].Trim();
        var content = string.Join("|", parts.Skip(2)).Trim();

        if (!int.TryParse(indexStr, out var index) || index < 1)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Refine edit has invalid index: {indexStr}");
            return null;
        }

        if (string.IsNullOrEmpty(content))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Refine edit has empty content");
            return null;
        }

        return RefinementOperation.Edit(index, content);
    }

    private RefinementOperation? ParseRemove(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        var indexStr = parts[1].Trim();
        if (!int.TryParse(indexStr, out var index) || index < 1)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Refine remove has invalid index: {indexStr}");
            return null;
        }

        return RefinementOperation.Remove(index);
    }
}