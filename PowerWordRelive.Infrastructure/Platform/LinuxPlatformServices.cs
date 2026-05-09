#if LINUX

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PowerWordRelive.Infrastructure.Platform;

internal sealed class LinuxPlatformServices : IPlatformServices
{
    public IDisposable? RegisterShutdownSignal(Action handler)
    {
        try
        {
            return PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => handler());
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    public void SendTermSignal(Process process)
    {
        if (process.HasExited) return;

        try
        {
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-TERM {process.Id}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            kill?.WaitForExit(1000);
        }
        catch
        {
        }
    }

    public void SendInterruptSignal(Process process)
    {
        if (process.HasExited) return;

        try
        {
            using var kill = Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-INT {process.Id}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            kill?.WaitForExit(500);
        }
        catch
        {
        }
    }

    public string GetPythonVenvExecutable(string venvDir)
    {
        return Path.Combine(venvDir, "bin", "python3");
    }
}

#endif