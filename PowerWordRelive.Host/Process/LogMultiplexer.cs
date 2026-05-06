using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Models;

namespace PowerWordRelive.Host.Process;

internal class LogMultiplexer
{
    private readonly string _processName;

    public LogMultiplexer(string processName)
    {
        _processName = processName;
    }

    public async Task ReadStdoutAsync(StreamReader reader)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<LogEntry>(line);
                if (entry is not null)
                {
                    LogRedirector.Log(entry.Level, _processName, entry.Message, entry.Data);
                    continue;
                }
            }
            catch (JsonException)
            {
                // raw text, wrap as INFO
            }

            LogRedirector.Info(_processName, line);
        }
    }

    public async Task ReadStderrAsync(StreamReader reader)
    {
        string? line;
        while ((line = await reader.ReadLineAsync()) != null) LogRedirector.Warn(_processName, line);
    }
}