namespace PowerWordRelive.LLMRequester.Parsing;

public record IncrementalOperation
{
    public enum OperationType
    {
        Insert,
        Append,
        Remove,
        Edit
    }

    private IncrementalOperation()
    {
    }

    public OperationType Type { get; init; }
    public int? DisplayIndex { get; init; }
    public string? Content { get; init; }

    public static IncrementalOperation Insert(int displayIndex, string content)
    {
        return new IncrementalOperation
        {
            Type = OperationType.Insert,
            DisplayIndex = displayIndex,
            Content = content
        };
    }

    public static IncrementalOperation Append(string content)
    {
        return new IncrementalOperation
        {
            Type = OperationType.Append,
            Content = content
        };
    }

    public static IncrementalOperation Remove(int displayIndex)
    {
        return new IncrementalOperation
        {
            Type = OperationType.Remove,
            DisplayIndex = displayIndex
        };
    }

    public static IncrementalOperation Edit(int displayIndex, string content)
    {
        return new IncrementalOperation
        {
            Type = OperationType.Edit,
            DisplayIndex = displayIndex,
            Content = content
        };
    }
}