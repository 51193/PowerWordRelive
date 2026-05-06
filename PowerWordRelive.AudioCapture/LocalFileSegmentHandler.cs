using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.AudioCapture;

internal class LocalFileSegmentHandler : ISegmentHandler
{
    private readonly IFileSystem _fs;

    public LocalFileSegmentHandler(IFileSystem fs)
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