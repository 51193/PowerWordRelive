#if WINDOWS
using NAudio.Wave;

namespace PowerWordRelive.AudioCapture;

internal sealed class WindowsLoopbackCapture : IAudioCaptureDevice
{
    private readonly int _targetSampleRate;
    private WasapiLoopbackCapture? _capture;
    private CancellationTokenSource? _cts;

    public WindowsLoopbackCapture(int targetSampleRate = 16000)
    {
        _targetSampleRate = targetSampleRate;
    }

    public Task StartAsync(Stream pcmOut, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _capture = new WasapiLoopbackCapture();

        var targetFormat = new WaveFormat(_targetSampleRate, 16, 1);

        var bufferedProvider = new BufferedWaveProvider(_capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(200)
        };

        var resampler = new MediaFoundationResampler(bufferedProvider, targetFormat)
        {
            ResamplerQuality = 60
        };

        _capture.DataAvailable += (_, e) =>
            bufferedProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

        _capture.StartRecording();

        _ = Task.Run(async () =>
        {
            var buffer = new byte[targetFormat.AverageBytesPerSecond / 10];
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var read = resampler.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                        await pcmOut.WriteAsync(buffer.AsMemory(0, read), _cts.Token);
                    else
                        await Task.Delay(10, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                resampler.Dispose();
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _capture?.StopRecording();
        _capture?.Dispose();
        _cts?.Dispose();
    }
}
#endif