using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Protocol.Aranet4;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace Aranet4Monitor.Bluetooth;

public sealed class Aranet4SensorSource : ISensorSource
{
    private const string WaitingForManufacturerBeacon = "Waiting for an Aranet manufacturer beacon.";

    private readonly ConcurrentDictionary<ulong, byte> knownAddresses = new();
    private BluetoothLEAdvertisementWatcher? watcher;

    public event EventHandler<SensorAdvertisementReceivedEventArgs>? AdvertisementReceived;

    public event EventHandler<SensorSourceStoppedEventArgs>? Stopped;

    public bool IsActive => watcher is not null;

    public void RegisterKnownAddress(ulong bluetoothAddress) => knownAddresses.TryAdd(bluetoothAddress, 0);

    public void ClearKnownAddresses() => knownAddresses.Clear();

    public void Start()
    {
        if (watcher is not null)
        {
            return;
        }

        var newWatcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        newWatcher.Received += Watcher_Received;
        newWatcher.Stopped += Watcher_Stopped;

        try
        {
            newWatcher.Start();
            watcher = newWatcher;
        }
        catch
        {
            newWatcher.Received -= Watcher_Received;
            newWatcher.Stopped -= Watcher_Stopped;
            throw;
        }
    }

    public void Stop()
    {
        var activeWatcher = watcher;
        if (activeWatcher is null)
        {
            return;
        }

        watcher = null;
        activeWatcher.Received -= Watcher_Received;
        activeWatcher.Stopped -= Watcher_Stopped;
        if (activeWatcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            activeWatcher.Stop();
        }
    }

    public void Dispose() => Stop();

    private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var advertisement = args.Advertisement;
        var manufacturerBlocks = advertisement.ManufacturerData
            .Select(block => new ManufacturerBlock(block.CompanyId, ToBytes(block.Data)))
            .ToArray();
        var isAranet = Aranet4AdvertisementFilter.IsCandidate(
            advertisement.LocalName,
            advertisement.ServiceUuids,
            manufacturerBlocks.Select(block => (block.CompanyId, block.Data.Length)));

        if (!isAranet && !knownAddresses.ContainsKey(args.BluetoothAddress))
        {
            return;
        }

        var packet = FormatPacket(args, manufacturerBlocks);
        var matchingBlock = manufacturerBlocks.FirstOrDefault(block => block.CompanyId == Aranet4BeaconParser.AranetCompanyId);
        Aranet4Measurement? measurement = null;
        var decodeMessage = WaitingForManufacturerBeacon;
        if (matchingBlock is not null)
        {
            Aranet4BeaconParser.TryParse(matchingBlock.Data, out measurement, out decodeMessage);
        }

        AdvertisementReceived?.Invoke(this, new SensorAdvertisementReceivedEventArgs(
            args.BluetoothAddress,
            FormatAddress(args.BluetoothAddress),
            args.Timestamp.LocalDateTime,
            args.RawSignalStrengthInDBm,
            advertisement.LocalName,
            args.AdvertisementType == BluetoothLEAdvertisementType.ScanResponse,
            packet,
            measurement,
            decodeMessage));
    }

    private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (watcher != sender)
        {
            return;
        }

        watcher = null;
        sender.Received -= Watcher_Received;
        sender.Stopped -= Watcher_Stopped;
        Stopped?.Invoke(this, new SensorSourceStoppedEventArgs(
            args.Error == BluetoothError.RadioNotAvailable,
            args.Error == BluetoothError.Success,
            args.Error.ToString()));
    }

    private static string FormatPacket(BluetoothLEAdvertisementReceivedEventArgs args, IEnumerable<ManufacturerBlock> manufacturerBlocks)
    {
        var advertisement = args.Advertisement;
        var builder = new StringBuilder();
        builder.AppendLine($"Type: {args.AdvertisementType}");
        builder.AppendLine($"Address: {FormatAddress(args.BluetoothAddress)}");
        builder.AppendLine($"RSSI: {args.RawSignalStrengthInDBm} dBm");
        if (!string.IsNullOrWhiteSpace(advertisement.LocalName))
        {
            builder.AppendLine($"Name: {advertisement.LocalName}");
        }

        if (advertisement.ServiceUuids.Count > 0)
        {
            builder.AppendLine($"Services: {string.Join(", ", advertisement.ServiceUuids)}");
        }

        foreach (var block in manufacturerBlocks)
        {
            builder.AppendLine($"Manufacturer 0x{block.CompanyId:X4}: {Convert.ToHexString(block.Data)}");
        }

        foreach (var section in advertisement.DataSections)
        {
            builder.AppendLine($"AD 0x{section.DataType:X2}: {Convert.ToHexString(ToBytes(section.Data))}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatAddress(ulong address) => string.Join(":", Enumerable.Range(0, 6)
        .Select(index => ((address >> ((5 - index) * 8)) & 0xFF).ToString("X2", CultureInfo.InvariantCulture)));

    private static byte[] ToBytes(IBuffer buffer)
    {
        using var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[checked((int)buffer.Length)];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private sealed record ManufacturerBlock(ushort CompanyId, byte[] Data);
}
