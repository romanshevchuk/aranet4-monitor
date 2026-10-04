using Aranet4Monitor.Application.Alerts;
using Xunit;

namespace Aranet4Monitor.Core.Tests.Alerts;

public sealed class Co2AlertServiceTests
{
    [Fact]
    public void AlertsOnlyAfterSustainedHighReadingAndRearmsBelowResetPoint()
    {
        var alerts = new Co2AlertService();
        var start = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        Assert.False(alerts.ShouldNotify("sensor", 1501, start));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(4)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(8)));
        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(10)));
        Assert.False(alerts.ShouldNotify("sensor", 1600, start.AddMinutes(11)));
        Assert.False(alerts.ShouldNotify("sensor", 1401, start.AddMinutes(12)));
        Assert.False(alerts.ShouldNotify("sensor", 1400, start.AddMinutes(13)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(14)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(18)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(22)));
        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(24)));
    }

    [Fact]
    public void AlertRequiresStrictlyMoreThanThresholdAndResetsAfterLongDataGap()
    {
        var alerts = new Co2AlertService();
        var start = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        Assert.False(alerts.ShouldNotify("sensor", 1500, start));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(1)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(5)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(12)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(16)));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(20)));
        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(22)));
    }

    [Fact]
    public void ExpectedMeasurementIntervalAllowsAContinuousHighPeriodAtTheGapBoundary()
    {
        var alerts = new Co2AlertService();
        var start = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        var interval = TimeSpan.FromMinutes(5);

        Assert.False(alerts.ShouldNotify("sensor", 1501, start, expectedInterval: interval));
        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(10), expectedInterval: interval));
    }

    [Fact]
    public void UsesConfiguredAlertPersistenceDuration()
    {
        var alerts = new Co2AlertService();
        var start = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        var duration = TimeSpan.FromMinutes(3);

        Assert.False(alerts.ShouldNotify("sensor", 1501, start, requiredDuration: duration));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(2), requiredDuration: duration));
        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(3), requiredDuration: duration));
    }

    [Fact]
    public void DeferringDueAlertAllowsNotificationAfterPauseWithoutRestartingDuration()
    {
        var alerts = new Co2AlertService();
        var start = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);
        var duration = TimeSpan.FromMinutes(3);

        Assert.False(alerts.ShouldNotify("sensor", 1501, start, requiredDuration: duration));
        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(3), requiredDuration: duration));

        alerts.DeferNotification("sensor");

        Assert.True(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(4), requiredDuration: duration));
        Assert.False(alerts.ShouldNotify("sensor", 1501, start.AddMinutes(5), requiredDuration: duration));
    }

    [Theory]
    [InlineData(1500, 1400)]
    [InlineData(1450, 1350)]   // a low threshold pulls the reset point down with it
    [InlineData(2500, 1400)]
    public void ResetThresholdStaysBelowTheAlertThreshold(int threshold, int expectedReset)
    {
        Assert.Equal(expectedReset, Co2AlertService.GetResetThreshold(threshold));
    }
}

