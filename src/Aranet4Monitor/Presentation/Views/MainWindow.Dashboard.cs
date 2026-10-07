using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Live.SelectedDevice is { } device)
        {
            ShowDetails(device);
        }

        UpdateDevicePopover();
        UpdateTray();
    }

    /// <summary>The tray icon follows the selected sensor and uses the shared freshness threshold.</summary>
    private void UpdateTray()
    {
        if (Live.SelectedDevice is not { } device)
        {
            notifications.SetReading(0, stale: false);
            return;
        }

        var stale = sensorMonitor.IsStale(
            device.Address,
            GetLastReadingTime(device),
            DateTime.Now);
        notifications.SetReading(device.Co2Ppm, stale);
    }

    private void ShowDetails(Aranet4Device device)
    {
        var hasReading = device.Co2Ppm > 0;
        LivePageControl.Co2Text.Text = hasReading ? device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture) : "—";
        LivePageControl.Co2UnitText.Visibility = hasReading ? Visibility.Visible : Visibility.Collapsed;
        UpdateCo2Hero(device);
        HistoryPageControl.SyncHistoryButton.IsEnabled = !History.IsSyncing;
        HistoryPageControl.SyncHistoryButton.ToolTip = !History.IsSyncing
            ? "Sync stored CO₂, temperature, humidity and pressure history for the selected sensor"
            : "A history sync is already in progress.";
        Title = hasReading ? $"{device.Co2Ppm:N0} ppm · Aranet4 Monitor" : "Aranet4 Monitor";

        LivePageControl.SetMetricValueAndUnit(LivePageControl.TemperatureText, LivePageControl.TemperatureUnitText, device.Temperature, MetricKind.Temperature, preferences.TemperatureDisplayUnit);
        LivePageControl.SetMetricValueAndUnit(LivePageControl.HumidityText, LivePageControl.HumidityUnitText, device.Humidity, MetricKind.Humidity, preferences.TemperatureDisplayUnit);
        LivePageControl.SetMetricValueAndUnit(LivePageControl.PressureText, LivePageControl.PressureUnitText, device.Pressure, MetricKind.Pressure, preferences.TemperatureDisplayUnit);
        AutomationProperties.SetName(LivePageControl.TemperatureTab, $"Temperature, {device.Temperature}, show history");
        AutomationProperties.SetName(LivePageControl.HumidityTab, $"Humidity, {device.Humidity}, show history");
        AutomationProperties.SetName(LivePageControl.PressureTab, $"Pressure, {device.Pressure}, show history");
        AutomationProperties.SetName(LivePageControl.Co2Tab, $"CO₂, {(hasReading ? $"{device.Co2Ppm:N0} ppm" : "no reading")}, show history");
        DevicePopupControl.BatteryText.Text = device.Battery;
        StatusBarControl.FooterBatteryText.Text = device.Battery;
        DevicePopupControl.BatteryBar.Value = device.BatteryValue;
        StatusBarControl.FooterBatteryBar.Value = device.BatteryValue;
        var batteryBrush = ThemeService.GetBrush(device.BatteryValue switch
        {
            <= 15 => "Danger",
            <= 35 => "Co2Fair",
            _ => "Co2Good",
        });
        DevicePopupControl.BatteryBar.Foreground = batteryBrush;
        StatusBarControl.FooterBatteryBar.Foreground = batteryBrush;
        StatusBarControl.FooterBatteryText.Foreground = batteryBrush;

        DevicePopupControl.DeviceMeasurementAgeText.Text = device.MeasurementAge;
        DevicePopupControl.DeviceIntervalText.Text = device.MeasurementInterval;
        StatusBarControl.RssiText.Text = device.Packets == 0 ? "—" : $"{device.Rssi} dBm";
        StatusBarControl.DetailSignal.Bars = device.SignalBars;
        HeaderBar.ChipNameText.Text = device.Name;
        ShowLastSeen(device);

        DevicePopupControl.AdvertisementText.Text = device.LastAdvertisement;
        DevicePopupControl.ScanResponseText.Text = device.LastScanResponse;
        UpdateDevicePopover();
        RefreshChart();
    }

    private void ShowLastSeen(Aranet4Device device)
    {
        UpdateHeaderSensorState();
        UpdateCo2Hero(device);
    }

    private DateTime GetLastReadingTime(Aranet4Device device) =>
        device.LastSeen != default
            ? device.LastSeen
            : device.History.LastOrDefault(sample => sample.Ppm > 0)?.Time ?? default;

    private void UpdateCo2Hero(Aranet4Device device)
    {
        LivePageControl.UpdateCo2DaySummary(device, DateTime.Now);

        if (device.Co2Ppm <= 0)
        {
            LivePageControl.Co2Text.Text = "–";
            LivePageControl.Co2Text.Opacity = 1;
            LivePageControl.Co2UnitText.Visibility = Visibility.Collapsed;
            LivePageControl.Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
            LivePageControl.Co2CaptionText.Visibility = Visibility.Visible;
            LivePageControl.Co2AdviceText.Visibility = Visibility.Collapsed;
            LivePageControl.TrendText.Visibility = Visibility.Collapsed;
            ShowQuality(0);
            AutomationProperties.SetName(LivePageControl.Co2Tab, "CO₂, no reading, show history");
            return;
        }

        var lastReading = GetLastReadingTime(device);
        var stale = sensorMonitor.IsStale(device.Address, lastReading, DateTime.Now);
        LivePageControl.Co2Text.Text = device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture);
        LivePageControl.Co2Text.Opacity = stale ? 0.6 : 1;
        LivePageControl.Co2UnitText.Visibility = Visibility.Visible;
        ShowQuality(device.Co2Ppm);

        if (stale)
        {
            var elapsed = DateTime.Now - lastReading;
            var age = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours} hr ago"
                : $"{Math.Max(1, (int)elapsed.TotalMinutes)} min ago";
            LivePageControl.TrendText.Text = $"Last reading {age}";
            LivePageControl.TrendText.Foreground = SeenStale;
            LivePageControl.TrendText.Visibility = Visibility.Visible;
            LivePageControl.Co2CaptionText.Visibility = Visibility.Collapsed;
            return;
        }

        var co2Samples = device.History.Where(sample => sample.Ppm > 0).ToArray();
        if (co2Samples.Length >= 2
            && co2Samples[^1].Time - co2Samples[0].Time >= TimeSpan.FromMinutes(30)
            && Co2Stats.Trend(device.History) is { } trend)
        {
            LivePageControl.TrendText.Text = trend.Kind == TrendKind.Steady ? "Steady" : trend.Text;
            LivePageControl.TrendText.Foreground = (Brush)FindResource("TextPrimary");
            LivePageControl.TrendText.Visibility = Visibility.Visible;
            LivePageControl.Co2CaptionText.Visibility = Visibility.Collapsed;
        }
        else
        {
            LivePageControl.TrendText.Visibility = Visibility.Collapsed;
            LivePageControl.Co2CaptionText.Text = "Live readings";
            LivePageControl.Co2CaptionText.Visibility = Visibility.Visible;
        }

        AutomationProperties.SetName(LivePageControl.Co2Tab, $"CO₂, {device.Co2Ppm:N0} ppm, show history");
    }

    private void RefreshChart()
    {
        var device = Live.SelectedDevice;
        var now = DateTime.Now;
        LivePageControl.RefreshChart(device, Live.SelectedMetric, Live.HistoryRange, preferences.TemperatureDisplayUnit, now);
        if (HistoryPageControl.Visibility == Visibility.Visible)
        {
            RefreshHistoryView();
        }
    }

    private void MetricTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag } || !Enum.TryParse<MetricKind>(tag, out var kind))
        {
            return;
        }

        Live.SelectedMetric = kind;
        if (LivePageControl.HistoryChart is null)
        {
            return; // Checked fires once while the XAML is still being loaded
        }

        RefreshChart();
    }

    /// <summary>Colours the badge and moves the gauge marker (scale: 400–2000 ppm).</summary>
    private void ShowQuality(int ppm) => LivePageControl.ShowQuality(ppm);

    private void ClearDetails()
    {
        LivePageControl.Co2Text.Text = LivePageControl.TemperatureText.Text = LivePageControl.HumidityText.Text = LivePageControl.PressureText.Text = DevicePopupControl.BatteryText.Text = StatusBarControl.FooterBatteryText.Text = DevicePopupControl.DeviceMeasurementAgeText.Text = DevicePopupControl.DeviceIntervalText.Text = StatusBarControl.RssiText.Text = "—";
        LivePageControl.Co2UnitText.Visibility = Visibility.Collapsed;
        LivePageControl.Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
        LivePageControl.UpdateCo2DaySummary(null, DateTime.Now);
        LivePageControl.Co2CaptionText.Visibility = Visibility.Visible;
        LivePageControl.Co2AdviceText.Visibility = Visibility.Collapsed;
        LivePageControl.Co2Text.Text = "–";
        LivePageControl.Co2Text.Opacity = 1;
        LivePageControl.TemperatureUnitText.Text = Metrics.Unit(MetricKind.Temperature, preferences.TemperatureDisplayUnit);
        LivePageControl.HumidityUnitText.Text = Metrics.Unit(MetricKind.Humidity, preferences.TemperatureDisplayUnit);
        LivePageControl.PressureUnitText.Text = Metrics.Unit(MetricKind.Pressure, preferences.TemperatureDisplayUnit);
        LivePageControl.HumidityRangeText.Text = string.Empty;
        DevicePopupControl.BatteryBar.Value = 0;
        StatusBarControl.FooterBatteryBar.Value = 0;
        StatusBarControl.DetailSignal.Bars = 0;
        ShowQuality(0);
        HistoryPageControl.SyncHistoryButton.IsEnabled = false;
        HistoryPageControl.SyncHistoryButton.ToolTip = "Select a sensor to sync its stored history.";
        HeaderBar.ChipNameText.Text = "Searching for sensor…";
        LivePageControl.TemperatureRangeText.Text = LivePageControl.HumidityRangeText.Text = LivePageControl.PressureRangeText.Text = "No readings yet";
        DevicePopupControl.AdvertisementText.Clear();
        DevicePopupControl.ScanResponseText.Clear();
        Title = "Aranet4 Monitor";
        LivePageControl.TrendText.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(LivePageControl.Co2Tab, "CO₂, no reading, show history");
        AutomationProperties.SetName(LivePageControl.TemperatureTab, "Temperature, no reading, show history");
        AutomationProperties.SetName(LivePageControl.HumidityTab, "Humidity, no reading, show history");
        AutomationProperties.SetName(LivePageControl.PressureTab, "Pressure, no reading, show history");
        LivePageControl.HistoryChart.Samples = null;
        LivePageControl.ChartStatsText.Text = "No readings yet";
        LivePageControl.HistoryChart.InvalidateVisual();
        UpdateHeaderSensorState();
        UpdateDevicePopover();
        UpdateTray();
    }
}
