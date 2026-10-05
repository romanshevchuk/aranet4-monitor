namespace Aranet4Monitor.Presentation;

/// <summary>WPF colors associated with the metric cards and charts.</summary>
public static class MetricColors
{
    public static Color Accent(MetricKind kind) => kind switch
    {
        MetricKind.Co2 => Color.FromRgb(0x08, 0x7B, 0x51),
        MetricKind.Temperature => Color.FromRgb(0xB7, 0x47, 0x1B),
        MetricKind.Humidity => Color.FromRgb(0x17, 0x6B, 0xC4),
        _ => Color.FromRgb(0x80, 0x52, 0xD6),
    };
}
