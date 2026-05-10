using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Security;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.RemoteBackend.Services;

byte[] keyBytes;
string[] urls;

if (Console.IsInputRedirected)
{
    var config = ChildConfigReader.ReadConfig();
    var localMode = config.GetValueOrDefault("local_mode", new Dictionary<string, string>());
    var remoteMode = config.GetValueOrDefault("remote_mode", new Dictionary<string, string>());

    var port = localMode.GetValueOrDefault("port", "");
    var keyBase64 = remoteMode.GetValueOrDefault("local.key", "");

    if (string.IsNullOrWhiteSpace(port))
    {
        LogRedirector.Error("RemoteBackend", "local_mode.port missing from stdin config");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(keyBase64))
    {
        LogRedirector.Error("RemoteBackend", "remote_mode.local.key missing from stdin config");
        return 1;
    }

    keyBytes = AesAuth.ParseKey(keyBase64);
    urls = new[] { $"http://127.0.0.1:{port}" };

    LogRedirector.Info("RemoteBackend", $"Starting in local mode on 127.0.0.1:{port}");
}
else
{
    var fs = new LocalFileSystem();
    var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "config"));

    if (!fs.FileExists(configPath))
    {
        Console.Error.WriteLine($"Config file not found: {configPath}");
        return 1;
    }

    var dict = ParseConfig(fs.ReadAllLines(configPath));
    var remoteMode = dict.GetValueOrDefault("remote_mode");
    if (remoteMode == null)
    {
        Console.Error.WriteLine("Missing 'remote_mode' domain in config");
        return 1;
    }

    var port = remoteMode.GetValueOrDefault("server.port", "");
    var keyPath = remoteMode.GetValueOrDefault("server.key_path", "");

    if (string.IsNullOrWhiteSpace(port))
    {
        Console.Error.WriteLine("Missing required config: remote_mode.server.port");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(keyPath))
    {
        Console.Error.WriteLine("Missing required config: remote_mode.server.key_path");
        return 1;
    }

    if (!fs.FileExists(keyPath))
    {
        Console.Error.WriteLine($"Key file not found: {keyPath}");
        return 1;
    }

    var keyBase64 = fs.ReadAllText(keyPath).Trim();
    keyBytes = AesAuth.ParseKey(keyBase64);
    urls = new[] { $"http://0.0.0.0:{port}" };

    Console.WriteLine($"Starting RemoteBackend standalone on 0.0.0.0:{port}");
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = new[] { "--urls", string.Join(";", urls) }
});

if (Console.IsInputRedirected)
{
    var logAdapter = new LogRedirectorLogAdapter();
    builder.Services.AddSingleton(_ => new BackendConnectionManager(keyBytes, logAdapter));
}
else
{
    builder.Services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<BackendConnectionManager>>();
        return new BackendConnectionManager(keyBytes, new AspNetLogAdapter(logger));
    });
}

builder.Services.Configure<HostOptions>(options => { options.ShutdownTimeout = TimeSpan.FromSeconds(2); });

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/ws/backend", async (HttpContext context, BackendConnectionManager manager) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }
    await manager.HandleBackendWebSocket(context);
});

app.Map("/ws/frontend", async (HttpContext context, BackendConnectionManager manager) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }
    await manager.HandleFrontendWebSocket(context);
});

var platform = PlatformServicesFactory.Create();
platform.RegisterShutdownSignal(() =>
{
    LogRedirector.Info("RemoteBackend", "Received shutdown signal");
    app.Lifetime.StopApplication();
});

app.Run();

return 0;

static Dictionary<string, Dictionary<string, string>> ParseConfig(string[] lines)
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
