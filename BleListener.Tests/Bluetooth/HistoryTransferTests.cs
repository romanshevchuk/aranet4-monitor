using BleListener;
using Xunit;

namespace BleListener.Tests;

public sealed class HistoryTransferTests
{
    [Fact]
    public void ReportsNoProgressForRepeatedOrStalePages()
    {
        // Downloading records 3..6; the sensor first replays old records, then repeats a page.
        var transfer = new HistoryTransfer(3, 6);

        Assert.False(transfer.Apply(new AranetHistoryPage(4, 1, [800, 810])));   // before the requested start
        Assert.Equal(2, transfer.LastIndex);

        Assert.True(transfer.Apply(new AranetHistoryPage(4, 3, [830, 840])));
        Assert.False(transfer.Apply(new AranetHistoryPage(4, 3, [830, 840])));   // repeated page: no progress
        Assert.False(transfer.IsComplete);
        Assert.Equal(2, transfer.MissingCount);
    }

    [Fact]
    public void CompletesAndIgnoresRecordsBeyondTheReportedTotal()
    {
        var transfer = new HistoryTransfer(3, 6);

        Assert.True(transfer.Apply(new AranetHistoryPage(4, 3, [830, 840])));
        Assert.True(transfer.Apply(new AranetHistoryPage(4, 5, [850, 860, 870, 880]))); // 7 and 8 arrived after we read the total

        Assert.True(transfer.IsComplete);
        Assert.Equal(0, transfer.MissingCount);
        Assert.Equal(new ushort?[] { 830, 840, 850, 860 }, transfer.Values);
    }

    [Fact]
    public void CountsRecordsTheSensorSkipped()
    {
        var transfer = new HistoryTransfer(1, 4);

        Assert.True(transfer.Apply(new AranetHistoryPage(4, 1, [800])));
        Assert.True(transfer.Apply(new AranetHistoryPage(4, 4, [900])));   // jumped over 2 and 3

        Assert.True(transfer.IsComplete);
        Assert.Equal(2, transfer.MissingCount);
    }
}
