using System.Diagnostics;
using PowerWordRelive.Infrastructure.Logging;

namespace PowerWordRelive.AudioCapture;

internal class RecordingProcess
{
    private readonly RecordingOptions _opt;

    public RecordingProcess(RecordingOptions options)
    {
        _opt = options;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        LogRedirector.Info("PowerWordRelive.AudioCapture", "Recording process started",
            new
            {
                outputDir = _opt.OutputDir,
                silenceTimeoutMs = _opt.SilenceTimeoutMs,
                maxSegmentSec = _opt.MaxSegmentSec,
                noSpeechTimeoutSec = _opt.NoSpeechTimeoutSec,
                minSpeechMs = _opt.MinSpeechMs
            });

        using var captureDevice = AudioCaptureDeviceFactory.Create();
        var pythonProcess = StartVadProcess();

        try
        {
            await captureDevice.StartAsync(pythonProcess.StandardInput.BaseStream, ct);
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.AudioCapture",
                "Failed to start audio capture", new { error = ex.Message });
            pythonProcess.Kill(true);
            return;
        }

        var stderrTask = ReadStderrAsync("python3", pythonProcess.StandardError, ct);

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
                        LogRedirector.Info("PowerWordRelive.AudioCapture", "Segment completed",
                            new { file = tempFile, sizeBytes = _opt.Fs.GetFileSize(tempFile) });
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
            captureDevice.Dispose();
            pythonProcess.StandardInput.Close();
            await TeardownPython(pythonProcess, ct);
            await stderrTask;
            LogRedirector.Info("PowerWordRelive.AudioCapture", "Child processes reclaimed");
        }

        LogRedirector.Info("PowerWordRelive.AudioCapture", "Recording process stopped");
    }

    private Process StartVadProcess()
    {
        var pythonArgs = $"\"{_opt.PythonScriptPath}\" " +
                         $"--output-dir \"{_opt.OutputDir}\" " +
                         $"--silence-ms {_opt.SilenceTimeoutMs} " +
                         $"--max-sec {_opt.MaxSegmentSec} " +
                         $"--min-speech-ms {_opt.MinSpeechMs} " +
                         $"--no-speech-timeout {_opt.NoSpeechTimeoutSec}";

        var psi = new ProcessStartInfo
        {
            FileName = _opt.PythonPath,
            Arguments = pythonArgs,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var torchHome = Path.Combine(_opt.CacheRoot, "torch");
        _opt.Fs.CreateDirectory(torchHome);
        psi.Environment["TORCH_HOME"] = torchHome;

        return Process.Start(psi)!;
    }

    private static async Task ReadStderrAsync(string source, StreamReader reader,
        CancellationToken ct)
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
