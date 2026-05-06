using System.Diagnostics;

namespace PowerWordRelive.Infrastructure.Timing;

public class CumulativeTimer
{
    private readonly Stopwatch _sw = new();
    private double _totalAudioDurationS;
    private long _totalFiles;
    private long _totalSegments;

    public void Start()
    {
        _sw.Start();
    }

    public void Record(double audioDurationS, int segments)
    {
        _totalAudioDurationS += audioDurationS;
        _totalSegments += segments;
        _totalFiles++;
    }

    public CumulativeTimingSnapshot Snapshot()
    {
        var elapsed = _sw.Elapsed.TotalSeconds;
        return new CumulativeTimingSnapshot(
            _totalFiles,
            _totalSegments,
            _totalAudioDurationS,
            elapsed,
            elapsed > 0 ? _totalAudioDurationS / elapsed : 0.0
        );
    }
}