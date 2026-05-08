using PowerWordRelive.LLMRequester.Core;

namespace PowerWordRelive.LLMRequester.Requests;

public record RefinementConfig(
    string Model,
    bool ThinkingEnabled,
    string ReasoningEffort,
    int DialogueWindow,
    int RefinementWindow)
    : LlmRequestConfig(Model, ThinkingEnabled, ReasoningEffort);