using BleListener.Bluetooth;
using Xunit;

namespace BleListener.Tests.Bluetooth;

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
