using PowerWordRelive.Infrastructure.Storage;

namespace PowerWordRelive.TranscriptionStore;

internal record TranscriptionStoreOptions(
    string InputDir,
    string SqlitePath,
    int PollIntervalSec,
    IFileSystem Fs
);