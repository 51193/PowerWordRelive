using System.Text.Json;

namespace PowerWordRelive.Infrastructure.Configuration;

public static class ChildConfigReader
{
    public static Dictionary<string, Dictionary<string, string>> ReadConfig()
    {
        var result = TryReadConfig();
        if (result == null)
            throw new InvalidOperationException("No configuration received from parent process via stdin");
        return result;
    }

    public static Dictionary<string, Dictionary<string, string>>? TryReadConfig()
    {
        var stdin = Console.In.ReadToEnd();
        if (string.IsNullOrWhiteSpace(stdin))
            return null;

        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stdin);
    }
}