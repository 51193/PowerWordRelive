using System.Diagnostics;
using LegendLore.Infrastructure.Storage;

namespace LegendLore.AudioCapture;

public class FfmpegWrapper
{
    private readonly string _pythonPath;
    private readonly string _monitor;
    private readonly int _sampleRate;
    private readonly string _torchHome;
    private readonly IFileSystem _fs;

    public FfmpegWrapper(string pythonPath, string cacheRoot, IFileSystem fs, int sampleRate = 16000)
    {
        _pythonPath = pythonPath;
        _monitor = DetectMonitor();
        _sampleRate = sampleRate;
        _torchHome = Path.Combine(cacheRoot, "torch");
        _fs = fs;
    }

    public string Monitor => _monitor;

    public (System.Diagnostics.Process Ffmpeg, System.Diagnostics.Process Python) Launch(
        string pythonScriptPath, string outputDir,
        int silenceTimeoutMs, int maxSegmentSec, int noSpeechTimeoutSec, int minSpeechMs)
    {
        var ffmpegArgs = $"-hide_banner -nostats -loglevel error " +
                         $"-f pulse -i {_monitor} -ac 1 -ar {_sampleRate} " +
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

        var ffmpegProcess = System.Diagnostics.Process.Start(ffmpegPsi)!;
        var pythonProcess = System.Diagnostics.Process.Start(pythonPsi)!;

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
        using var process = System.Diagnostics.Process.Start(psi)!;
        var sink = process.StandardOutput.ReadToEnd().Trim();
        if (!string.IsNullOrEmpty(sink))
            return $"{sink}.monitor";

        return "default";
    }
}
