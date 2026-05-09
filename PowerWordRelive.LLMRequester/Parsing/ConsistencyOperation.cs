namespace PowerWordRelive.LLMRequester.Parsing;

public record ConsistencyOperation
{
    public enum OperationType
    {
        Append,
        Remove,
        Edit
    }

    private ConsistencyOperation()
    {
    }

    public OperationType Type { get; init; }
    public string? Name { get; init; }
    public string? Detail { get; init; }

    public static ConsistencyOperation Append(string name, string detail)
    {
        return new ConsistencyOperation
        {
            Type = OperationType.Append,
            Name = name,
            Detail = detail
        };
    }

    public static ConsistencyOperation Remove(string name)
    {
        return new ConsistencyOperation
        {
            Type = OperationType.Remove,
            Name = name
        };
    }

    public static ConsistencyOperation Edit(string name, string detail)
    {
        return new ConsistencyOperation
        {
            Type = OperationType.Edit,
            Name = name,
            Detail = detail
        };
    }
}