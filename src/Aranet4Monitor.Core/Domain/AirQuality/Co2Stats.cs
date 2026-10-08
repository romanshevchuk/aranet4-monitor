using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Domain.AirQuality;

/// <summary>Small helpers that turn a history into the texts shown around the chart.</summary>
public static class Co2Stats
{
    public static string Describe(IReadOnlyList<Co2Sample> samples, TimeSpan? range, DateTime now) =>
        Metrics.Describe(samples, range, now, MetricKind.Co2);

    /// <summary>Compares the newest reading with one from ~30 minutes earlier. Null when there isn't enough history.</summary>
    public static (string Text, TrendKind Kind)? Trend(IReadOnlyList<Co2Sample> samples)
    {
        var co2Samples = samples.Where(sample => sample.Ppm > 0).ToArray();
        if (co2Samples.Length < 2)
        {
            return null;
        }

        var latest = co2Samples[^1];

        var maxAge = TimeSpan.FromMinutes(60);
        Co2Sample? reference = null;
        for (var i = co2Samples.Length - 2; i >= 0; i--)
        {
            var age = latest.Time - co2Samples[i].Time;
            if (age > maxAge)
            {
                break;
            }

            if (age >= TimeSpan.FromMinutes(30))
            {
                reference = co2Samples[i];
                break;
            }
        }

        // Not 30 minutes of data yet: use the oldest sample inside the last hour if it is at least 10 minutes back.
        if (reference is null)
        {
            var oldestRecent = co2Samples.FirstOrDefault(sample => latest.Time - sample.Time <= maxAge);
            if (oldestRecent is not null && latest.Time - oldestRecent.Time >= TimeSpan.FromMinutes(10))
            {
                reference = oldestRecent;
            }
        }

        if (reference is null)
        {
            return null;
        }

        var delta = latest.Ppm - reference.Ppm;

        var minutes = (int)Math.Round((latest.Time - reference.Time).TotalMinutes);
        return delta switch
        {
            >= 25 => ($"▲ Rising  {Math.Abs(delta):N0} ppm over {minutes} min", TrendKind.Rising),
            <= -25 => ($"▼ Falling  {Math.Abs(delta):N0} ppm over {minutes} min", TrendKind.Falling),
            _ => ($"● Steady over the last {minutes} min", TrendKind.Steady),
        };
    }
}

public enum TrendKind
{
    Rising, Falling, Steady
}
