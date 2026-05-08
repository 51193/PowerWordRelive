using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.LocalBackend.Models;

public record LocalBackendOptions(
    string RemoteHost,
    int RemotePort,
    string KeyPath,
    int MaxReconnectAttempts,
    double InitialReconnectDelaySec,
    string SqlitePath)
{
    public static LocalBackendOptions FromConfig(
        Dictionary<string, Dictionary<string, string>> config,
        IFileSystem fs)
    {
        var lb = RequireDomain(config, "local_backend");
        var storage = RequireDomain(config, "storage");

        var remoteHost = RequireKey(lb, "remote_host", "local_backend.remote_host");
        var remotePort = RequireInt(lb, "remote_port", "local_backend.remote_port");
        var keyPath = RequireKey(lb, "key_path", "local_backend.key_path");
        var maxReconnect = ParseInt(lb, "max_reconnect_attempts", "local_backend.max_reconnect_attempts", 5);
        var initialDelay =
            ParseDouble(lb, "initial_reconnect_delay_sec", "local_backend.initial_reconnect_delay_sec", 2);
        var sqlitePath = RequireKey(storage, "sqlite_path", "storage.sqlite_path");

        if (!fs.FileExists(keyPath))
        {
            Console.Error.WriteLine($"Key file not found: {keyPath}");
            Environment.Exit(1);
        }

        return new LocalBackendOptions(remoteHost, remotePort, keyPath, maxReconnect, initialDelay, sqlitePath);
    }

    private static Dictionary<string, string> RequireDomain(
        Dictionary<string, Dictionary<string, string>> config, string domain)
    {
        if (config.TryGetValue(domain, out var d))
            return d;
        Console.Error.WriteLine($"Missing config domain: {domain}");
        Environment.Exit(1);
        return null!;
    }

    private static string RequireKey(Dictionary<string, string> domain, string key, string fullName)
    {
        if (domain.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;
        Console.Error.WriteLine($"Missing required config: {fullName}");
        Environment.Exit(1);
        return "";
    }

    private static int RequireInt(Dictionary<string, string> domain, string key, string fullName)
    {
        if (domain.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;
        Console.Error.WriteLine($"Missing or invalid config: {fullName}");
        Environment.Exit(1);
        return 0;
    }

    private static int ParseInt(Dictionary<string, string> domain, string key, string fullName, int defaultValue)
    {
        if (domain.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;
        return defaultValue;
    }

    private static double ParseDouble(Dictionary<string, string> domain, string key, string fullName,
        double defaultValue)
    {
        if (domain.TryGetValue(key, out var value) && double.TryParse(value, out var result))
            return result;
        return defaultValue;
    }
}