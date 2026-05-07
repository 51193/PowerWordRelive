using System.Runtime.InteropServices;
using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.LLMRequester.Core;
using PowerWordRelive.LLMRequester.Requests;

var fs = new LocalFileSystem();

var config = ChildConfigReader.ReadConfig();
var llmConfig = config.GetValueOrDefault("llm", new Dictionary<string, string>());
var llmRequestConfig = config.GetValueOrDefault("llm_request", new Dictionary<string, string>());

var llmToken = llmConfig.GetValueOrDefault("token", "");
if (string.IsNullOrWhiteSpace(llmToken))
{
    LogRedirector.Error("PowerWordRelive.LLMRequester",
        "Missing required config: llm.token");
    return 1;
}

var timerIntervals = new Dictionary<string, TimeSpan>();
foreach (var (k, v) in llmRequestConfig)
{
    if (!k.StartsWith("timer."))
        continue;

    if (!int.TryParse(v, out var sec) || sec <= 0)
    {
        LogRedirector.Warn("PowerWordRelive.LLMRequester",
            $"Invalid timer interval for '{k}': {v}, skipping");
        continue;
    }

    var key = k["timer.".Length..];
    timerIntervals[key] = TimeSpan.FromSeconds(sec);
}

if (timerIntervals.Count == 0)
{
    LogRedirector.Error("PowerWordRelive.LLMRequester",
        "No timer intervals configured in llm_request domain");
    return 1;
}

var queue = new ConcurrentRequestQueue();
var registry = RequestRegistry.Build(llmToken);

LogRedirector.Info("PowerWordRelive.LLMRequester", "LLM Requester starting",
    new { timerCount = timerIntervals.Count });

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