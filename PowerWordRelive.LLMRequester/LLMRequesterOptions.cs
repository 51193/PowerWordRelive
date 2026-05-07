namespace PowerWordRelive.LLMRequester;

internal record LLMRequesterOptions(
    string LlmToken,
    Dictionary<string, TimeSpan> TimerIntervals
);