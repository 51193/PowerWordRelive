using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.LocalBackend.Models;
using PowerWordRelive.LocalBackend.Services;

var fs = new LocalFileSystem();

var platform = PlatformServicesFactory.Create();

var config = ChildConfigReader.ReadConfig();
var options = LocalBackendOptions.FromConfig(config, fs);

LogRedirector.Info("LocalBackend", $"Starting, remote={options.RemoteHost}:{options.RemotePort}");

var dbService = new DatabaseReadService(options.SqlitePath, fs);

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

var connectionService = new RemoteConnectionService(options, fs);

for (var attempt = 0; attempt <= options.MaxReconnectAttempts; attempt++)
{
    if (attempt > 0)
    {
        var delaySec = options.InitialReconnectDelaySec * Math.Pow(2, attempt - 1);
        LogRedirector.Info("LocalBackend",
            $"Reconnect attempt {attempt}/{options.MaxReconnectAttempts}, waiting {delaySec:F0}s");
        await Task.Delay(TimeSpan.FromSeconds(delaySec));
    }

    try
    {
        await connectionService.ConnectAndRelayAsync(dbService);
    }
    catch (Exception ex)
    {
        LogRedirector.Warn("LocalBackend", $"Connection failed: {ex.Message}");
    }
}

LogRedirector.Error("LocalBackend", $"Max reconnect attempts ({options.MaxReconnectAttempts}) exhausted, exiting");
Environment.Exit(1);