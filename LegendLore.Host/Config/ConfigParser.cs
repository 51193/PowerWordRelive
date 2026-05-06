using LegendLore.Infrastructure.Logging;

namespace LegendLore.Host.Config;

public static class ConfigParser
{
    public static Dictionary<string, Dictionary<string, string>> Parse(string filePath)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        if (!File.Exists(filePath))
        {
            LogRedirector.Error("LegendLore.Host", $"Config file not found: {filePath}");
            return result;
        }

        foreach (var rawLine in File.ReadAllLines(filePath))
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

        LogRedirector.Info("LegendLore.Host", "Config loaded", new { file = filePath, domains = result.Count });

        return result;
    }
}