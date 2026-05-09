using PowerWordRelive.AudioCapture;
using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Storage;

var platform = PlatformServicesFactory.Create();
var device = AudioCaptureDeviceFactory.Create();
var fs = new LocalFileSystem();

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
audioConfig.TryGetValue("windows_audio_device", out var windowsAudioDevice);

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
    outputDir = Path.GetFullPath(Path.Combine(workRoot, outputDir));
else if (!Path.IsPathRooted(outputDir))
    outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, outputDir));

fs.CreateDirectory(outputDir);

var cacheRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "cache"));

var pythonPath = platform.GetPythonVenvExecutable(Path.Combine(AppContext.BaseDirectory, "vad_venv"));
var pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "vad_segmenter.py");

if (!fs.FileExists(pythonPath))
{
    LogRedirector.Error("PowerWordRelive.AudioCapture",
        $"Python not found: {pythonPath}");
    return;
}

if (!fs.FileExists(pythonScriptPath))
{
    LogRedirector.Error("PowerWordRelive.AudioCapture",
        $"Python VAD script not found: {pythonScriptPath}");
    return;
}

var handler = new LocalFileSegmentHandler(fs);
var options = new RecordingOptions(
    outputDir, pythonScriptPath, pythonPath, cacheRoot, fs, handler,
    platform, device, windowsAudioDevice,
    silenceMs, maxSec, noSpeechTimeoutSec, minSpeechMs);
var process = new RecordingProcess(options);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("PowerWordRelive.AudioCapture", "Shutting down...");
    cts.Cancel();
};

platform.RegisterShutdownSignal(() =>
{
    LogRedirector.Info("PowerWordRelive.AudioCapture", "Shutting down...");
    cts.Cancel();
});

await process.RunAsync(cts.Token);