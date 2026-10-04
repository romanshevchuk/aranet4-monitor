using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Application.History;

/// <param name="SyncedThrough">Cursor to save. Null when nothing was synced or the download was incomplete.</param>
/// <param name="MissingRecords">Records the sensor never delivered; positive values prevent the cursor from advancing.</param>
public sealed record HistoryDownloadResult(
    IReadOnlyList<Co2Sample> Samples,
    DateTime? SyncedThrough,
    int MissingRecords = 0);

public sealed record HistoryMergeResult(int AddedSamples, IReadOnlyList<Co2Sample> Samples);

public sealed record HistorySyncOutcome(int AddedSamples, int MissingRecords);
