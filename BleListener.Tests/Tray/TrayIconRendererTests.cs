using BleListener;
using Xunit;

namespace BleListener.Tests;

public sealed class TrayIconRendererTests
{
    [Theory]
    [InlineData(0, "--")]
    [InlineData(415, "415")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.0k")]
    [InlineData(1280, "1.3k")]
    [InlineData(1949, "1.9k")]
    [InlineData(1950, "2.0k")]
    [InlineData(9949, "9.9k")]
    [InlineData(9950, "10k")]
    [InlineData(12000, "10k")]
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
}
