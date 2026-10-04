using System.Text.Json;

namespace Aranet4Monitor.Storage;

public sealed class JsonPreferencesStore : IPreferencesStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AranetHome", "settings.json");

    public AppPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(FilePath)) ?? new AppPreferences();
            }
        }
        catch
        {
        }

        return new AppPreferences();
    }

    public void Save(AppPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch
        {
        }
    }
}
