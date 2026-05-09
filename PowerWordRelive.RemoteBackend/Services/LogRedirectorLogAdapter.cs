using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Models;

namespace PowerWordRelive.RemoteBackend.Services;

public class LogRedirectorLogAdapter : ILogAdapter
{
    public void Info(string message)
    {
        WriteLine("INFO", message, null);
    }

    public void Warn(string message)
    {
        WriteLine("WARN", message, null);
    }

    public void Error(string message, Exception? ex = null)
    {
        WriteLine("ERROR", message, ex is not null ? new { error = ex.Message } : null);
    }

    private static void WriteLine(string level, string message, object? data)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            Level = level,
            Source = "RemoteBackend",
            Message = message,
            Data = data
        };
        Console.Error.WriteLine(JsonSerializer.Serialize(entry));
    }
}