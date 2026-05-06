using System.Text.Json;

namespace PowerWordRelive.Infrastructure.Configuration;

public static class ChildConfigReader
{
    public static Dictionary<string, Dictionary<string, string>> ReadConfig()
    {
        var stdin = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(stdin))
            throw new InvalidOperationException("No configuration received from parent process via stdin");

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stdin)
               ?? throw new InvalidOperationException("Failed to deserialize configuration from stdin");
    }
}