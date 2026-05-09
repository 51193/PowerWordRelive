namespace PowerWordRelive.AudioCapture;

internal interface IAudioCaptureDevice
{
    string BuildFfmpegInputArgs(string? configuredDevice);
}