using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Application.Alerts;
using Aranet4Monitor.Application.History;
using Aranet4Monitor.Domain.Measurements;
using Aranet4Monitor.Protocol.Aranet4;

namespace Aranet4Monitor.Application.Monitoring;

public sealed class SensorMonitor(IHistoryStore historyStore)
{
    private readonly Co2AlertService alertService = new();
    private readonly HashSet<string> notifiedSensors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int?> measurementIntervals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Co2Sample>> histories = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Co2Sample> LoadHistory(string sensorId)
    {
        if (!histories.TryGetValue(sensorId, out var history))
        {
            history = HistoryMerger.Normalize(historyStore.LoadHistory(sensorId)).ToList();
            histories.Add(sensorId, history);
        }

        return history.ToArray();
    }

    public SensorMeasurementResult ProcessMeasurement(
        string sensorId,
        Aranet4Measurement measurement,
        DateTime observedAt,
        int thresholdPpm,
        TimeSpan requiredDuration,
        bool notificationsPaused)
    {
        UpdateMeasurementInterval(sensorId, measurement.IntervalSeconds);
        var history = GetHistory(sensorId);
        var measuredAt = observedAt - TimeSpan.FromSeconds(measurement.AgeSeconds ?? 0);
        var minimumGap = TimeSpan.FromSeconds(Math.Max(10, (measurement.IntervalSeconds ?? 60) * 0.5));
        if (history.Count > 0 && measuredAt - history[^1].Time < minimumGap)
        {
            return new SensorMeasurementResult(false, true, history.ToArray(), null);
        }

        history.Add(new Co2Sample(
            measuredAt,
            measurement.Co2,
            measurement.TemperatureCelsius,
            measurement.HumidityPercent,
            measurement.PressureHpa));
        if (history.Count > HistoryMerger.MaximumSamples)
        {
            history.RemoveAt(0);
        }

        var historySaved = historyStore.SaveHistory(sensorId, history);
        var monitoring = ObserveMeasurement(
            sensorId,
            measurement.Co2,
            observedAt,
            measurement.IntervalSeconds,
            thresholdPpm,
            requiredDuration,
            notificationsPaused);

        return new SensorMeasurementResult(true, historySaved, history.ToArray(), monitoring);
    }

    public void ReplaceHistory(string sensorId, IReadOnlyList<Co2Sample> samples)
    {
        histories[sensorId] = HistoryMerger.Normalize(samples).ToList();
    }

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
        histories.Clear();
    }

    private List<Co2Sample> GetHistory(string sensorId)
    {
        if (!histories.TryGetValue(sensorId, out var history))
        {
            history = HistoryMerger.Normalize(historyStore.LoadHistory(sensorId)).ToList();
            histories.Add(sensorId, history);
        }

        return history;
    }
}

public sealed record SensorMeasurementResult(
    bool SampleAdded,
    bool HistorySaved,
    IReadOnlyList<Co2Sample> History,
    SensorMonitorResult? Monitoring);

public sealed record SensorMonitorResult(
    bool AlertDue,
    bool NotificationDeferred,
    bool RecoveryNotificationDue,
    int ResetThresholdPpm)
{
    public bool NotificationReady => AlertDue && !NotificationDeferred;
}
