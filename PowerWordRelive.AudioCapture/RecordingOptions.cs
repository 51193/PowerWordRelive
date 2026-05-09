using PowerWordRelive.Infrastructure.Platform;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.AudioCapture;

internal record RecordingOptions(
    string OutputDir,
    string PythonScriptPath,
    string PythonPath,
    string CacheRoot,
    IFileSystem Fs,
    ISegmentHandler SegmentHandler,
    IPlatformServices Platform,
    IAudioCaptureDevice Device,
    string? WindowsAudioDevice = null,
    int SilenceTimeoutMs = 800,
    int MaxSegmentSec = 120,
    int NoSpeechTimeoutSec = 30,
    int MinSpeechMs = 500
);
