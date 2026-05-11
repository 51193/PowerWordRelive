namespace PowerWordRelive.AudioCapture;

internal interface IAudioCaptureDevice : IDisposable
{
    Task StartAsync(Stream pcmOut, CancellationToken ct);
}
