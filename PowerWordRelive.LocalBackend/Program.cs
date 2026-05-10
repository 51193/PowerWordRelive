using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Security;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.LocalBackend.Services;

var fs = new LocalFileSystem();
var platform = PlatformServicesFactory.Create();
var config = ChildConfigReader.ReadConfig();

var localMode = config.GetValueOrDefault("local_mode", new Dictionary<string, string>());
var remoteMode = config.GetValueOrDefault("remote_mode", new Dictionary<string, string>());
var lbConfig = config.GetValueOrDefault("local_backend", new Dictionary<string, string>());
var storageConfig = config.GetValueOrDefault("storage", new Dictionary<string, string>());
var generalConfig = config.GetValueOrDefault("general", new Dictionary<string, string>());

var workRoot = generalConfig.GetValueOrDefault("work_root", "");
var sqlitePath = storageConfig.GetValueOrDefault("sqlite_path", "");
if (string.IsNullOrWhiteSpace(sqlitePath))
{
    LogRedirector.Error("LocalBackend", "Missing required config: storage.sqlite_path");
    return 1;
}

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
    sqlitePath = Path.GetFullPath(Path.Combine(workRoot, sqlitePath));
else if (!Path.IsPathRooted(sqlitePath))
    sqlitePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, sqlitePath));

var pollIntervalSec = int.TryParse(lbConfig.GetValueOrDefault("poll_interval_sec", "3"), out var pi) ? pi : 3;
var maxReconnect = int.TryParse(lbConfig.GetValueOrDefault("max_reconnect_attempts", "5"), out var mr) ? mr : 5;
var initialDelay = double.TryParse(lbConfig.GetValueOrDefault("initial_reconnect_delay_sec", "2"), out var id) ? id : 2;

LogRedirector.Info("LocalBackend", "Starting", new { sqlitePath, pollIntervalSec, maxReconnect, initialDelay });

platform.RegisterShutdownSignal(() =>
{
    LogRedirector.Info("LocalBackend", "Received shutdown signal, exiting");
    Environment.Exit(0);
});

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("LocalBackend", "Received SIGINT, exiting");
    Environment.Exit(0);
};

var dbReader = new DatabaseReader(sqlitePath, fs);
using var cts = new CancellationTokenSource();

var tasks = new List<Task>();

var localEnabled = remoteMode.GetValueOrDefault("local.enabled", "false") == "true";
if (localEnabled)
{
    var localPort = remoteMode.GetValueOrDefault("local.port", "");
    var localKeyBase64 = remoteMode.GetValueOrDefault("local.key", "");
    if (string.IsNullOrWhiteSpace(localPort) || string.IsNullOrWhiteSpace(localKeyBase64))
    {
        LogRedirector.Error("LocalBackend", "remote_mode.local.enabled but port or key missing");
        return 1;
    }

    var localKey = AesAuth.ParseKey(localKeyBase64);
    var url = $"ws://127.0.0.1:{localPort}/ws/backend";
    var conn = new PushConnection("local", url, localKey, pollIntervalSec, maxReconnect, initialDelay);
    tasks.Add(conn.RunAsync(dbReader, cts.Token));
}

var remoteEnabled = remoteMode.GetValueOrDefault("remote.enabled", "false") == "true";
if (remoteEnabled)
{
    var remoteHost = remoteMode.GetValueOrDefault("remote.host", "");
    var remotePort = remoteMode.GetValueOrDefault("remote.port", "");
    var remoteKeyPath = remoteMode.GetValueOrDefault("remote.key_path", "");

    if (string.IsNullOrWhiteSpace(remoteHost) || string.IsNullOrWhiteSpace(remotePort) ||
        string.IsNullOrWhiteSpace(remoteKeyPath))
    {
        LogRedirector.Error("LocalBackend", "remote_mode.remote.enabled but host/port/key_path missing");
        return 1;
    }

    if (!fs.FileExists(remoteKeyPath))
    {
        LogRedirector.Error("LocalBackend", $"Remote key file not found: {remoteKeyPath}");
        return 1;
    }

    var remoteKeyBase64 = fs.ReadAllText(remoteKeyPath).Trim();
    var remoteKey = AesAuth.ParseKey(remoteKeyBase64);
    var url = $"ws://{remoteHost}:{remotePort}/ws/backend";
    var conn = new PushConnection("remote", url, remoteKey, pollIntervalSec, maxReconnect, initialDelay);
    tasks.Add(conn.RunAsync(dbReader, cts.Token));
}

if (tasks.Count == 0)
{
    LogRedirector.Error("LocalBackend", "No connections configured (neither local nor remote enabled)");
    return 1;
}

try
{
    await Task.WhenAny(tasks);
    cts.Cancel();
    await Task.WhenAll(tasks);
}
catch (OperationCanceledException)
{
}
catch (Exception ex)
{
    LogRedirector.Error("LocalBackend", $"Unexpected error: {ex.Message}");
    return 1;
}

return 0;
