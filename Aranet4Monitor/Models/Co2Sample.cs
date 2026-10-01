namespace Aranet4Monitor.Models;

/// <summary>One decoded CO₂ measurement. <see cref="Time"/> is when the sensor took it (local time).</summary>
public sealed record Co2Sample(
    DateTime Time,
    int Ppm,
    decimal? TemperatureCelsius = null,
    int? HumidityPercent = null,
    decimal? PressureHpa = null)
{
    public bool HasAnyMetric => Ppm > 0 || TemperatureCelsius.HasValue || HumidityPercent.HasValue || PressureHpa.HasValue;

    public static DateTime ToWholeSecond(DateTime time) =>
        new(time.Ticks - time.Ticks % TimeSpan.TicksPerSecond, time.Kind);
}
