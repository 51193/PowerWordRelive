using PowerWordRelive.LLMRequester.Core;

namespace PowerWordRelive.LLMRequester.Requests;

public record SpeakerIdentificationConfig(string Model, bool ThinkingEnabled, string ReasoningEffort, int ContextWindow)
    : LlmRequestConfig(Model, ThinkingEnabled, ReasoningEffort);