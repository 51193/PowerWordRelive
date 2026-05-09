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
        Dictionary<string, LlmRequestConfig> requestConfigs,
        TaskAccessor taskAccessor,
        ConsistencyAccessor consistencyAccessor)
    {
        var apiClient = new LlmApiClient();
        var registry = new Dictionary<string, IRequest>();

        foreach (var (key, config) in requestConfigs)
        {
            IRequest inner = key switch
            {
                "speaker_identification" => new SpeakerIdentificationRequest(
                    apiUrl, llmToken, db, assembler, (SpeakerIdentificationConfig)config, apiClient),

                "refinement" => CreateRefinementRequest(
                    apiUrl, llmToken, db, assembler, (RefinementConfig)config, apiClient, consistencyAccessor),

                "story_progress" => CreateStoryProgressRequest(
                    apiUrl, llmToken, db, assembler, (StoryProgressConfig)config, apiClient, consistencyAccessor),

                "task" => CreateTaskRequest(
                    apiUrl, llmToken, db, assembler, (TaskConfig)config, apiClient, taskAccessor, consistencyAccessor),

                "consistency" => CreateConsistencyRequest(
                    apiUrl, llmToken, db, assembler, (ConsistencyConfig)config, apiClient,
                    taskAccessor, consistencyAccessor),

                _ => throw new InvalidOperationException($"Unknown request key: {key}")
            };

            registry[key] = new LoggingRequestDecorator(key, inner);
        }

        return registry;
    }

    private static RefinementRequest CreateRefinementRequest(
        string apiUrl,
        string llmToken,
        LLMDatabase db,
        PromptAssembler assembler,
        RefinementConfig config,
        LlmApiClient apiClient,
        ConsistencyAccessor consistencyAccessor)
    {
        var container = new RefinementContainer(db);
        return new RefinementRequest(apiUrl, llmToken, db, container, consistencyAccessor, assembler, config,
            apiClient);
    }

    private static StoryProgressRequest CreateStoryProgressRequest(
        string apiUrl,
        string llmToken,
        LLMDatabase db,
        PromptAssembler assembler,
        StoryProgressConfig config,
        LlmApiClient apiClient,
        ConsistencyAccessor consistencyAccessor)
    {
        var container = new StoryProgressContainer(db);
        var refContainer = new RefinementContainer(db);
        return new StoryProgressRequest(apiUrl, llmToken, db, container, refContainer, consistencyAccessor,
            assembler, config, apiClient);
    }

    private static TaskRequest CreateTaskRequest(
        string apiUrl,
        string llmToken,
        LLMDatabase db,
        PromptAssembler assembler,
        TaskConfig config,
        LlmApiClient apiClient,
        TaskAccessor taskAccessor,
        ConsistencyAccessor consistencyAccessor)
    {
        var refContainer = new RefinementContainer(db);
        var spContainer = new StoryProgressContainer(db);
        return new TaskRequest(apiUrl, llmToken, db, refContainer, spContainer,
            taskAccessor, consistencyAccessor, assembler, config, apiClient);
    }

    private static ConsistencyRequest CreateConsistencyRequest(
        string apiUrl,
        string llmToken,
        LLMDatabase db,
        PromptAssembler assembler,
        ConsistencyConfig config,
        LlmApiClient apiClient,
        TaskAccessor taskAccessor,
        ConsistencyAccessor consistencyAccessor)
    {
        var refContainer = new RefinementContainer(db);
        var spContainer = new StoryProgressContainer(db);
        return new ConsistencyRequest(apiUrl, llmToken, db, refContainer, spContainer,
            taskAccessor, consistencyAccessor, assembler, config, apiClient);
    }
}