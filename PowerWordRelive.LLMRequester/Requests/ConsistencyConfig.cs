using PowerWordRelive.LLMRequester.Core;

namespace PowerWordRelive.LLMRequester.Requests;

public record ConsistencyConfig(
    string Model,
    bool ThinkingEnabled,
    string ReasoningEffort,
    int RefinementWindow,
    int StoryProgressWindow)
    : LlmRequestConfig(Model, ThinkingEnabled, ReasoningEffort);