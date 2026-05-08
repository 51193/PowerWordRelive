using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerWordRelive.Infrastructure.Messages;

public record WsMessage
{
    [JsonPropertyName("type")] public string? Type { get; init; }

    [JsonPropertyName("id")] public string? Id { get; init; }

    [JsonPropertyName("query")] public string? Query { get; init; }

    [JsonPropertyName("params")] public JsonElement? Params { get; init; }

    [JsonPropertyName("data")] public JsonElement? Data { get; init; }

    [JsonPropertyName("message")] public string? Message { get; init; }

    [JsonPropertyName("total")] public int? Total { get; init; }

    [JsonPropertyName("backend_connected")]
    public bool? BackendConnected { get; init; }
}