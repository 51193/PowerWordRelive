using System.Text.Json;

namespace PowerWordRelive.Infrastructure.Timing;

public static class TimingParser
{
    public static TimingResult? FromJson(JsonElement timingElement)
    {
        if (timingElement.ValueKind != JsonValueKind.Object)
            return null;

        return new TimingResult(
            timingElement.GetProperty("audio_duration_s").GetDouble(),
            timingElement.GetProperty("elapsed_s").GetDouble(),
            timingElement.GetProperty("speed").GetDouble()
        );
    }

    public static object ToLogData(TimingResult? t)
    {
        if (t is null)
            return new { };

        return new
        {
            audio_duration_s = t.AudioDurationS,
            elapsed_s = t.ElapsedS,
            speed = t.Speed
        };
    }
}