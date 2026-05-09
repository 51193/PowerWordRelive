using System.Diagnostics;

namespace PowerWordRelive.Infrastructure.Platform;

public interface IPlatformServices
{
    IDisposable? RegisterShutdownSignal(Action handler);
    void SendTermSignal(Process process);
    void SendInterruptSignal(Process process);
    string GetPythonVenvExecutable(string venvDir);
}