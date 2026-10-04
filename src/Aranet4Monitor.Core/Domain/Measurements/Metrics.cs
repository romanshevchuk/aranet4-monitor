using System.Globalization;

namespace Aranet4Monitor.Domain.Measurements;

/// <summary>The four measurements the sensor records; each one gets a card and a chart tab.</summary>
public enum MetricKind
{
    Co2, Temperature, Humidity, Pressure
}

/// <summary>Per-metric facts (units, colours, formatting, summaries) shared by the cards, the chart and the stats text.</summary>
public static class Metrics
{
    /// <summary>The value of <paramref name="kind"/> in a sample, or null when that sample doesn't carry it.</summary>
    public static double? Value(Co2Sample sample, MetricKind kind, TemperatureUnit temperatureUnit = TemperatureUnit.Celsius) => kind switch
    {
        MetricKind.Co2 => sample.Ppm > 0 ? (double?)sample.Ppm : null,
        MetricKind.Temperature => sample.TemperatureCelsius is { } t ? ConvertTemperature((double)t, temperatureUnit) : null,
        MetricKind.Humidity => sample.HumidityPercent is { } h ? (double?)h : null,
        MetricKind.Pressure => sample.PressureHpa is { } p ? (double?)p : null,
        _ => null,
    };

    public static double ConvertTemperature(double celsius, TemperatureUnit temperatureUnit) =>
        temperatureUnit == TemperatureUnit.Fahrenheit ? celsius * 9 / 5 + 32 : celsius;

    public static string Title(MetricKind kind) => kind switch
    {
        MetricKind.Co2 => "CO₂",
        MetricKind.Temperature => "Temperature",
        MetricKind.Humidity => "Humidity",
        _ => "Pressure",
    };

    public static string Unit(MetricKind kind, TemperatureUnit temperatureUnit = TemperatureUnit.Celsius) => kind switch
    {
        MetricKind.Co2 => "ppm",
        MetricKind.Temperature => temperatureUnit == TemperatureUnit.Fahrenheit ? "°F" : "°C",
        MetricKind.Humidity => "%",
        _ => "hPa",
    };

    /// <summary>The smallest vertical span a chart shows, so tiny sensor noise isn't blown up into dramatic swings.</summary>
    public static double MinSpan(MetricKind kind, TemperatureUnit temperatureUnit = TemperatureUnit.Celsius) => kind switch
    {
        MetricKind.Co2 => 200,
        MetricKind.Temperature => temperatureUnit == TemperatureUnit.Fahrenheit ? 3.6 : 2,
        MetricKind.Humidity => 10,
        _ => 4,
    };

    public static string Format(double value, MetricKind kind) => kind switch
    {
        MetricKind.Co2 or MetricKind.Humidity => value.ToString("N0", CultureInfo.CurrentCulture),
        _ => value.ToString("N1", CultureInfo.CurrentCulture),
    };

    public static string FormatWithUnit(double value, MetricKind kind, TemperatureUnit temperatureUnit = TemperatureUnit.Celsius) =>
        kind == MetricKind.Humidity ? $"{Format(value, kind)}%" : $"{Format(value, kind)} {Unit(kind, temperatureUnit)}";

    /// <summary>Min/max of a metric within the time range, or null when there are no readings.</summary>
    public static (double Min, double Max)? Range(
        IReadOnlyList<Co2Sample> samples,
        TimeSpan? range,
        DateTime now,
        MetricKind kind,
        TemperatureUnit temperatureUnit = TemperatureUnit.Celsius)
    {
        var from = range is null ? DateTime.MinValue : now - range.Value;
        var any = false;
        double min = double.MaxValue, max = double.MinValue;
        foreach (var sample in samples)
        {
            if (sample.Time < from || Value(sample, kind, temperatureUnit) is not { } value)
            {
                continue;
            }

            any = true;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        return any ? (min, max) : null;
    }

    /// <summary>"Min 783 · Avg 998 · Max 1,299 ppm · 169 readings" for the chart header.</summary>
    public static string Describe(
        IReadOnlyList<Co2Sample> samples,
        TimeSpan? range,
        DateTime now,
        MetricKind kind,
        TemperatureUnit temperatureUnit = TemperatureUnit.Celsius)
    {
        var from = range is null ? DateTime.MinValue : now - range.Value;
        var count = 0;
        double min = double.MaxValue, max = double.MinValue, sum = 0;
        foreach (var sample in samples)
        {
            if (sample.Time < from || Value(sample, kind, temperatureUnit) is not { } value)
            {
                continue;
            }

            count++;
            sum += value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        if (count == 0)
        {
            return samples.Any(sample => Value(sample, kind) is not null)
                ? "No readings in this time range"
                : "No readings yet — the first point arrives with the next measurement";
        }

        var unit = kind == MetricKind.Humidity ? "%" : $" {Unit(kind, temperatureUnit)}";
        return $"Min {Format(min, kind)}  ·  Avg {Format(sum / count, kind)}  ·  Max {Format(max, kind)}{unit}  ·  {count:N0} reading{(count == 1 ? "" : "s")}";
    }
}
