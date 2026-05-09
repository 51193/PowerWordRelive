namespace PowerWordRelive.LocalBackend.Models;

public record LocalBackendOptions(
    string Host,
    int Port,
    byte[] Key,
    int MaxReconnectAttempts,
    double InitialReconnectDelaySec,
    string SqlitePath)
{
    public static LocalBackendOptions Create(string[] args,
        Dictionary<string, Dictionary<string, string>> config)
    {
        var mode = ParseArg(args, "--mode", 1);
        if (mode == "local")
        {
            var port = ParseIntArg(args, "--port");
            var key = Convert.FromBase64String(ParseArg(args, "--key", null));
            return ForLocalMode(port, key, config);
        }

        if (mode == "remote")
        {
            var host = ParseArg(args, "--host", null);
            var port = ParseIntArg(args, "--port");
            var key = Convert.FromBase64String(ParseArg(args, "--key", null));
            return ForRemoteMode(host, port, key, config);
        }

        Console.Error.WriteLine("Usage: --mode local --port <n> --key <base64>");
        Console.Error.WriteLine("Usage: --mode remote --host <h> --port <n> --key <base64>");
        Environment.Exit(1);
        return null!;
    }

    public static LocalBackendOptions ForLocalMode(int port, byte[] key,
        Dictionary<string, Dictionary<string, string>> config)
    {
        if (key.Length == 0)
            throw new ArgumentException("Key must not be empty", nameof(key));

        var storage = RequireDomain(config, "storage");
        var sqlitePath = RequireKey(storage, "sqlite_path", "storage.sqlite_path");

        return new LocalBackendOptions("127.0.0.1", port, key, 20, 1, sqlitePath);
    }

    public static LocalBackendOptions ForRemoteMode(string host, int port, byte[] key,
        Dictionary<string, Dictionary<string, string>> config)
    {
        if (key.Length == 0)
            throw new ArgumentException("Key must not be empty", nameof(key));

        var lb = RequireDomain(config, "local_backend");
        var maxReconnect = ParseInt(lb, "max_reconnect_attempts", 5);
        var initialDelay = ParseDouble(lb, "initial_reconnect_delay_sec", 2);
        var storage = RequireDomain(config, "storage");
        var sqlitePath = RequireKey(storage, "sqlite_path", "storage.sqlite_path");

        return new LocalBackendOptions(host, port, key, maxReconnect, initialDelay, sqlitePath);
    }

    private static string ParseArg(string[] args, string name, int? position)
    {
        if (position.HasValue)
        {
            if (position.Value < args.Length)
                return args[position.Value];
            Console.Error.WriteLine($"Missing required argument at position {position.Value}");
            Environment.Exit(1);
            return "";
        }

        var idx = Array.IndexOf(args, name);
        if (idx < 0 || idx + 1 >= args.Length)
        {
            Console.Error.WriteLine($"Missing required argument: {name}");
            Environment.Exit(1);
        }

        return args[idx + 1];
    }

    private static int ParseIntArg(string[] args, string name)
    {
        var value = ParseArg(args, name, null);
        if (int.TryParse(value, out var result))
            return result;
        Console.Error.WriteLine($"Invalid integer for {name}: {value}");
        Environment.Exit(1);
        return 0;
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

    private static int ParseInt(Dictionary<string, string> domain, string key, int defaultValue)
    {
        if (domain.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            return result;
        return defaultValue;
    }

    private static double ParseDouble(Dictionary<string, string> domain, string key, double defaultValue)
    {
        if (domain.TryGetValue(key, out var value) && double.TryParse(value, out var result))
            return result;
        return defaultValue;
    }
}
