using System.Globalization;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void DevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Dashboard.SelectedDevice is { } device)
        {
            ShowDetails(device);
        }

        UpdateDevicePopover();
        UpdateTray();
    }

    /// <summary>The tray icon follows the selected sensor and uses the shared freshness threshold.</summary>
    private void UpdateTray()
    {
        if (Dashboard.SelectedDevice is not { } device)
        {
            notifications.SetReading(0, stale: false);
            return;
        }

        var stale = SensorFreshness.IsStale(
            GetLastReadingTime(device),
            measurementIntervals.GetValueOrDefault(device),
            DateTime.Now);
        notifications.SetReading(device.Co2Ppm, stale);
    }

    private void ShowDetails(Aranet4Device device)
    {
        var hasReading = device.Co2Ppm > 0;
        Co2Text.Text = hasReading ? device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture) : "—";
        Co2UnitText.Visibility = hasReading ? Visibility.Visible : Visibility.Collapsed;
        UpdateCo2Hero(device);
        SyncHistoryButton.IsEnabled = syncCancellation is null;
        SyncHistoryButton.ToolTip = syncCancellation is null
            ? "Sync stored CO₂, temperature, humidity and pressure history for the selected sensor"
            : "A history sync is already in progress.";
        Title = hasReading ? $"{device.Co2Ppm:N0} ppm · Aranet4 Monitor" : "Aranet4 Monitor";

        SetMetricValueAndUnit(TemperatureText, TemperatureUnitText, device.Temperature, MetricKind.Temperature);
        SetMetricValueAndUnit(HumidityText, HumidityUnitText, device.Humidity, MetricKind.Humidity);
        SetMetricValueAndUnit(PressureText, PressureUnitText, device.Pressure, MetricKind.Pressure);
        AutomationProperties.SetName(TemperatureTab, $"Temperature, {device.Temperature}, show history");
        AutomationProperties.SetName(HumidityTab, $"Humidity, {device.Humidity}, show history");
        AutomationProperties.SetName(PressureTab, $"Pressure, {device.Pressure}, show history");
        AutomationProperties.SetName(Co2Tab, $"CO₂, {(hasReading ? $"{device.Co2Ppm:N0} ppm" : "no reading")}, show history");
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
        if (device.Co2Ppm <= 0)
        {
            Co2Text.Text = "–";
            Co2Text.Opacity = 1;
            Co2UnitText.Visibility = Visibility.Collapsed;
            Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
            Co2CaptionText.Visibility = Visibility.Visible;
            Co2AdviceText.Visibility = Visibility.Collapsed;
            TrendText.Visibility = Visibility.Collapsed;
            ShowQuality(0);
            AutomationProperties.SetName(Co2Tab, "CO₂, no reading, show history");
            return;
        }

        var lastReading = GetLastReadingTime(device);
        var stale = SensorFreshness.IsStale(lastReading, measurementIntervals.GetValueOrDefault(device), DateTime.Now);
        Co2Text.Text = device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture);
        Co2Text.Opacity = stale ? 0.6 : 1;
        Co2UnitText.Visibility = Visibility.Visible;
        ShowQuality(device.Co2Ppm);

        if (stale)
        {
            var elapsed = DateTime.Now - lastReading;
            var age = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours} hr ago"
                : $"{Math.Max(1, (int)elapsed.TotalMinutes)} min ago";
            TrendText.Text = $"Last reading {age}";
            TrendText.Foreground = SeenStale;
            TrendText.Visibility = Visibility.Visible;
            Co2CaptionText.Visibility = Visibility.Collapsed;
            return;
        }

        var co2Samples = device.History.Where(sample => sample.Ppm > 0).ToArray();
        if (co2Samples.Length >= 2
            && co2Samples[^1].Time - co2Samples[0].Time >= TimeSpan.FromMinutes(30)
            && Co2Stats.Trend(device.History) is { } trend)
        {
            TrendText.Text = trend.Kind == TrendKind.Steady ? "Steady" : trend.Text;
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
            Co2CaptionText.Text = "Live readings";
            Co2CaptionText.Visibility = Visibility.Visible;
        }

        AutomationProperties.SetName(Co2Tab, $"CO₂, {device.Co2Ppm:N0} ppm, show history");
    }

    private void SetMetricValueAndUnit(TextBlock valueText, TextBlock unitText, string display, MetricKind kind)
    {
        var unit = Metrics.Unit(kind, preferences.TemperatureDisplayUnit);
        valueText.Text = display.EndsWith(unit, StringComparison.Ordinal)
            ? display[..^unit.Length].TrimEnd()
            : display;
        unitText.Text = unit;
    }

    private string RangeSummary(Aranet4Device? device, MetricKind kind, DateTime now)
    {
        if (device is null || Metrics.Range(device.History, Dashboard.HistoryRange, now, kind, preferences.TemperatureDisplayUnit) is not { } range)
        {
            return "No readings yet";
        }

        var label = Dashboard.HistoryRange is { } selectedRange
            ? $"Last {selectedRange.TotalHours:0} h"
            : "All";
        var summary = kind == MetricKind.Humidity
            ? $"{label}: {Metrics.Format(range.Min, kind)}–{Metrics.Format(range.Max, kind)}%"
            : $"{label}: {Metrics.Format(range.Min, kind)}–{Metrics.Format(range.Max, kind)} {Metrics.Unit(kind, preferences.TemperatureDisplayUnit)}";

        return summary;
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

        ChartTitleText.Text = $"{Metrics.Title(Dashboard.SelectedMetric)} history";
        ChartDot.Fill = AccentBrushes[Dashboard.SelectedMetric];
        ChartStatsText.Text = device is null ? "No readings yet" : Metrics.Describe(device.History, Dashboard.HistoryRange, now, Dashboard.SelectedMetric, preferences.TemperatureDisplayUnit);

        TemperatureRangeText.Text = RangeSummary(device, MetricKind.Temperature, now);
        HumidityRangeText.Text = RangeSummary(device, MetricKind.Humidity, now);
        PressureRangeText.Text = RangeSummary(device, MetricKind.Pressure, now);
        UpdateHumidityComfort(device);
    }

    /// <summary>Comfort word and badge next to the humidity label (Dry under 40%, Comfortable 40–60%, Humid above 60%).</summary>
    private void UpdateHumidityComfort(Aranet4Device? device)
    {
        var latest = device?.History.LastOrDefault(sample => sample.HumidityPercent.HasValue)?.HumidityPercent;
        if (latest is not { } humidity)
        {
            HumidityComfortBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var comfortable = humidity >= 40 && humidity <= 60;
        HumidityComfortText.Text = humidity < 40 ? "Dry" : comfortable ? "Comfortable" : "Humid";
        HumidityComfortText.Foreground = new SolidColorBrush(comfortable ? Color.FromRgb(0x1A, 0x7A, 0x45) : Color.FromRgb(0x7A, 0x4B, 0x00));
        HumidityComfortBadge.Background = new SolidColorBrush(comfortable ? Color.FromRgb(0xE3, 0xF6, 0xEA) : Color.FromRgb(0xFF, 0xF4, 0xDC));
        HumidityComfortBadge.Visibility = Visibility.Visible;
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

        var level = Co2Quality.Classify(ppm);
        var (icon, color) = level switch
        {
            Co2Level.Good => ("✓", (Color)FindResource("Co2GoodColor")),
            Co2Level.Fair => ("!", (Color)FindResource("Co2FairColor")),
            _ => ("!", (Color)FindResource("Co2PoorColor")),
        };
        QualityIcon.Text = icon;
        QualityText.Text = Co2Quality.Describe(level);
        QualityBadge.Background = new SolidColorBrush(color);
        QualityBadge.Visibility = Visibility.Visible;
        Co2AdviceText.Text = ppm < Co2Quality.FairFromPpm
            ? string.Empty
            : ppm < Co2Quality.PoorFromPpm
                ? "Consider opening a window"
                : "Ventilate now";
        Co2AdviceText.Visibility = ppm < Co2Quality.FairFromPpm ? Visibility.Collapsed : Visibility.Visible;

        var fraction = Math.Clamp((ppm - 400) / 1600.0, 0.0, 1.0);
        GaugeLeft.Width = new GridLength(Math.Max(fraction, 0.001), GridUnitType.Star);
        GaugeRight.Width = new GridLength(Math.Max(1 - fraction, 0.001), GridUnitType.Star);
        GaugeMarkerGrid.Visibility = Visibility.Visible;
    }

    private void GaugeNumberCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => PositionGaugeNumberLabels();

    private void GaugeNumberLabel_SizeChanged(object sender, SizeChangedEventArgs e) => PositionGaugeNumberLabels();

    private void PositionGaugeNumberLabels()
    {
        var canvasWidth = GaugeNumberCanvas.ActualWidth;
        if (canvasWidth <= 0)
        {
            return;
        }

        foreach (var (label, value) in new[]
        {
            (Gauge400Label, 400),
            (Gauge1000Label, 1000),
            (Gauge1400Label, 1400),
            (Gauge2000Label, 2000),
        })
        {
            var x = (value - 400) / 1600.0 * canvasWidth;
            var left = Math.Clamp(x - label.ActualWidth / 2, 0, canvasWidth - label.ActualWidth);
            Canvas.SetLeft(label, left);
        }
    }

    private void ClearDetails()
    {
        Co2Text.Text = TemperatureText.Text = HumidityText.Text = PressureText.Text = BatteryText.Text = AgeText.Text = IntervalText.Text = RssiText.Text = "—";
        Co2UnitText.Visibility = Visibility.Collapsed;
        Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
        Co2CaptionText.Visibility = Visibility.Visible;
        Co2AdviceText.Visibility = Visibility.Collapsed;
        Co2Text.Text = "–";
        Co2Text.Opacity = 1;
        TemperatureUnitText.Text = Metrics.Unit(MetricKind.Temperature, preferences.TemperatureDisplayUnit);
        HumidityUnitText.Text = Metrics.Unit(MetricKind.Humidity, preferences.TemperatureDisplayUnit);
        PressureUnitText.Text = Metrics.Unit(MetricKind.Pressure, preferences.TemperatureDisplayUnit);
        HumidityRangeText.Text = string.Empty;
        BatteryBar.Value = 0;
        DetailSignal.Bars = 0;
        ShowQuality(0);
        SyncHistoryButton.IsEnabled = false;
        SyncHistoryButton.ToolTip = "Select a sensor to sync its stored history.";
        ChipNameText.Text = "Searching for sensor…";
        DeviceDot.Fill = DotIdle;
        TemperatureRangeText.Text = HumidityRangeText.Text = PressureRangeText.Text = "No readings yet";
        AdvertisementText.Clear();
        ScanResponseText.Clear();
        Title = "Aranet4 Monitor";
        TrendText.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(Co2Tab, "CO₂, no reading, show history");
        AutomationProperties.SetName(TemperatureTab, "Temperature, no reading, show history");
        AutomationProperties.SetName(HumidityTab, "Humidity, no reading, show history");
        AutomationProperties.SetName(PressureTab, "Pressure, no reading, show history");
        HistoryChart.Samples = null;
        UpdateHumidityComfort(null);
        ChartStatsText.Text = "No readings yet";
        HistoryChart.InvalidateVisual();
        UpdateHeaderSensorState();
        UpdateDevicePopover();
        UpdateTray();
    }
}
