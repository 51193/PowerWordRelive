using LegendLore.Host.Config;
using LegendLore.Host.Process;
using LegendLore.Infrastructure.Logging;

var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "config"));
LogRedirector.Info("LegendLore.Host", "Host starting", new { config = configPath });

var config = ConfigParser.Parse(configPath);

var manager = new ProcessManager(config);
await manager.LaunchAllAsync();
await manager.WaitAllAsync();