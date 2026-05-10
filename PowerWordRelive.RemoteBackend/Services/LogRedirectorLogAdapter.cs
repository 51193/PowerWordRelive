using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.RemoteBackend.Services;

public class LogRedirectorLogAdapter : ILogAdapter
{
    public void Info(string message)
    {
        LogRedirector.Info("RemoteBackend", message);
    }

    public void Warn(string message)
    {
        LogRedirector.Warn("RemoteBackend", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        LogRedirector.Error("RemoteBackend", message, ex is not null ? new { error = ex.Message } : null);
    }
}
