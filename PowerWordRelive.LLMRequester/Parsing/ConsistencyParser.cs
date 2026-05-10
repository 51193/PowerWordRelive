using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Parsing;

public class ConsistencyParser : CommandParser<ConsistencyOperation>
{
    private const string ConsistencyPrefix = "consistency|";

    private static readonly HashSet<string> ValidTags = new()
        { "world", "character", "item", "event", "null" };

    protected override string Prefix => ConsistencyPrefix;

    protected override ConsistencyOperation? ParseLine(string line)
    {
        var body = line[ConsistencyPrefix.Length..];
        var parts = body.Split('|');

        if (parts.Length < 2)
            return null;

        var command = parts[0].Trim();

        return command switch
        {
            "append" => ParseAppend(parts),
            "remove" => ParseRemove(parts),
            "edit" => ParseEdit(parts),
            "edit_tag" => ParseEditTag(parts),
            _ => null
        };
    }

    private ConsistencyOperation? ParseAppend(string[] parts)
    {
        if (parts.Length < 4)
            return null;

        var name = parts[1].Trim();
        var detail = string.Join("|", parts.Skip(2).Take(parts.Length - 3)).Trim();
        var tag = parts[^1].Trim();

        if (string.IsNullOrEmpty(name))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Consistency append has empty name");
            return null;
        }

        if (string.IsNullOrEmpty(detail))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Consistency append has empty detail");
            return null;
        }

        if (!ValidTags.Contains(tag))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Consistency append has invalid tag: '{tag}'");
            return null;
        }

        return ConsistencyOperation.Append(name, detail, tag);
    }

    private ConsistencyOperation? ParseRemove(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        var name = parts[1].Trim();
        if (string.IsNullOrEmpty(name))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Consistency remove has empty name");
            return null;
        }

        return ConsistencyOperation.Remove(name);
    }

    private ConsistencyOperation? ParseEdit(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var name = parts[1].Trim();
        var detail = string.Join("|", parts.Skip(2)).Trim();

        if (string.IsNullOrEmpty(name))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Consistency edit has empty name");
            return null;
        }

        if (string.IsNullOrEmpty(detail))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Consistency edit has empty detail");
            return null;
        }

        return ConsistencyOperation.Edit(name, detail);
    }

    private ConsistencyOperation? ParseEditTag(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var name = parts[1].Trim();
        var tag = parts[^1].Trim();

        if (string.IsNullOrEmpty(name))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Consistency edit_tag has empty name");
            return null;
        }

        if (!ValidTags.Contains(tag))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Consistency edit_tag has invalid tag: '{tag}'");
            return null;
        }

        return ConsistencyOperation.EditTag(name, tag);
    }
}