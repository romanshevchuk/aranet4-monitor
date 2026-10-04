using Xunit;

namespace Aranet4Monitor.Core.Tests.History;

public sealed class HistoryMergerTests
{
    [Fact]
    public void CombinesMetricsFromSamplesAtTheSameTimestamp()
    {
        var timestamp = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        var result = HistoryMerger.Merge(
            [new Co2Sample(timestamp, 900)],
            [
                new Co2Sample(timestamp, 0, 20.5m, 42, 1001.2m),
                new Co2Sample(timestamp.AddMinutes(5), 950, 21.0m, 43, 1000.8m),
            ]);

        Assert.Equal(1, result.AddedSamples);
        Assert.Equal(2, result.Samples.Count);
        Assert.Equal(900, result.Samples[0].Ppm);
        Assert.Equal(20.5m, result.Samples[0].TemperatureCelsius);
        Assert.Equal(42, result.Samples[0].HumidityPercent);
        Assert.Equal(1001.2m, result.Samples[0].PressureHpa);
    }

    [Fact]
    public void TreatsNearbyTimestampsAsOneMeasurement()
    {
        var measured = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        var result = HistoryMerger.Merge(
            [new Co2Sample(measured, 900)],
            [new Co2Sample(measured.AddSeconds(4), 0, 21.5m, 40, 1002.0m)]);

        Assert.Equal(0, result.AddedSamples);
        Assert.Single(result.Samples);
        Assert.Equal(900, result.Samples[0].Ppm);
        Assert.Equal(21.5m, result.Samples[0].TemperatureCelsius);
    }

    [Fact]
    public void KeepsMeasurementsOneIntervalApart()
    {
        var measured = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        var result = HistoryMerger.Merge(
            [new Co2Sample(measured, 900)],
            [new Co2Sample(measured.AddSeconds(60), 910)]);

        Assert.Equal(1, result.AddedSamples);
        Assert.Equal(2, result.Samples.Count);
    }
}
