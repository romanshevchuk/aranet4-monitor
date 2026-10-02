using Aranet4Monitor.Presentation;
using Xunit;

namespace Aranet4Monitor.Tests.Presentation;

public class SensorFreshnessTests
{
    [Fact]
    public void StaleAfter_UsesFiveMinuteMinimum()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), SensorFreshness.StaleAfter(60));
    }

    [Fact]
    public void StaleAfter_UsesTwiceTheMeasurementIntervalWhenLonger()
    {
        Assert.Equal(TimeSpan.FromMinutes(6), SensorFreshness.StaleAfter(180));
    }

    [Fact]
    public void IsStale_RequiresAnExistingReadingAndElapsedThreshold()
    {
        var now = new DateTime(2026, 10, 2, 12, 0, 0);

        Assert.False(SensorFreshness.IsStale(default, 60, now));
        Assert.False(SensorFreshness.IsStale(now.AddMinutes(-5), 60, now));
        Assert.True(SensorFreshness.IsStale(now.AddMinutes(-5).AddSeconds(-1), 60, now));
    }
}