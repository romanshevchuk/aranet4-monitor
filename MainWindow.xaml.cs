using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace BleListener;

public partial class MainWindow : Window
{
    private static readonly Guid AranetServiceUuid = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");
    private readonly Dictionary<ulong, Aranet4Device> _devicesByAddress = new();
    private BluetoothLEAdvertisementWatcher? _watcher;

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Closed += (_, _) => StopWatching();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
            _watcher.Received += Watcher_Received;
            _watcher.Stopped += Watcher_Stopped;
            _watcher.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusText.Text = "Listening for Aranet4 beacon packets…";
        }
        catch (Exception ex) { StatusText.Text = $"Could not start: {ex.Message}"; }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => StopWatching();

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _devicesByAddress.Clear();
        Devices.Clear();
        ClearDetails();
    }

    private void StopWatching()
    {
        if (_watcher is null) return;
        _watcher.Received -= Watcher_Received;
        _watcher.Stopped -= Watcher_Stopped;
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started) _watcher.Stop();
        _watcher = null;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        StatusText.Text = "Stopped";
    }

    private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (_watcher == sender)
            {
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusText.Text = $"Listener stopped: {args.Error}";
            }
        });

    private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var advertisement = args.Advertisement;
        var manufacturerBlocks = advertisement.ManufacturerData
            .Select(block => new ManufacturerBlock(block.CompanyId, ToBytes(block.Data))).ToArray();
        var hasAranetManufacturerBlock = manufacturerBlocks.Any(block => block.CompanyId == Aranet4BeaconParser.AranetCompanyId);
        var isAranet = advertisement.LocalName.StartsWith("Aranet4", StringComparison.OrdinalIgnoreCase)
            || advertisement.ServiceUuids.Contains(AranetServiceUuid) || hasAranetManufacturerBlock;
        if (!isAranet && !_devicesByAddress.ContainsKey(args.BluetoothAddress)) return;

        var packet = FormatPacket(args, manufacturerBlocks);
        var matchingBlock = manufacturerBlocks.FirstOrDefault(block => block.CompanyId == Aranet4BeaconParser.AranetCompanyId);
        Aranet4Measurement? measurement = null;
        var decodeMessage = "Waiting for an Aranet manufacturer beacon.";
        if (matchingBlock is not null) Aranet4BeaconParser.TryParse(matchingBlock.Data, out measurement, out decodeMessage);
        Dispatcher.InvokeAsync(() => UpdateDevice(args, packet, measurement, decodeMessage));
    }

    private void UpdateDevice(BluetoothLEAdvertisementReceivedEventArgs args, string packet, Aranet4Measurement? measurement, string decodeMessage)
    {
        if (!_devicesByAddress.TryGetValue(args.BluetoothAddress, out var device))
        {
            device = new Aranet4Device { Address = FormatAddress(args.BluetoothAddress) };
            _devicesByAddress.Add(args.BluetoothAddress, device);
            Devices.Add(device);
        }

        var advertisement = args.Advertisement;
        device.LastSeen = args.Timestamp.LocalDateTime;
        device.Rssi = args.RawSignalStrengthInDBm;
        device.Packets++;
        if (!string.IsNullOrWhiteSpace(advertisement.LocalName)) device.Name = advertisement.LocalName;
        if (args.AdvertisementType == BluetoothLEAdvertisementType.ScanResponse) device.LastScanResponse = packet;
        else device.LastAdvertisement = packet;

        if (measurement is not null)
        {
            device.Firmware = measurement.Firmware;
            device.Co2 = $"{measurement.Co2:N0} ppm";
            device.Temperature = $"{measurement.TemperatureCelsius:0.0} °C";
            device.Pressure = $"{measurement.PressureHpa:0.0} hPa";
            device.Humidity = $"{measurement.HumidityPercent}%";
            device.Battery = measurement.BatteryPercent is null ? "—" : $"{measurement.BatteryPercent}%";
            device.MeasurementInterval = measurement.IntervalSeconds is null ? "—" : $"{measurement.IntervalSeconds} s";
            device.MeasurementAge = measurement.AgeSeconds is null ? "—" : $"{measurement.AgeSeconds} s";
            device.IntegrationState = "Live Smart Home beacon decoded";
        }
        else if (decodeMessage != "Waiting for an Aranet manufacturer beacon.") device.IntegrationState = decodeMessage;

        StatusText.Text = $"Listening — {Devices.Count} Aranet4 device(s), last packet {device.Name}";
        if (DevicesGrid.SelectedItem == device) ShowDetails(device);
    }

    private void DevicesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DevicesGrid.SelectedItem is Aranet4Device device) ShowDetails(device);
    }

    private void ShowDetails(Aranet4Device device)
    {
        Co2Text.Text = device.Co2;
        TemperatureText.Text = device.Temperature;
        HumidityText.Text = device.Humidity;
        PressureText.Text = device.Pressure;
        DeviceInfoText.Text = $"{device.Name} · {device.Address} · RSSI {device.Rssi} dBm · firmware {device.Firmware} · battery {device.Battery} · measurement age {device.MeasurementAge} · interval {device.MeasurementInterval}.{Environment.NewLine}{device.IntegrationState}";
        RawPacketText.Text = $"ADVERTISEMENT{Environment.NewLine}{device.LastAdvertisement}{Environment.NewLine}{Environment.NewLine}SCAN RESPONSE{Environment.NewLine}{device.LastScanResponse}";
    }

    private void ClearDetails()
    {
        Co2Text.Text = TemperatureText.Text = HumidityText.Text = PressureText.Text = "—";
        DeviceInfoText.Text = "Select a device to inspect its beacon captures.";
        RawPacketText.Clear();
    }

    private static string FormatPacket(BluetoothLEAdvertisementReceivedEventArgs args, IEnumerable<ManufacturerBlock> manufacturerBlocks)
    {
        var advertisement = args.Advertisement;
        var builder = new StringBuilder();
        builder.AppendLine($"Type: {args.AdvertisementType}");
        builder.AppendLine($"Address: {FormatAddress(args.BluetoothAddress)}");
        builder.AppendLine($"RSSI: {args.RawSignalStrengthInDBm} dBm");
        if (!string.IsNullOrWhiteSpace(advertisement.LocalName)) builder.AppendLine($"Name: {advertisement.LocalName}");
        if (advertisement.ServiceUuids.Count > 0) builder.AppendLine($"Services: {string.Join(", ", advertisement.ServiceUuids)}");
        foreach (var block in manufacturerBlocks) builder.AppendLine($"Manufacturer 0x{block.CompanyId:X4}: {Convert.ToHexString(block.Data)}");
        foreach (var section in advertisement.DataSections) builder.AppendLine($"AD 0x{section.DataType:X2}: {Convert.ToHexString(ToBytes(section.Data))}");
        return builder.ToString().TrimEnd();
    }

    private static string FormatAddress(ulong address) => string.Join(":", Enumerable.Range(0, 6)
        .Select(index => ((address >> ((5 - index) * 8)) & 0xFF).ToString("X2", CultureInfo.InvariantCulture)));

    private static byte[] ToBytes(IBuffer buffer)
    {
        var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[checked((int)buffer.Length)];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private sealed record ManufacturerBlock(ushort CompanyId, byte[] Data);
}
