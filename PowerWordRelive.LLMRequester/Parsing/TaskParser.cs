using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Parsing;

public class TaskParser : CommandParser<TaskOperation>
{
    private const string TaskPrefix = "task|";

    protected override string Prefix => TaskPrefix;

    protected override TaskOperation? ParseLine(string line)
    {
        var body = line[TaskPrefix.Length..];
        var parts = body.Split('|');

        if (parts.Length < 2)
            return null;

        var command = parts[0].Trim();

        return command switch
        {
            "append" => ParseAppend(parts),
            "remove" => ParseRemove(parts),
            "edit" => ParseEdit(parts),
            "replace" => ParseReplace(parts),
            "finish" => ParseFinish(parts),
            _ => null
        };
    }

    private TaskOperation? ParseAppend(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var key = parts[1].Trim();
        var value = string.Join("|", parts.Skip(2)).Trim();

        if (string.IsNullOrEmpty(key))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task append has empty key");
            return null;
        }

        if (string.IsNullOrEmpty(value))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task append has empty value");
            return null;
        }

        return TaskOperation.Append(key, value);
    }

    private TaskOperation? ParseRemove(string[] parts)
    {
        if (parts.Length < 2)
            return null;

        var key = parts[1].Trim();
        if (string.IsNullOrEmpty(key))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task remove has empty key");
            return null;
        }

        return TaskOperation.Remove(key);
    }

    private TaskOperation? ParseEdit(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var key = parts[1].Trim();
        var value = string.Join("|", parts.Skip(2)).Trim();

        if (string.IsNullOrEmpty(key))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task edit has empty key");
            return null;
        }

        if (string.IsNullOrEmpty(value))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task edit has empty value");
            return null;
        }

        return TaskOperation.Edit(key, value);
    }

    private TaskOperation? ParseReplace(string[] parts)
    {
        if (parts.Length < 4)
            return null;

        var key = parts[1].Trim();
        var newKey = parts[2].Trim();
        var value = string.Join("|", parts.Skip(3)).Trim();

        if (string.IsNullOrEmpty(key))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task replace has empty key");
            return null;
        }

        if (string.IsNullOrEmpty(newKey))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task replace has empty newKey");
            return null;
        }

        if (string.IsNullOrEmpty(value))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task replace has empty value");
            return null;
        }

        return TaskOperation.Replace(key, newKey, value);
    }

    private TaskOperation? ParseFinish(string[] parts)
    {
        if (parts.Length < 3)
            return null;

        var key = parts[1].Trim();
        var status = parts[2].Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(key))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                "Task finish has empty key");
            return null;
        }

        if (status is not ("complete" or "fail" or "discard"))
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Task finish has invalid status: {status}");
            return null;
        }

        return TaskOperation.Finish(key, status);
    }
}