using Aranet4Monitor.Application.Alerts;

namespace Aranet4Monitor.Application.Monitoring;

public sealed class SensorMonitor
{
    private readonly Co2AlertService alertService = new();
    private readonly HashSet<string> notifiedSensors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int?> measurementIntervals = new(StringComparer.OrdinalIgnoreCase);

    public void UpdateMeasurementInterval(string sensorId, int? intervalSeconds)
    {
        measurementIntervals[sensorId] = intervalSeconds;
    }

    public bool IsStale(string sensorId, DateTime lastSeen, DateTime now) =>
        SensorFreshness.IsStale(lastSeen, measurementIntervals.GetValueOrDefault(sensorId), now);

    public SensorMonitorResult ObserveMeasurement(
        string sensorId,
        int co2Ppm,
        DateTime observedAt,
        int? intervalSeconds,
        int thresholdPpm,
        TimeSpan requiredDuration,
        bool notificationsPaused)
    {
        UpdateMeasurementInterval(sensorId, intervalSeconds);
        var resetThreshold = Co2AlertService.GetResetThreshold(thresholdPpm);
        var alertDue = alertService.ShouldNotify(
            sensorId,
            co2Ppm,
            observedAt,
            thresholdPpm,
            TimeSpan.FromSeconds(intervalSeconds ?? 60),
            requiredDuration);
        var notificationDeferred = alertDue && notificationsPaused;

        if (notificationDeferred)
        {
            alertService.DeferNotification(sensorId);
        }
        else if (alertDue)
        {
            notifiedSensors.Add(sensorId);
        }

        var recoveryNotificationDue = co2Ppm <= resetThreshold
            && notifiedSensors.Remove(sensorId)
            && !notificationsPaused;

        return new SensorMonitorResult(alertDue, notificationDeferred, recoveryNotificationDue, resetThreshold);
    }

    /// <summary>Forgets dashboard tracking when the user clears the detected device list.</summary>
    public void ClearSensorTracking()
    {
        measurementIntervals.Clear();
        notifiedSensors.Clear();
    }
}

public sealed record SensorMonitorResult(
    bool AlertDue,
    bool NotificationDeferred,
    bool RecoveryNotificationDue,
    int ResetThresholdPpm)
{
    public bool NotificationReady => AlertDue && !NotificationDeferred;
}
