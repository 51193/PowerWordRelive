namespace LegendLore.AudioCapture;

public class LocalFileSegmentHandler : ISegmentHandler
{
    public Task HandleSegmentAsync(string tempFilePath, DateTime startTime, CancellationToken ct)
    {
        var finalPath = tempFilePath[..^4];

        if (File.Exists(finalPath))
            File.Delete(finalPath);

        File.Move(tempFilePath, finalPath);
        return Task.CompletedTask;
    }
}
