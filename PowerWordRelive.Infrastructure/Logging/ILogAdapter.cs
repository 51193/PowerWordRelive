namespace PowerWordRelive.Infrastructure.Logging;

public interface ILogAdapter
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}