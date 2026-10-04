using Aranet4Monitor.Abstractions;

namespace Aranet4Monitor.Storage;

public sealed class JsonHistoryStore : IHistoryStore
{
    public DateTime? LoadSyncCursor(string deviceAddress) => HistoryStore.LoadSyncCursor(deviceAddress);

    public IReadOnlyList<Co2Sample> LoadHistory(string deviceAddress) => HistoryStore.Load(deviceAddress);

    public bool SaveHistory(string deviceAddress, IReadOnlyList<Co2Sample> samples) =>
        HistoryStore.Save(deviceAddress, samples);

    public bool SaveSyncCursor(string deviceAddress, DateTime syncedThrough) =>
        HistoryStore.SaveSyncCursor(deviceAddress, syncedThrough);
}
