using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.TranscriptionStore.Database;
using PowerWordRelive.TranscriptionStore.Models;
using PowerWordRelive.TranscriptionStore.Subtitles;

namespace PowerWordRelive.TranscriptionStore;

internal class TranscriptionStoreProcess
{
    private const string ProcessingExtension = ".processing";
    private readonly TranscriptionStoreOptions _opt;

    public TranscriptionStoreProcess(TranscriptionStoreOptions options)
    {
        _opt = options;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        LogRedirector.Info("PowerWordRelive.TranscriptionStore", "Database initialization starting",
            new { dbPath = _opt.SqlitePath });

        using var db = new TranscriptionDatabase(_opt.SqlitePath);

        LogRedirector.Info("PowerWordRelive.TranscriptionStore", "Database ready, entering scan loop",
            new { inputDir = _opt.InputDir, pollIntervalSec = _opt.PollIntervalSec });

        while (!ct.IsCancellationRequested)
        {
            try
            {
                ProcessPendingFiles(db);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRedirector.Error("PowerWordRelive.TranscriptionStore",
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

    private void ProcessPendingFiles(TranscriptionDatabase db)
    {
        var files = _opt.Fs.GetFiles(_opt.InputDir, "*.srt")
            .Where(f => !f.EndsWith(ProcessingExtension))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files) ProcessFile(file, db);
    }

    private void ProcessFile(string srtPath, TranscriptionDatabase db)
    {
        var fileName = Path.GetFileName(srtPath);

        if (!_opt.Fs.TryAcquireForProcessing(srtPath, out var processingPath))
        {
            LogRedirector.Warn("PowerWordRelive.TranscriptionStore",
                "Failed to acquire file for processing", new { file = fileName });
            return;
        }

        LogRedirector.Info("PowerWordRelive.TranscriptionStore",
            "Indexing file", new { file = fileName });

        try
        {
            var lines = _opt.Fs.ReadAllLines(processingPath);
            var parsed = FilenameParser.Parse(fileName);
            var subtitles = SrtParser.Parse(lines);

            var entries = new List<TranscriptionEntry>(subtitles.Count);
            foreach (var sub in subtitles)
            {
                var startMs = parsed.WallClockMs + parsed.OffsetMs + sub.StartMs;
                var endMs = parsed.WallClockMs + parsed.OffsetMs + sub.EndMs;

                entries.Add(new TranscriptionEntry(startMs, endMs, parsed.SpeakerId, sub.Text, fileName));
            }

            db.Insert(entries);

            LogRedirector.Info("PowerWordRelive.TranscriptionStore",
                "File indexed", new { file = fileName, subtitles = entries.Count });

            _opt.Fs.TryCompleteProcessing(processingPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRedirector.Error("PowerWordRelive.TranscriptionStore",
                "Failed to index file", new { file = fileName, error = ex.Message });

            _opt.Fs.TryReleaseProcessing(processingPath, srtPath);
        }
    }
}