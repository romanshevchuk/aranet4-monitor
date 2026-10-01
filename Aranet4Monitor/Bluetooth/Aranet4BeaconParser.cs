using System.Buffers.Binary;

namespace Aranet4Monitor.Bluetooth;

/// <summary>Decoder for the Aranet4 Smart Home Integrations BLE beacon payload.</summary>
public static class Aranet4BeaconParser
{
    public const ushort AranetCompanyId = 0x0702;
    public const int MinimumMeasurementPayloadLength = 15;

    public static bool TryParse(byte[] payload, out Aranet4Measurement? measurement, out string message)
    {
        measurement = null;
        if (payload.Length < MinimumMeasurementPayloadLength)
        {
            message = $"Aranet manufacturer payload is {payload.Length} byte(s); {MinimumMeasurementPayloadLength} are needed for core measurements.";
            return false;
        }

        if ((payload[0] & 0x20) == 0)
        {
            message = "Smart Home Integrations is disabled on the Aranet4. Enable it in the Aranet Home app to broadcast readable measurements.";
            return false;
        }

        var co2 = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(8, 2));
        var temperature = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10, 2)) / 20m;
        var pressure = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(12, 2)) / 10m;
        var humidity = payload[14];
        if (co2 is < 1 or > 10_000 || temperature is < -40 or > 60 || pressure is < 300 or > 1_200 || humidity > 100)
        {
            message = "The Aranet manufacturer block has values outside expected measurement ranges.";
            return false;
        }

        var battery = payload.Length > 15 ? payload[15] : (byte?)null;
        var status = payload.Length > 16 ? payload[16] : (byte?)null;
        var intervalSeconds = payload.Length >= 19 ? BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(17, 2)) : (ushort?)null;
        var ageSeconds = payload.Length >= 21 ? BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(19, 2)) : (ushort?)null;
        measurement = new Aranet4Measurement(
            payload.Length >= 4 ? $"{payload[3]}.{payload[2]}.{payload[1]}" : "—", co2, temperature, pressure, humidity,
            battery, status, intervalSeconds, ageSeconds);
        message = "Measurement decoded from Smart Home Integrations beacon.";
        return true;
    }
}

public sealed record Aranet4Measurement(string Firmware, ushort Co2, decimal TemperatureCelsius,
    decimal PressureHpa, byte HumidityPercent, byte? BatteryPercent, byte? Status,
    ushort? IntervalSeconds, ushort? AgeSeconds);
