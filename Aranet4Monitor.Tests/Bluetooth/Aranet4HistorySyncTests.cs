using Aranet4Monitor.Bluetooth;
using Xunit;

namespace Aranet4Monitor.Tests.Bluetooth;

public sealed class Aranet4HistorySyncTests
{
    [Fact]
    public async Task HistorySyncHonorsCancellationBeforeConnecting()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Aranet4HistorySync.SyncAsync(0, null, null, cancellation.Token));
    }
}
