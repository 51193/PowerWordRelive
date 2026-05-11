#if LINUX

using System.Diagnostics;
using System.Runtime.InteropServices;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.AudioCapture;

internal sealed class PulseAudioCaptureDevice : IAudioCaptureDevice
{
    private const int SampleRate = 16000;
    private const int BufferBytes = 4096;

    private IntPtr _handle;
    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public Task StartAsync(Stream pcmOut, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var sink = DetectPulseAudioSink();
        var monitorSource = $"{sink}.monitor";

        var ss = new PaSampleSpec
        {
            Format = PaSampleFormat.S16LE,
            Rate = SampleRate,
            Channels = 1
        };

        var error = PaErrorCode.Ok;
        _handle = PulseSimple.pa_simple_new(
            null, "PowerWordRelive", PaStreamDirection.Record, monitorSource,
            "audio capture", ref ss, IntPtr.Zero, IntPtr.Zero, ref error);

        if (_handle == IntPtr.Zero)
        {
            var msg = Marshal.PtrToStringAnsi(PulseSimple.pa_strerror((int)error)) ?? error.ToString();
            throw new InvalidOperationException(
                $"pa_simple_new failed for '{monitorSource}': {msg}");
        }

        LogRedirector.Info("PowerWordRelive.AudioCapture",
            "PulseAudio capture started", new { monitorSource });

        _captureTask = Task.Run(() => CaptureLoop(pcmOut, _cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private void CaptureLoop(Stream pcmOut, CancellationToken ct)
    {
        var buffer = new byte[BufferBytes];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var error = PaErrorCode.Ok;
                var ret = PulseSimple.pa_simple_read(
                    _handle, buffer, BufferBytes, ref error);

                if (ret < 0)
                {
                    var msg = Marshal.PtrToStringAnsi(
                        PulseSimple.pa_strerror((int)error)) ?? error.ToString();
                    LogRedirector.Warn("PowerWordRelive.AudioCapture",
                        "pa_simple_read failed", new { error = msg });
                    break;
                }

                pcmOut.Write(buffer, 0, BufferBytes);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();

        if (_handle != IntPtr.Zero)
        {
            PulseSimple.pa_simple_free(_handle);
            _handle = IntPtr.Zero;
        }

        try
        {
            _captureTask?.Wait(2000);
        }
        catch (AggregateException)
        {
        }

        _cts?.Dispose();
    }

    private static string DetectPulseAudioSink()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pactl",
            Arguments = "get-default-sink",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        var sink = process.StandardOutput.ReadToEnd().Trim();
        if (!string.IsNullOrEmpty(sink))
            return sink;

        return "default";
    }
}

#endif
