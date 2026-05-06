namespace PowerWordRelive.Infrastructure.Timing;

public class CumulativeTimer
{
    private double _totalAudioDurationS;
    private double _totalEffectiveElapsedS;
    private long _totalFiles;
    private long _totalSegments;

    public void Start()
    {
    }

    public void Record(double audioDurationS, double effectiveElapsedS, int segments)
    {
        _totalAudioDurationS += audioDurationS;
        _totalEffectiveElapsedS += effectiveElapsedS;
        _totalSegments += segments;
        _totalFiles++;
    }

    public CumulativeTimingSnapshot Snapshot()
    {
        return new CumulativeTimingSnapshot(
            _totalFiles,
            _totalSegments,
            _totalAudioDurationS,
            _totalEffectiveElapsedS,
            _totalEffectiveElapsedS > 0 ? _totalAudioDurationS / _totalEffectiveElapsedS : 0.0
        );
    }
}