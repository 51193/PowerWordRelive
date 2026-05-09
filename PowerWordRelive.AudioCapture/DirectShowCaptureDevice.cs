#if WINDOWS
namespace PowerWordRelive.AudioCapture;

internal sealed class DirectShowCaptureDevice : IAudioCaptureDevice
{
    private const string DefaultDevice = "VB-Audio Virtual Cable";

    public string BuildFfmpegInputArgs(string? configuredDevice)
    {
        var name = configuredDevice ?? DefaultDevice;
        return $"-f dshow -i audio=\"{name}\"";
    }
}

#endif