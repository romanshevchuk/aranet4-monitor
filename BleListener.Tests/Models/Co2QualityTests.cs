using BleListener;
using Xunit;

namespace BleListener.Tests;

public sealed class Co2QualityTests
{
    [Theory]
    [InlineData(0, Co2Level.Unknown)]
    [InlineData(-5, Co2Level.Unknown)]
    [InlineData(450, Co2Level.Good)]
    [InlineData(999, Co2Level.Good)]
    [InlineData(1000, Co2Level.Fair)]
    [InlineData(1399, Co2Level.Fair)]
    [InlineData(1400, Co2Level.Poor)]
    [InlineData(3000, Co2Level.Poor)]
    public void ClassifiesReadingsIntoTheDashboardBands(int ppm, Co2Level expected)
    {
        Assert.Equal(expected, Co2Quality.Classify(ppm));
    }
}
