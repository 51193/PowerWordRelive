namespace PowerWordRelive.Infrastructure.Platform;

public static class PlatformServicesFactory
{
    public static IPlatformServices Create()
    {
#if WINDOWS
        return new WindowsPlatformServices();
#elif LINUX
        return new LinuxPlatformServices();
#else
        throw new PlatformNotSupportedException("Unsupported operating system");
#endif
    }
}