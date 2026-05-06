using System.Text.Json;

namespace PowerWordRelive.Infrastructure.Configuration;

public static class ChildConfigReader
{
    public static Dictionary<string, Dictionary<string, string>> ReadConfig()
    {
        var stdin = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(stdin)) return new Dictionary<string, Dictionary<string, string>>();

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stdin)
               ?? new Dictionary<string, Dictionary<string, string>>();
    }
}