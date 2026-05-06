using System.Runtime.InteropServices;
using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.SpeakerSplit;

var fs = new LocalFileSystem();

var config = ChildConfigReader.ReadConfig();
var spConfig = config.GetValueOrDefault("speaker_split", new Dictionary<string, string>());
var generalConfig = config.GetValueOrDefault("general", new Dictionary<string, string>());
var hfConfig = config.GetValueOrDefault("huggingface", new Dictionary<string, string>());

var workRoot = generalConfig.GetValueOrDefault("work_root", "");
var inputDir = spConfig.GetValueOrDefault("input_dir", "./segments");
var outputDir = spConfig.GetValueOrDefault("output_dir", "./speaker_segments");
var embeddingsDir = spConfig.GetValueOrDefault("embeddings_dir", "./speaker_embeddings");
var device = spConfig.GetValueOrDefault("device", "cpu");
var hfToken = hfConfig.GetValueOrDefault("token", "");
float.TryParse(spConfig.GetValueOrDefault("match_threshold", "0.55"), out var matchThreshold);
int.TryParse(spConfig.GetValueOrDefault("poll_interval_sec", "1"), out var pollIntervalSec);
int.TryParse(spConfig.GetValueOrDefault("omp_num_threads", "8"), out var ompNumThreads);
int.TryParse(spConfig.GetValueOrDefault("segmentation_batch_size", "64"), out var segBatchSize);
int.TryParse(spConfig.GetValueOrDefault("embedding_batch_size", "64"), out var embBatchSize);

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
{
    inputDir = Path.GetFullPath(Path.Combine(workRoot, inputDir));
    outputDir = Path.GetFullPath(Path.Combine(workRoot, outputDir));
    embeddingsDir = Path.GetFullPath(Path.Combine(workRoot, embeddingsDir));
}
else
{
    if (!Path.IsPathRooted(inputDir))
        inputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, inputDir));
    if (!Path.IsPathRooted(outputDir))
        outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outputDir));
    if (!Path.IsPathRooted(embeddingsDir))
        embeddingsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, embeddingsDir));
}

var cacheRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "cache"));

fs.CreateDirectory(inputDir);
fs.CreateDirectory(outputDir);
fs.CreateDirectory(embeddingsDir);

var pythonPath = Path.Combine(AppContext.BaseDirectory, "speaker_split_venv", "bin", "python3");
var pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "speaker_diarize.py");

if (!fs.FileExists(pythonPath))
{
    LogRedirector.Error("PowerWordRelive.SpeakerSplit",
        $"Python not found: {pythonPath}");
    return 1;
}

if (!fs.FileExists(pythonScriptPath))
{
    LogRedirector.Error("PowerWordRelive.SpeakerSplit",
        $"Python script not found: {pythonScriptPath}");
    return 1;
}

LogRedirector.Info("PowerWordRelive.SpeakerSplit", "SpeakerSplit starting",
    new { inputDir, outputDir, embeddingsDir, device, matchThreshold, pollIntervalSec });

var process = new SpeakerSplitProcess(new SpeakerSplitOptions(
    inputDir, outputDir, embeddingsDir,
    pythonScriptPath, pythonPath, cacheRoot,
    hfToken, device, matchThreshold, ompNumThreads,
    segBatchSize, embBatchSize, pollIntervalSec, fs));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("PowerWordRelive.SpeakerSplit", "Shutting down...");
    cts.Cancel();
};

PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
{
    LogRedirector.Info("PowerWordRelive.SpeakerSplit", "Shutting down...");
    cts.Cancel();
});

try
{
    await process.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
}

LogRedirector.Info("PowerWordRelive.SpeakerSplit", "SpeakerSplit stopped");
return 0;