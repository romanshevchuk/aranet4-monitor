using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Application.History;

public static class HistoryMerger
{
    public static readonly TimeSpan SameMeasurementTolerance = TimeSpan.FromSeconds(15);

    internal const int MaximumSamples = 10_000;

    public static HistoryMergeResult Merge(
        IEnumerable<Co2Sample> existing,
        IEnumerable<Co2Sample> imported)
    {
        var current = existing.ToArray();
        var merged = MergeNearbySamples(current.Concat(imported.Where(sample => sample.HasAnyMetric))
                .OrderBy(sample => sample.Time))
            .TakeLast(MaximumSamples)
            .ToArray();

        return new HistoryMergeResult(Math.Max(0, merged.Length - current.Length), merged);
    }

    public static IReadOnlyList<Co2Sample> Normalize(IEnumerable<Co2Sample> samples) => samples
        .OrderBy(sample => sample.Time)
        .TakeLast(MaximumSamples)
        .ToArray();

    private static List<Co2Sample> MergeNearbySamples(IEnumerable<Co2Sample> ordered)
    {
        var result = new List<Co2Sample>();
        var group = new List<Co2Sample>();
        foreach (var sample in ordered)
        {
            if (group.Count > 0 && sample.Time - group[0].Time > SameMeasurementTolerance)
            {
                result.Add(MergeSamplesAtSameTime(group));
                group.Clear();
            }

            group.Add(sample);
        }

        if (group.Count > 0)
        {
            result.Add(MergeSamplesAtSameTime(group));
        }

        return result;
    }

    private static Co2Sample MergeSamplesAtSameTime(IEnumerable<Co2Sample> samples)
    {
        var entries = samples.ToArray();
        return new Co2Sample(
            entries[0].Time,
            entries.LastOrDefault(sample => sample.Ppm > 0)?.Ppm ?? 0,
            entries.LastOrDefault(sample => sample.TemperatureCelsius.HasValue)?.TemperatureCelsius,
            entries.LastOrDefault(sample => sample.HumidityPercent.HasValue)?.HumidityPercent,
            entries.LastOrDefault(sample => sample.PressureHpa.HasValue)?.PressureHpa);
    }
}
