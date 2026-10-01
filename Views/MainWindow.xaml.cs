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
    private readonly AppPreferences _preferences = AppPreferences.Load();
    private readonly Co2AlertService _co2Alerts = new();
    private readonly TrayIconService _notifications = new();
    private readonly HashSet<string> _alertedDevices = new(StringComparer.OrdinalIgnoreCase);
    private bool _startHiddenInTray = Environment.GetCommandLineArgs().Contains(StartupRegistration.TrayArgument);
    private DateTime? _alertsPausedUntil;
    private CancellationTokenSource? _syncCancellation;
    private TimeSpan? _range = TimeSpan.FromHours(6); // null = show all recorded history
    private MetricKind _metric = MetricKind.Co2;          // which metric the chart shows
    private DateTime _devicePopupClosedAt;
    private DateTime _statusHoldUntil;
    private int _tickCount;
    private bool _allowClose;

    private static readonly Brush DotLive = Frozen(Color.FromRgb(0x2E, 0xC2, 0x7E));
    private static readonly Brush DotStale = Frozen(Color.FromRgb(0xF5, 0xB9, 0x42));
    private static readonly Brush DotIdle = Frozen(Color.FromRgb(0x8F, 0xA3, 0xBF));
    private static readonly Brush SeenLive = Frozen(Color.FromRgb(0x9D, 0xB8, 0xE0));
    private static readonly Brush SeenStale = Frozen(Color.FromRgb(0xFF, 0xD2, 0x7A));
    private static readonly Dictionary<MetricKind, Brush> AccentBrushes =
        Enum.GetValues<MetricKind>().ToDictionary(kind => kind, kind => Frozen(Metrics.Accent(kind)));
    private static readonly Brush TrendRising = Frozen(Color.FromRgb(0xFF, 0xD2, 0x7A));
    private static readonly Brush TrendFalling = Frozen(Color.FromRgb(0x7B, 0xE0, 0xAE));
    private static readonly Brush TrendSteady = Frozen(Color.FromRgb(0x9D, 0xB8, 0xE0));

    private static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        DevicesList.ItemsSource = Devices;
        AlertThresholdTextBox.Text = _preferences.AlertThresholdPpm.ToString(CultureInfo.InvariantCulture);
        AlertDurationTextBox.Text = _preferences.AlertDurationMinutes.ToString(CultureInfo.InvariantCulture);
        AlertStatusText.Text = _preferences.LastCo2AlertAt is { } lastAlert
            ? $"Last notification: {_preferences.LastCo2AlertPpm:N0} ppm at {lastAlert:t}"
            : "No high-CO₂ alerts sent yet.";
        _notifications.RestoreRequested += (_, _) => RestoreFromTray();
        _notifications.ExitRequested += (_, _) => ExitFromTray();
        _notifications.PauseToggleRequested += (_, _) => ToggleAlertPause();
        _notifications.StartWithWindowsToggled += (_, enabled) =>
        {
            // If Windows refuses the change, put the menu check mark back to the real state.
            if (!StartupRegistration.SetEnabled(enabled)) _notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
        };
        _notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
        _notifications.SetShowNumber(_preferences.TrayShowNumber);
        _notifications.SetLargePopups(_preferences.LargePopups);
        _notifications.ShowNumberToggled += (_, enabled) => { _preferences.TrayShowNumber = enabled; _preferences.Save(); };
        _notifications.LargePopupsToggled += (_, enabled) => { _preferences.LargePopups = enabled; _preferences.Save(); };

        if (_startHiddenInTray)
        {
            // Started by Windows: come up minimized so no window flashes; Loaded then hides it in the tray.
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
        }
        Closing += MainWindow_Closing;
        Closed += (_, _) => { _tickTimer.Stop(); _syncCancellation?.Cancel(); StopWatching(); _notifications.Dispose(); };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !_startHiddenInTray) HideToTray();
        };

        Devices.CollectionChanged += (_, _) =>
        {
            DevicesCountText.Text = Devices.Count.ToString(CultureInfo.InvariantCulture);
            EmptyHintText.Visibility = Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };

        // Keeps "3s ago" labels and the live dots fresh between packets.
        _tickTimer.Tick += (_, _) =>
        {
            foreach (var device in Devices) device.Tick();
            if (DevicesList.SelectedItem is Aranet4Device selected) ShowLastSeen(selected);
            if (_alertsPausedUntil is { } until && DateTime.Now >= until)
            {
                _alertsPausedUntil = null;
                UpdatePauseUi();
            }

            if (++_tickCount % 10 == 0)
            {
                RefreshChart(); // the window "now" keeps moving even without new data
                UpdateTray();   // turns the tray icon grey if the sensor has gone quiet
            }
        };
        _tickTimer.Start();

        // The app exists to listen, so start right away (Stop still works as before).
        Loaded += (_, _) =>
        {
            StartListening();
            if (!_startHiddenInTray) return;
            Hide();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            _startHiddenInTray = false; // from now on minimizing hides to the tray as usual
        };
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        HideToTray();
    }

    /// <summary>Hides the window; the first time ever, explains where the app went.</summary>
    private void HideToTray()
    {
        Hide();
        if (_preferences.TrayHintShown) return;

        _preferences.TrayHintShown = true;
        _preferences.Save();
        _notifications.NotifyHint("🫧 Still here, in the tray", "I'll keep listening quietly. Click the tray icon to open me, right-click for options.");
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
                var notificationSent = alertDue && !AlertsPaused;
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

    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DevicesList.SelectedItem is Aranet4Device device) ShowDetails(device);
        UpdateTray();
    }

    /// <summary>The tray icon follows the selected sensor and turns grey if it hasn't been heard from for 5 minutes.</summary>
    private void UpdateTray()
    {
        if (DevicesList.SelectedItem is not Aranet4Device device)
        {
            _notifications.SetReading(0, stale: false);
            return;
        }

        var stale = device.LastSeen != default && DateTime.Now - device.LastSeen > TimeSpan.FromMinutes(5);
        _notifications.SetReading(device.Co2Ppm, stale);
    }

    private bool AlertsPaused => _alertsPausedUntil is { } until && DateTime.Now < until;

    private void ToggleAlertPause()
    {
        _alertsPausedUntil = AlertsPaused ? null : DateTime.Now.AddHours(1);
        UpdatePauseUi();
    }

    private void UpdatePauseUi()
    {
        _notifications.SetAlertsPaused(AlertsPaused, _alertsPausedUntil);
        PauseAlertsButton.Content = AlertsPaused ? "Resume alerts" : "Pause 1 h";
        AlertStatusText.Text = AlertsPaused ? $"Alerts paused until {_alertsPausedUntil:t}." : "Alerts are on.";
    }

    private void PauseAlerts_Click(object sender, RoutedEventArgs e) => ToggleAlertPause();

    private void SendTestAlert_Click(object sender, RoutedEventArgs e)
    {
        // Preview the real notification, using a level just above the user's threshold.
        _notifications.NotifyHighCo2(AlertThreshold + 80);
        AlertStatusText.Text = "Test notification sent. Click it to bring this window back.";
    }

    private enum StatusKind { Idle, Listening, Error }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusText.ToolTip = text;
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
        ChipNameText.Text = device.Name;
        ShowLastSeen(device);

        AdvertisementText.Text = device.LastAdvertisement;
        ScanResponseText.Text = device.LastScanResponse;
        RefreshChart();
    }

    private void ShowLastSeen(Aranet4Device device)
    {
        var hasData = device.LastSeen != default;
        LastSeenText.Text = hasData ? device.LastSeenAgo : "no data yet";
        // Amber when we haven't heard from the sensor for a while, so stale numbers aren't mistaken for live ones.
        DeviceDot.Fill = !hasData ? DotIdle : device.IsLive ? DotLive : DotStale;
        LastSeenText.Foreground = hasData && !device.IsLive ? SeenStale : SeenLive;
    }

    private string RangeLabel => _range is { } range ? $"{range.TotalHours:0}h" : "All";

    private string RangeSummary(Aranet4Device? device, MetricKind kind, DateTime now)
    {
        if (device is null || Metrics.Range(device.History, _range, now, kind) is not { } range) return string.Empty;
        return $"{RangeLabel}: {Metrics.Format(range.Min, kind)}–{Metrics.Format(range.Max, kind)} {Metrics.Unit(kind)}";
    }

    private void RefreshChart()
    {
        var device = DevicesList.SelectedItem as Aranet4Device;
        var now = DateTime.Now;

        HistoryChart.Metric = _metric;
        HistoryChart.Samples = device?.History;
        HistoryChart.Range = _range;
        HistoryChart.InvalidateVisual();

        ChartTitleText.Text = $"{Metrics.Title(_metric).ToUpperInvariant()} HISTORY";
        ChartDot.Fill = AccentBrushes[_metric];
        ChartStatsText.Text = device is null ? "No readings yet" : Metrics.Describe(device.History, _range, now, _metric);

        TemperatureRangeText.Text = RangeSummary(device, MetricKind.Temperature, now);
        HumidityRangeText.Text = RangeSummary(device, MetricKind.Humidity, now);
        PressureRangeText.Text = RangeSummary(device, MetricKind.Pressure, now);

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
            Co2CaptionText.Visibility = Visibility.Collapsed;
        }
        else
        {
            TrendText.Visibility = Visibility.Collapsed;
            Co2CaptionText.Visibility = Visibility.Visible;
        }
    }

    private void MetricTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag } || !Enum.TryParse<MetricKind>(tag, out var kind)) return;
        _metric = kind;
        if (HistoryChart is null) return; // Checked fires once while the XAML is still being loaded
        RefreshChart();
    }

    private void DeviceChip_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePopup.IsOpen) { DevicePopup.IsOpen = false; return; }
        // The same click that dismissed the popup (it closes on any outside press) must not reopen it.
        if ((DateTime.UtcNow - _devicePopupClosedAt).TotalMilliseconds < 250) return;
        DevicePopup.IsOpen = true;
    }

    private void DevicePopup_Closed(object? sender, EventArgs e) => _devicePopupClosedAt = DateTime.UtcNow;

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.IsOpen = true;
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
            MessageBox.Show(this, "There are no readings to export yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"aranet4-co2-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog(this) != true) return;

        var csv = new StringBuilder("time,co2_ppm,temperature_c,humidity_percent,pressure_hpa\n");
        foreach (var sample in device.History)
        {
            csv.Append(sample.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.Ppm > 0 ? sample.Ppm.ToString(CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(sample.TemperatureCelsius?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(sample.HumidityPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(sample.PressureHpa?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append('\n');
        }
        try { File.WriteAllText(dialog.FileName, csv.ToString()); }
        catch (IOException ex) { MessageBox.Show(this, ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void SyncHistory_Click(object sender, RoutedEventArgs e)
    {
        if (DevicesList.SelectedItem is not Aranet4Device device) return;

        var bluetoothAddress = _devicesByAddress.FirstOrDefault(entry => ReferenceEquals(entry.Value, device)).Key;
        if (bluetoothAddress == 0)
        {
            SetStatus("Could not identify the selected sensor.", StatusKind.Error);
            return;
        }

        SyncHistoryButton.IsEnabled = false;
        CancelHistorySyncButton.IsEnabled = true;
        var cancellation = new CancellationTokenSource();
        _syncCancellation = cancellation;
        try
        {
            var progress = new Progress<string>(message => SetStatus(message, _watcher is null ? StatusKind.Idle : StatusKind.Listening));
            var cursor = HistoryStore.LoadSyncCursor(device.Address);
            var result = await Aranet4HistorySync.SyncAsync(bluetoothAddress, cursor, progress, cancellation.Token);
            var added = device.MergeHistory(result.Samples);
            if (!HistoryStore.Save(device.Address, device.History))
                throw new IOException("Downloaded history could not be saved locally. The sync cursor was not advanced; retry after checking disk space.");
            if (result.SyncedThrough is { } syncedThrough && !HistoryStore.SaveSyncCursor(device.Address, syncedThrough))
                throw new IOException("The sync cursor could not be saved. Retrying will safely import the overlap again.");
            if (DevicesList.SelectedItem == device) ShowDetails(device);
            var statusKind = _watcher is null ? StatusKind.Idle : StatusKind.Listening;
            _statusHoldUntil = DateTime.Now.AddSeconds(12);
            SetStatus(result.MissingRecords > 0
                ? $"History sync incomplete: {added:N0} new readings saved, {result.MissingRecords:N0} records weren't received. Sync again to fill the gap."
                : $"History sync complete: {added:N0} new readings.", statusKind);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            _statusHoldUntil = DateTime.Now.AddSeconds(12);
            SetStatus("History sync cancelled. Retry to resume from the last completed sync.", _watcher is null ? StatusKind.Idle : StatusKind.Listening);
        }
        catch (Exception ex)
        {
            _statusHoldUntil = DateTime.Now.AddSeconds(12);
            SetStatus($"History sync failed: {ex.Message}", StatusKind.Error);
            MessageBox.Show(this, ex.Message, "History sync", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _syncCancellation = null;
            cancellation.Dispose();
            SyncHistoryButton.IsEnabled = true;
            CancelHistorySyncButton.IsEnabled = false;
        }
    }

    private void CancelHistorySync_Click(object sender, RoutedEventArgs e)
    {
        CancelHistorySyncButton.IsEnabled = false;
        _syncCancellation?.Cancel();
    }

    private int AlertThreshold => int.TryParse(AlertThresholdTextBox.Text, out var value)
        ? Math.Clamp(value, 800, 5_000)
        : Co2AlertService.RecommendedVentilationThresholdPpm;

    private int AlertDurationMinutes => int.TryParse(AlertDurationTextBox.Text, out var value)
        ? Math.Clamp(value, 1, 60)
        : 10;

    private void AlertThreshold_LostFocus(object sender, RoutedEventArgs e)
    {
        _preferences.AlertThresholdPpm = AlertThreshold;
        _preferences.AlertDurationMinutes = AlertDurationMinutes;
        AlertThresholdTextBox.Text = _preferences.AlertThresholdPpm.ToString(CultureInfo.InvariantCulture);
        AlertDurationTextBox.Text = _preferences.AlertDurationMinutes.ToString(CultureInfo.InvariantCulture);
        _preferences.Save();
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        Close();
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
        Co2CaptionText.Visibility = Visibility.Visible;
        HumidityBar.Value = BatteryBar.Value = 0;
        DetailSignal.Bars = 0;
        ShowQuality(0);
        ChipNameText.Text = "Searching for sensor…";
        LastSeenText.Text = string.Empty;
        DeviceDot.Fill = DotIdle;
        TemperatureRangeText.Text = HumidityRangeText.Text = PressureRangeText.Text = string.Empty;
        AdvertisementText.Clear();
        ScanResponseText.Clear();
        Title = "Aranet4 Home";
        TrendText.Visibility = Visibility.Collapsed;
        HistoryChart.Samples = null;
        ChartStatsText.Text = "No readings yet";
        HistoryChart.InvalidateVisual();
        UpdateTray();
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
