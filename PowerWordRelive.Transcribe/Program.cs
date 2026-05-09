using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.Transcribe;

var platform = PlatformServicesFactory.Create();
var fs = new LocalFileSystem();

var config = ChildConfigReader.ReadConfig();
var trConfig = config.GetValueOrDefault("transcribe", new Dictionary<string, string>());
var generalConfig = config.GetValueOrDefault("general", new Dictionary<string, string>());
var msConfig = config.GetValueOrDefault("modelscope", new Dictionary<string, string>());

var workRoot = generalConfig.GetValueOrDefault("work_root", "");
var inputDir = trConfig.GetValueOrDefault("input_dir", "./speaker_segments");
var outputDir = trConfig.GetValueOrDefault("output_dir", "./transcriptions");
var model = trConfig.GetValueOrDefault("model", "paraformer-zh");
var device = trConfig.GetValueOrDefault("device", "cuda");
int.TryParse(trConfig.GetValueOrDefault("poll_interval_sec", "1"), out var pollIntervalSec);
var msToken = msConfig.GetValueOrDefault("token", "");

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
{
    inputDir = Path.GetFullPath(Path.Combine(workRoot, inputDir));
    outputDir = Path.GetFullPath(Path.Combine(workRoot, outputDir));
}
else
{
    if (!Path.IsPathRooted(inputDir))
        inputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, inputDir));
    if (!Path.IsPathRooted(outputDir))
        outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outputDir));
}

var cacheRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "cache"));

fs.CreateDirectory(inputDir);
fs.CreateDirectory(outputDir);

var pythonPath = platform.GetPythonVenvExecutable(Path.Combine(AppContext.BaseDirectory, "transcribe_venv"));
var pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "transcribe_server.py");

if (!fs.FileExists(pythonPath))
{
    LogRedirector.Error("PowerWordRelive.Transcribe",
        $"Python not found: {pythonPath}");
    return 1;
}

if (!fs.FileExists(pythonScriptPath))
{
    LogRedirector.Error("PowerWordRelive.Transcribe",
        $"Python script not found: {pythonScriptPath}");
    return 1;
}

LogRedirector.Info("PowerWordRelive.Transcribe", "Transcribe starting",
    new { inputDir, outputDir, model, device, pollIntervalSec });

var process = new TranscribeProcess(new TranscribeOptions(
    inputDir, outputDir, pythonScriptPath, pythonPath, cacheRoot,
    model, device, pollIntervalSec, fs, msToken));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("PowerWordRelive.Transcribe", "Shutting down...");
    cts.Cancel();
};

platform.RegisterShutdownSignal(() =>
{
    LogRedirector.Info("PowerWordRelive.Transcribe", "Shutting down...");
    cts.Cancel();
});

try
{
    await process.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
}

LogRedirector.Info("PowerWordRelive.Transcribe", "Transcribe stopped");
return 0;