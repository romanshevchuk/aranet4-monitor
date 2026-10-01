using BleListener;
using Xunit;

namespace BleListener.Tests;

public sealed class TrayIconRendererTests
{
    [Theory]
    [InlineData(0, "--")]
    [InlineData(415, "415")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.0")]
    [InlineData(1280, "1.3")]
    [InlineData(1949, "1.9")]
    [InlineData(1950, "2.0")]
    [InlineData(9949, "9.9")]
    [InlineData(9950, "10")]
    [InlineData(12000, "10")]
    public void FormatsTheNumberToFitASmallIcon(int ppm, string expected)
    {
        Assert.Equal(expected, TrayIconRenderer.FormatLabel(ppm));
    }

    [Theory]
    [InlineData(450, false)]
    [InlineData(1280, false)]
    [InlineData(2300, false)]
    [InlineData(820, true)]
    public void RendersAnIcon(int ppm, bool stale)
    {
        using var icon = TrayIconRenderer.Create(ppm, stale);

        Assert.True(icon.Width >= 16);
        Assert.True(icon.Height >= 16);
    }

    [Fact]
    public void RendersAColourOnlyIcon()
    {
        using var icon = TrayIconRenderer.Create(1280, stale: false, showNumber: false);

        Assert.True(icon.Width >= 16);
        Assert.True(icon.Height >= 16);
    }
}
