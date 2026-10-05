using System.Text.Json.Serialization;
using Aranet4Monitor.Application.Alerts;
using Aranet4Monitor.Presentation;

namespace Aranet4Monitor.Storage;

public sealed class AppPreferences
{
    public int AlertThresholdPpm { get; set; } = Co2AlertService.RecommendedVentilationThresholdPpm;

    public int AlertDurationMinutes { get; set; } = 10;

    [JsonConverter(typeof(JsonStringEnumConverter<TemperatureUnit>))]
    public TemperatureUnit TemperatureDisplayUnit { get; set; } = TemperatureUnit.Celsius;

    [JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
    public AppTheme Theme { get; set; } = AppTheme.Auto;

    public DateTime? LastCo2AlertAt { get; set; }

    public int? LastCo2AlertPpm { get; set; }

    /// <summary>True once the "I live in the tray now" hint has been shown.</summary>
    public bool TrayHintShown { get; set; }

    /// <summary>Draw the CO₂ number on the tray icon (otherwise the icon is just a coloured disc).</summary>
    public bool TrayShowNumber { get; set; } = true;

    /// <summary>Show alerts as the app's own large pop-up instead of the small Windows notification.</summary>
    public bool LargePopups { get; set; } = true;

}
