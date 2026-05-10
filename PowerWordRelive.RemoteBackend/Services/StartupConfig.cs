using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.RemoteBackend.Services;

internal record StartupConfig(
    byte[] Key,
    string[] BuilderArgs,
    string ContentRoot,
    Action<IServiceCollection, byte[]> RegisterServices)
{
    public static StartupConfig Create(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--mode" && args[1] == "local")
            return CreateLocal(args);

        return CreateRemote(args);
    }

    private static StartupConfig CreateLocal(string[] args)
    {
        var port = ParseArg(args, "--port");
        var key = Convert.FromBase64String(ParseArg(args, "--key"));

        return new StartupConfig(
            key,
            ["--urls", $"http://127.0.0.1:{port}"],
            AppContext.BaseDirectory,
            (services, k) => services.AddSingleton(sp =>
                new BackendConnectionManager(k, new LogRedirectorLogAdapter())));
    }

    private static StartupConfig CreateRemote(string[] args)
    {
        var fs = new LocalFileSystem();
        var config = new ConfigService(fs);

        var keyBase64 = fs.ReadAllText(config.KeyPath).Trim();
        var key = Convert.FromBase64String(keyBase64);

        return new StartupConfig(
            key,
            ["--urls", $"http://0.0.0.0:{config.Port}"],
            AppContext.BaseDirectory,
            (services, k) => services.AddSingleton(sp =>
                new BackendConnectionManager(k, new AspNetLogAdapter(
                    sp.GetRequiredService<ILogger<BackendConnectionManager>>()))));
    }

    private static string ParseArg(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        if (idx < 0 || idx + 1 >= args.Length)
        {
            Console.Error.WriteLine($"Missing required argument: {name}");
            Environment.Exit(1);
        }

        return args[idx + 1];
    }
}