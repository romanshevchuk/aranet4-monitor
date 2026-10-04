using Xunit;

namespace Aranet4Monitor.Tests.Models;

public sealed class Aranet4DeviceTests
{
    [Fact]
    public void LoadHistoryShowsTheLatestRealCo2Measurement()
    {
        var device = new Aranet4Device { Address = "sensor" };
        var timestamp = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        device.LoadHistory([
            new Co2Sample(timestamp, 900),
            new Co2Sample(timestamp.AddMinutes(1), 0, 20.5m, 42, 1001.2m),
        ]);

        Assert.Equal(2, device.History.Count);
        Assert.Equal(900, device.Co2Ppm);
    }

    [Fact]
    public void ReplaceHistoryDoesNotReplaceCurrentLiveReading()
    {
        var device = new Aranet4Device { Address = "sensor" };
        var measured = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        device.Co2Ppm = 1000;

        device.ReplaceHistory([new Co2Sample(measured, 900)]);

        Assert.Equal(1000, device.Co2Ppm);
        Assert.Single(device.History);
    }
}
