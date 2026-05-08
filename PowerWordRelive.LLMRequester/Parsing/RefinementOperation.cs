namespace PowerWordRelive.LLMRequester.Parsing;

public record RefinementOperation
{
    public enum OperationType
    {
        Insert,
        Append,
        Remove,
        Edit
    }

    public OperationType Type { get; init; }
    public int? DisplayIndex { get; init; }
    public string? Content { get; init; }

    private RefinementOperation()
    {
    }

    public static RefinementOperation Insert(int displayIndex, string content)
    {
        return new RefinementOperation
        {
            Type = OperationType.Insert,
            DisplayIndex = displayIndex,
            Content = content
        };
    }

    public static RefinementOperation Append(string content)
    {
        return new RefinementOperation
        {
            Type = OperationType.Append,
            Content = content
        };
    }

    public static RefinementOperation Remove(int displayIndex)
    {
        return new RefinementOperation
        {
            Type = OperationType.Remove,
            DisplayIndex = displayIndex
        };
    }

    public static RefinementOperation Edit(int displayIndex, string content)
    {
        return new RefinementOperation
        {
            Type = OperationType.Edit,
            DisplayIndex = displayIndex,
            Content = content
        };
    }
}