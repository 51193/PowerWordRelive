using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Requests;

internal class SpeakerIdentificationRequest : IRequest
{
    private const string SystemPromptFile = "prompts/speaker_id_system.md";
    private const string UserPromptFile = "prompts/speaker_id_user.md";
    private const string UnknownMarker = "__UNKNOWN__";

    private readonly string _apiUrl;
    private readonly string _token;
    private readonly LLMDatabase _db;
    private readonly PromptAssembler _assembler;
    private readonly LlmRequestConfig _config;
    private readonly LlmApiClient _apiClient;

    public SpeakerIdentificationRequest(
        string apiUrl,
        string token,
        LLMDatabase db,
        PromptAssembler assembler,
        LlmRequestConfig config,
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
        var unassigned = _db.GetUnassignedSpeakers();
        if (unassigned.Count == 0)
            return;

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            $"Processing {unassigned.Count} unassigned speaker(s)");

        foreach (var spk in unassigned)
            try
            {
                var dialogue = _db.GetDialogueForSpeaker(spk.SpeakerId);
                if (dialogue.Count == 0)
                {
                    LogRedirector.Warn("PowerWordRelive.LLMRequester",
                        $"No dialogue found for speaker '{spk.SpeakerId}', skipping");
                    continue;
                }

                var combinedText = string.Join("\n", dialogue);
                var emptyVars = new Dictionary<string, string>();
                var userVars = new Dictionary<string, string> { ["dialogue"] = combinedText };

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
}