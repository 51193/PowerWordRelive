using LegendLore.Host.Models;
using LegendLore.Infrastructure.Logging;

namespace LegendLore.Host.Process;

public class ProcessManager
{
    private readonly Dictionary<string, Dictionary<string, string>> _config;
    private readonly List<ProcessSpawner.SpawnedProcess> _spawned = new();

    public ProcessManager(Dictionary<string, Dictionary<string, string>> config)
    {
        _config = config;
    }

    public async Task LaunchAllAsync()
    {
        var definitions = GetProcessDefinitions();
        if (definitions.Count == 0)
        {
            LogRedirector.Warn("LegendLore.Host", "No processes defined in config");
            return;
        }

        foreach (var def in definitions)
        {
            var subConfig = BuildSubConfig(def.Domains);
            var dllPath = ProcessResolver.ResolveDllPath(def.ProjectName);

            if (!File.Exists(dllPath))
            {
                LogRedirector.Error("LegendLore.Host", "Child process binary not found",
                    new { process = def.Name, path = dllPath });
                continue;
            }

            try
            {
                var spawned = ProcessSpawner.Spawn(def.Name, dllPath, subConfig);
                _spawned.Add(spawned);
            }
            catch (Exception ex)
            {
                LogRedirector.Error("LegendLore.Host", "Failed to spawn process",
                    new { process = def.Name, error = ex.Message });
            }
        }
    }

    public async Task WaitAllAsync(CancellationToken ct = default)
    {
        var tasks = _spawned.Select(async spawned =>
        {
            try
            {
                await spawned.SystemProcess.WaitForExitAsync(ct);

                var exitCode = spawned.SystemProcess.ExitCode;
                var info = $"Child process {spawned.SystemProcess.StartInfo.Arguments} exited with code {exitCode}";
                if (exitCode == 0)
                    LogRedirector.Info("LegendLore.Host", info);
                else
                    LogRedirector.Warn("LegendLore.Host", info);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogRedirector.Error("LegendLore.Host", "Error while waiting for process exit",
                    new { error = ex.Message });
            }
        });

        await Task.WhenAll(tasks);

        LogRedirector.Info("LegendLore.Host", "All child processes exited, shutting down");
    }

    public void KillAll()
    {
        foreach (var spawned in _spawned)
        {
            try
            {
                if (!spawned.SystemProcess.HasExited)
                    spawned.SystemProcess.Kill(true);
            }
            catch { }
        }
    }

    private List<ProcessDefinition> GetProcessDefinitions()
    {
        var definitions = new List<ProcessDefinition>();

        if (!_config.TryGetValue("processes", out var processes))
            return definitions;

        var processConfig = _config.GetValueOrDefault("process_config", new Dictionary<string, string>());

        foreach (var (name, projectName) in processes)
        {
            var domainsKey = $"{name}.domains";
            var domainsStr = processConfig.GetValueOrDefault(domainsKey, string.Empty);
            var domains = domainsStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            definitions.Add(new ProcessDefinition(name, projectName, domains));
        }

        return definitions;
    }

    private Dictionary<string, Dictionary<string, string>> BuildSubConfig(string[] domains)
    {
        var subConfig = new Dictionary<string, Dictionary<string, string>>();

        foreach (var domain in domains)
            if (_config.TryGetValue(domain, out var entries))
                subConfig[domain] = new Dictionary<string, string>(entries);

        return subConfig;
    }
}
