using System.Text.Json;
using LegendLore.Infrastructure.Models;

namespace LegendLore.Infrastructure.Logging;

public static class LogRedirector
{
    public static void Log(string level, string source, string message, object? data = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            Level = level,
            Source = source,
            Message = message,
            Data = data
        };

        Console.WriteLine(JsonSerializer.Serialize(entry));
    }

    public static void Debug(string source, string message, object? data = null)
    {
        Log(LogLevel.Debug, source, message, data);
    }

    public static void Info(string source, string message, object? data = null)
    {
        Log(LogLevel.Info, source, message, data);
    }

    public static void Warn(string source, string message, object? data = null)
    {
        Log(LogLevel.Warn, source, message, data);
    }

    public static void Error(string source, string message, object? data = null)
    {
        Log(LogLevel.Error, source, message, data);
    }
}