using Microsoft.Data.Sqlite;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;
using PowerWordRelive.LLMRequester.Models;

namespace PowerWordRelive.LLMRequester.Requests;

internal class SpeakerIdentificationRequest : IRequest
{
    private const string SystemPromptFile = "prompts/speaker_id/speaker_id_system.md";
    private const string UserPromptFile = "prompts/speaker_id/speaker_id_user.md";
    private const string UnknownMarker = "__UNKNOWN__";
    private const string ClusterSeparator = "\n---对话分割---\n";
    private const int MaxClustersBeforeSampling = 15;
    private const int HeadKeep = 3;
    private const int TailKeep = 12;

    private readonly string _apiUrl;
    private readonly string _token;
    private readonly LLMDatabase _db;
    private readonly PromptAssembler _assembler;
    private readonly SpeakerIdentificationConfig _config;
    private readonly LlmApiClient _apiClient;

    public SpeakerIdentificationRequest(
        string apiUrl,
        string token,
        LLMDatabase db,
        PromptAssembler assembler,
        SpeakerIdentificationConfig config,
        LlmApiClient apiClient)
    {
        _apiUrl = apiUrl;
        _token = token;
        _db = db;
        _assembler = assembler;
        _config = config;
        _apiClient = apiClient;
    }

    public async Task Request()
    {
        List<SpeakerMapping> unassigned;
        Dictionary<string, string> nameMap;

        try
        {
            unassigned = _db.GetUnassignedSpeakers();
            if (unassigned.Count == 0)
                return;

            nameMap = _db.GetSpeakerNameMap();
        }
        catch (SqliteException ex)
        {
            LogRedirector.Info("PowerWordRelive.LLMRequester",
                $"Database not ready: {ex.Message}");
            return;
        }

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            $"Processing {unassigned.Count} unassigned speaker(s)");

        foreach (var spk in unassigned)
            try
            {
                var ids = _db.GetTranscriptionIdsForSpeaker(spk.SpeakerId);
                if (ids.Count == 0)
                {
                    LogRedirector.Warn("PowerWordRelive.LLMRequester",
                        $"No dialogue found for speaker '{spk.SpeakerId}', skipping");
                    continue;
                }

                var clusters = DialogueClusterer.BuildClusters(ids, _config.ContextWindow);
                var dialogueText = BuildDialogueText(clusters, spk.SpeakerId, nameMap);

                var emptyVars = new Dictionary<string, string>();
                var userVars = new Dictionary<string, string> { ["dialogue"] = dialogueText };
                var systemPrompt = _assembler.Assemble(SystemPromptFile, emptyVars);
                var userPrompt = _assembler.Assemble(UserPromptFile, userVars);

                var response = await _apiClient.SendAsync(_apiUrl, _token, _config,
                    systemPrompt, userPrompt);

                var name = response.Content.Trim();
                if (name == UnknownMarker || string.IsNullOrEmpty(name))
                {
                    LogRedirector.Info("PowerWordRelive.LLMRequester",
                        $"Speaker '{spk.SpeakerId}' remains unidentified");
                    continue;
                }

                _db.UpdateSpeakerRole(spk.SpeakerId, name);
                LogRedirector.Info("PowerWordRelive.LLMRequester",
                    $"Speaker '{spk.SpeakerId}' identified as: {name}");
            }
            catch (Exception ex)
            {
                LogRedirector.Error("PowerWordRelive.LLMRequester",
                    $"Failed to identify speaker '{spk.SpeakerId}': {ex.Message}");
            }
    }

    private string BuildDialogueText(
        List<List<long>> clusters,
        string targetSpeakerId,
        Dictionary<string, string> nameMap)
    {
        var segments = new List<string>();
        var totalClusters = clusters.Count;
        int omitted;

        if (totalClusters > MaxClustersBeforeSampling)
        {
            omitted = totalClusters - HeadKeep - TailKeep;
            clusters = clusters.Take(HeadKeep)
                .Concat(clusters.Skip(totalClusters - TailKeep))
                .ToList();
        }
        else
        {
            omitted = 0;
        }

        foreach (var cluster in clusters)
        {
            var minId = cluster[0] - _config.ContextWindow;
            var maxId = cluster[^1] + _config.ContextWindow;
            var entries = _db.GetDialogueRange(minId, maxId);
            var formatted = DialogueClusterer.FormatContext(entries, targetSpeakerId, nameMap);
            segments.Add(formatted);
        }

        if (omitted > 0)
            segments.Add($"--- 省略 {omitted} 段对话 ---");

        return string.Join(ClusterSeparator, segments);
    }
}
