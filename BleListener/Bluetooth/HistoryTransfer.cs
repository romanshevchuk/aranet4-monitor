namespace BleListener.Bluetooth;

/// <summary>
/// Collects the pages of one metric download and says whether each page moved the transfer forward.
/// The BLE loops use this to detect a stalled sensor instead of polling forever.
/// </summary>
public sealed class HistoryTransfer
{
    private readonly ushort startIndex;

    public HistoryTransfer(ushort startIndex, ushort total)
    {
        if (startIndex == 0 || startIndex > total) throw new ArgumentOutOfRangeException(nameof(startIndex));
        this.startIndex = startIndex;
        Total = total;
        LastIndex = startIndex - 1;
        Values = new ushort?[total - startIndex + 1];
    }

    public ushort Total { get; }

    /// <summary>One slot per requested record; null means the sensor never sent it.</summary>
    public ushort?[] Values { get; }

    /// <summary>Highest record index received so far.</summary>
    public int LastIndex { get; private set; }

    public bool IsComplete => LastIndex >= Total;

    public int MissingCount => Values.Count(value => value is null);

    /// <summary>Stores the page and returns true only if it advanced <see cref="LastIndex"/>.</summary>
    public bool Apply(AranetHistoryPage page)
    {
        var before = LastIndex;
        for (var offset = 0; offset < page.Values.Count; offset++)
        {
            var index = page.StartIndex + offset;
            if (index > Total) break;
            if (index >= startIndex) Values[index - startIndex] = page.Values[offset];
            LastIndex = Math.Max(LastIndex, index);
        }

        return LastIndex > before;
    }
}
