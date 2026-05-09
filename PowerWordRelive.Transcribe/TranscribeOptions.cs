using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.Transcribe;

internal record TranscribeOptions(
    string InputDir,
    string OutputDir,
    string PythonScriptPath,
    string PythonPath,
    string CacheRoot,
    string Model,
    string Device,
    int PollIntervalSec,
    IFileSystem Fs,
    string? ModelscopeToken = null
);