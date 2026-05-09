using PowerWordRelive.LLMRequester.Core;

namespace PowerWordRelive.LLMRequester.Requests;

public record TaskConfig(
    string Model,
    bool ThinkingEnabled,
    string ReasoningEffort,
    int RefinementWindow,
    int StoryProgressWindow,
    int ActiveTaskWindow,
    int FinishedTaskWindow)
    : LlmRequestConfig(Model, ThinkingEnabled, ReasoningEffort);