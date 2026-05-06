namespace LegendLore.AudioCapture;

public class LocalFileSegmentHandler : ISegmentHandler
{
    private readonly LegendLore.Infrastructure.Storage.IFileSystem _fs;

    public LocalFileSegmentHandler(LegendLore.Infrastructure.Storage.IFileSystem fs)
    {
        _fs = fs;
    }

    public Task HandleSegmentAsync(string tempFilePath, DateTime startTime, CancellationToken ct)
    {
        var finalPath = tempFilePath[..^4];

        if (_fs.FileExists(finalPath))
            _fs.DeleteFile(finalPath);

        _fs.MoveFile(tempFilePath, finalPath);
        return Task.CompletedTask;
    }
}
