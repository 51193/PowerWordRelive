using System.Security.Cryptography;
using PowerWordRelive.Host.Models;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Host.Process;

internal class ProcessManager
{
    private readonly Dictionary<string, Dictionary<string, string>> _config;
    private readonly IFileSystem _fs;
    private readonly IPlatformServices _platform;
    private readonly List<ProcessSpawner.SpawnedProcess> _spawned = new();

    public ProcessManager(Dictionary<string, Dictionary<string, string>> config, IFileSystem fs,
        IPlatformServices platform)
    {
        _config = config;
        _fs = fs;
        _platform = platform;
    }

    public async Task LaunchAllAsync()
    {
        var definitions = GetProcessDefinitions();
        definitions = RemoveManagedDefinitions(definitions);
        AddLocalWebProcesses(definitions);

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
                var spawned = ProcessSpawner.Spawn(def.Name, dllPath, subConfig, def.ExtraArgs);
                _spawned.Add(spawned);
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.Host", "Failed to spawn process",
                    new { process = def.Name, error = ex.Message });
            }
        }
    }

    private static List<ProcessDefinition> RemoveManagedDefinitions(List<ProcessDefinition> definitions)
    {
        return definitions.Where(d => d.Name != "local_backend_remote").ToList();
    }

    private void AddLocalWebProcesses(List<ProcessDefinition> definitions)
    {
        var (localPort, localKeyBase64) = ReadLocalModeConfig();
        if (!localPort.HasValue)
            return;

        definitions.RemoveAll(d => d.Name == "local_backend");

        definitions.Add(new ProcessDefinition(
            "remote_backend", "PowerWordRelive.RemoteBackend",
            Array.Empty<string>(),
            $"--mode local --port {localPort.Value} --key {localKeyBase64}"));

        definitions.Add(new ProcessDefinition(
            "local_backend_local", "PowerWordRelive.LocalBackend",
            new[] { "storage", "general" },
            $"--mode local --port {localPort.Value} --key {localKeyBase64}"));

        TryAddRemoteBackend(definitions);
    }

    private (int? port, string? keyBase64) ReadLocalModeConfig()
    {
        if (!_config.TryGetValue("local_mode", out var localMode))
        {
            LogRedirector.Error("PowerWordRelive.Host",
                "local_mode.local_port not configured, skipping local web suite");
            return (null, null);
        }

        if (!localMode.TryGetValue("local_port", out var portStr) ||
            !int.TryParse(portStr, out var port))
        {
            LogRedirector.Error("PowerWordRelive.Host",
                "local_mode.local_port is missing or invalid, skipping local web suite");
            return (null, null);
        }

        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var keyBase64 = Convert.ToBase64String(keyBytes);

        return (port, keyBase64);
    }

    private void TryAddRemoteBackend(List<ProcessDefinition> definitions)
    {
        if (!_config.TryGetValue("local_backend", out var lbConfig))
            return;

        if (!lbConfig.TryGetValue("remote_enabled", out var remoteEnabled) || remoteEnabled != "true")
            return;

        var remoteHost = lbConfig.GetValueOrDefault("remote_host", "");
        var remotePortStr = lbConfig.GetValueOrDefault("remote_port", "");
        var keyPath = lbConfig.GetValueOrDefault("key_path", "");

        if (string.IsNullOrEmpty(remoteHost) ||
            string.IsNullOrEmpty(remotePortStr) ||
            !int.TryParse(remotePortStr, out var remotePort) ||
            string.IsNullOrEmpty(keyPath))
        {
            LogRedirector.Error("PowerWordRelive.Host",
                "local_backend.remote_enabled is true but remote_host/remote_port/key_path is missing or invalid");
            return;
        }

        if (!_fs.FileExists(keyPath))
        {
            LogRedirector.Error("PowerWordRelive.Host",
                $"Key file not found for remote backend: {keyPath}");
            return;
        }

        try
        {
            var remoteKeyBase64 = _fs.ReadAllText(keyPath).Trim();

            definitions.Add(new ProcessDefinition(
                "local_backend_remote", "PowerWordRelive.LocalBackend",
                new[] { "local_backend", "storage", "general" },
                $"--mode remote --host {remoteHost} --port {remotePort} --key {remoteKeyBase64}"));
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.Host",
                $"Failed to read key file for remote backend: {ex.Message}");
        }
    }

    public async Task WaitAllAsync(CancellationToken ct = default)
    {
        var exitTasks = _spawned.Select(async spawned =>
        {
            try
            {
                await spawned.SystemProcess.WaitForExitAsync(ct);

                var exitCode = spawned.SystemProcess.ExitCode;
                LogRedirector.Info("PowerWordRelive.Host",
                    $"Child process {spawned.ProcessName} exited with code {exitCode}",
                    new { process = spawned.ProcessName, exitCode });
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

        await Task.WhenAll(exitTasks);

        await Task.WhenAll(_spawned.Select(s => s.StdoutTask));
        await Task.WhenAll(_spawned.Select(s => s.StderrTask));

        LogRedirector.Info("PowerWordRelive.Host", "All child processes exited, shutting down");
    }

    public void KillAll()
    {
        LogRedirector.Info("PowerWordRelive.Host", "Sending termination signal to all child processes",
            new { count = _spawned.Count });

        foreach (var spawned in _spawned)
        {
            var pid = spawned.SystemProcess.Id;
            var name = spawned.ProcessName;

            try
            {
                if (spawned.SystemProcess.HasExited)
                {
                    LogRedirector.Info("PowerWordRelive.Host", "Child process already exited before termination",
                        new { process = name, pid, exitCode = spawned.SystemProcess.ExitCode });
                    continue;
                }

                LogRedirector.Info("PowerWordRelive.Host", "Sending SIGTERM to child process",
                    new { process = name, pid });

                _platform.SendTermSignal(spawned.SystemProcess);
                spawned.SystemProcess.WaitForExit(5000);

                if (spawned.SystemProcess.HasExited)
                {
                    LogRedirector.Info("PowerWordRelive.Host", "Child process exited after SIGTERM",
                        new { process = name, pid, exitCode = spawned.SystemProcess.ExitCode });
                    continue;
                }

                LogRedirector.Warn("PowerWordRelive.Host", "Child process did not exit, sending SIGKILL",
                    new { process = name, pid });

                spawned.SystemProcess.Kill(true);
                spawned.SystemProcess.WaitForExit(3000);

                if (spawned.SystemProcess.HasExited)
                    LogRedirector.Info("PowerWordRelive.Host", "Child process killed by SIGKILL",
                        new { process = name, pid });
                else
                    LogRedirector.Warn("PowerWordRelive.Host", "Child process still alive after SIGKILL",
                        new { process = name, pid });
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.Host", "Error terminating child process",
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
