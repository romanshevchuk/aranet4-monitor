namespace Aranet4Monitor.Presentation;

/// <summary>WPF colors associated with the metric cards and charts.</summary>
public static class MetricColors
{
    public static Color Accent(MetricKind kind) => kind switch
    {
        MetricKind.Co2 => Color.FromRgb(0x3B, 0x82, 0xF6),
        MetricKind.Temperature => Color.FromRgb(0xF9, 0x73, 0x16),
        MetricKind.Humidity => Color.FromRgb(0x06, 0xB6, 0xD4),
        _ => Color.FromRgb(0x8B, 0x5C, 0xF6),
    };
}
