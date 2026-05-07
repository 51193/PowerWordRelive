using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.LLMRequester.Core;

public record LlmRequestConfig(string Model, bool ThinkingEnabled, string ReasoningEffort);

public record LlmResponse(string Content, int OutputTokens, int CachedInputTokens, int MissInputTokens);

public class LlmApiClient
{
    private static readonly HttpClient HttpClient = new();

    public async Task<LlmResponse> SendAsync(
        string apiUrl,
        string token,
        LlmRequestConfig config,
        string systemPrompt,
        string userPrompt)
    {
        var body = new
        {
            model = config.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            thinking = config.ThinkingEnabled
                ? new { type = "enabled" }
                : new { type = "disabled" },
            reasoning_effort = config.ReasoningEffort
        };

        var requestJson = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var requestCharCount = requestJson.Length;
        var sw = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);
        sw.Stop();

        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            LogRedirector.Error("PowerWordRelive.LLMRequester",
                $"API request failed (HTTP {(int)response.StatusCode})",
                new { status = (int)response.StatusCode, requestCharCount, elapsedMs = sw.ElapsedMilliseconds });
            throw new HttpRequestException(
                $"LLM API returned {(int)response.StatusCode}: {responseJson[..Math.Min(responseJson.Length, 200)]}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        var usage = root.GetProperty("usage");
        var outputTokens = usage.GetProperty("completion_tokens").GetInt32();
        var cachedInputTokens = usage.TryGetProperty("prompt_cache_hit_tokens", out var hit)
            ? hit.GetInt32()
            : 0;
        var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var missInputTokens = promptTokens - cachedInputTokens;
        var cacheHitRate = promptTokens > 0
            ? (double)cachedInputTokens / promptTokens
            : 0;

        LogRedirector.Info("PowerWordRelive.LLMRequester",
            "API request completed",
            new
            {
                requestCharCount,
                elapsedMs = sw.ElapsedMilliseconds,
                outputTokens,
                cachedInputTokens,
                missInputTokens,
                cacheHitRate = cacheHitRate.ToString("P1")
            });

        return new LlmResponse(content, outputTokens, cachedInputTokens, missInputTokens);
    }
}