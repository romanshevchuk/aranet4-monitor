namespace Aranet4Monitor.Models;

/// <summary>Small helpers that turn a history into the texts shown around the chart.</summary>
public static class Co2Stats
{
    public static string Describe(IReadOnlyList<Co2Sample> samples, TimeSpan? range, DateTime now) =>
        Metrics.Describe(samples, range, now, MetricKind.Co2);

    /// <summary>Compares the newest reading with one from ~30 minutes earlier. Null when there isn't enough history.</summary>
    public static (string Text, TrendKind Kind)? Trend(IReadOnlyList<Co2Sample> samples)
    {
        var co2Samples = samples.Where(sample => sample.Ppm > 0).ToArray();
        if (co2Samples.Length < 2) return null;
        var latest = co2Samples[^1];

        Co2Sample? reference = null;
        for (var i = co2Samples.Length - 2; i >= 0; i--)
        {
            if (latest.Time - co2Samples[i].Time >= TimeSpan.FromMinutes(30)) { reference = co2Samples[i]; break; }
        }
        // Not 30 minutes of data yet: fall back to the oldest sample if it's at least 10 minutes back.
        if (reference is null && latest.Time - co2Samples[0].Time >= TimeSpan.FromMinutes(10)) reference = co2Samples[0];
        if (reference is null) return null;

        var delta = latest.Ppm - reference.Ppm;
        var minutes = (int)Math.Round((latest.Time - reference.Time).TotalMinutes);
        return delta switch
        {
            >= 25 => ($"▲ Rising  +{delta:N0} ppm over {minutes} min", TrendKind.Rising),
            <= -25 => ($"▼ Falling  −{-delta:N0} ppm over {minutes} min", TrendKind.Falling),
            _ => ($"● Steady over the last {minutes} min", TrendKind.Steady),
        };
    }
}

public enum TrendKind { Rising, Falling, Steady }
