using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.RemoteBackend.Services;

public class AspNetLogAdapter : ILogAdapter
{
    private readonly ILogger _logger;

    public AspNetLogAdapter(ILogger<BackendConnectionManager> logger)
    {
        _logger = logger;
    }

    public void Info(string message)
    {
        _logger.LogInformation("{Message}", message);
    }

    public void Warn(string message)
    {
        _logger.LogWarning("{Message}", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        _logger.LogError(ex, "{Message}", message);
    }
}
