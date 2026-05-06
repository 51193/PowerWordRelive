using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.SpeakerSplit;

internal record SpeakerSplitOptions(
    string InputDir,
    string OutputDir,
    string EmbeddingsDir,
    string PythonScriptPath,
    string PythonPath,
    string CacheRoot,
    string HfToken,
    string Device,
    float MatchThreshold,
    int OmpNumThreads,
    int SegBatchSize,
    int EmbBatchSize,
    int PollIntervalSec,
    IFileSystem Fs
);