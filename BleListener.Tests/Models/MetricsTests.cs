using BleListener.Models;
using Xunit;

namespace BleListener.Tests.Models;

public sealed class MetricsTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0);

    private static Co2Sample[] Samples() =>
    [
        new(Now.AddMinutes(-30), 800, 20.5m, 50, 996.2m),
        new(Now.AddMinutes(-20), 0, 21.0m, null, null), // e.g. a temperature-only history row
        new(Now.AddMinutes(-10), 1200, 21.8m, 54, 997.8m),
    ];

    [Fact]
    public void ReadsEachMetricFromASample()
    {
        var sample = Samples()[2];

        Assert.Equal(1200.0, Metrics.Value(sample, MetricKind.Co2));
        Assert.Equal(21.8, Metrics.Value(sample, MetricKind.Temperature));
        Assert.Equal(54.0, Metrics.Value(sample, MetricKind.Humidity));
        Assert.Equal(997.8, Metrics.Value(sample, MetricKind.Pressure));
    }

    [Fact]
    public void RowsWithoutAMetricAreSkipped()
    {
        var sample = Samples()[1];

        Assert.Null(Metrics.Value(sample, MetricKind.Co2));
        Assert.Null(Metrics.Value(sample, MetricKind.Humidity));
        Assert.Equal(21.0, Metrics.Value(sample, MetricKind.Temperature));
    }

    [Fact]
    public void RangeCoversOnlyReadingsInTheWindow()
    {
        var all = Metrics.Range(Samples(), null, Now, MetricKind.Temperature);
        var recent = Metrics.Range(Samples(), TimeSpan.FromMinutes(15), Now, MetricKind.Temperature);

        Assert.Equal(20.5, all!.Value.Min);
        Assert.Equal(21.8, all.Value.Max);
        Assert.Equal(21.8, recent!.Value.Min);
        Assert.Equal(21.8, recent.Value.Max);
        Assert.Null(Metrics.Range(Samples(), TimeSpan.FromMinutes(5), Now, MetricKind.Temperature));
    }

    [Fact]
    public void FormatsValuesWithUnits()
    {
        Assert.EndsWith("ppm", Metrics.FormatWithUnit(1284, MetricKind.Co2));
        Assert.EndsWith("°C", Metrics.FormatWithUnit(21.8, MetricKind.Temperature));
        Assert.EndsWith("%", Metrics.FormatWithUnit(52, MetricKind.Humidity));
        Assert.EndsWith("hPa", Metrics.FormatWithUnit(997.8, MetricKind.Pressure));
    }
}
