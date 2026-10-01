using System.Buffers.Binary;
using System.Diagnostics;
using System.Threading.Channels;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;
using BleListener.Models;

namespace BleListener.Bluetooth;

/// <param name="SyncedThrough">Cursor to save. Null when nothing was synced or the download was incomplete.</param>
/// <param name="MissingRecords">Records the sensor never delivered; &gt; 0 means the cursor must not advance.</param>
public sealed record AranetHistorySyncResult(IReadOnlyList<Co2Sample> Samples, DateTime? SyncedThrough, int MissingRecords = 0);

public static class Aranet4HistorySync
{
    private static readonly Guid CurrentService = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");
    private static readonly Guid LegacyService = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");
    private static readonly Guid CommandCharacteristic = Guid.Parse("f0cd1402-95da-4f4b-9ac8-aa55d312af0c");
    private static readonly Guid TotalReadingsCharacteristic = Guid.Parse("f0cd2001-95da-4f4b-9ac8-aa55d312af0c");
    private static readonly Guid IntervalCharacteristic = Guid.Parse("f0cd2002-95da-4f4b-9ac8-aa55d312af0c");
    private static readonly Guid HistoryV1Characteristic = Guid.Parse("f0cd2003-95da-4f4b-9ac8-aa55d312af0c");
    private static readonly Guid SecondsSinceUpdateCharacteristic = Guid.Parse("f0cd2004-95da-4f4b-9ac8-aa55d312af0c");
    private static readonly Guid HistoryV2Characteristic = Guid.Parse("f0cd2005-95da-4f4b-9ac8-aa55d312af0c");
    /// <summary>Hard cap for one metric download.</summary>
    private static readonly TimeSpan TransferTimeout = TimeSpan.FromMinutes(2);

    /// <summary>A transfer that makes no progress for this long is abandoned instead of polled forever.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(20);

    public static Task<AranetHistorySyncResult> SyncAsync(ulong bluetoothAddress, IProgress<string>? progress = null) =>
        SyncAsync(bluetoothAddress, null, progress, CancellationToken.None);

    public static async Task<AranetHistorySyncResult> SyncAsync(
        ulong bluetoothAddress,
        DateTime? after,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress)
            ?? throw new InvalidOperationException("Could not open the Aranet4 Bluetooth device. Make sure it is nearby.");

        var pairing = device.DeviceInformation.Pairing;
        if (pairing.CanPair && !pairing.IsPaired)
        {
            progress?.Report("Pairing with Aranet4...");
            var pairResult = await pairing.PairAsync();
            if (pairResult.Status is not DevicePairingResultStatus.Paired and not DevicePairingResultStatus.AlreadyPaired)
                throw new InvalidOperationException($"Windows could not pair with the sensor ({pairResult.Status}).");
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Connecting to Aranet4...");
        var service = await GetAranetServiceAsync(device);
        using (service)
        {
            var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            EnsureSuccess(characteristicsResult.Status, "Could not read Aranet4 GATT characteristics");

            var characteristics = characteristicsResult.Characteristics.ToDictionary(characteristic => characteristic.Uuid);
            var command = GetRequired(characteristics, CommandCharacteristic, "command");
            var totalReadingsCharacteristic = GetRequired(characteristics, TotalReadingsCharacteristic, "record count");
            var intervalCharacteristic = GetRequired(characteristics, IntervalCharacteristic, "record interval");
            var ageCharacteristic = GetRequired(characteristics, SecondsSinceUpdateCharacteristic, "last-record age");

            var total = await ReadUInt16Async(totalReadingsCharacteristic);
            var interval = await ReadUInt16Async(intervalCharacteristic);
            var age = await ReadUInt16Async(ageCharacteristic);
            if (total == 0) return new AranetHistorySyncResult([], null);
            if (interval == 0) throw new InvalidOperationException("The sensor reported an invalid history interval.");

            var now = DateTime.Now;
            var latest = Co2Sample.ToWholeSecond(now.AddSeconds(-age));
            var first = latest.AddSeconds(-((long)total - 1) * interval);
            var startIndex = AranetHistoryProtocol.GetIncrementalStartIndex(after, first, interval, total);
            var downloadCount = total - startIndex + 1;
            progress?.Report($"Downloading {downloadCount:N0} new sensor records...");

            var historyV2 = characteristics.GetValueOrDefault(HistoryV2Characteristic);
            var co2 = await ReadMetricAsync(command, historyV2, characteristics, (byte)AranetHistoryParameter.Co2, total, startIndex, progress, cancellationToken);
            var temperature = await ReadMetricAsync(command, historyV2, characteristics, (byte)AranetHistoryParameter.Temperature, total, startIndex, progress, cancellationToken);
            var humidity = await ReadMetricAsync(command, historyV2, characteristics, (byte)AranetHistoryParameter.Humidity, total, startIndex, progress, cancellationToken);
            var pressure = await ReadMetricAsync(command, historyV2, characteristics, (byte)AranetHistoryParameter.Pressure, total, startIndex, progress, cancellationToken);

            var samples = new List<Co2Sample>(downloadCount);
            for (var index = startIndex; index <= total; index++)
            {
                var offset = index - startIndex;
                var time = first.AddSeconds((long)(index - 1) * interval);
                var sample = new Co2Sample(
                    time,
                    DecodeCo2(co2[offset]),
                    DecodeTemperature(temperature[offset]),
                    DecodeHumidity(humidity[offset]),
                    DecodePressure(pressure[offset]));
                if (sample.HasAnyMetric) samples.Add(sample);
            }

            // If any record never arrived, don't advance the cursor: the gap would otherwise be skipped forever.
            var missingRecords = new[] { co2, temperature, humidity, pressure }.Max(values => values.Count(value => value is null));
            return new AranetHistorySyncResult(samples, missingRecords == 0 ? latest : null, missingRecords);
        }
    }

    private static async Task<ushort?[]> ReadMetricAsync(
        GattCharacteristic command,
        GattCharacteristic? historyV2,
        IReadOnlyDictionary<Guid, GattCharacteristic> characteristics,
        byte parameter,
        ushort total,
        ushort startIndex,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return historyV2 is not null
                ? await ReadHistoryV2Async(command, historyV2, parameter, total, startIndex, progress, cancellationToken)
                : await ReadHistoryV1Async(command, GetRequired(characteristics, HistoryV1Characteristic, "history"), parameter, total, startIndex, progress, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own transfer timeout fired, not the user's Cancel button: report it as a timeout, not "task canceled".
            throw new TimeoutException("Downloading history from the sensor took too long and was stopped. Move closer to the sensor and try again.");
        }
    }

    private static int DecodeCo2(ushort? raw) => raw is { } value && value > 0 && (value & 0x8000) == 0 ? value : 0;

    private static decimal? DecodeTemperature(ushort? raw) =>
        raw is { } value && value > 0 && (value & 0x4000) == 0 ? value / 20m : null;

    private static decimal? DecodePressure(ushort? raw) =>
        raw is { } value && value > 0 && (value & 0x8000) == 0 ? value / 10m : null;

    private static int? DecodeHumidity(ushort? raw) => raw is { } value && value <= 100 ? value : null;

    private static async Task<GattDeviceService> GetAranetServiceAsync(BluetoothLEDevice device)
    {
        foreach (var serviceId in new[] { CurrentService, LegacyService })
        {
            var result = await device.GetGattServicesForUuidAsync(serviceId, BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success && result.Services.Count > 0)
                return result.Services[0];
            foreach (var unused in result.Services) unused.Dispose();
        }

        throw new InvalidOperationException("The Aranet4 history service was not found. Check that the sensor is awake and nearby.");
    }

    private static async Task<ushort?[]> ReadHistoryV2Async(
        GattCharacteristic command,
        GattCharacteristic history,
        byte parameter,
        ushort total,
        ushort startIndex,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var transfer = new HistoryTransfer(startIndex, total);
        var request = new byte[] { 0x61, parameter, (byte)startIndex, (byte)(startIndex >> 8) };
        await WriteAsync(command, request);

        using var timeout = CreateTransferToken(cancellationToken);
        var sinceProgress = Stopwatch.StartNew();
        while (!transfer.IsComplete)
        {
            timeout.Token.ThrowIfCancellationRequested();

            byte[] packet;
            try { packet = await ReadBytesAsync(history).WaitAsync(StallTimeout, timeout.Token); }
            catch (TimeoutException) { throw StalledTransfer(transfer); }

            var progressed = false;
            try
            {
                var page = AranetHistoryProtocol.ParseV2(packet, parameter);
                progressed = page.StartIndex != 0 && page.Count != 0 && transfer.Apply(page);
            }
            catch (FormatException)
            {
                // Stale or partial packet; handled as "no progress" below.
            }

            if (progressed)
            {
                sinceProgress.Restart();
                progress?.Report($"Downloaded {transfer.LastIndex:N0} of {total:N0} sensor readings...");
            }
            else
            {
                // Empty, stale or repeated page. Give the sensor a moment, but never wait on it forever.
                if (sinceProgress.Elapsed >= StallTimeout) throw StalledTransfer(transfer);
                await Task.Delay(100, timeout.Token);
            }
        }

        return transfer.Values;
    }

    private static async Task<ushort?[]> ReadHistoryV1Async(
        GattCharacteristic command,
        GattCharacteristic history,
        byte parameter,
        ushort total,
        ushort startIndex,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var transfer = new HistoryTransfer(startIndex, total);
        var packets = Channel.CreateUnbounded<byte[]>();
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (_, args) =>
        {
            try { packets.Writer.TryWrite(ToArray(args.CharacteristicValue)); }
            catch (ObjectDisposedException) { }
        };

        history.ValueChanged += handler;
        try
        {
            var notifyStatus = await history.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            EnsureSuccess(notifyStatus, "Could not subscribe to sensor history notifications");

            var request = new byte[8];
            request[0] = 0x82;
            request[1] = parameter;
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(4, 2), startIndex);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6, 2), total);
            await WriteAsync(command, request);

            using var timeout = CreateTransferToken(cancellationToken);
            var sinceProgress = Stopwatch.StartNew();
            while (!transfer.IsComplete)
            {
                byte[] packet;
                try { packet = await packets.Reader.ReadAsync(timeout.Token).AsTask().WaitAsync(StallTimeout, timeout.Token); }
                catch (TimeoutException) { throw StalledTransfer(transfer); }

                AranetHistoryPage page;
                try { page = AranetHistoryProtocol.ParseV1(packet, parameter); }
                catch (FormatException)
                {
                    if (sinceProgress.Elapsed >= StallTimeout) throw StalledTransfer(transfer);
                    continue;
                }

                // An empty page or an index past the end is the sensor's "no more data" marker.
                if (page.Count == 0 || page.StartIndex > total) break;

                if (transfer.Apply(page))
                {
                    sinceProgress.Restart();
                    progress?.Report($"Downloaded {transfer.LastIndex:N0} of {total:N0} sensor readings...");
                }
                else if (sinceProgress.Elapsed >= StallTimeout)
                {
                    throw StalledTransfer(transfer);
                }
            }
        }
        finally
        {
            history.ValueChanged -= handler;
            packets.Writer.TryComplete();
            // Best effort: if the sensor already disconnected this must not hide the real error.
            try
            {
                await history.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch (Exception) { }
        }

        return transfer.Values;
    }

    private static TimeoutException StalledTransfer(HistoryTransfer transfer) =>
        new($"The sensor stopped sending history at record {transfer.LastIndex:N0} of {transfer.Total:N0}. Move closer to the sensor and try again.");

    private static async Task<ushort> ReadUInt16Async(GattCharacteristic characteristic)
    {
        var bytes = await ReadBytesAsync(characteristic);
        if (bytes.Length < 2) throw new InvalidOperationException($"The sensor returned an invalid value for {characteristic.Uuid}.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    private static async Task<byte[]> ReadBytesAsync(GattCharacteristic characteristic)
    {
        var result = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
        EnsureSuccess(result.Status, $"Could not read GATT characteristic {characteristic.Uuid}");
        return ToArray(result.Value);
    }

    private static CancellationTokenSource CreateTransferToken(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TransferTimeout);
        return timeout;
    }

    private static async Task WriteAsync(GattCharacteristic characteristic, byte[] bytes)
    {
        var writer = new DataWriter();
        writer.WriteBytes(bytes);
        var status = await characteristic.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
        EnsureSuccess(status, "Could not send a history request to the sensor");
    }

    private static byte[] ToArray(IBuffer buffer)
    {
        using var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[checked((int)buffer.Length)];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static GattCharacteristic GetRequired(IReadOnlyDictionary<Guid, GattCharacteristic> characteristics, Guid id, string name) =>
        characteristics.TryGetValue(id, out var characteristic)
            ? characteristic
            : throw new InvalidOperationException($"The sensor does not expose the {name} characteristic.");

    private static void EnsureSuccess(GattCommunicationStatus status, string message)
    {
        if (status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"{message} ({status}).");
    }
}