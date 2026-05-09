namespace PowerWordRelive.LLMRequester.Parsing;

public record TaskOperation
{
    public enum OperationType
    {
        Append,
        Remove,
        Edit,
        Replace,
        Finish
    }

    private TaskOperation()
    {
    }

    public OperationType Type { get; init; }
    public string? Key { get; init; }
    public string? NewKey { get; init; }
    public string? Value { get; init; }
    public string? Status { get; init; }

    public static TaskOperation Append(string key, string value)
    {
        return new TaskOperation
        {
            Type = OperationType.Append,
            Key = key,
            Value = value
        };
    }

    public static TaskOperation Remove(string key)
    {
        return new TaskOperation
        {
            Type = OperationType.Remove,
            Key = key
        };
    }

    public static TaskOperation Edit(string key, string value)
    {
        return new TaskOperation
        {
            Type = OperationType.Edit,
            Key = key,
            Value = value
        };
    }

    public static TaskOperation Replace(string key, string newKey, string value)
    {
        return new TaskOperation
        {
            Type = OperationType.Replace,
            Key = key,
            NewKey = newKey,
            Value = value
        };
    }

    public static TaskOperation Finish(string key, string status)
    {
        return new TaskOperation
        {
            Type = OperationType.Finish,
            Key = key,
            Status = status
        };
    }
}