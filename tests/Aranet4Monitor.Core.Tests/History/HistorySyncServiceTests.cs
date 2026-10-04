using Xunit;

namespace Aranet4Monitor.Core.Tests.History;

public sealed class HistorySyncServiceTests
{
    [Fact]
    public async Task CompleteDownloadPersistsHistoryBeforeAdvancingCursor()
    {
        var cursor = new DateTime(2026, 10, 1, 12, 0, 0);
        var store = new FakeHistoryStore();
        var client = new FakeHistoryClient(new HistoryDownloadResult([], cursor));
        var service = new HistorySyncService(client, store);

        var result = await service.SyncAsync("sensor", 123, _ => new HistoryMergeResult(2, []));

        Assert.Equal(2, result.AddedSamples);
        Assert.Equal(cursor, store.SavedCursor);
        Assert.Equal(new[] { "history", "cursor" }, store.SaveOrder);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0), client.RequestedCursor);
    }

    [Fact]
    public async Task IncompleteDownloadPersistsPartialHistoryWithoutAdvancingCursor()
    {
        var cursor = new DateTime(2026, 10, 1, 12, 0, 0);
        var store = new FakeHistoryStore();
        var client = new FakeHistoryClient(new HistoryDownloadResult([], cursor, MissingRecords: 1));
        var service = new HistorySyncService(client, store);

        var result = await service.SyncAsync("sensor", 123, _ => new HistoryMergeResult(1, []));

        Assert.Equal(1, result.MissingRecords);
        Assert.True(store.HistorySaved);
        Assert.Null(store.SavedCursor);
        Assert.Equal(new[] { "history" }, store.SaveOrder);
    }

    [Fact]
    public async Task HistorySaveFailurePreventsCursorAdvancement()
    {
        var cursor = new DateTime(2026, 10, 1, 12, 0, 0);
        var store = new FakeHistoryStore { SaveHistoryResult = false };
        var client = new FakeHistoryClient(new HistoryDownloadResult([], cursor));
        var service = new HistorySyncService(client, store);

        await Assert.ThrowsAsync<IOException>(() =>
            service.SyncAsync("sensor", 123, _ => new HistoryMergeResult(0, [])));

        Assert.Null(store.SavedCursor);
        Assert.Equal(new[] { "history" }, store.SaveOrder);
    }

    [Fact]
    public async Task CancelledDownloadDoesNotPersistHistoryOrAdvanceCursor()
    {
        var store = new FakeHistoryStore();
        var client = new FakeHistoryClient(new OperationCanceledException());
        var service = new HistorySyncService(client, store);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SyncAsync("sensor", 123, _ => throw new Xunit.Sdk.XunitException("The merge callback should not run.")));

        Assert.False(store.HistorySaved);
        Assert.Null(store.SavedCursor);
        Assert.Empty(store.SaveOrder);
    }

    [Fact]
    public async Task CursorSaveFailureReportsFailureAfterHistoryIsPersisted()
    {
        var cursor = new DateTime(2026, 10, 1, 12, 0, 0);
        var store = new FakeHistoryStore { SaveCursorResult = false };
        var client = new FakeHistoryClient(new HistoryDownloadResult([], cursor));
        var service = new HistorySyncService(client, store);

        await Assert.ThrowsAsync<IOException>(() =>
            service.SyncAsync("sensor", 123, _ => new HistoryMergeResult(0, [])));

        Assert.True(store.HistorySaved);
        Assert.Equal(new[] { "history", "cursor" }, store.SaveOrder);
    }

    private sealed class FakeHistoryClient(HistoryDownloadResult result) : IHistoryClient
    {
        private readonly Exception? error = null;

        public FakeHistoryClient(Exception error) : this(new HistoryDownloadResult([], null))
        {
            this.error = error;
        }

        public DateTime? RequestedCursor { get; private set; }

        public Task<HistoryDownloadResult> DownloadAsync(
            ulong bluetoothAddress,
            DateTime? after,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            RequestedCursor = after;
            return error is null ? Task.FromResult(result) : Task.FromException<HistoryDownloadResult>(error);
        }
    }

    private sealed class FakeHistoryStore : IHistoryStore
    {
        public bool SaveHistoryResult { get; init; } = true;

        public bool SaveCursorResult { get; init; } = true;

        public bool HistorySaved { get; private set; }

        public DateTime? SavedCursor { get; private set; }

        public List<string> SaveOrder { get; } = [];

        public DateTime? LoadSyncCursor(string deviceAddress) => new(2026, 9, 1, 0, 0, 0);

        public IReadOnlyList<Co2Sample> LoadHistory(string deviceAddress) => [];

        public bool SaveHistory(string deviceAddress, IReadOnlyList<Co2Sample> samples)
        {
            SaveOrder.Add("history");
            HistorySaved = SaveHistoryResult;
            return SaveHistoryResult;
        }

        public bool SaveSyncCursor(string deviceAddress, DateTime syncedThrough)
        {
            SaveOrder.Add("cursor");
            if (SaveCursorResult)
            {
                SavedCursor = syncedThrough;
            }

            return SaveCursorResult;
        }
    }
}
