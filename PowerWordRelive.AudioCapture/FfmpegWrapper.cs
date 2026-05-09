using System.Diagnostics;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.AudioCapture;

internal class FfmpegWrapper
{
    private readonly IAudioCaptureDevice _device;
    private readonly IFileSystem _fs;
    private readonly string _pythonPath;
    private readonly int _sampleRate;
    private readonly string _torchHome;

    public FfmpegWrapper(string pythonPath, string cacheRoot, IFileSystem fs,
        IAudioCaptureDevice device, string? windowsAudioDevice = null, int sampleRate = 16000)
    {
        _pythonPath = pythonPath;
        _device = device;
        Monitor = device.BuildFfmpegInputArgs(windowsAudioDevice);
        _sampleRate = sampleRate;
        _torchHome = Path.Combine(cacheRoot, "torch");
        _fs = fs;
    }

    public string Monitor { get; }

    public (Process Ffmpeg, Process Python) Launch(
        string pythonScriptPath, string outputDir,
        int silenceTimeoutMs, int maxSegmentSec, int noSpeechTimeoutSec, int minSpeechMs)
    {
        var ffmpegArgs = $"-hide_banner -nostats -loglevel error " +
                         $"{Monitor} -ac 1 -ar {_sampleRate} " +
                         $"-f s16le pipe:1";

        var ffmpegPsi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = ffmpegArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var pythonArgs = $"{pythonScriptPath} " +
                         $"--output-dir \"{outputDir}\" " +
                         $"--silence-ms {silenceTimeoutMs} " +
                         $"--max-sec {maxSegmentSec} " +
                         $"--min-speech-ms {minSpeechMs} " +
                         $"--no-speech-timeout {noSpeechTimeoutSec}";

        var pythonPsi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = pythonArgs,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _fs.CreateDirectory(_torchHome);

        pythonPsi.Environment["TORCH_HOME"] = _torchHome;

        var ffmpegProcess = Process.Start(ffmpegPsi)!;
        var pythonProcess = Process.Start(pythonPsi)!;

        _ = Task.Run(async () =>
        {
            try
            {
                await ffmpegProcess.StandardOutput.BaseStream.CopyToAsync(
                    pythonProcess.StandardInput.BaseStream);
            }
            catch
            {
            }
            finally
            {
                pythonProcess.StandardInput.Close();
            }
        });

        return (ffmpegProcess, pythonProcess);
    }
}
