#if LINUX

using System.Diagnostics;

namespace PowerWordRelive.AudioCapture;

internal sealed class PulseAudioCaptureDevice : IAudioCaptureDevice
{
    public string BuildFfmpegInputArgs(string? _)
    {
        var sink = DetectPulseAudioSink();
        return $"-f pulse -i {sink}.monitor";
    }

    private static string DetectPulseAudioSink()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pactl",
            Arguments = "get-default-sink",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        var sink = process.StandardOutput.ReadToEnd().Trim();
        if (!string.IsNullOrEmpty(sink))
            return sink;

        return "default";
    }
}

#endif