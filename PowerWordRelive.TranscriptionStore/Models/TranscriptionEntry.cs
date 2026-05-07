namespace PowerWordRelive.TranscriptionStore.Models;

internal record TranscriptionEntry(
    long StartTimestampMs,
    long EndTimestampMs,
    string SpeakerId,
    string Text,
    string SourceFile
);