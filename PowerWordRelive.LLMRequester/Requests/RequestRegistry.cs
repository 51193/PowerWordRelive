using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Requests;

public static class RequestRegistry
{
    public static Dictionary<string, IRequest> Build(
        string apiUrl,
        string llmToken,
        LLMDatabase db,
        PromptAssembler assembler,
        Dictionary<string, LlmRequestConfig> requestConfigs)
    {
        var apiClient = new LlmApiClient();
        var registry = new Dictionary<string, IRequest>();

        foreach (var (key, config) in requestConfigs)
        {
            IRequest inner = key switch
            {
                "speaker_identification" => new SpeakerIdentificationRequest(
                    apiUrl, llmToken, db, assembler, (SpeakerIdentificationConfig)config, apiClient),

                _ => throw new InvalidOperationException($"Unknown request key: {key}")
            };

            registry[key] = new LoggingRequestDecorator(key, inner);
        }

        return registry;
    }
}
