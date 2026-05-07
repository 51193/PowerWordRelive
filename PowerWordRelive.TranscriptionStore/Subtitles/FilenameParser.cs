namespace PowerWordRelive.TranscriptionStore.Subtitles;

internal record ParsedFilename(long WallClockMs, int OffsetMs, string SpeakerId);

internal static class FilenameParser
{
    public static ParsedFilename Parse(string filename)
    {
        var stem = Path.GetFileNameWithoutExtension(filename);
        if (stem.Length < 32)
            throw new FormatException($"Filename too short: {filename}");

        var tsStr = stem[..22];
        var wallClockMs = ParseTimestamp(tsStr);

        var rest = stem[23..];
        var parts = rest.Split('+');

        var (offsetMs, speakerId) = parts.Length switch
        {
            1 => (0, parts[0]),
            2 => (int.Parse(parts[0]), parts[1]),
            _ => throw new FormatException(
                $"Unexpected filename format (expected 1 or 2 '+' after timestamp): {filename}")
        };

        return new ParsedFilename(wallClockMs, offsetMs, speakerId);
    }

    private static long ParseTimestamp(string ts)
    {
        if (ts.Length != 22 || ts[8] != '_' || ts[15] != '_')
            throw new FormatException($"Invalid timestamp format: {ts}");

        var year = int.Parse(ts[..4]);
        var month = int.Parse(ts[4..6]);
        var day = int.Parse(ts[6..8]);
        var hour = int.Parse(ts[9..11]);
        var min = int.Parse(ts[11..13]);
        var sec = int.Parse(ts[13..15]);
        var micro = int.Parse(ts[16..22]);

        var dto = new DateTimeOffset(year, month, day, hour, min, sec, TimeSpan.Zero);
        return dto.ToUnixTimeMilliseconds() + micro / 1000;
    }
}