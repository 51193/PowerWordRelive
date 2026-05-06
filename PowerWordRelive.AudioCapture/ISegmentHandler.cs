namespace PowerWordRelive.AudioCapture;

internal interface ISegmentHandler
{
    Task HandleSegmentAsync(string tempFilePath, DateTime startTime, CancellationToken ct);
}