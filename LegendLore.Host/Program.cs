using LegendLore.Host.Config;
using LegendLore.Host.Process;
using LegendLore.Infrastructure.Logging;
using LegendLore.Infrastructure.Storage;

var fs = new LocalFileSystem();

var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "config"));
LogRedirector.Info("LegendLore.Host", "Host starting", new { config = configPath });

var configParser = new ConfigParser(fs);
var config = configParser.Parse(configPath);

var manager = new ProcessManager(config, fs);
await manager.LaunchAllAsync();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("LegendLore.Host", "Shutting down...");
    cts.Cancel();
    manager.KillAll();
};

System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGTERM, _ =>
    {
        LogRedirector.Info("LegendLore.Host", "Shutting down...");
        cts.Cancel();
        manager.KillAll();
    });

await manager.WaitAllAsync(cts.Token);
