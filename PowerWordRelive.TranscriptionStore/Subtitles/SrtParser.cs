namespace PowerWordRelive.TranscriptionStore.Subtitles;

internal record SubtitleBlock(long StartMs, long EndMs, string Text);

internal static class SrtParser
{
    public static List<SubtitleBlock> Parse(string[] lines)
    {
        var blocks = new List<SubtitleBlock>();
        var i = 0;

        while (i < lines.Length)
        {
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;

            if (i >= lines.Length)
                break;

            if (!int.TryParse(lines[i], out _))
                throw new FormatException($"Expected subtitle index at line {i + 1}, got: {lines[i]}");
            i++;

            if (i >= lines.Length)
                throw new FormatException("Unexpected end of file after subtitle index");

            var (startMs, endMs) = ParseTimestampLine(lines[i]);
            i++;

            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(lines[i]);
                i++;
            }

            if (textLines.Count == 0)
                throw new FormatException($"Subtitle block has no text (line {i})");

            blocks.Add(new SubtitleBlock(startMs, endMs, string.Join(Environment.NewLine, textLines)));
        }

        return blocks;
    }

    private static (long StartMs, long EndMs) ParseTimestampLine(string line)
    {
        var arrowIndex = line.IndexOf(" --> ", StringComparison.Ordinal);
        if (arrowIndex < 0)
            throw new FormatException($"Invalid timestamp line (missing ' --> '): {line}");

        var startStr = line[..arrowIndex];
        var endStr = line[(arrowIndex + 5)..];

        return (ParseSrtTime(startStr), ParseSrtTime(endStr));
    }

    private static long ParseSrtTime(string s)
    {
        var parts = s.Split(':', ',');
        if (parts.Length != 4)
            throw new FormatException($"Invalid SRT timestamp: {s}");

        var hours = int.Parse(parts[0]);
        var minutes = int.Parse(parts[1]);
        var seconds = int.Parse(parts[2]);
        var millis = int.Parse(parts[3]);

        return hours * 3600000L + minutes * 60000L + seconds * 1000L + millis;
    }
}