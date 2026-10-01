using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aranet4Monitor.Models;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Dashboard.SelectedDevice is { } device)
        {
            ShowDetails(device);
        }

        UpdateTray();
    }

    /// <summary>The tray icon follows the selected sensor and turns grey if it hasn't been heard from for 5 minutes.</summary>
    private void UpdateTray()
    {
        if (Dashboard.SelectedDevice is not { } device)
        {
            notifications.SetReading(0, stale: false);
            return;
        }

        var stale = device.LastSeen != default && DateTime.Now - device.LastSeen > TimeSpan.FromMinutes(5);
        notifications.SetReading(device.Co2Ppm, stale);
    }

    private void ShowDetails(Aranet4Device device)
    {
        var hasReading = device.Co2Ppm > 0;
        Co2Text.Text = hasReading ? device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture) : "—";
        Co2UnitText.Visibility = hasReading ? Visibility.Visible : Visibility.Collapsed;
        Co2CaptionText.Text = device.IntegrationState;
        SyncHistoryButton.IsEnabled = syncCancellation is null;
        ShowQuality(device.Co2Ppm);
        Title = hasReading ? $"{device.Co2Ppm:N0} ppm · Aranet4 Monitor" : "Aranet4 Monitor";

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

    private string RangeLabel => Dashboard.HistoryRange is { } range ? $"{range.TotalHours:0}h" : "All";

    private string RangeSummary(Aranet4Device? device, MetricKind kind, DateTime now)
    {
        if (device is null || Metrics.Range(device.History, Dashboard.HistoryRange, now, kind, preferences.TemperatureDisplayUnit) is not { } range)
        {
            return string.Empty;
        }

        return $"{RangeLabel}: {Metrics.Format(range.Min, kind)} – {Metrics.Format(range.Max, kind)} {Metrics.Unit(kind, preferences.TemperatureDisplayUnit)}";
    }

    private void RefreshChart()
    {
        var device = Dashboard.SelectedDevice;
        var now = DateTime.Now;

        HistoryChart.Metric = Dashboard.SelectedMetric;
        HistoryChart.TemperatureDisplayUnit = preferences.TemperatureDisplayUnit;
        HistoryChart.Samples = device?.History;
        HistoryChart.Range = Dashboard.HistoryRange;
        HistoryChart.InvalidateVisual();

        ChartTitleText.Text = $"{Metrics.Title(Dashboard.SelectedMetric).ToUpperInvariant()} HISTORY";
        ChartDot.Fill = AccentBrushes[Dashboard.SelectedMetric];
        ChartStatsText.Text = device is null ? "No readings yet" : Metrics.Describe(device.History, Dashboard.HistoryRange, now, Dashboard.SelectedMetric, preferences.TemperatureDisplayUnit);

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
        if (sender is not RadioButton { Tag: string tag } || !Enum.TryParse<MetricKind>(tag, out var kind))
        {
            return;
        }

        Dashboard.SelectedMetric = kind;
        if (HistoryChart is null)
        {
            return; // Checked fires once while the XAML is still being loaded
        }

        RefreshChart();
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
            _ => ("High CO₂ · ventilate", Color.FromRgb(0xEF, 0x5B, 0x5B)),
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
        SyncHistoryButton.IsEnabled = false;
        ChipNameText.Text = "Searching for sensor…";
        LastSeenText.Text = string.Empty;
        DeviceDot.Fill = DotIdle;
        TemperatureRangeText.Text = HumidityRangeText.Text = PressureRangeText.Text = string.Empty;
        AdvertisementText.Clear();
        ScanResponseText.Clear();
        Title = "Aranet4 Monitor";
        TrendText.Visibility = Visibility.Collapsed;
        HistoryChart.Samples = null;
        ChartStatsText.Text = "No readings yet";
        HistoryChart.InvalidateVisual();
        UpdateTray();
    }
}
