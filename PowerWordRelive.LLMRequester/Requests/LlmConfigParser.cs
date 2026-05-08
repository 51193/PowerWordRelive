using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Core;

namespace PowerWordRelive.LLMRequester.Requests;

public static class LlmConfigParser
{
    public static Dictionary<string, LlmRequestConfig> Parse(
        IEnumerable<string> keys,
        Dictionary<string, string> llmRequestConfig)
    {
        var result = new Dictionary<string, LlmRequestConfig>();
        foreach (var key in keys)
            result[key] = key switch
            {
                "speaker_identification" => ParseSpeakerIdentification(key, llmRequestConfig),
                "refinement" => ParseRefinement(key, llmRequestConfig),
                "story_progress" => ParseStoryProgress(key, llmRequestConfig),
                _ => throw new InvalidOperationException($"Unknown request key: {key}")
            };
        return result;
    }

    private static SpeakerIdentificationConfig ParseSpeakerIdentification(
        string key, Dictionary<string, string> cfg)
    {
        var model = ParseModel(key, cfg);
        var thinkingEnabled = ParseThinkingEnabled(key, cfg);
        var reasoningEffort = ParseReasoningEffort(key, cfg);
        var contextWindow = ParseContextWindow(key, cfg);
        return new SpeakerIdentificationConfig(model, thinkingEnabled, reasoningEffort, contextWindow);
    }

    private static string ParseModel(string key, Dictionary<string, string> cfg)
    {
        return cfg.GetValueOrDefault($"{key}.model", "deepseek-v4-flash");
    }

    private static bool ParseThinkingEnabled(string key, Dictionary<string, string> cfg)
    {
        var thinkingStr = cfg.GetValueOrDefault($"{key}.thinking_enabled", "false");
        var thinkingEnabled = thinkingStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (!bool.TryParse(thinkingStr, out var parsedThinking))
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Invalid thinking_enabled for '{key}': {thinkingStr}, defaulting to false");
        else
            thinkingEnabled = parsedThinking;
        return thinkingEnabled;
    }

    private static string ParseReasoningEffort(string key, Dictionary<string, string> cfg)
    {
        return cfg.GetValueOrDefault($"{key}.reasoning_effort", "high").ToLowerInvariant() switch
        {
            "max" or "xhigh" => "max",
            _ => "high"
        };
    }

    private static RefinementConfig ParseRefinement(string key, Dictionary<string, string> cfg)
    {
        var model = ParseModel(key, cfg);
        var thinkingEnabled = ParseThinkingEnabled(key, cfg);
        var reasoningEffort = ParseReasoningEffort(key, cfg);
        var dialogueWindow = ParseIntConfig(key, "dialogue_window", cfg, 30);
        var refinementWindow = ParseIntConfig(key, "refinement_window", cfg, 20);
        return new RefinementConfig(model, thinkingEnabled, reasoningEffort, dialogueWindow, refinementWindow);
    }

    private static StoryProgressConfig ParseStoryProgress(string key, Dictionary<string, string> cfg)
    {
        var model = ParseModel(key, cfg);
        var thinkingEnabled = ParseThinkingEnabled(key, cfg);
        var reasoningEffort = ParseReasoningEffort(key, cfg);
        var refinementWindow = ParseIntConfig(key, "refinement_window", cfg, 20);
        var storyProgressWindow = ParseIntConfig(key, "story_progress_window", cfg, 15);
        return new StoryProgressConfig(model, thinkingEnabled, reasoningEffort, refinementWindow, storyProgressWindow);
    }

    private static int ParseIntConfig(string key, string subKey, Dictionary<string, string> cfg, int defaultValue)
    {
        var str = cfg.GetValueOrDefault($"{key}.{subKey}", defaultValue.ToString());
        if (!int.TryParse(str, out var val) || val <= 0)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Invalid {subKey} for '{key}': {str}, defaulting to {defaultValue}");
            val = defaultValue;
        }

        return val;
    }

    private static int ParseContextWindow(string key, Dictionary<string, string> cfg)
    {
        var ctxStr = cfg.GetValueOrDefault($"{key}.context_window", "2");
        if (!int.TryParse(ctxStr, out var ctxWin) || ctxWin < 0)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Invalid context_window for '{key}': {ctxStr}, defaulting to 2");
            ctxWin = 2;
        }

        return ctxWin;
    }
}