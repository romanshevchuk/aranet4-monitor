using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Domain.AirQuality;

/// <summary>One sharp CO₂ fall that looks like a window being opened.</summary>
public sealed record AiringEvent(DateTime Start, DateTime End, int DropPpm)
{
    public TimeSpan Duration => End - Start;
}

public static class AiringDetector
{
    /// <summary>A fall of this much within two readings marks the start of a candidate event.</summary>
    public const int TriggerDropPpm = 100;

    /// <summary>The whole fall, from the preceding peak to the trough, must reach this much.</summary>
    public const int MinimumDropPpm = 200;

    /// <summary>Finds airing events in CO₂ samples ordered by time, oldest first.</summary>
    public static IReadOnlyList<AiringEvent> Detect(IReadOnlyList<Co2Sample> samples)
    {
        var events = new List<AiringEvent>();
        var index = 0;
        while (index < samples.Count - 2)
        {
            if (samples[index].Ppm - samples[index + 2].Ppm < TriggerDropPpm)
            {
                index++;
                continue;
            }

            var startIndex = index;
            while (startIndex > 0 && samples[startIndex - 1].Ppm > samples[startIndex].Ppm)
            {
                startIndex--;
            }

            var endIndex = index + 2;
            while (endIndex < samples.Count - 1 && samples[endIndex + 1].Ppm < samples[endIndex].Ppm)
            {
                endIndex++;
            }

            var drop = samples[startIndex].Ppm - samples[endIndex].Ppm;
            if (drop >= MinimumDropPpm)
            {
                events.Add(new AiringEvent(samples[startIndex].Time, samples[endIndex].Time, drop));
            }

            index = endIndex;
        }

        return events;
    }
}
