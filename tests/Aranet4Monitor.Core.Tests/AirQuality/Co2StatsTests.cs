using Xunit;

namespace Aranet4Monitor.Tests.Models;

public sealed class Co2StatsTests
{
    [Theory]
    [InlineData(500, 536, "▲ Rising  36 ppm over 32 min", TrendKind.Rising)]
    [InlineData(1000, 540, "▼ Falling  460 ppm over 30 min", TrendKind.Falling)]
    public void TrendUsesAnUnsignedMagnitude(int olderPpm, int latestPpm, string expectedText, TrendKind expectedKind)
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var samples = new[]
        {
            new Co2Sample(start, olderPpm),
            new Co2Sample(start.AddMinutes(olderPpm == 500 ? 32 : 30), latestPpm),
        };

        var trend = Co2Stats.Trend(samples);

        Assert.NotNull(trend);
        Assert.Equal(expectedText, trend.Value.Text);
        Assert.Equal(expectedKind, trend.Value.Kind);
    }
}
