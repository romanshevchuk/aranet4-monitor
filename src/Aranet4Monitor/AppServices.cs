using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Application.History;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Bluetooth;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor;

public sealed class AppServices
{
    public AppServices()
    {
        HistoryStore = new JsonHistoryStore();
        PreferencesStore = new JsonPreferencesStore();
        Preferences = PreferencesStore.Load();
        HistorySyncService = new HistorySyncService(new Aranet4HistoryClient(), HistoryStore);
        SensorSource = new Aranet4SensorSource();
        SensorMonitor = new SensorMonitor();
    }

    public IHistoryStore HistoryStore { get; }

    public IPreferencesStore PreferencesStore { get; }

    public AppPreferences Preferences { get; }

    public HistorySyncService HistorySyncService { get; }

    public ISensorSource SensorSource { get; }

    public SensorMonitor SensorMonitor { get; }
}
