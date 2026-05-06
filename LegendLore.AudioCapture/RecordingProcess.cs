using System.Diagnostics;
using LegendLore.Infrastructure.Logging;

namespace LegendLore.AudioCapture;

public class RecordingProcess
{
    private readonly string _outputDir;
    private readonly string _pythonScriptPath;
    private readonly ISegmentHandler _segmentHandler;
    private readonly FfmpegWrapper _ffmpeg;
    private readonly int _silenceTimeoutMs;
    private readonly int _maxSegmentSec;
    private readonly int _noSpeechTimeoutSec;
    private readonly int _minSpeechMs;

    public RecordingProcess(
        string outputDir,
        string pythonScriptPath,
        string pythonPath,
        ISegmentHandler segmentHandler,
        int silenceTimeoutMs = 800,
        int maxSegmentSec = 120,
        int noSpeechTimeoutSec = 30,
        int minSpeechMs = 500)
    {
        _outputDir = outputDir;
        _pythonScriptPath = pythonScriptPath;
        _segmentHandler = segmentHandler;
        _silenceTimeoutMs = silenceTimeoutMs;
        _maxSegmentSec = maxSegmentSec;
        _noSpeechTimeoutSec = noSpeechTimeoutSec;
        _minSpeechMs = minSpeechMs;
        _ffmpeg = new FfmpegWrapper(pythonPath, 16000);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        LogRedirector.Info("LegendLore.AudioCapture", "Recording process started",
            new
            {
                monitor = _ffmpeg.Monitor, outputDir = _outputDir,
                silenceTimeoutMs = _silenceTimeoutMs,
                maxSegmentSec = _maxSegmentSec,
                noSpeechTimeoutSec = _noSpeechTimeoutSec,
                minSpeechMs = _minSpeechMs
            });

        var (ffmpegProcess, pythonProcess) = _ffmpeg.Launch(
            _pythonScriptPath, _outputDir,
            _silenceTimeoutMs, _maxSegmentSec, _noSpeechTimeoutSec, _minSpeechMs);

        var stderrTask = ReadStderrAsync(ffmpegProcess, pythonProcess, ct);

        try
        {
            using var reader = pythonProcess.StandardOutput;
            string? line;
            while ((line = await ReadLineWithCancellationAsync(reader, ct)) != null)
            {
                if (line.StartsWith("SEGMENT_COMPLETE "))
                {
                    var tempFile = line["SEGMENT_COMPLETE ".Length..];
                    if (File.Exists(tempFile))
                    {
                        var info = new FileInfo(tempFile);
                        LogRedirector.Info("LegendLore.AudioCapture", "Segment completed",
                            new { file = tempFile, sizeBytes = info.Length });
                        await _segmentHandler.HandleSegmentAsync(tempFile, DateTime.UtcNow, ct);
                    }
                }
                else if (line.StartsWith("SEGMENT_TOO_SHORT "))
                {
                    var path = line["SEGMENT_TOO_SHORT ".Length..];
                    LogRedirector.Debug("LegendLore.AudioCapture",
                        "Segment discarded (too short)", new { file = path });
                    TryDelete(path);
                }
                else if (line == "SILENCE_TIMEOUT")
                {
                    LogRedirector.Debug("LegendLore.AudioCapture", "Silence timeout");
                }
            }

            LogRedirector.Warn("LegendLore.AudioCapture", "Python stdout closed - exiting");
        }
        finally
        {
            StopProcess(ffmpegProcess);
            await Task.WhenAny(pythonProcess.WaitForExitAsync(ct), Task.Delay(2000, ct));
            if (!pythonProcess.HasExited)
                pythonProcess.Kill(true);
            await stderrTask;
        }

        LogRedirector.Info("LegendLore.AudioCapture", "Recording process stopped");
    }

    private static void StopProcess(System.Diagnostics.Process process)
    {
        try
        {
            if (process.HasExited)
                return;

            using var sigint = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-INT {process.Id}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            sigint?.WaitForExit(500);

            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit(1000);
            }
        }
        catch { }
    }

    private static async Task ReadStderrAsync(
        System.Diagnostics.Process ffmpeg, System.Diagnostics.Process python,
        CancellationToken ct)
    {
        async Task ReadStream(StreamReader reader, string source)
        {
            try
            {
                string? line;
                while ((line = await ReadLineWithCancellationAsync(reader, ct)) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        LogRedirector.Warn(source, line);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        await Task.WhenAll(
            ReadStream(ffmpeg.StandardError, "ffmpeg"),
            ReadStream(python.StandardError, "python3"));
    }

    private static async Task<string?> ReadLineWithCancellationAsync(
        StreamReader reader, CancellationToken ct)
    {
        var readTask = reader.ReadLineAsync();
        var cancelTask = Task.Delay(Timeout.Infinite, ct);
        var completed = await Task.WhenAny(readTask, cancelTask);
        if (completed == cancelTask)
            return null;

        return await readTask;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }
}
