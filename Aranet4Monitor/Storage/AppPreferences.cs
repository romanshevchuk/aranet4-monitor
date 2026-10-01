using System.Text.Json;
using System.Text.Json.Serialization;
using Aranet4Monitor.Alerts;
using Aranet4Monitor.Models;

namespace Aranet4Monitor.Storage;

public sealed class AppPreferences
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AranetHome", "settings.json");

    public int AlertThresholdPpm { get; set; } = Co2AlertService.RecommendedVentilationThresholdPpm;
    public int AlertDurationMinutes { get; set; } = 10;
    [JsonConverter(typeof(JsonStringEnumConverter<TemperatureUnit>))]
    public TemperatureUnit TemperatureDisplayUnit { get; set; } = TemperatureUnit.Celsius;
    public DateTime? LastCo2AlertAt
    {
        get; set;
    }
    public int? LastCo2AlertPpm
    {
        get; set;
    }

    /// <summary>True once the "I live in the tray now" hint has been shown.</summary>
    public bool TrayHintShown
    {
        get; set;
    }

    /// <summary>Draw the CO₂ number on the tray icon (otherwise the icon is just a coloured disc).</summary>
    public bool TrayShowNumber { get; set; } = true;

    /// <summary>Show alerts as the app's own large pop-up instead of the small Windows notification.</summary>
    public bool LargePopups { get; set; } = true;

    public static AppPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(FilePath)) ?? new AppPreferences();
            }
        }
        catch { }
        return new AppPreferences();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch { }
    }
}
