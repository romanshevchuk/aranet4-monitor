using System.Buffers.Binary;

namespace BleListener.Bluetooth;

public readonly record struct AranetHistoryPage(byte Parameter, ushort StartIndex, IReadOnlyList<ushort> Values)
{
    public ushort Count => checked((ushort)Values.Count);
}

public static class AranetHistoryProtocol
{
    public static ushort GetIncrementalStartIndex(DateTime? latestSaved, DateTime firstReading, ushort intervalSeconds, ushort totalReadings)
    {
        if (totalReadings == 0) return 0;
        if (intervalSeconds == 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        if (latestSaved is null) return 1;

        var latestExistingIndex = (long)Math.Floor((latestSaved.Value - firstReading).TotalSeconds / intervalSeconds) + 1;
        return (ushort)Math.Clamp(latestExistingIndex - 1, 1, totalReadings);
    }

    public static AranetHistoryPage ParseV1(ReadOnlySpan<byte> packet, byte expectedParameter)
    {
        if (packet.Length < 4) throw new FormatException("History v1 packet is shorter than its header.");
        ValidateParameter(packet[0], expectedParameter);

        var start = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(1, 2));
        var count = packet[3];
        return new AranetHistoryPage(packet[0], start, ParseValues(packet[4..], count, expectedParameter));
    }

    public static AranetHistoryPage ParseV2(ReadOnlySpan<byte> packet, byte expectedParameter)
    {
        if (packet.Length < 10) throw new FormatException("History v2 packet is shorter than its header.");
        ValidateParameter(packet[0], expectedParameter);

        var start = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(7, 2));
        var count = packet[9];
        return new AranetHistoryPage(packet[0], start, ParseValues(packet[10..], count, expectedParameter));
    }

    private static IReadOnlyList<ushort> ParseValues(ReadOnlySpan<byte> data, byte count, byte parameter)
    {
        var width = parameter == (byte)AranetHistoryParameter.Humidity ? 1 : 2;
        if (data.Length < count * width)
            throw new FormatException("History packet payload is shorter than its declared sample count.");

        var values = new ushort[count];
        for (var index = 0; index < count; index++)
            values[index] = width == 1 ? data[index] : BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(index * width, width));
        return values;
    }

    private static void ValidateParameter(byte actual, byte expected)
    {
        if (actual != expected)
            throw new FormatException($"History packet parameter {actual} did not match requested parameter {expected}.");
    }
}

public enum AranetHistoryParameter : byte
{
    Temperature = 1,
    Humidity = 2,
    Pressure = 3,
    Co2 = 4,
}