using Aranet4Monitor.Application.Monitoring;
using Xunit;

namespace Aranet4Monitor.Core.Tests.Monitoring;

public sealed class SensorMonitorTests
{
    [Fact]
    public void DefersDueNotificationAndAllowsItAfterPauseWithoutRestartingDuration()
    {
        var monitor = new SensorMonitor();
        var start = new DateTime(2026, 10, 3, 12, 0, 0);
        var duration = TimeSpan.FromMinutes(3);

        Assert.False(monitor.ObserveMeasurement("sensor", 1501, start, 60, 1500, duration, false).AlertDue);

        var deferred = monitor.ObserveMeasurement(
            "sensor", 1501, start.AddMinutes(3), 60, 1500, duration, true);
        Assert.True(deferred.AlertDue);
        Assert.True(deferred.NotificationDeferred);
        Assert.False(deferred.NotificationReady);

        var resumed = monitor.ObserveMeasurement(
            "sensor", 1501, start.AddMinutes(4), 60, 1500, duration, false);
        Assert.True(resumed.NotificationReady);
        Assert.False(monitor.ObserveMeasurement(
            "sensor", 1501, start.AddMinutes(5), 60, 1500, duration, false).AlertDue);
    }

    [Fact]
    public void ReportsRecoveryOnceAfterAReportedAlertAndRearmsTheSensor()
    {
        var monitor = new SensorMonitor();
        var start = new DateTime(2026, 10, 3, 12, 0, 0);
        var duration = TimeSpan.FromMinutes(1);

        monitor.ObserveMeasurement("sensor", 1501, start, 60, 1500, duration, false);
        var alert = monitor.ObserveMeasurement(
            "sensor", 1501, start.AddMinutes(1), 60, 1500, duration, false);

        Assert.True(alert.NotificationReady);

        var recovery = monitor.ObserveMeasurement(
            "sensor", 1400, start.AddMinutes(2), 60, 1500, duration, false);
        Assert.True(recovery.RecoveryNotificationDue);

        var nextReading = monitor.ObserveMeasurement(
            "sensor", 1400, start.AddMinutes(3), 60, 1500, duration, false);
        Assert.False(nextReading.RecoveryNotificationDue);

        var rearmed = monitor.ObserveMeasurement(
            "sensor", 1501, start.AddMinutes(4), 60, 1500, duration, false);
        Assert.False(rearmed.AlertDue);
    }

    [Fact]
    public void KeepsNotificationStateIndependentBetweenSensors()
    {
        var monitor = new SensorMonitor();
        var start = new DateTime(2026, 10, 3, 12, 0, 0);
        var duration = TimeSpan.FromMinutes(1);

        monitor.ObserveMeasurement("sensor-a", 1501, start, 60, 1500, duration, false);
        var alert = monitor.ObserveMeasurement(
            "sensor-a", 1501, start.AddMinutes(1), 60, 1500, duration, false);
        Assert.True(alert.NotificationReady);

        var otherSensorRecovery = monitor.ObserveMeasurement(
            "sensor-b", 1400, start.AddMinutes(2), 60, 1500, duration, false);
        Assert.False(otherSensorRecovery.RecoveryNotificationDue);

        var originalSensorRecovery = monitor.ObserveMeasurement(
            "sensor-a", 1400, start.AddMinutes(2), 60, 1500, duration, false);
        Assert.True(originalSensorRecovery.RecoveryNotificationDue);
    }

    [Fact]
    public void UsesTheLatestPerSensorIntervalForFreshness()
    {
        var monitor = new SensorMonitor();
        var now = new DateTime(2026, 10, 3, 12, 0, 0);

        monitor.UpdateMeasurementInterval("sensor", 180);

        Assert.False(monitor.IsStale("sensor", now.AddMinutes(-6), now));
        Assert.True(monitor.IsStale("sensor", now.AddMinutes(-6).AddSeconds(-1), now));
    }
}
