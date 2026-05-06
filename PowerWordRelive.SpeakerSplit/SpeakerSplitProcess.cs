using System.Diagnostics;
using System.Text.Json;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.Infrastructure.Timing;

namespace PowerWordRelive.SpeakerSplit;

public class SpeakerSplitProcess
{
    private const string ProcessingExtension = ".processing";
    private readonly string _cacheRoot;
    private readonly CumulativeTimer _cumulativeTimer = new();
    private readonly string _device;
    private readonly int _embBatchSize;
    private readonly string _embeddingsDir;
    private readonly IFileSystem _fs;
    private readonly string _hfToken;

    private readonly string _inputDir;
    private readonly float _matchThreshold;
    private readonly int _ompNumThreads;
    private readonly string _outputDir;
    private readonly int _pollIntervalSec;
    private readonly string _pythonPath;
    private readonly string _pythonScriptPath;
    private readonly int _segBatchSize;

    public SpeakerSplitProcess(
        string inputDir,
        string outputDir,
        string embeddingsDir,
        string pythonScriptPath,
        string pythonPath,
        string cacheRoot,
        string hfToken,
        string device,
        float matchThreshold,
        int ompNumThreads,
        int segBatchSize,
        int embBatchSize,
        int pollIntervalSec,
        IFileSystem fs)
    {
        _inputDir = inputDir;
        _outputDir = outputDir;
        _embeddingsDir = embeddingsDir;
        _pythonScriptPath = pythonScriptPath;
        _pythonPath = pythonPath;
        _cacheRoot = cacheRoot;
        _hfToken = hfToken;
        _device = device;
        _matchThreshold = matchThreshold;
        _ompNumThreads = ompNumThreads;
        _segBatchSize = segBatchSize;
        _embBatchSize = embBatchSize;
        _pollIntervalSec = pollIntervalSec;
        _fs = fs;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _cumulativeTimer.Start();

        Process? pythonProc = null;
        StreamWriter? pythonStdin = null;
        StreamReader? pythonStdout = null;
        StreamReader? pythonStderr = null;

        try
        {
            pythonProc = LaunchPython();
            pythonStdin = pythonProc.StandardInput;
            pythonStdout = pythonProc.StandardOutput;
            pythonStderr = pythonProc.StandardError;

            _ = ReadStderrAsync(pythonStderr);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingFilesAsync(pythonStdin, pythonStdout, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogRedirector.Error("PowerWordRelive.SpeakerSplit",
                        "Error processing files", new { error = ex.Message });
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSec), ct);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            try
            {
                pythonStdin?.Close();
            }
            catch
            {
            }

            if (pythonProc is not null && !pythonProc.HasExited)
            {
                try
                {
                    pythonProc.WaitForExit(5000);
                }
                catch
                {
                }

                if (!pythonProc.HasExited)
                {
                    try
                    {
                        pythonProc.Kill(true);
                    }
                    catch
                    {
                    }

                    try
                    {
                        pythonProc.WaitForExit(2000);
                    }
                    catch
                    {
                    }
                }
            }

            pythonProc?.Dispose();

            var s = _cumulativeTimer.Snapshot();
            LogRedirector.Info("PowerWordRelive.SpeakerSplit", "Session summary", new
            {
                files = s.TotalFiles,
                segments = s.TotalSegments,
                total_audio_s = s.TotalAudioDurationS,
                total_elapsed_s = s.TotalElapsedS,
                speed = s.Speed
            });
        }
    }

    private Process LaunchPython()
    {
        var torchHome = Path.Combine(_cacheRoot, "torch");
        var hfHome = Path.Combine(_cacheRoot, "huggingface");

        _fs.CreateDirectory(torchHome);
        _fs.CreateDirectory(hfHome);

        var arguments = $"\"{_pythonScriptPath}\" " +
                        $"--output-dir \"{_outputDir}\" " +
                        $"--embeddings-dir \"{_embeddingsDir}\" " +
                        $"--hf-token \"{_hfToken}\" " +
                        $"--segmentation-batch-size {_segBatchSize} " +
                        $"--embedding-batch-size {_embBatchSize} " +
                        $"--device \"{_device}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["TORCH_HOME"] = torchHome;
        psi.Environment["HF_HOME"] = hfHome;
        psi.Environment["OMP_NUM_THREADS"] = _ompNumThreads.ToString();
        psi.Environment["MKL_NUM_THREADS"] = _ompNumThreads.ToString();
        if (_device == "cpu")
            psi.Environment["CUDA_VISIBLE_DEVICES"] = "";

        return Process.Start(psi)!;
    }

    private async Task ProcessPendingFilesAsync(
        StreamWriter stdin, StreamReader stdout, CancellationToken ct)
    {
        var files = _fs.GetFiles(_inputDir, "*.wav")
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
        var processingPath = wavPath + ProcessingExtension;
        var fileName = Path.GetFileName(wavPath);

        try
        {
            _fs.MoveFile(wavPath, processingPath);
        }
        catch (Exception ex)
        {
            LogRedirector.Warn("PowerWordRelive.SpeakerSplit",
                "Failed to mark file as processing", new { file = fileName, error = ex.Message });
            return;
        }

        LogRedirector.Info("PowerWordRelive.SpeakerSplit",
            "Processing file", new { file = fileName });

        try
        {
            var request = JsonSerializer.Serialize(new
            {
                input = processingPath,
                match_threshold = _matchThreshold
            });

            await stdin.WriteLineAsync(request.AsMemory(), CancellationToken.None);
            await stdin.FlushAsync(CancellationToken.None);

            var response = await ReadLineAsync(stdout, ct);
            if (string.IsNullOrEmpty(response))
                throw new Exception("Python server closed stdout unexpectedly");

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
                throw new Exception(err.GetString() ?? "unknown error");

            if (root.TryGetProperty("segments", out var segsElement) &&
                segsElement.GetArrayLength() > 0)
            {
                var speakerSet = new HashSet<string?>();
                foreach (var seg in segsElement.EnumerateArray())
                    speakerSet.Add(seg.GetProperty("speaker").GetString());

                var timing = root.TryGetProperty("timing", out var t)
                    ? TimingHelper.FromJson(t)
                    : null;
                _cumulativeTimer.Record(timing?.AudioDurationS ?? 0, segsElement.GetArrayLength());
                var cumulative = _cumulativeTimer.Snapshot();

                LogRedirector.Info("PowerWordRelive.SpeakerSplit",
                    "File processed", new
                    {
                        input = fileName,
                        speakers = speakerSet.ToList(),
                        segments = segsElement.GetArrayLength(),
                        timing = TimingHelper.ToLogData(timing),
                        cumulative = new
                        {
                            files = cumulative.TotalFiles,
                            total_audio_s = cumulative.TotalAudioDurationS,
                            total_elapsed_s = cumulative.TotalElapsedS,
                            speed = cumulative.Speed
                        }
                    });
            }

            _fs.DeleteFile(processingPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRedirector.Error("PowerWordRelive.SpeakerSplit",
                "Failed to process file", new { file = fileName, error = ex.Message });

            try
            {
                if (_fs.FileExists(processingPath))
                    _fs.MoveFile(processingPath, wavPath);
            }
            catch
            {
            }
        }
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
}