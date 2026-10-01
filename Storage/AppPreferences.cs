using System.Text.Json;

namespace BleListener;

public sealed class AppPreferences
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AranetHome", "settings.json");

    public int AlertThresholdPpm { get; set; } = Co2AlertService.RecommendedVentilationThresholdPpm;
    public int AlertDurationMinutes { get; set; } = 10;
    public DateTime? LastCo2AlertAt { get; set; }
    public int? LastCo2AlertPpm { get; set; }

    /// <summary>True once the "I live in the tray now" hint has been shown.</summary>
    public bool TrayHintShown { get; set; }

    public static AppPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(FilePath)) ?? new AppPreferences();
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