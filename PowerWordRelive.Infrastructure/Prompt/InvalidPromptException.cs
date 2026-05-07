namespace PowerWordRelive.Infrastructure.Prompt;

public class InvalidPromptException : Exception
{
    public InvalidPromptException(string message) : base(message)
    {
    }

    public InvalidPromptException(string message, Exception inner) : base(message, inner)
    {
    }
}