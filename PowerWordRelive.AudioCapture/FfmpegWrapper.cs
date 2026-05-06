using System.Diagnostics;
using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.AudioCapture;

internal class FfmpegWrapper
{
    private readonly IFileSystem _fs;
    private readonly string _pythonPath;
    private readonly int _sampleRate;
    private readonly string _torchHome;

    public FfmpegWrapper(string pythonPath, string cacheRoot, IFileSystem fs, int sampleRate = 16000)
    {
        _pythonPath = pythonPath;
        Monitor = DetectMonitor();
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
                         $"-f pulse -i {Monitor} -ac 1 -ar {_sampleRate} " +
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

        pythonPsi.Environment["TORCH_HOME"] = _torchHome;
        _fs.CreateDirectory(_torchHome);

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
                // pipe broken when ffmpeg killed, expected
            }
            finally
            {
                pythonProcess.StandardInput.Close();
            }
        });

        return (ffmpegProcess, pythonProcess);
    }

    private static string DetectMonitor()
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
            return $"{sink}.monitor";

        return "default";
    }
}