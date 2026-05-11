namespace PowerWordRelive.AudioCapture;

internal static class AudioCaptureDeviceFactory
{
    public static IAudioCaptureDevice Create()
    {
#if WINDOWS
        return new WindowsLoopbackCapture();
#elif LINUX
        return new PulseAudioCaptureDevice();
#else
        throw new PlatformNotSupportedException("Unsupported operating system");
#endif
    }
}
