namespace Aranet4Monitor.Storage;

public interface IPreferencesStore
{
    AppPreferences Load();

    void Save(AppPreferences preferences);
}
