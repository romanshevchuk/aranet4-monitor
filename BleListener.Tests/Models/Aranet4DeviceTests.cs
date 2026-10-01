using BleListener;
using Xunit;

namespace BleListener.Tests;

public sealed class Aranet4DeviceTests
{
    [Fact]
    public void MergeHistoryCombinesMetricsAtTheSameTimestamp()
    {
        var device = new Aranet4Device { Address = "sensor" };
        var timestamp = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        device.MergeHistory([new Co2Sample(timestamp, 900)]);

        var added = device.MergeHistory([
            new Co2Sample(timestamp, 0, 20.5m, 42, 1001.2m),
            new Co2Sample(timestamp.AddMinutes(5), 950, 21.0m, 43, 1000.8m),
        ]);

        Assert.Equal(1, added);
        Assert.Equal(2, device.History.Count);
        Assert.Equal(900, device.History[0].Ppm);
        Assert.Equal(20.5m, device.History[0].TemperatureCelsius);
        Assert.Equal(42, device.History[0].HumidityPercent);
        Assert.Equal(1001.2m, device.History[0].PressureHpa);
    }

    [Fact]
    public void MergeHistoryTreatsNearbyTimestampsAsOneMeasurement()
    {
        var device = new Aranet4Device { Address = "sensor" };
        var measured = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        device.MergeHistory([new Co2Sample(measured, 900)]);

        // The same measurement downloaded from the sensor history, timestamped 4 s later.
        var added = device.MergeHistory([new Co2Sample(measured.AddSeconds(4), 0, 21.5m, 40, 1002.0m)]);

        Assert.Equal(0, added);
        Assert.Single(device.History);
        Assert.Equal(900, device.History[0].Ppm);
        Assert.Equal(21.5m, device.History[0].TemperatureCelsius);
    }

    [Fact]
    public void MergeHistoryKeepsMeasurementsOneIntervalApart()
    {
        var device = new Aranet4Device { Address = "sensor" };
        var measured = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        device.MergeHistory([new Co2Sample(measured, 900)]);

        var added = device.MergeHistory([new Co2Sample(measured.AddSeconds(60), 910)]);

        Assert.Equal(1, added);
        Assert.Equal(2, device.History.Count);
    }
}
