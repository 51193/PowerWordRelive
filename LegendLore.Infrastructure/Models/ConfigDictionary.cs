namespace LegendLore.Infrastructure.Models;

public static class ConfigDictionary
{
    public static Dictionary<string, Dictionary<string, string>> DeepCopy(
        Dictionary<string, Dictionary<string, string>> source)
    {
        var copy = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (domain, entries) in source) copy[domain] = new Dictionary<string, string>(entries);
        return copy;
    }
}