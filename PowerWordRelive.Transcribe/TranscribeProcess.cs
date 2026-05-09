using System.Diagnostics;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Timing;

namespace PowerWordRelive.Transcribe;

internal class TranscribeProcess
{
    private const string ProcessingExtension = ".processing";
    private readonly CumulativeTimer _cumulativeTimer = new();
    private readonly TranscribeOptions _opt;

    public TranscribeProcess(TranscribeOptions options)
    {
        _opt = options;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _cumulativeTimer.Start();

        Process? pythonProc = null;
        StreamWriter? pythonStdin = null;
        StreamReader? pythonStdout = null;

        try
        {
            pythonProc = LaunchPython();
            pythonStdin = pythonProc.StandardInput;
            pythonStdout = pythonProc.StandardOutput;

            _ = ReadStderrAsync(pythonProc.StandardError);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingFilesAsync(pythonStdin, pythonStdout, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogRedirector.Error("PowerWordRelive.Transcribe",
                        "Error processing files", new { error = ex.Message });
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_opt.PollIntervalSec), ct);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            TeardownPython(pythonProc, pythonStdin);
            LogSummary();
        }
    }

    private Process LaunchPython()
    {
        var torchHome = Path.Combine(_opt.CacheRoot, "torch");
        var hfHome = Path.Combine(_opt.CacheRoot, "huggingface");
        var modelscopeHome = Path.Combine(_opt.CacheRoot, "modelscope");

        _opt.Fs.CreateDirectory(torchHome);
        _opt.Fs.CreateDirectory(hfHome);
        _opt.Fs.CreateDirectory(modelscopeHome);

        var arguments = $"\"{_opt.PythonScriptPath}\" " +
                        $"--output-dir \"{_opt.OutputDir}\" " +
                        $"--model \"{_opt.Model}\" " +
                        $"--device \"{_opt.Device}\"";
        if (!string.IsNullOrEmpty(_opt.ModelscopeToken))
            arguments += $" --ms-token \"{_opt.ModelscopeToken}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _opt.PythonPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["TORCH_HOME"] = torchHome;
        psi.Environment["HF_HOME"] = hfHome;
        psi.Environment["MODELSCOPE_CACHE"] = modelscopeHome;
        if (!string.IsNullOrEmpty(_opt.ModelscopeToken))
            psi.Environment["MODELSCOPE_API_TOKEN"] = _opt.ModelscopeToken;

        return Process.Start(psi)!;
    }

    private async Task ProcessPendingFilesAsync(
        StreamWriter stdin, StreamReader stdout, CancellationToken ct)
    {
        var files = _opt.Fs.GetFiles(_opt.InputDir, "*.wav")
            .Where(f => !f.EndsWith(ProcessingExtension))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested)
                break;
            await ProcessFileAsync(file, stdin, stdout, ct);
        }
    }

    private async Task ProcessFileAsync(
        string wavPath, StreamWriter stdin, StreamReader stdout, CancellationToken ct)
    {
        var fileName = Path.GetFileName(wavPath);

        if (!_opt.Fs.TryAcquireForProcessing(wavPath, out var processingPath))
        {
            LogRedirector.Warn("PowerWordRelive.Transcribe",
                "Failed to acquire file for processing", new { file = fileName });
            return;
        }

        LogRedirector.Info("PowerWordRelive.Transcribe",
            "Transcribing file", new { file = fileName });

        try
        {
            var request = JsonSerializer.Serialize(new { input = processingPath });

            await stdin.WriteLineAsync(request.AsMemory(), CancellationToken.None);
            await stdin.FlushAsync(CancellationToken.None);

            var response = await ReadLineAsync(stdout, ct);
            if (string.IsNullOrEmpty(response))
                throw new Exception("Python server closed stdout unexpectedly");

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
                throw new Exception(err.GetString() ?? "unknown error");

            var timing = root.TryGetProperty("audio_duration_s", out _)
                ? TimingParser.FromJson(root)
                : null;
            _cumulativeTimer.Record(timing?.AudioDurationS ?? 0, timing?.ElapsedS ?? 0, 1);
            var cumulative = _cumulativeTimer.Snapshot();

            LogRedirector.Info("PowerWordRelive.Transcribe",
                "File transcribed", new
                {
                    file = fileName,
                    timing = TimingParser.ToLogData(timing),
                    cumulative = new
                    {
                        files = cumulative.TotalFiles,
                        total_audio_s = cumulative.TotalAudioDurationS,
                        total_elapsed_s = cumulative.TotalElapsedS,
                        speed = cumulative.Speed
                    }
                });

            _opt.Fs.TryCompleteProcessing(processingPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRedirector.Error("PowerWordRelive.Transcribe",
                "Failed to transcribe file", new { file = fileName, error = ex.Message });

            _opt.Fs.TryReleaseProcessing(processingPath, wavPath);
        }
    }

    private void LogSummary()
    {
        var s = _cumulativeTimer.Snapshot();
        LogRedirector.Info("PowerWordRelive.Transcribe", "Session summary", new
        {
            files = s.TotalFiles,
            total_audio_s = s.TotalAudioDurationS,
            total_elapsed_s = s.TotalElapsedS,
            speed = s.Speed
        });
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken ct)
    {
        var readTask = reader.ReadLineAsync(CancellationToken.None).AsTask();
        var cancelTask = Task.Delay(Timeout.Infinite, ct);
        var completed = await Task.WhenAny(readTask, cancelTask);
        if (completed == cancelTask)
            return null;
        return await readTask;
    }

    private static async Task ReadStderrAsync(StreamReader stderr)
    {
        try
        {
            string? line;
            while ((line = await stderr.ReadLineAsync()) != null)
                if (!string.IsNullOrWhiteSpace(line))
                    LogRedirector.Warn("python3", line);
        }
        catch
        {
        }
    }

    private void TeardownPython(Process? proc, StreamWriter? stdin)
    {
        try
        {
            stdin?.Close();
        }
        catch
        {
        }

        if (proc is null)
            return;

        var pid = proc.Id;

        try
        {
            if (proc.HasExited)
            {
                LogRedirector.Info("PowerWordRelive.Transcribe",
                    "Python transcription server already exited before teardown",
                    new { pid, exitCode = proc.ExitCode });
                proc.Dispose();
                return;
            }

            LogRedirector.Info("PowerWordRelive.Transcribe",
                "Waiting for Python transcription server to exit", new { pid });

            proc.WaitForExit(5000);

            if (proc.HasExited)
            {
                LogRedirector.Info("PowerWordRelive.Transcribe",
                    "Python transcription server exited gracefully", new { pid });
                proc.Dispose();
                return;
            }

            LogRedirector.Warn("PowerWordRelive.Transcribe",
                "Python transcription server did not exit, force killing", new { pid });

            proc.Kill(true);
            proc.WaitForExit(2000);

            if (proc.HasExited)
                LogRedirector.Info("PowerWordRelive.Transcribe",
                    "Python transcription server killed", new { pid });
            else
                LogRedirector.Warn("PowerWordRelive.Transcribe",
                    "Python transcription server still alive after kill", new { pid });
        }
        catch (Exception ex)
        {
            LogRedirector.Error("PowerWordRelive.Transcribe",
                "Error tearing down Python transcription server", new { pid, error = ex.Message });
        }

        proc.Dispose();
    }
}