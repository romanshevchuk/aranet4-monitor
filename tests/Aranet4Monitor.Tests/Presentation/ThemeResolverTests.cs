using Aranet4Monitor.Presentation;
using Xunit;

namespace Aranet4Monitor.Tests.Presentation;

public sealed class ThemeResolverTests
{
    [Theory]
    [InlineData(AppTheme.Auto, true, false)]
    [InlineData(AppTheme.Auto, false, true)]
    [InlineData(AppTheme.Light, false, false)]
    [InlineData(AppTheme.Dark, true, true)]
    public void ResolvesTheModeAgainstTheWindowsSetting(AppTheme mode, bool windowsUsesLightApps, bool expectedDark)
    {
        Assert.Equal(expectedDark, ThemeResolver.IsDark(mode, windowsUsesLightApps));
    }
}
