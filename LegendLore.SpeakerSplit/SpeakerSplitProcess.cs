using System.Diagnostics;
using System.Text.Json;
using LegendLore.Infrastructure.Logging;
using LegendLore.Infrastructure.Storage;

namespace LegendLore.SpeakerSplit;

public class SpeakerSplitProcess
{
    private const string ProcessingExtension = ".processing";

    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly string _embeddingsDir;
    private readonly string _pythonScriptPath;
    private readonly string _pythonPath;
    private readonly string _cacheRoot;
    private readonly string _hfToken;
    private readonly string _device;
    private readonly float _matchThreshold;
    private readonly int _pollIntervalSec;
    private readonly IFileSystem _fs;

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
        _pollIntervalSec = pollIntervalSec;
        _fs = fs;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingFilesAsync(ct);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                LogRedirector.Error("LegendLore.SpeakerSplit",
                    "Error processing files", new { error = ex.Message });
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSec), ct);
            }
            catch (OperationCanceledException) { }
        }
    }

    private async Task ProcessPendingFilesAsync(CancellationToken ct)
    {
        var files = _fs.GetFiles(_inputDir, "*.wav")
            .Where(f => !f.EndsWith(ProcessingExtension))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested)
                break;
            await ProcessFileAsync(file, ct);
        }
    }

    private async Task ProcessFileAsync(string wavPath, CancellationToken ct)
    {
        var processingPath = wavPath + ProcessingExtension;
        var fileName = Path.GetFileName(wavPath);

        try
        {
            _fs.MoveFile(wavPath, processingPath);
        }
        catch (Exception ex)
        {
            LogRedirector.Warn("LegendLore.SpeakerSplit",
                "Failed to mark file as processing", new { file = fileName, error = ex.Message });
            return;
        }

        LogRedirector.Info("LegendLore.SpeakerSplit",
            "Processing file", new { file = fileName });

        try
        {
            var result = await RunDiarizationAsync(processingPath, ct);

            var segments = JsonSerializer.Deserialize<List<JsonElement>>(result);
            if (segments is not null && segments.Count > 0)
            {
                var speakers = segments
                    .Select(s => s.GetProperty("speaker").GetString())
                    .Distinct();
                LogRedirector.Info("LegendLore.SpeakerSplit",
                    "File processed", new
                    {
                        input = fileName,
                        speakers = speakers.ToList(),
                        segments = segments.Count
                    });
            }

            _fs.DeleteFile(processingPath);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            LogRedirector.Error("LegendLore.SpeakerSplit",
                "Failed to process file", new { file = fileName, error = ex.Message });

            try
            {
                if (_fs.FileExists(processingPath))
                    _fs.MoveFile(processingPath, wavPath);
            }
            catch { }
        }
    }

    private async Task<string> RunDiarizationAsync(string wavPath, CancellationToken ct)
    {
        var torchHome = Path.Combine(_cacheRoot, "torch");
        var hfHome = Path.Combine(_cacheRoot, "huggingface");

        _fs.CreateDirectory(torchHome);
        _fs.CreateDirectory(hfHome);

        var arguments = $"\"{_pythonScriptPath}\" " +
                        $"--input \"{wavPath}\" " +
                        $"--output-dir \"{_outputDir}\" " +
                        $"--embeddings-dir \"{_embeddingsDir}\" " +
                        $"--hf-token \"{_hfToken}\" " +
                        $"--match-threshold {_matchThreshold} " +
                        $"--device \"{_device}\"";

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["TORCH_HOME"] = torchHome;
        psi.Environment["HF_HOME"] = hfHome;
        if (_device == "cpu")
            psi.Environment["CUDA_VISIBLE_DEVICES"] = "";

        using var proc = Process.Start(psi)!;

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = proc.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(2000, CancellationToken.None));
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (proc.ExitCode != 0)
            throw new Exception($"Python exited with code {proc.ExitCode}: {stderr.TrimEnd()}");

        return stdout.Trim();
    }
}
