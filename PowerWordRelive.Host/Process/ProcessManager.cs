using PowerWordRelive.Host.Models;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Host.Process;

internal class ProcessManager
{
    private readonly Dictionary<string, Dictionary<string, string>> _config;
    private readonly IFileSystem _fs;
    private readonly List<ProcessSpawner.SpawnedProcess> _spawned = new();

    public ProcessManager(Dictionary<string, Dictionary<string, string>> config, IFileSystem fs)
    {
        _config = config;
        _fs = fs;
    }

    public async Task LaunchAllAsync()
    {
        var definitions = GetProcessDefinitions();
        if (definitions.Count == 0)
        {
            LogRedirector.Warn("PowerWordRelive.Host", "No processes defined in config");
            return;
        }

        foreach (var def in definitions)
        {
            var subConfig = BuildSubConfig(def.Domains);
            var dllPath = ProcessResolver.ResolveDllPath(def.ProjectName);

            if (!_fs.FileExists(dllPath))
            {
                LogRedirector.Error("PowerWordRelive.Host", "Child process binary not found",
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
                LogRedirector.Error("PowerWordRelive.Host", "Failed to spawn process",
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
                    LogRedirector.Info("PowerWordRelive.Host", info);
                else
                    LogRedirector.Warn("PowerWordRelive.Host", info);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.Host", "Error while waiting for process exit",
                    new { error = ex.Message });
            }
        });

        await Task.WhenAll(tasks);

        LogRedirector.Info("PowerWordRelive.Host", "All child processes exited, shutting down");
    }

    public void KillAll()
    {
        LogRedirector.Info("PowerWordRelive.Host", "Killing all child processes", new { count = _spawned.Count });

        foreach (var spawned in _spawned)
        {
            var pid = spawned.SystemProcess.Id;
            var name = spawned.ProcessName;

            try
            {
                if (spawned.SystemProcess.HasExited)
                {
                    LogRedirector.Info("PowerWordRelive.Host", "Child process already exited before kill",
                        new { process = name, pid, exitCode = spawned.SystemProcess.ExitCode });
                    continue;
                }

                LogRedirector.Info("PowerWordRelive.Host", "Killing child process",
                    new { process = name, pid });

                spawned.SystemProcess.Kill(true);

                spawned.SystemProcess.WaitForExit(3000);

                if (spawned.SystemProcess.HasExited)
                    LogRedirector.Info("PowerWordRelive.Host", "Child process killed",
                        new { process = name, pid });
                else
                    LogRedirector.Warn("PowerWordRelive.Host", "Child process still alive after kill",
                        new { process = name, pid });
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.Host", "Error killing child process",
                    new { process = name, pid, error = ex.Message });
            }
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