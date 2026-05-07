using System.Runtime.InteropServices;
using PowerWordRelive.Infrastructure.Configuration;
using PowerWordRelive.Infrastructure.Logging;
using PowerWordRelive.Infrastructure.Storage;
using PowerWordRelive.TranscriptionStore;

var fs = new LocalFileSystem();

var config = ChildConfigReader.ReadConfig();
var tsConfig = config.GetValueOrDefault("transcription_store", new Dictionary<string, string>());
var storageConfig = config.GetValueOrDefault("storage", new Dictionary<string, string>());
var generalConfig = config.GetValueOrDefault("general", new Dictionary<string, string>());

var workRoot = generalConfig.GetValueOrDefault("work_root", "");
var inputDir = tsConfig.GetValueOrDefault("input_dir", "./transcriptions");
var sqlitePath = storageConfig.GetValueOrDefault("sqlite_path", "");
int.TryParse(tsConfig.GetValueOrDefault("poll_interval_sec", "1"), out var pollIntervalSec);

if (string.IsNullOrEmpty(sqlitePath))
{
    LogRedirector.Error("PowerWordRelive.TranscriptionStore",
        "storage.sqlite_path is required but not configured");
    return 1;
}

if (!string.IsNullOrEmpty(workRoot) && Path.IsPathRooted(workRoot))
{
    inputDir = Path.GetFullPath(Path.Combine(workRoot, inputDir));
    sqlitePath = Path.GetFullPath(Path.Combine(workRoot, sqlitePath));
}
else
{
    if (!Path.IsPathRooted(inputDir))
        inputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, inputDir));
    if (!Path.IsPathRooted(sqlitePath))
        sqlitePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, sqlitePath));
}

fs.CreateDirectory(inputDir);

var dbDir = Path.GetDirectoryName(sqlitePath);
if (!string.IsNullOrEmpty(dbDir))
    fs.CreateDirectory(dbDir);

LogRedirector.Info("PowerWordRelive.TranscriptionStore", "TranscriptionStore starting",
    new { inputDir, sqlitePath, pollIntervalSec });

var process = new TranscriptionStoreProcess(new TranscriptionStoreOptions(
    inputDir, sqlitePath, pollIntervalSec, fs));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    LogRedirector.Info("PowerWordRelive.TranscriptionStore", "Shutting down...");
    cts.Cancel();
};

PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ =>
{
    LogRedirector.Info("PowerWordRelive.TranscriptionStore", "Shutting down...");
    cts.Cancel();
});

try
{
    await process.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
}

LogRedirector.Info("PowerWordRelive.TranscriptionStore", "TranscriptionStore stopped");
return 0;