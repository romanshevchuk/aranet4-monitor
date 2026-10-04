using Aranet4Monitor.Abstractions;

namespace Aranet4Monitor.Application.History;

public sealed class HistorySyncService(IHistoryClient historyClient, IHistoryStore historyStore)
{
    public DateTime? LoadSyncCursor(string deviceAddress) => historyStore.LoadSyncCursor(deviceAddress);

    public async Task<HistorySyncOutcome> SyncAsync(
        string deviceAddress,
        ulong bluetoothAddress,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var cursor = historyStore.LoadSyncCursor(deviceAddress);
        var download = await historyClient.DownloadAsync(bluetoothAddress, cursor, progress, cancellationToken);
        var merged = HistoryMerger.Merge(historyStore.LoadHistory(deviceAddress), download.Samples);

        if (!historyStore.SaveHistory(deviceAddress, merged.Samples))
        {
            throw new IOException("Downloaded history could not be saved locally. The sync cursor was not advanced; retry after checking disk space.");
        }

        if (download.MissingRecords == 0
            && download.SyncedThrough is { } syncedThrough
            && !historyStore.SaveSyncCursor(deviceAddress, syncedThrough))
        {
            throw new IOException("The sync cursor could not be saved. Retrying will safely import the overlap again.");
        }

        return new HistorySyncOutcome(merged.AddedSamples, download.MissingRecords, merged.Samples);
    }
}
