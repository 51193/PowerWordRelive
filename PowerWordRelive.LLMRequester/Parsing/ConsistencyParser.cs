using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Parsing;

public class ConsistencyParser : CommandParser<ConsistencyOperation>
{
    private const string ConsistencyPrefix = "consistency|";

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
            _ => null
        };
    }

    private ConsistencyOperation? ParseAppend(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var name = parts[1].Trim();
        var detail = string.Join("|", parts.Skip(2)).Trim();

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

        return ConsistencyOperation.Append(name, detail);
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
}