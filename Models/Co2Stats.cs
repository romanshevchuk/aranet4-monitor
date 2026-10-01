namespace BleListener;

/// <summary>Small helpers that turn a history into the texts shown around the chart.</summary>
public static class Co2Stats
{
    public static string Describe(IReadOnlyList<Co2Sample> samples, TimeSpan? range, DateTime now)
    {
        var from = range is null ? DateTime.MinValue : now - range.Value;
        int count = 0, min = int.MaxValue, max = int.MinValue;
        long sum = 0;
        foreach (var sample in samples)
        {
            if (sample.Time < from || sample.Ppm <= 0) continue;
            count++;
            sum += sample.Ppm;
            min = Math.Min(min, sample.Ppm);
            max = Math.Max(max, sample.Ppm);
        }

        if (count == 0)
            return samples.Count == 0
                ? "No readings yet — the first point arrives with the next measurement"
                : "No readings in this time range";

        var avg = (int)Math.Round(sum / (double)count);
        return $"Min {min:N0}  ·  Avg {avg:N0}  ·  Max {max:N0} ppm  ·  {count:N0} reading{(count == 1 ? "" : "s")}";
    }

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
