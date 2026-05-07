using System.Diagnostics;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.AudioCapture;

internal class RecordingProcess
{
    private readonly FfmpegWrapper _ffmpeg;
    private readonly RecordingOptions _opt;

    public RecordingProcess(RecordingOptions options)
    {
        _opt = options;
        _ffmpeg = new FfmpegWrapper(
            _opt.PythonPath, _opt.CacheRoot, _opt.Fs);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        LogRedirector.Info("PowerWordRelive.AudioCapture", "Recording process started",
            new
            {
                monitor = _ffmpeg.Monitor, outputDir = _opt.OutputDir,
                silenceTimeoutMs = _opt.SilenceTimeoutMs,
                maxSegmentSec = _opt.MaxSegmentSec,
                noSpeechTimeoutSec = _opt.NoSpeechTimeoutSec,
                minSpeechMs = _opt.MinSpeechMs
            });

        var (ffmpegProcess, pythonProcess) = _ffmpeg.Launch(
            _opt.PythonScriptPath, _opt.OutputDir,
            _opt.SilenceTimeoutMs, _opt.MaxSegmentSec,
            _opt.NoSpeechTimeoutSec, _opt.MinSpeechMs);

        var stderrTask = ReadStderrAsync(ffmpegProcess, pythonProcess, ct);

        try
        {
            using var reader = pythonProcess.StandardOutput;
            string? line;
            while ((line = await ReadLineWithCancellationAsync(reader, ct)) != null)
                if (line.StartsWith("SEGMENT_COMPLETE "))
                {
                    var tempFile = line["SEGMENT_COMPLETE ".Length..];
                    if (_opt.Fs.FileExists(tempFile))
                    {
                        var info = new FileInfo(tempFile);
                        LogRedirector.Info("PowerWordRelive.AudioCapture", "Segment completed",
                            new { file = tempFile, sizeBytes = info.Length });
                        await _opt.SegmentHandler.HandleSegmentAsync(tempFile, DateTime.UtcNow, ct);
                    }
                }
                else if (line.StartsWith("SEGMENT_TOO_SHORT "))
                {
                    var path = line["SEGMENT_TOO_SHORT ".Length..];
                    LogRedirector.Debug("PowerWordRelive.AudioCapture",
                        "Segment discarded (too short)", new { file = path });
                    TryDelete(path);
                }
                else if (line == "SILENCE_TIMEOUT")
                {
                    LogRedirector.Debug("PowerWordRelive.AudioCapture", "Silence timeout");
                }

            LogRedirector.Warn("PowerWordRelive.AudioCapture", "Python stdout closed - exiting");
        }
        finally
        {
            TeardownFfmpeg(ffmpegProcess);
            await TeardownPython(pythonProcess, ct);
            await stderrTask;
            LogRedirector.Info("PowerWordRelive.AudioCapture", "Child processes reclaimed");
        }

        LogRedirector.Info("PowerWordRelive.AudioCapture", "Recording process stopped");
    }

    private void TeardownFfmpeg(Process process)
    {
        var pid = process.Id;
        try
        {
            if (process.HasExited)
            {
                LogRedirector.Info("PowerWordRelive.AudioCapture",
                    "ffmpeg already exited before teardown",
                    new { pid, exitCode = process.ExitCode });
                return;
            }

            LogRedirector.Info("PowerWordRelive.AudioCapture",
                "Sending SIGINT to ffmpeg", new { pid });

            using var sigint = Process.Start(new ProcessStartInfo
            {
                FileName = "kill",
                Arguments = $"-INT {pid}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            sigint?.WaitForExit(500);

            if (process.HasExited)
            {
                LogRedirector.Info("PowerWordRelive.AudioCapture",
                    "ffmpeg exited after SIGINT", new { pid });
                return;
            }

            LogRedirector.Warn("PowerWordRelive.AudioCapture",
                "ffmpeg did not exit after SIGINT, force killing", new { pid });
            process.Kill(true);
            process.WaitForExit(1000);

            if (process.HasExited)
                LogRedirector.Info("PowerWordRelive.AudioCapture",
                    "ffmpeg killed", new { pid });
            else
                LogRedirector.Warn("PowerWordRelive.AudioCapture",
                    "ffmpeg still alive after kill", new { pid });
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.AudioCapture",
                "Error tearing down ffmpeg", new { pid, error = ex.Message });
        }
    }

    private async Task TeardownPython(Process process, CancellationToken ct)
    {
        var pid = process.Id;
        try
        {
            if (process.HasExited)
            {
                LogRedirector.Info("PowerWordRelive.AudioCapture",
                    "Python VAD already exited before teardown",
                    new { pid, exitCode = process.ExitCode });
                return;
            }

            LogRedirector.Info("PowerWordRelive.AudioCapture",
                "Waiting for Python VAD to exit", new { pid });

            await Task.WhenAny(process.WaitForExitAsync(CancellationToken.None), Task.Delay(2000, ct));

            if (process.HasExited)
            {
                LogRedirector.Info("PowerWordRelive.AudioCapture",
                    "Python VAD exited gracefully", new { pid });
                return;
            }

            LogRedirector.Warn("PowerWordRelive.AudioCapture",
                "Python VAD did not exit, force killing", new { pid });
            process.Kill(true);

            await Task.WhenAny(process.WaitForExitAsync(CancellationToken.None), Task.Delay(2000, ct));

            if (process.HasExited)
                LogRedirector.Info("PowerWordRelive.AudioCapture",
                    "Python VAD killed", new { pid });
            else
                LogRedirector.Warn("PowerWordRelive.AudioCapture",
                    "Python VAD still alive after kill", new { pid });
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.AudioCapture",
                "Error tearing down Python VAD", new { pid, error = ex.Message });
        }
    }

    private static async Task ReadStderrAsync(
        Process ffmpeg, Process python,
        CancellationToken ct)
    {
        async Task ReadStream(StreamReader reader, string source)
        {
            try
            {
                string? line;
                while ((line = await ReadLineWithCancellationAsync(reader, ct)) != null)
                    if (!string.IsNullOrWhiteSpace(line))
                        LogRedirector.Warn(source, line);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
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

    private void TryDelete(string path)
    {
        try
        {
            if (_opt.Fs.FileExists(path))
                _opt.Fs.DeleteFile(path);
        }
        catch
        {
        }
    }
}