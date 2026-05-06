using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Host.Config;

internal class ConfigParser
{
    private readonly IFileSystem _fs;

    public ConfigParser(IFileSystem fs)
    {
        _fs = fs;
    }

    public Dictionary<string, Dictionary<string, string>> Parse(string filePath)
    {
        if (!_fs.FileExists(filePath))
            throw new FileNotFoundException($"Config file not found: {filePath}");

        var result = new Dictionary<string, Dictionary<string, string>>();

        foreach (var rawLine in _fs.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                continue;

            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            var dotIndex = key.IndexOf('.');
            if (dotIndex < 0)
                continue;

            var domain = key[..dotIndex];
            var exactConfig = key[(dotIndex + 1)..];

            if (!result.ContainsKey(domain))
                result[domain] = new Dictionary<string, string>();

            result[domain][exactConfig] = value;
        }

        LogRedirector.Info("PowerWordRelive.Host", "Config loaded", new { file = filePath, domains = result.Count });

        return result;
    }
}