using System;
using System.Collections.Generic;
using System.Linq;
using Aranet4Monitor.Domain.AirQuality;
using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Presentation.Formatting;

public readonly record struct HistoryZoneShares(TimeSpan Good, TimeSpan Elevated, TimeSpan High)
{
    public TimeSpan Total => Good + Elevated + High;

    public int GoodPercent => Percent(Good, Total);

    public int ElevatedPercent => Percent(Elevated, Total);

    public int HighPercent => Percent(High, Total);

    private static int Percent(TimeSpan duration, TimeSpan total) =>
        total <= TimeSpan.Zero ? 0 : (int)Math.Round(duration.TotalSeconds * 100 / total.TotalSeconds);
}

public static class HistoryZoneShareCalculator
{
    private static readonly TimeSpan MaximumSampleHold = TimeSpan.FromMinutes(30);

    public static HistoryZoneShares Calculate(IReadOnlyList<Co2Sample> samples, DateTime end)
    {
        var goodSeconds = 0d;
        var elevatedSeconds = 0d;
        var highSeconds = 0d;
        var orderedSamples = samples.OrderBy(sample => sample.Time).ToArray();

        for (var index = 0; index < orderedSamples.Length; index++)
        {
            var sample = orderedSamples[index];
            var intervalEnd = index + 1 < orderedSamples.Length
                ? orderedSamples[index + 1].Time
                : end;
            var duration = intervalEnd - sample.Time;
            if (duration <= TimeSpan.Zero)
            {
                continue;
            }

            // Do not infer air quality across long gaps that the chart itself breaks.
            duration = duration > MaximumSampleHold ? MaximumSampleHold : duration;
            switch (Co2Quality.Classify(sample.Ppm))
            {
                case Co2Level.Good:
                    goodSeconds += duration.TotalSeconds;
                    break;
                case Co2Level.Fair:
                    elevatedSeconds += duration.TotalSeconds;
                    break;
                case Co2Level.Poor:
                    highSeconds += duration.TotalSeconds;
                    break;
            }
        }

        return new HistoryZoneShares(
            TimeSpan.FromSeconds(goodSeconds),
            TimeSpan.FromSeconds(elevatedSeconds),
            TimeSpan.FromSeconds(highSeconds));
    }
}
