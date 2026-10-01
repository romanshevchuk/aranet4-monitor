namespace Aranet4Monitor.Alerts;

public sealed class Co2AlertService
{
    public const int RecommendedVentilationThresholdPpm = 1_500;
    public const int ResetThresholdPpm = 1_400;
    public static readonly TimeSpan RequiredDuration = TimeSpan.FromMinutes(10);

    /// <summary>The level CO₂ must fall to (or below) before an alert can fire again.</summary>
    public static int GetResetThreshold(int thresholdPpm) => Math.Min(ResetThresholdPpm, thresholdPpm - 100);

    private readonly Dictionary<string, AlertState> _states = new(StringComparer.OrdinalIgnoreCase);

    public void DeferNotification(string deviceId)
    {
        if (_states.TryGetValue(deviceId, out var state))
        {
            state.Notified = false;
        }
    }

    public bool ShouldNotify(
        string deviceId,
        int ppm,
        DateTime observedAt,
        int thresholdPpm = RecommendedVentilationThresholdPpm,
        TimeSpan? expectedInterval = null,
        TimeSpan? requiredDuration = null)
    {
        var resetThreshold = GetResetThreshold(thresholdPpm);
        var normalSampleGap = TimeSpan.FromMinutes(5);
        var intervalGap = expectedInterval is { } interval
            ? TimeSpan.FromTicks(interval.Ticks * 2)
            : normalSampleGap;
        var maximumSampleGap = intervalGap > normalSampleGap ? intervalGap : normalSampleGap;
        if (!_states.TryGetValue(deviceId, out var state))
        {
            state = new AlertState();
            _states.Add(deviceId, state);
        }

        if (ppm <= resetThreshold)
        {
            state.Reset();
            return false;
        }

        if (state.LastObserved is { } lastObserved && observedAt - lastObserved > maximumSampleGap)
        {
            state.Reset();
        }

        state.LastObserved = observedAt;
        if (ppm <= thresholdPpm)
        {
            state.AboveThresholdSince = null;
            return false;
        }

        state.AboveThresholdSince ??= observedAt;
        if (state.Notified || observedAt - state.AboveThresholdSince < (requiredDuration ?? RequiredDuration))
        {
            return false;
        }

        state.Notified = true;
        return true;
    }

    private sealed class AlertState
    {
        public DateTime? AboveThresholdSince
        {
            get; set;
        }
        public DateTime? LastObserved
        {
            get; set;
        }
        public bool Notified
        {
            get; set;
        }

        public void Reset()
        {
            AboveThresholdSince = null;
            LastObserved = null;
            Notified = false;
        }
    }
}
