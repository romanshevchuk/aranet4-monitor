using System.Text.Json;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Storage;
using Xunit;

namespace Aranet4Monitor.Tests.Storage;

public sealed class AppPreferencesTests
{
    [Fact]
    public void OlderSettingsWithoutTemperatureUnitDefaultToCelsius()
    {
        var preferences = JsonSerializer.Deserialize<AppPreferences>("{\"AlertThresholdPpm\":1200}")!;

        Assert.Equal(TemperatureUnit.Celsius, preferences.TemperatureDisplayUnit);
    }

    [Fact]
    public void TemperatureUnitPersistsAsAReadableSetting()
    {
        var preferences = new AppPreferences { TemperatureDisplayUnit = TemperatureUnit.Fahrenheit };

        var json = JsonSerializer.Serialize(preferences);
        var loaded = JsonSerializer.Deserialize<AppPreferences>(json)!;

        Assert.Contains("\"TemperatureDisplayUnit\":\"Fahrenheit\"", json);
        Assert.Equal(TemperatureUnit.Fahrenheit, loaded.TemperatureDisplayUnit);
    }

    [Fact]
    public void OlderSettingsWithoutThemeFollowWindows()
    {
        var preferences = JsonSerializer.Deserialize<AppPreferences>("{\"AlertThresholdPpm\":1200}")!;

        Assert.Equal(AppTheme.Auto, preferences.Theme);
    }

    [Fact]
    public void ThemePersistsAsAReadableSetting()
    {
        var json = JsonSerializer.Serialize(new AppPreferences { Theme = AppTheme.Dark });
        var loaded = JsonSerializer.Deserialize<AppPreferences>(json)!;

        Assert.Contains("\"Theme\":\"Dark\"", json);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
    }
}
