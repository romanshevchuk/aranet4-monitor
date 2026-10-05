namespace Aranet4Monitor.Presentation;

/// <summary>WPF colors associated with the metric cards and charts, taken from the active theme.</summary>
public static class MetricColors
{
    public static Color Accent(MetricKind kind) => ThemeService.GetColor(kind switch
    {
        MetricKind.Co2 => "PositiveColor",
        MetricKind.Temperature => "TemperatureAccentColor",
        MetricKind.Humidity => "HumidityAccentColor",
        _ => "PressureAccentColor",
    });
}
