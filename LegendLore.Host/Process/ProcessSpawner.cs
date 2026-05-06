using System.Diagnostics;
using System.Text.Json;
using LegendLore.Infrastructure.Logging;

namespace LegendLore.Host.Process;

public class ProcessSpawner
{
    public static SpawnedProcess Spawn(string processName, string dllPath,
        Dictionary<string, Dictionary<string, string>> config)
    {
        LogRedirector.Info("LegendLore.Host", "Launching child process",
            new { process = processName, dll = dllPath });

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\"",
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

        return new SpawnedProcess(process, stdoutTask, stderrTask);
    }

    public record SpawnedProcess(System.Diagnostics.Process SystemProcess, Task StdoutTask, Task StderrTask);
}