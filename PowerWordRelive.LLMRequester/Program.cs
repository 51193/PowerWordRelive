using System.Runtime.InteropServices;
using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Prompt;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Database;
using PowerWordRelive.LLMRequester.Requests;

var fs = new LocalFileSystem();

var config = ChildConfigReader.ReadConfig();
var llmConfig = config.GetValueOrDefault("llm", new Dictionary<string, string>());
var llmRequestConfig = config.GetValueOrDefault("llm_request", new Dictionary<string, string>());
var generalConfig = config.GetValueOrDefault("general", new Dictionary<string, string>());
var storageConfig = config.GetValueOrDefault("storage", new Dictionary<string, string>());
var textDataConfig = config.GetValueOrDefault("text_data", new Dictionary<string, string>());

var llmToken = llmConfig.GetValueOrDefault("token", "");
var apiUrl = llmConfig.GetValueOrDefault("api_url", "");

if (string.IsNullOrWhiteSpace(llmToken))
{
    LogRedirector.Error("PowerWordRelive.LLMRequester", "Missing required config: llm.token");
    return 1;
}

if (string.IsNullOrWhiteSpace(apiUrl))
{
    LogRedirector.Error("PowerWordRelive.LLMRequester", "Missing required config: llm.api_url");
    return 1;
}

var workRoot = generalConfig.GetValueOrDefault("work_root", "");

var sqlitePath = storageConfig.GetValueOrDefault("sqlite_path", "");
if (string.IsNullOrWhiteSpace(sqlitePath))
{
    LogRedirector.Error("PowerWordRelive.LLMRequester", "Missing required config: storage.sqlite_path");
    return 1;
}

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
    sqlitePath = Path.GetFullPath(Path.Combine(workRoot, sqlitePath));
else if (!Path.IsPathRooted(sqlitePath))
    sqlitePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, sqlitePath));

var textDataBaseDir = textDataConfig.GetValueOrDefault("base_dir", "text_data/");
if (!Path.IsPathRooted(textDataBaseDir))
    textDataBaseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", textDataBaseDir));

LogRedirector.Info("PowerWordRelive.LLMRequester", "Text data dir resolved",
    new { textDataBaseDir });

if (!fs.DirectoryExists(textDataBaseDir))
{
    LogRedirector.Error("PowerWordRelive.LLMRequester",
        $"Text data directory not found: {textDataBaseDir}");
    return 1;
}

if (!fs.FileExists(sqlitePath))
{
    LogRedirector.Warn("PowerWordRelive.LLMRequester",
        $"SQLite database not yet available: {sqlitePath}, will wait for producer");
}

var timerIntervals = new Dictionary<string, TimeSpan>();
var requestConfigs = new Dictionary<string, LlmRequestConfig>();

foreach (var (k, v) in llmRequestConfig)
{
    if (k.StartsWith("timer."))
    {
        var key = k["timer.".Length..];
        if (!int.TryParse(v, out var sec) || sec <= 0)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Invalid timer interval for '{k}': {v}, skipping");
            continue;
        }

        timerIntervals[key] = TimeSpan.FromSeconds(sec);
    }
}

foreach (var key in timerIntervals.Keys)
{
    var model = llmRequestConfig.GetValueOrDefault($"{key}.model", "deepseek-v4-pro");
    var thinkingStr = llmRequestConfig.GetValueOrDefault($"{key}.thinking_enabled", "false");
    var reasoningStr = llmRequestConfig.GetValueOrDefault($"{key}.reasoning_effort", "high");
    var contextWindowStr = llmRequestConfig.GetValueOrDefault($"{key}.context_window", "2");

    var thinkingEnabled = thinkingStr.Equals("true", StringComparison.OrdinalIgnoreCase);

    if (!bool.TryParse(thinkingStr, out var parsedThinking))
        LogRedirector.Warn("PowerWordRelive.LLMRequester",
            $"Invalid thinking_enabled for '{key}': {thinkingStr}, defaulting to false");
    else
        thinkingEnabled = parsedThinking;

    var reasoningEffort = reasoningStr.ToLowerInvariant() switch
    {
        "low" or "medium" or "high" => "high",
        "max" or "xhigh" => "max",
        _ => "high"
    };

    if (!int.TryParse(contextWindowStr, out var contextWindow) || contextWindow < 0)
    {
        LogRedirector.Warn("PowerWordRelive.LLMRequester",
            $"Invalid context_window for '{key}': {contextWindowStr}, defaulting to 2");
        contextWindow = 2;
    }

    requestConfigs[key] = new LlmRequestConfig(model, thinkingEnabled, reasoningEffort, contextWindow);
}

if (timerIntervals.Count == 0)
{
    LogRedirector.Error("PowerWordRelive.LLMRequester",
        "No timer intervals configured in llm_request domain");
    return 1;
}

using var db = new LLMDatabase(sqlitePath);
var assembler = new PromptAssembler(fs, textDataBaseDir);

var registry = RequestRegistry.Build(apiUrl, llmToken, db, assembler, requestConfigs);
var queue = new ConcurrentRequestQueue();

LogRedirector.Info("PowerWordRelive.LLMRequester", "LLM Requester starting",
    new { apiUrl, timerCount = timerIntervals.Count, requestKeys = requestConfigs.Keys.ToArray() });

var engine = new RequesterEngine(queue, registry);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("PowerWordRelive.LLMRequester", "Shutting down...");
    cts.Cancel();
};

PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
{
    LogRedirector.Info("PowerWordRelive.LLMRequester", "Shutting down...");
    cts.Cancel();
});

var timerTasks = TimerManager.StartTimers(timerIntervals, queue, cts.Token);

try
{
    await engine.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
}

LogRedirector.Info("PowerWordRelive.LLMRequester", "LLM Requester stopped");
return 0;
