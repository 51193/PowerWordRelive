using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.LLMRequester.Database;

namespace PowerWordRelive.LLMRequester.Core;

public record LlmRequestConfig(string Model, bool ThinkingEnabled, string ReasoningEffort);

public record LlmResponse(string Content, int OutputTokens, int CachedInputTokens, int MissInputTokens);

public class LlmApiClient
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(120)
    };
#if DEBUG
    private static readonly object LogLock = new();
#endif

    private readonly LLMDatabase _db;

    public LlmApiClient(LLMDatabase db)
    {
        _db = db;
    }

    public async Task<LlmResponse> SendAsync(
        string apiUrl,
        string token,
        LlmRequestConfig config,
        string systemPrompt,
        string userPrompt,
        string requestKey)
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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var requestCharCount = requestJson.Length;
        var sendTime = DateTime.Now;
        var sw = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request);
        sw.Stop();
        var receiveTime = DateTime.Now;

        var responseJson = await response.Content.ReadAsStringAsync();

#if DEBUG
        AppendDebugLog(sendTime, receiveTime, requestJson, responseJson);
#endif

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

        try
        {
            _db.TryEnsureTokenUsageTable();
            _db.InsertTokenUsage(requestKey, outputTokens, cachedInputTokens, missInputTokens);
        }
        catch (Exception ex)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Failed to write token usage: {ex.Message}");
        }

        return new LlmResponse(content, outputTokens, cachedInputTokens, missInputTokens);
    }

#if DEBUG
    private static void AppendDebugLog(
        DateTime sendTime,
        DateTime receiveTime,
        string requestJson,
        string responseJson)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "prompts.log");
        var ts = sendTime.ToString("yyyy-MM-dd HH:mm:ss");
        var tr = receiveTime.ToString("yyyy-MM-dd HH:mm:ss");

        var entry = $"""
                     === LLM Request  @ {ts} ===
                     {requestJson}
                     --- LLM Response @ {tr} ---
                     {responseJson}
                     ==============================

                     """;

        lock (LogLock)
        {
            File.AppendAllText(logPath, entry);
        }
    }
#endif
}