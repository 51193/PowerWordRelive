using PowerWordRelive.Host.Config;
using PowerWordRelive.Host.Process;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Storage;

var fs = new LocalFileSystem();
var platform = PlatformServicesFactory.Create();

var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "config"));
LogRedirector.Info("PowerWordRelive.Host", "Host starting", new { config = configPath });

var configParser = new ConfigParser(fs);
var config = configParser.Parse(configPath);

var manager = new ProcessManager(config, fs, platform);
await manager.LaunchAllAsync();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("PowerWordRelive.Host", "Shutting down...");
    cts.Cancel();
    manager.KillAll();
};

platform.RegisterShutdownSignal(() =>
{
    LogRedirector.Info("PowerWordRelive.Host", "Shutting down...");
    cts.Cancel();
    manager.KillAll();
});

await manager.WaitAllAsync(CancellationToken.None);

LogRedirector.Info("PowerWordRelive.Host", "Host exiting");
Console.Out.Flush();