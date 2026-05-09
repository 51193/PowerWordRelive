using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.RemoteBackend.Services;

public class ConfigService
{
    public ConfigService(IFileSystem fs)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config");
        if (!fs.FileExists(configPath))
        {
            Console.Error.WriteLine($"Config file not found: {configPath}");
            Environment.Exit(1);
        }

        var dict = ParseConfig(fs.ReadAllLines(configPath));

        var remote = dict.GetValueOrDefault("remote_backend");
        if (remote == null)
        {
            Console.Error.WriteLine("Missing 'remote_backend' domain in config");
            Environment.Exit(1);
        }

        Port = ParseInt(remote, "port", "remote_backend.port");
        KeyPath = ParseRequired(remote, "key_path", "remote_backend.key_path");
    }

    public int Port { get; }
    public string KeyPath { get; }

    private static Dictionary<string, Dictionary<string, string>> ParseConfig(string[] lines)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var dotIdx = trimmed.IndexOf(':');
            if (dotIdx < 0) continue;

            var key = trimmed[..dotIdx].Trim();
            var value = trimmed[(dotIdx + 1)..].Trim();

            var periodIdx = key.IndexOf('.');
            if (periodIdx < 0) continue;

            var domain = key[..periodIdx];
            var exactKey = key[(periodIdx + 1)..];

            if (!result.ContainsKey(domain))
                result[domain] = new Dictionary<string, string>();
            result[domain][exactKey] = value;
        }

        return result;
    }

    private static string ParseRequired(Dictionary<string, string> domain, string key, string fullName)
    {
        if (domain.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        Console.Error.WriteLine($"Missing required config: {fullName}");
        Environment.Exit(1);
        return "";
    }

    private static int ParseInt(Dictionary<string, string> domain, string key, string fullName)
    {
        var str = ParseRequired(domain, key, fullName);
        if (int.TryParse(str, out var val))
            return val;
        Console.Error.WriteLine($"Invalid integer for {fullName}: {str}");
        Environment.Exit(1);
        return 0;
    }
}