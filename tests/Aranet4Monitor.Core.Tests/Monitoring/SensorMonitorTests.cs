using Aranet4Monitor.Application.Monitoring;
using Xunit;

namespace Aranet4Monitor.Core.Tests.Monitoring;

public sealed class SensorMonitorTests
{
    [Fact]
    public void DefersDueNotificationAndAllowsItAfterPauseWithoutRestartingDuration()
    {
        var monitor = new SensorMonitor(new FakeHistoryStore());
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
        var monitor = new SensorMonitor(new FakeHistoryStore());
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
        var monitor = new SensorMonitor(new FakeHistoryStore());
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
        var monitor = new SensorMonitor(new FakeHistoryStore());
        var now = new DateTime(2026, 10, 3, 12, 0, 0);

        monitor.UpdateMeasurementInterval("sensor", 180);

        Assert.False(monitor.IsStale("sensor", now.AddMinutes(-6), now));
        Assert.True(monitor.IsStale("sensor", now.AddMinutes(-6).AddSeconds(-1), now));
    }

    [Fact]
    public void ProcessesAndPersistsEachLiveMeasurementOnlyOnce()
    {
        var store = new FakeHistoryStore();
        var monitor = new SensorMonitor(store);
        var observedAt = new DateTime(2026, 10, 3, 12, 0, 0);
        var measurement = new Aranet4Measurement("1.2.3", 900, 20.5m, 1010m, 45, null, null, 60, 0);

        var first = monitor.ProcessMeasurement("sensor", measurement, observedAt, 1500, TimeSpan.FromMinutes(10), false);
        var repeated = monitor.ProcessMeasurement("sensor", measurement, observedAt.AddSeconds(5), 1500, TimeSpan.FromMinutes(10), false);

        Assert.True(first.SampleAdded);
        Assert.True(first.HistorySaved);
        Assert.False(repeated.SampleAdded);
        Assert.Equal(1, store.SaveCount);
        Assert.Single(monitor.LoadHistory("sensor"));
    }

    private sealed class FakeHistoryStore : IHistoryStore
    {
        private readonly Dictionary<string, IReadOnlyList<Co2Sample>> histories = new(StringComparer.OrdinalIgnoreCase);

        public int SaveCount { get; private set; }

        public DateTime? LoadSyncCursor(string deviceAddress) => null;

        public IReadOnlyList<Co2Sample> LoadHistory(string deviceAddress) =>
            histories.GetValueOrDefault(deviceAddress, []);

        public bool SaveHistory(string deviceAddress, IReadOnlyList<Co2Sample> samples)
        {
            SaveCount++;
            histories[deviceAddress] = samples.ToArray();
            return true;
        }

        public bool SaveSyncCursor(string deviceAddress, DateTime syncedThrough) => true;
    }
}
