using System.Diagnostics;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Models;
using PowerWordRelive.Infrastructure.Storage;

var fs = new LocalFileSystem();

var baseDir = AppContext.BaseDirectory;
var hostDir = Path.GetFullPath(Path.Combine(baseDir, "..", "PowerWordRelive.Host"));
var hostDll = Path.Combine(hostDir, "PowerWordRelive.Host.dll");

if (!fs.FileExists(hostDll))
{
    Console.Error.WriteLine($"Host binary not found: {hostDll}");
    return 1;
}

var psi = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = $"\"{hostDll}\"",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

using var process = Process.Start(psi)!;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (!process.HasExited)
        process.Kill();
};

var stdoutTask = Task.Run(async () =>
{
    string? line;
    while ((line = await process.StandardOutput.ReadLineAsync()) != null)
        Console.WriteLine(FormatLogLine(line));
});

var stderrTask = Task.Run(async () =>
{
    string? line;
    while ((line = await process.StandardError.ReadLineAsync()) != null)
        Console.Error.WriteLine($"[ERR] {line}");
});

await process.WaitForExitAsync();
await Task.WhenAll(stdoutTask, stderrTask);

return process.ExitCode;

static string FormatLogLine(string line)
{
    try
    {
        var entry = JsonSerializer.Deserialize<LogEntry>(line);
        if (entry is null)
            return line;

        var ts = DateTime.TryParse(entry.Timestamp, out var dt)
            ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : entry.Timestamp;

        var dataStr = string.Empty;
        if (entry.Data is JsonElement { ValueKind: JsonValueKind.Object } obj &&
            obj.EnumerateObject().Any())
        {
            var parts = obj.EnumerateObject()
                .Select(p => $"{p.Name}={p.Value}")
                .ToList();
            dataStr = "  (" + string.Join(", ", parts) + ")";
        }

        return $"[{ts}] [{entry.Level}] {entry.Source}: {entry.Message}{dataStr}";
    }
    catch (JsonException)
    {
        return line;
    }
}