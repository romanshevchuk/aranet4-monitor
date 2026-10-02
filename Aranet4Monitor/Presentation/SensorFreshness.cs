namespace Aranet4Monitor.Presentation;

public static class SensorFreshness
{
    public static readonly TimeSpan MinimumStaleAge = TimeSpan.FromMinutes(5);

    public static TimeSpan StaleAfter(int? measurementIntervalSeconds)
    {
        if (measurementIntervalSeconds is not > 0)
        {
            return MinimumStaleAge;
        }

        var intervalAge = TimeSpan.FromSeconds(measurementIntervalSeconds.Value * 2d);
        return intervalAge > MinimumStaleAge ? intervalAge : MinimumStaleAge;
    }

    public static bool IsStale(DateTime lastSeen, int? measurementIntervalSeconds, DateTime now)
    {
        return lastSeen != default && now - lastSeen > StaleAfter(measurementIntervalSeconds);
    }
}
