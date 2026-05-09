using System.Diagnostics;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.Host.Process;

internal class ProcessSpawner
{
    public static SpawnedProcess Spawn(string processName, string dllPath,
        Dictionary<string, Dictionary<string, string>> config, string? extraArgs = null)
    {
        var arguments = $"\"{dllPath}\"";
        if (!string.IsNullOrWhiteSpace(extraArgs))
            arguments += " " + extraArgs;

        LogRedirector.Info("PowerWordRelive.Host", "Launching child process",
            new { process = processName, dll = dllPath });

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = System.Diagnostics.Process.Start(psi)!;

        var jsonConfig = JsonSerializer.Serialize(config);
        process.StandardInput.Write(jsonConfig);
        process.StandardInput.Close();

        var multiplexer = new LogMultiplexer(processName);
        var stdoutTask = multiplexer.ReadStdoutAsync(process.StandardOutput);
        var stderrTask = multiplexer.ReadStderrAsync(process.StandardError);

        return new SpawnedProcess(processName, process, stdoutTask, stderrTask);
    }

    public record SpawnedProcess(
        string ProcessName,
        System.Diagnostics.Process SystemProcess,
        Task StdoutTask,
        Task StderrTask);
}
