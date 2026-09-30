using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace BleListener;

public partial class MainWindow : Window
{
    private static readonly Guid AranetServiceUuid = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");
    private readonly Dictionary<ulong, Aranet4Device> _devicesByAddress = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly DispatcherTimer _tickTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan? _range = TimeSpan.FromHours(6); // null = show all recorded history
    private int _tickCount;

    private static readonly Brush ChipLive = Frozen(Color.FromRgb(0x2C, 0x56, 0x8B));
    private static readonly Brush ChipStale = Frozen(Color.FromRgb(0x6B, 0x55, 0x2A));
    private static readonly Brush TrendRising = Frozen(Color.FromRgb(0xFF, 0xD2, 0x7A));
    private static readonly Brush TrendFalling = Frozen(Color.FromRgb(0x7B, 0xE0, 0xAE));
    private static readonly Brush TrendSteady = Frozen(Color.FromRgb(0x9D, 0xB8, 0xE0));

    private static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Closed += (_, _) => { _tickTimer.Stop(); StopWatching(); };

        Devices.CollectionChanged += (_, _) =>
        {
            DevicesCountText.Text = Devices.Count.ToString(CultureInfo.InvariantCulture);
            EmptyState.Visibility = Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };

        // Keeps "3s ago" labels and the live dots fresh between packets.
        _tickTimer.Tick += (_, _) =>
        {
            foreach (var device in Devices) device.Tick();
            if (DevicesList.SelectedItem is Aranet4Device selected) ShowLastSeen(selected);
            if (++_tickCount % 10 == 0) RefreshChart(); // the window "now" keeps moving even without new data
        };
        _tickTimer.Start();

        // The app exists to listen, so start right away (Stop still works as before).
        Loaded += (_, _) => StartListening();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e) => StartListening();

    private void StartListening()
    {
        if (_watcher is not null) return;
        try
        {
            _watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
            _watcher.Received += Watcher_Received;
            _watcher.Stopped += Watcher_Stopped;
            _watcher.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            SetStatus("Listening for Aranet4 beacon packets…", StatusKind.Listening);
            EmptyHintText.Text = "Listening… power-cycle or move the sensor closer if nothing shows up.";
        }
        catch (Exception ex)
        {
            _watcher = null; // allow another attempt
            SetStatus($"Could not start: {ex.Message}", StatusKind.Error);
        }
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
        SetStatus("Stopped", StatusKind.Idle);
    }

    private void Watcher_Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args) =>
        Dispatcher.InvokeAsync(() =>
        {
            if (_watcher == sender)
            {
                sender.Received -= Watcher_Received;
                sender.Stopped -= Watcher_Stopped;
                _watcher = null; // otherwise StartListening() would think we're still running
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                SetStatus($"Listener stopped: {args.Error}", args.Error == BluetoothError.Success ? StatusKind.Idle : StatusKind.Error);
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
            device.LoadHistory(HistoryStore.Load(device.Address));
            _devicesByAddress.Add(args.BluetoothAddress, device);
            Devices.Add(device);

            // The dashboard is designed around the primary sensor; keep the first one in focus.
            if (DevicesList.SelectedItem is null)
            {
                DevicesList.SelectedItem = device;
                DevicesList.ScrollIntoView(device);
            }
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
            device.Co2Ppm = measurement.Co2;
            device.Temperature = $"{measurement.TemperatureCelsius:0.0} °C";
            device.Pressure = $"{measurement.PressureHpa:0.0} hPa";
            device.Humidity = $"{measurement.HumidityPercent}%";
            device.HumidityValue = measurement.HumidityPercent;
            device.Battery = measurement.BatteryPercent is null ? "—" : $"{measurement.BatteryPercent}%";
            device.BatteryValue = measurement.BatteryPercent ?? 0;
            device.MeasurementInterval = measurement.IntervalSeconds is null ? "—" : $"{measurement.IntervalSeconds} s";
            device.MeasurementAge = measurement.AgeSeconds is null ? "—" : $"{measurement.AgeSeconds} s";
            device.IntegrationState = "Live Smart Home beacon decoded";

            // The same measurement is repeated in many packets; TryAddSample keeps one point per measurement.
            var measuredAt = args.Timestamp.LocalDateTime - TimeSpan.FromSeconds(measurement.AgeSeconds ?? 0);
            var minGap = TimeSpan.FromSeconds(Math.Max(10, (measurement.IntervalSeconds ?? 60) * 0.5));
            if (device.TryAddSample(measuredAt, measurement.Co2, minGap)) HistoryStore.Save(device.Address, device.History);
        }
        else if (decodeMessage != "Waiting for an Aranet manufacturer beacon.") device.IntegrationState = decodeMessage;

        SetStatus($"Listening — {Devices.Count} Aranet4 device(s), last packet {device.Name}", StatusKind.Listening);
        if (DevicesList.SelectedItem == device) ShowDetails(device);
    }

    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DevicesList.SelectedItem is Aranet4Device device) ShowDetails(device);
    }

    private enum StatusKind { Idle, Listening, Error }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusDot.Tag = kind switch { StatusKind.Listening => "on", StatusKind.Error => "error", _ => null };
    }

    private void ShowDetails(Aranet4Device device)
    {
        var hasReading = device.Co2Ppm > 0;
        Co2Text.Text = hasReading ? device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture) : "—";
        Co2UnitText.Visibility = hasReading ? Visibility.Visible : Visibility.Collapsed;
        Co2CaptionText.Text = device.IntegrationState;
        ShowQuality(device.Co2Ppm);
        Title = hasReading ? $"{device.Co2Ppm:N0} ppm · Aranet4 Home" : "Aranet4 Home";

        TemperatureText.Text = device.Temperature;
        HumidityText.Text = device.Humidity;
        HumidityBar.Value = device.HumidityValue;
        PressureText.Text = device.Pressure;
        BatteryText.Text = device.Battery;
        BatteryBar.Value = device.BatteryValue;
        BatteryBar.Foreground = new SolidColorBrush(device.BatteryValue switch
        {
            <= 15 => Color.FromRgb(0xEF, 0x5B, 0x5B),
            <= 35 => Color.FromRgb(0xF5, 0xB9, 0x42),
            _ => Color.FromRgb(0x22, 0xC5, 0x5E),
        });

        AgeText.Text = device.MeasurementAge;
        IntervalText.Text = device.MeasurementInterval;
        RssiText.Text = device.Packets == 0 ? "—" : $"{device.Rssi} dBm";
        DetailSignal.Bars = device.SignalBars;
        ShowLastSeen(device);

        InfoNameText.Text = device.Name;
        InfoAddressText.Text = device.Address;
        InfoFirmwareText.Text = $"Firmware {device.Firmware}";
        InfoPacketsText.Text = device.Packets == 1 ? "1 packet" : $"{device.Packets:N0} packets";
        AdvertisementText.Text = device.LastAdvertisement;
        ScanResponseText.Text = device.LastScanResponse;
        RefreshChart();
    }

    private void ShowLastSeen(Aranet4Device device)
    {
        LastSeenText.Text = device.LastSeen == default ? "No data yet" : $"Seen {device.LastSeenAgo}";
        // Amber when we haven't heard from the sensor for a while, so stale numbers aren't mistaken for live ones.
        LastSeenChip.Background = device.LastSeen == default || device.IsLive ? ChipLive : ChipStale;
    }

    private void RefreshChart()
    {
        var device = DevicesList.SelectedItem as Aranet4Device;
        HistoryChart.Samples = device?.History;
        HistoryChart.Range = _range;
        HistoryChart.InvalidateVisual();
        ChartStatsText.Text = device is null ? "No readings yet" : Co2Stats.Describe(device.History, _range, DateTime.Now);

        if (device is not null && Co2Stats.Trend(device.History) is { } trend)
        {
            TrendText.Text = trend.Text;
            TrendText.Foreground = trend.Kind switch
            {
                TrendKind.Rising => TrendRising,
                TrendKind.Falling => TrendFalling,
                _ => TrendSteady,
            };
            TrendText.Visibility = Visibility.Visible;
        }
        else
        {
            TrendText.Visibility = Visibility.Collapsed;
        }
    }

    private void RangeButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;
        var hours = int.Parse(tag, CultureInfo.InvariantCulture);
        _range = hours == 0 ? null : TimeSpan.FromHours(hours);
        if (HistoryChart is null) return; // Checked fires once while the XAML is still being loaded
        RefreshChart();
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not Aranet4Device { History.Count: > 0 } device)
        {
            MessageBox.Show(this, "There are no CO₂ readings to export yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"aranet4-co2-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog(this) != true) return;

        var csv = new StringBuilder("time,co2_ppm\n");
        foreach (var sample in device.History)
            csv.Append(sample.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',').Append(sample.Ppm).Append('\n');
        try { File.WriteAllText(dialog.FileName, csv.ToString()); }
        catch (IOException ex) { MessageBox.Show(this, ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    /// <summary>Colours the badge and moves the gauge marker (scale: 400–2000 ppm).</summary>
    private void ShowQuality(int ppm)
    {
        if (ppm <= 0)
        {
            QualityBadge.Visibility = Visibility.Collapsed;
            GaugeMarkerGrid.Visibility = Visibility.Collapsed;
            return;
        }

        var (label, color) = ppm switch
        {
            < 1000 => ("Good air", Color.FromRgb(0x2E, 0xC2, 0x7E)),
            < 1400 => ("Getting stuffy", Color.FromRgb(0xF5, 0xB9, 0x42)),
            _ => ("Poor — ventilate", Color.FromRgb(0xEF, 0x5B, 0x5B)),
        };
        QualityText.Text = label;
        QualityBadge.Background = new SolidColorBrush(color);
        QualityBadge.Visibility = Visibility.Visible;

        var fraction = Math.Clamp((ppm - 400) / 1600.0, 0.0, 1.0);
        GaugeLeft.Width = new GridLength(Math.Max(fraction, 0.001), GridUnitType.Star);
        GaugeRight.Width = new GridLength(Math.Max(1 - fraction, 0.001), GridUnitType.Star);
        GaugeMarkerGrid.Visibility = Visibility.Visible;
    }

    private void ClearDetails()
    {
        Co2Text.Text = TemperatureText.Text = HumidityText.Text = PressureText.Text = BatteryText.Text = AgeText.Text = IntervalText.Text = RssiText.Text = "—";
        Co2UnitText.Visibility = Visibility.Collapsed;
        Co2CaptionText.Text = "Waiting for a live beacon";
        LastSeenText.Text = "No data yet";
        HumidityBar.Value = BatteryBar.Value = 0;
        DetailSignal.Bars = 0;
        ShowQuality(0);
        InfoNameText.Text = "No device selected";
        InfoAddressText.Text = "Address —";
        InfoFirmwareText.Text = "Firmware —";
        InfoPacketsText.Text = "0 packets";
        AdvertisementText.Clear();
        ScanResponseText.Clear();
        Title = "Aranet4 Home";
        TrendText.Visibility = Visibility.Collapsed;
        LastSeenChip.Background = ChipLive;
        HistoryChart.Samples = null;
        ChartStatsText.Text = "No readings yet";
        HistoryChart.InvalidateVisual();
    }

    private void CopyPacket_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string which }) return;
        var text = which == "adv" ? AdvertisementText.Text : ScanResponseText.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        try { Clipboard.SetText(text); } catch (System.Runtime.InteropServices.COMException) { /* clipboard busy */ }
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
