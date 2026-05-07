using System.Text;
using PowerWordRelive.LLMRequester.Models;

namespace PowerWordRelive.LLMRequester.Core;

public static class DialogueClusterer
{
    public static List<List<long>> BuildClusters(List<long> sortedIds, int contextWindow)
    {
        var clusters = new List<List<long>>();
        if (sortedIds.Count == 0)
            return clusters;

        var current = new List<long> { sortedIds[0] };

        for (var i = 1; i < sortedIds.Count; i++)
            if (sortedIds[i] - current[^1] > contextWindow * 2)
            {
                clusters.Add(current);
                current = new List<long> { sortedIds[i] };
            }
            else
            {
                current.Add(sortedIds[i]);
            }

        clusters.Add(current);
        return clusters;
    }

    public static string FormatContext(
        List<DialogueEntry> entries,
        string targetSpeakerId,
        Dictionary<string, string> nameMap)
    {
        var sb = new StringBuilder();

        foreach (var entry in entries)
        {
            var displayName = ResolveDisplayName(entry.SpeakerId, nameMap);
            var marker = entry.SpeakerId == targetSpeakerId ? ">>>" : "   ";
            sb.AppendLine($"{marker} [{displayName}]: {entry.Text}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string ResolveDisplayName(string speakerId, Dictionary<string, string> nameMap)
    {
        if (nameMap.TryGetValue(speakerId, out var name)
            && !string.IsNullOrEmpty(name)
            && name != "__UNASSIGNED__"
            && name != "__UNKNOWN__")
            return name;

        return speakerId;
    }
}