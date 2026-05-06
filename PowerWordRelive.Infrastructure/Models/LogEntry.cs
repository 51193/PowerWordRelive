using System.Text.Json.Serialization;

namespace PowerWordRelive.Infrastructure.Models;

public record LogEntry
{
    [JsonPropertyName("timestamp")] public string Timestamp { get; init; } = DateTime.UtcNow.ToString("O");

    [JsonPropertyName("level")] public string Level { get; init; } = "INFO";

    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;

    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")] public object? Data { get; init; }
}