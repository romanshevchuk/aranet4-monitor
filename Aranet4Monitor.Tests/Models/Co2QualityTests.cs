using Aranet4Monitor.Models;
using Xunit;

namespace Aranet4Monitor.Tests.Models;

public sealed class Co2QualityTests
{
    [Theory]
    [InlineData(0, Co2Level.Unknown, "No reading")]
    [InlineData(-5, Co2Level.Unknown, "No reading")]
    [InlineData(450, Co2Level.Good, "Good air")]
    [InlineData(999, Co2Level.Good, "Good air")]
    [InlineData(1000, Co2Level.Fair, "Getting stuffy")]
    [InlineData(1399, Co2Level.Fair, "Getting stuffy")]
    [InlineData(1400, Co2Level.Poor, "Poor air")]
    [InlineData(1401, Co2Level.Poor, "Poor air")]
    [InlineData(3000, Co2Level.Poor, "Poor air")]
    public void ClassifiesReadingsIntoTheDashboardBands(int ppm, Co2Level expected, string description)
    {
        Assert.Equal(expected, Co2Quality.Classify(ppm));
        Assert.Equal(description, Co2Quality.Describe(Co2Quality.Classify(ppm)));
    }
}
