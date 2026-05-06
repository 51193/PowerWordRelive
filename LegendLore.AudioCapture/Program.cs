using LegendLore.AudioCapture;
using LegendLore.Infrastructure.Configuration;
using LegendLore.Infrastructure.Logging;

var config = ChildConfigReader.ReadConfig();
var audioConfig = config.GetValueOrDefault("audio_capture", new Dictionary<string, string>());
var generalConfig = config.GetValueOrDefault("general", new Dictionary<string, string>());

var workRoot = generalConfig.GetValueOrDefault("work_root", "");
var outputDir = audioConfig.GetValueOrDefault("output_dir", "./segments");
int.TryParse(audioConfig.GetValueOrDefault("sample_rate", "16000"), out var sampleRate);
int.TryParse(audioConfig.GetValueOrDefault("silence_timeout_ms", "300"), out var silenceMs);
int.TryParse(audioConfig.GetValueOrDefault("max_segment_sec", "120"), out var maxSec);
int.TryParse(audioConfig.GetValueOrDefault("no_speech_timeout_sec", "30"), out var noSpeechTimeoutSec);
int.TryParse(audioConfig.GetValueOrDefault("min_speech_ms", "500"), out var minSpeechMs);

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
    outputDir = Path.GetFullPath(Path.Combine(workRoot, outputDir));
else if (!Path.IsPathRooted(outputDir))
    outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outputDir));

Directory.CreateDirectory(outputDir);

var pythonPath = Path.Combine(AppContext.BaseDirectory, "venv", "bin", "python3");
var pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "vad_segmenter.py");

if (!File.Exists(pythonPath))
{
    LogRedirector.Error("LegendLore.AudioCapture",
        $"Python not found: {pythonPath}");
    return;
}

if (!File.Exists(pythonScriptPath))
{
    LogRedirector.Error("LegendLore.AudioCapture",
        $"Python VAD script not found: {pythonScriptPath}");
    return;
}

var handler = new LocalFileSegmentHandler();
var process = new RecordingProcess(
    outputDir, pythonScriptPath, pythonPath, handler,
    silenceMs, maxSec, noSpeechTimeoutSec, minSpeechMs);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("LegendLore.AudioCapture", "Shutting down...");
    cts.Cancel();
};

await process.RunAsync(cts.Token);
