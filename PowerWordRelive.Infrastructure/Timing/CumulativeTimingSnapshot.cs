namespace PowerWordRelive.Infrastructure.Timing;

public record CumulativeTimingSnapshot(
    long TotalFiles,
    long TotalSegments,
    double TotalAudioDurationS,
    double TotalElapsedS,
    double Speed
);