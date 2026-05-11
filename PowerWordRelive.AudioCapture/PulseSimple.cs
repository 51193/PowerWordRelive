#if LINUX

using System.Runtime.InteropServices;

namespace PowerWordRelive.AudioCapture;

internal enum PaSampleFormat
{
    U8 = 0,
    ALaw = 1,
    ULaw = 2,
    S16LE = 3,
    S16BE = 4,
    Float32LE = 5,
    Float32BE = 6,
    S32LE = 7,
    S32BE = 8,
    S24LE = 9,
    S24BE = 10,
    S24_32LE = 11,
    S24_32BE = 12
}

internal enum PaStreamDirection
{
    NoDirection = 0,
    Playback = 1,
    Record = 2,
    Upload = 3
}

internal enum PaErrorCode
{
    Ok = 0,
    Access = 1,
    Command = 2,
    Invalid = 3,
    Exist = 4,
    NoEntity = 5,
    ConnectionRefused = 6,
    Protocol = 7,
    Timeout = 8,
    AuthKey = 9,
    Internal = 10,
    ConnectionTerminated = 11,
    Killed = 12,
    InvalidServer = 13,
    ModInitFailed = 14,
    BadState = 15,
    NoData = 16,
    Version = 17,
    TooLarge = 18,
    NotSupported = 19,
    Unknown = 20,
    NoExtension = 21,
    Obsolete = 22,
    NotImplemented = 23,
    Forked = 24,
    IO = 25,
    Busy = 26
}

[StructLayout(LayoutKind.Sequential)]
internal struct PaSampleSpec
{
    public PaSampleFormat Format;
    public uint Rate;
    public byte Channels;
}

internal static class PulseSimple
{
    private const string Library = "libpulse-simple.so.0";

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pa_simple_new(
        [MarshalAs(UnmanagedType.LPStr)] string? server,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        PaStreamDirection dir,
        [MarshalAs(UnmanagedType.LPStr)] string? dev,
        [MarshalAs(UnmanagedType.LPStr)] string streamName,
        ref PaSampleSpec ss,
        IntPtr map,
        IntPtr attr,
        ref PaErrorCode error);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void pa_simple_free(IntPtr s);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int pa_simple_read(
        IntPtr s,
        byte[] data,
        UIntPtr bytes,
        ref PaErrorCode error);

    [DllImport("libpulse.so.0", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr pa_strerror(int error);
}

#endif