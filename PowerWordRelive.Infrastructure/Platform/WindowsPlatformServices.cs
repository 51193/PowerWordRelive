#if WINDOWS
using System.Diagnostics;

namespace PowerWordRelive.Infrastructure.Platform;

internal sealed class WindowsPlatformServices : IPlatformServices
{
    public IDisposable? RegisterShutdownSignal(Action handler)
    {
        return null;
    }

    public void SendTermSignal(Process process)
    {
        if (process.HasExited) return;
        try { process.Kill(true); } catch { }
    }

    public void SendInterruptSignal(Process process)
    {
        if (process.HasExited) return;
        try { process.Kill(true); } catch { }
    }

    public string GetPythonVenvExecutable(string venvDir)
    {
        return Path.Combine(venvDir, "Scripts", "python.exe");
    }
}

#endif