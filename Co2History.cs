using System.IO;
using System.Text.Json;

namespace BleListener;

/// <summary>One decoded CO₂ measurement. <see cref="Time"/> is when the sensor took it (local time).</summary>
public sealed record Co2Sample(DateTime Time, int Ppm);

/// <summary>Saves each device's CO₂ history under %LocalAppData%\AranetHome\history so charts survive restarts.</summary>
public static class HistoryStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AranetHome", "history");

    private static string PathFor(string address) => Path.Combine(Folder, address.Replace(':', '-') + ".json");

    public static List<Co2Sample> Load(string address)
    {
        try
        {
            var path = PathFor(address);
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<Co2Sample>>(File.ReadAllText(path)) ?? [];
        }
        catch { return []; } // corrupt or unreadable history must never break the live view
    }

    public static void Save(string address, IReadOnlyList<Co2Sample> samples)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = PathFor(address);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(samples));
            File.Move(temp, path, overwrite: true);
        }
        catch { /* disk full / locked: keep running without persistence */ }
    }
}

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
            if (sample.Time < from) continue;
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
        if (samples.Count < 2) return null;
        var latest = samples[^1];

        Co2Sample? reference = null;
        for (var i = samples.Count - 2; i >= 0; i--)
        {
            if (latest.Time - samples[i].Time >= TimeSpan.FromMinutes(30)) { reference = samples[i]; break; }
        }
        // Not 30 minutes of data yet: fall back to the oldest sample if it's at least 10 minutes back.
        if (reference is null && latest.Time - samples[0].Time >= TimeSpan.FromMinutes(10)) reference = samples[0];
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
