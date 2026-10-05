using Xunit;

namespace Aranet4Monitor.Core.Tests.AirQuality;

public sealed class AiringDetectorTests
{
    private static readonly DateTime Start = new(2026, 10, 3, 10, 0, 0);

    private static Co2Sample[] Series(params int[] ppm) =>
        ppm.Select((value, index) => new Co2Sample(Start.AddMinutes(index * 5), value)).ToArray();

    [Fact]
    public void DetectsAFallFromThePeakToTheTrough()
    {
        var events = AiringDetector.Detect(Series(900, 1300, 1000, 700, 600, 650));

        var airing = Assert.Single(events);
        Assert.Equal(Start.AddMinutes(5), airing.Start);
        Assert.Equal(Start.AddMinutes(20), airing.End);
        Assert.Equal(700, airing.DropPpm);
        Assert.Equal(TimeSpan.FromMinutes(15), airing.Duration);
    }

    [Fact]
    public void IgnoresFallsSmallerThanTheMinimumDrop()
    {
        Assert.Empty(AiringDetector.Detect(Series(900, 850, 800, 760, 760, 780)));
    }

    [Fact]
    public void FindsSeparateEventsInOrder()
    {
        var events = AiringDetector.Detect(Series(1200, 900, 600, 900, 1200, 1500, 1100, 800, 800));

        Assert.Equal(2, events.Count);
        Assert.True(events[0].Start < events[1].Start);
    }

    [Fact]
    public void ReturnsNothingForTooFewSamples()
    {
        Assert.Empty(AiringDetector.Detect(Series(1500, 700)));
    }
}
