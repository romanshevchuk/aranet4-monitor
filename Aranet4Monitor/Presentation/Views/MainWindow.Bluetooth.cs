using System.Globalization;
using System.Text;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Aranet4Monitor.Alerts;
using Aranet4Monitor.Bluetooth;
using Aranet4Monitor.Models;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private readonly Dictionary<ulong, Aranet4Device> _devicesByAddress = new();
    private BluetoothLEAdvertisementWatcher? _watcher;
    private readonly Co2AlertService _co2Alerts = new();
    private readonly HashSet<string> _alertedDevices = new(StringComparer.OrdinalIgnoreCase);

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
            StartMenuItem.IsEnabled = false;
            StopMenuItem.IsEnabled = true;
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
        _alertedDevices.Clear();
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
        StartMenuItem.IsEnabled = true;
        StopMenuItem.IsEnabled = false;
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
                StartMenuItem.IsEnabled = true;
                StopMenuItem.IsEnabled = false;
                SetStatus($"Listener stopped: {args.Error}", args.Error == BluetoothError.Success ? StatusKind.Idle : StatusKind.Error);
            }
        });

    private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var advertisement = args.Advertisement;
        var manufacturerBlocks = advertisement.ManufacturerData
            .Select(block => new ManufacturerBlock(block.CompanyId, ToBytes(block.Data))).ToArray();
        var isAranet = Aranet4AdvertisementFilter.IsCandidate(
            advertisement.LocalName,
            advertisement.ServiceUuids,
            manufacturerBlocks.Select(block => (block.CompanyId, block.Data.Length)));
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
            device.TemperatureCelsius = measurement.TemperatureCelsius;
            device.Temperature = Metrics.FormatWithUnit((double)measurement.TemperatureCelsius, MetricKind.Temperature, _preferences.TemperatureDisplayUnit);
            device.Pressure = $"{measurement.PressureHpa:0.0} hPa";
            device.Humidity = $"{measurement.HumidityPercent}%";
            device.HumidityValue = measurement.HumidityPercent;
            device.Battery = measurement.BatteryPercent is null ? "—" : $"{measurement.BatteryPercent}%";
            device.BatteryValue = measurement.BatteryPercent ?? 0;
            device.MeasurementInterval = measurement.IntervalSeconds is null ? "—" : $"{measurement.IntervalSeconds} s";
            device.MeasurementAge = measurement.AgeSeconds is null ? "—" : $"{measurement.AgeSeconds} s";
            device.IntegrationState = "Live Smart Home beacon decoded";

            // The same measurement is repeated in many packets; TryAddSample keeps one point per measurement.
            var now = args.Timestamp.LocalDateTime;
            var measuredAt = now - TimeSpan.FromSeconds(measurement.AgeSeconds ?? 0);
            var minGap = TimeSpan.FromSeconds(Math.Max(10, (measurement.IntervalSeconds ?? 60) * 0.5));
            if (device.TryAddSample(
                measuredAt,
                measurement.Co2,
                minGap,
                measurement.TemperatureCelsius,
                measurement.HumidityPercent,
                measurement.PressureHpa))
            {
                HistoryStore.Save(device.Address, device.History);
                // Always feed the service so its sustained-high tracking stays correct, even while alerts are paused.
                var alertDue = _co2Alerts.ShouldNotify(
                    device.Address,
                    measurement.Co2,
                    now,
                    AlertThreshold,
                    TimeSpan.FromSeconds(measurement.IntervalSeconds ?? 60),
                    TimeSpan.FromMinutes(AlertDurationMinutes));
                var alertsPaused = AlertsPaused;
                if (alertDue && alertsPaused) _co2Alerts.DeferNotification(device.Address);
                var notificationSent = alertDue && !alertsPaused;
                if (notificationSent)
                {
                    _alertedDevices.Add(device.Address);
                    _preferences.LastCo2AlertAt = now;
                    _preferences.LastCo2AlertPpm = measurement.Co2;
                    _preferences.Save();
                    AlertStatusText.Text = $"Alert sent: {measurement.Co2:N0} ppm at {now:t}";
                    _notifications.NotifyHighCo2(measurement.Co2);
                }
                else if (alertDue)
                {
                    AlertStatusText.Text = $"Above {AlertThreshold:N0} ppm, but alerts are paused until {_alertsPausedUntil:t}.";
                }
                else if (measurement.Co2 > AlertThreshold)
                {
                    AlertStatusText.Text = $"Above {AlertThreshold:N0} ppm; waiting for {AlertDurationMinutes} minutes of sustained readings.";
                }
                else if (measurement.Co2 <= Co2AlertService.GetResetThreshold(AlertThreshold))
                {
                    // Air is fine again. If we had raised the alarm, celebrate with a short all-clear.
                    if (_alertedDevices.Remove(device.Address) && !AlertsPaused)
                        _notifications.NotifyRecovered(measurement.Co2);

                    AlertStatusText.Text = _preferences.LastCo2AlertAt is { } previousAlert
                        ? $"Recovered below {Co2AlertService.GetResetThreshold(AlertThreshold):N0} ppm. Last alert {previousAlert:t}."
                        : "No active high-CO₂ alert.";
                }
            }
        }
        else if (decodeMessage != "Waiting for an Aranet manufacturer beacon.") device.IntegrationState = decodeMessage;

        // Don't overwrite a sync progress/result message the user is still reading.
        if (_syncCancellation is null && DateTime.Now >= _statusHoldUntil) SetStatus("Listening", StatusKind.Listening);
        if (DevicesList.SelectedItem == device)
        {
            ShowDetails(device);
            UpdateTray();
        }
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