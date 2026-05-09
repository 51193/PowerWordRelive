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

var timerIntervals = new Dictionary<TimeSpan, List<string>>();

foreach (var (k, v) in llmRequestConfig)
{
    if (k.StartsWith("timer."))
    {
        var intervalStr = k["timer.".Length..];
        if (!int.TryParse(intervalStr, out var sec) || sec <= 0)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"Invalid timer interval value '{intervalStr}' for '{k}', skipping");
            continue;
        }

        var keys = v.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keys.Length == 0)
        {
            LogRedirector.Warn("PowerWordRelive.LLMRequester",
                $"No request keys in timer '{k}', skipping");
            continue;
        }

        var interval = TimeSpan.FromSeconds(sec);
        if (!timerIntervals.TryGetValue(interval, out var list))
        {
            list = new List<string>();
            timerIntervals[interval] = list;
        }

        foreach (var key in keys)
            list.Add(key);
    }
}

var distinctKeys = timerIntervals.Values
    .SelectMany(l => l).Distinct().ToList();
var requestConfigs = LlmConfigParser.Parse(distinctKeys, llmRequestConfig);

if (timerIntervals.Count == 0)
{
    LogRedirector.Error("PowerWordRelive.LLMRequester",
        "No timer intervals configured in llm_request domain");
    return 1;
}

using var db = new LLMDatabase(sqlitePath);
var assembler = new PromptAssembler(fs, textDataBaseDir);

var activeTaskLimit = int.TryParse(
    llmRequestConfig.GetValueOrDefault("window_limits.active_task", "30"), out var atl)
    ? atl
    : 30;
var consistencyLimit = int.TryParse(
    llmRequestConfig.GetValueOrDefault("window_limits.consistency", "50"), out var cl)
    ? cl
    : 50;

var taskAccessor = new TaskAccessor(db, activeTaskLimit);
var consistencyAccessor = new ConsistencyAccessor(db, consistencyLimit);

var registry = RequestRegistry.Build(apiUrl, llmToken, db, assembler, requestConfigs,
    taskAccessor, consistencyAccessor);
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
