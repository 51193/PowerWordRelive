namespace PowerWordRelive.LLMRequester.Parsing;

public record ConsistencyOperation
{
    public enum OperationType
    {
        Append,
        Remove,
        Edit,
        EditTag
    }

    private ConsistencyOperation()
    {
    }

    public OperationType Type { get; init; }
    public string? Name { get; init; }
    public string? Detail { get; init; }
    public string Tag { get; init; } = "null";

    public static ConsistencyOperation Append(string name, string detail, string tag)
    {
        return new ConsistencyOperation
        {
            Type = OperationType.Append,
            Name = name,
            Detail = detail,
            Tag = tag
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

    public static ConsistencyOperation EditTag(string name, string tag)
    {
        return new ConsistencyOperation
        {
            Type = OperationType.EditTag,
            Name = name,
            Tag = tag
        };
    }
}