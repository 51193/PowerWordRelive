namespace PowerWordRelive.LLMRequester.Parsing;

public abstract class CommandParser<TCommand>
{
    protected abstract string Prefix { get; }

    protected abstract TCommand? ParseLine(string line);

    protected virtual bool IsEmptyMarker(string line)
    {
        return line == "EMPTY";
    }

    public List<TCommand> Parse(string text)
    {
        var results = new List<TCommand>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (IsEmptyMarker(line))
                return results;

            if (!line.StartsWith(Prefix))
                continue;

            var command = ParseLine(line);
            if (command != null)
                results.Add(command);
        }

        return results;
    }
}