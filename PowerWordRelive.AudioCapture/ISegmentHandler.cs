namespace PowerWordRelive.AudioCapture;

public interface ISegmentHandler
{
    Task HandleSegmentAsync(string tempFilePath, DateTime startTime, CancellationToken ct);
}