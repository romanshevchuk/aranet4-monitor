using System.Buffers.Binary;
using Xunit;

namespace Aranet4Monitor.Core.Tests.Protocol;

public sealed class Aranet4BeaconParserTests
{
    [Fact]
    public void ParsesCoreAndOptionalMeasurementFields()
    {
        var payload = CreatePayload();

        var parsed = Aranet4BeaconParser.TryParse(payload, out var measurement, out var message);

        Assert.True(parsed, message);
        Assert.NotNull(measurement);
        Assert.Equal("3.2.1", measurement.Firmware);
        Assert.Equal((ushort)950, measurement.Co2);
        Assert.Equal(22m, measurement.TemperatureCelsius);
        Assert.Equal(999m, measurement.PressureHpa);
        Assert.Equal((byte)42, measurement.HumidityPercent);
        Assert.Equal((byte)88, measurement.BatteryPercent);
        Assert.Equal((byte)1, measurement.Status);
        Assert.Equal((ushort)300, measurement.IntervalSeconds);
        Assert.Equal((ushort)20, measurement.AgeSeconds);
    }

    [Fact]
    public void RejectsPayloadsShorterThanTheCoreMeasurementLength()
    {
        var parsed = Aranet4BeaconParser.TryParse(new byte[14], out var measurement, out var message);

        Assert.False(parsed);
        Assert.Null(measurement);
        Assert.Contains("15", message);
    }

    [Fact]
    public void RejectsPayloadWhenSmartHomeIntegrationsAreDisabled()
    {
        var payload = CreatePayload();
        payload[0] = 0;

        var parsed = Aranet4BeaconParser.TryParse(payload, out var measurement, out var message);

        Assert.False(parsed);
        Assert.Null(measurement);
        Assert.Contains("disabled", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsMeasurementValuesOutsideExpectedRanges()
    {
        var payload = CreatePayload();
        payload[14] = 101;

        var parsed = Aranet4BeaconParser.TryParse(payload, out var measurement, out var message);

        Assert.False(parsed);
        Assert.Null(measurement);
        Assert.Contains("outside expected", message);
    }

    private static byte[] CreatePayload()
    {
        var payload = new byte[21];
        payload[0] = 0x20;
        payload[1] = 1;
        payload[2] = 2;
        payload[3] = 3;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), 950);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10, 2), 440);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12, 2), 9990);
        payload[14] = 42;
        payload[15] = 88;
        payload[16] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(17, 2), 300);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(19, 2), 20);
        return payload;
    }
}
