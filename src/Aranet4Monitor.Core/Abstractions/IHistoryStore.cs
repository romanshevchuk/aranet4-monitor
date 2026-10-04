using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Abstractions;

public interface IHistoryStore
{
    DateTime? LoadSyncCursor(string deviceAddress);

    IReadOnlyList<Co2Sample> LoadHistory(string deviceAddress);

    bool SaveHistory(string deviceAddress, IReadOnlyList<Co2Sample> samples);

    bool SaveSyncCursor(string deviceAddress, DateTime syncedThrough);
}
