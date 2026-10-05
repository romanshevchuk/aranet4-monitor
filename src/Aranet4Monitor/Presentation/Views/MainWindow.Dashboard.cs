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

        var stale = sensorMonitor.IsStale(
            device.Address,
            GetLastReadingTime(device),
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
        FooterBatteryText.Text = device.Battery;
        BatteryBar.Value = device.BatteryValue;
        FooterBatteryBar.Value = device.BatteryValue;
        var batteryBrush = new SolidColorBrush(device.BatteryValue switch
        {
            <= 15 => Color.FromRgb(0xEF, 0x5B, 0x5B),
            <= 35 => Color.FromRgb(0xF5, 0xB9, 0x42),
            _ => Color.FromRgb(0x22, 0xC5, 0x5E),
        });
        BatteryBar.Foreground = batteryBrush;
        FooterBatteryBar.Foreground = batteryBrush;
        FooterBatteryText.Foreground = batteryBrush;

        DeviceMeasurementAgeText.Text = device.MeasurementAge;
        DeviceIntervalText.Text = device.MeasurementInterval;
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
        UpdateCo2DaySummary(device, DateTime.Now);

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
        var stale = sensorMonitor.IsStale(device.Address, lastReading, DateTime.Now);
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
            TrendText.Foreground = (Brush)FindResource("TextPrimary");
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

    private string MetricSummary(Aranet4Device? device, MetricKind kind, DateTime now)
    {
        if (device is null)
        {
            return "No readings yet";
        }

        if (kind == MetricKind.Humidity)
        {
            int? humidity = device.HumidityValue > 0
                ? (int)Math.Round(device.HumidityValue)
                : device.History.LastOrDefault(sample => sample.HumidityPercent.HasValue)?.HumidityPercent;
            if (humidity is not { } value)
            {
                return "No readings yet";
            }

            var condition = value < 30 ? "Dry" : value <= 50 ? "Comfortable" : value <= 60 ? "Humid" : "Damp";
            return $"{condition} · ideal 30–50%";
        }

        var readings = device.History
            .Where(sample => sample.Time >= now - TimeSpan.FromHours(1) && sample.Time <= now)
            .Select(sample => (sample.Time, Value: Metrics.Value(sample, kind, preferences.TemperatureDisplayUnit)))
            .Where(item => item.Value.HasValue)
            .Select(item => (item.Time, Value: item.Value!.Value))
            .OrderBy(item => item.Time)
            .ToArray();
        if (readings.Length < 2)
        {
            return "No recent trend";
        }

        var delta = readings[^1].Value - readings[0].Value;
        var threshold = kind == MetricKind.Temperature && preferences.TemperatureDisplayUnit == TemperatureUnit.Fahrenheit ? 0.9 : kind == MetricKind.Temperature ? 0.5 : 1;
        var trend = Math.Abs(delta) < threshold ? "Steady" : delta > 0 ? "Rising" : "Falling";

        return $"{trend} this hour";
    }

    private void UpdateCo2DaySummary(Aranet4Device? device, DateTime now)
    {
        var samples = device?.History
            .Where(sample => sample.Ppm > 0 && sample.Time >= now - TimeSpan.FromHours(24) && sample.Time <= now)
            .OrderBy(sample => sample.Time)
            .ToArray() ?? [];

        if (samples.Length == 0)
        {
            Co2DayShareText.Text = "—";
            Co2DayShareDetailText.Text = "No readings · 24 h";
            Co2DayPeakText.Text = "—";
            Co2DayPeakTimeText.Text = "No readings";
            Co2AiringTimeText.Text = "—";
            Co2AiringDetailText.Text = "No clear drop detected";
            return;
        }

        var belowThreshold = samples.Count(sample => sample.Ppm < 1000);
        Co2DayShareText.Text = $"{Math.Round(belowThreshold * 100.0 / samples.Length):0}%";
        Co2DayShareDetailText.Text = "Good · 24 h";

        var peak = samples.MaxBy(sample => sample.Ppm)!;
        Co2DayPeakText.Text = $"{peak.Ppm:N0} ppm";
        Co2DayPeakTimeText.Text = $"Peak · {peak.Time.ToString("t", CultureInfo.CurrentCulture)}";

        var airingEvents = AiringDetector.Detect(samples);
        if (airingEvents.Count == 0)
        {
            Co2AiringTimeText.Text = "—";
            Co2AiringDetailText.Text = "No clear drop detected";
            return;
        }

        var latestAiring = airingEvents[^1];
        Co2AiringTimeText.Text = latestAiring.Start.ToString("t", CultureInfo.CurrentCulture);
        Co2AiringDetailText.Text = $"Down {latestAiring.DropPpm:N0} ppm over {DescribeDuration(latestAiring.Duration)}";
    }

    private void RefreshChart()
    {
        var device = Dashboard.SelectedDevice;
        var now = DateTime.Now;

        UpdateCo2DaySummary(device, now);

        HistoryChart.Metric = Dashboard.SelectedMetric;
        HistoryChart.TemperatureDisplayUnit = preferences.TemperatureDisplayUnit;
        HistoryChart.Samples = device?.History;
        HistoryChart.Range = Dashboard.HistoryRange;
        HistoryChart.InvalidateVisual();
        AutomationProperties.SetName(HistoryChart, $"{Metrics.Title(Dashboard.SelectedMetric)} history chart");

        ChartTitleText.Text = Metrics.Title(Dashboard.SelectedMetric);
        ChartDot.Fill = AccentBrushes[Dashboard.SelectedMetric];
        ChartContextDot.Fill = AccentBrushes[Dashboard.SelectedMetric];
        UpdateChartContext();
        ChartStatsText.Text = device is null
            ? "No readings yet"
            : DescribeChartSummary(device.History, Dashboard.HistoryRange, now, Dashboard.SelectedMetric);

        TemperatureRangeText.Text = MetricSummary(device, MetricKind.Temperature, now);
        HumidityRangeText.Text = MetricSummary(device, MetricKind.Humidity, now);
        PressureRangeText.Text = MetricSummary(device, MetricKind.Pressure, now);

        if (HistoryView is { Visibility: Visibility.Visible })
        {
            RefreshHistoryView();
        }
    }

    private string DescribeChartSummary(
        IReadOnlyList<Co2Sample> samples,
        TimeSpan? range,
        DateTime now,
        MetricKind kind)
    {
        var from = range is null ? DateTime.MinValue : now - range.Value;
        var values = samples
            .Where(sample => sample.Time >= from && sample.Time <= now)
            .Select(sample => Metrics.Value(sample, kind, preferences.TemperatureDisplayUnit))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        if (values.Length == 0)
        {
            return Metrics.Describe(samples, range, now, kind, preferences.TemperatureDisplayUnit);
        }

        var unit = kind == MetricKind.Humidity
            ? "%"
            : $" {Metrics.Unit(kind, preferences.TemperatureDisplayUnit)}";
        return $"Low {Metrics.Format(values.Min(), kind)} · average {Metrics.Format(values.Average(), kind)} · high {Metrics.Format(values.Max(), kind)}{unit}";
    }

    private void UpdateChartContext()
    {
        var metric = Dashboard.SelectedMetric;
        var device = Dashboard.SelectedDevice;
        var unit = preferences.TemperatureDisplayUnit;
        Brush dot = AccentBrushes[metric];
        string title, text;

        switch (metric)
        {
            case MetricKind.Co2:
                title = "Outdoor reference · about 420 ppm";
                text = "Fresh outdoor air is the lowest level a room can reach.";
                dot = ThemeBrush("TextSecondary", dot); // neutral: an outdoor reference is not a quality verdict
                break;

            case MetricKind.Humidity when device?.HumidityValue is { } humidity:
                (title, text, dot) = humidity switch
                {
                    < 30 => ("Dry humidity", "Air is dry. The recommended range for homes is 30–50%.", ThemeBrush("Co2Fair", dot)),
                    <= 50 => ("Comfortable humidity", "Within the recommended range for homes (30–50%).", ThemeBrush("Co2Good", dot)),
                    <= 60 => ("Humid", "Above 50% dust mites start to thrive. Aim for 30–50%.", ThemeBrush("Co2Fair", dot)),
                    _ => ("Damp", "Mould and dust mites flourish above 60%. Ventilate or dehumidify.", ThemeBrush("Co2Poor", dot)),
                };
                break;

            case MetricKind.Humidity:
                title = "Recommended range · 30–50%";
                text = "Comfort depends on the room; this is a general guide for homes.";
                break;

            case MetricKind.Temperature when device?.TemperatureCelsius is { } temperature:
                {
                    var celsius = (double)temperature;
                    var comfort = $"{Metrics.ConvertTemperature(20, unit):0}–{Metrics.ConvertTemperature(24, unit):0} {Metrics.Unit(MetricKind.Temperature, unit)}";
                    (title, text) = celsius switch
                    {
                        < 18 => ("Cold", $"Well below the {comfort} comfort range."),
                        < 20 => ("Cool", $"Slightly below the {comfort} comfort range."),
                        <= 24 => ("Comfortable", $"Within the {comfort} comfort range."),
                        _ => ("Warm", $"Above the {comfort} comfort range."),
                    };
                    break;
                }

            case MetricKind.Temperature:
                title = "Temperature trend";
                text = $"Temperature is shown in {Metrics.Unit(MetricKind.Temperature, unit)}; comfort varies by person and activity.";
                break;

            default:
                title = "Pressure trend";
                text = "Absolute pressure varies with altitude; trends are more useful.";
                break;
        }

        ChartContextTitleText.Text = title;
        ChartContextText.Text = text;
        ChartContextDot.Fill = dot;
        ChartContextInfo.Visibility = metric == MetricKind.Co2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Brush ThemeBrush(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

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
            Co2Level.Good => ("●", (Color)FindResource("Co2GoodColor")),
            Co2Level.Fair => ("●", (Color)FindResource("Co2FairColor")),
            _ => ("●", (Color)FindResource("Co2PoorColor")),
        };
        QualityIcon.Text = icon;
        QualityText.Text = level switch
        {
            Co2Level.Good => "Good",
            Co2Level.Fair => "Elevated",
            _ => "High",
        };
        var statusColor = level switch
        {
            Co2Level.Good => (Color)FindResource("PositiveColor"),
            Co2Level.Fair => (Color)FindResource("WarningColor"),
            _ => (Color)FindResource("DangerColor"),
        };
        QualityBadge.Background = new SolidColorBrush(Color.FromArgb(0x1F, color.R, color.G, color.B));
        QualityBadge.BorderBrush = new SolidColorBrush(color);
        QualityBadge.BorderThickness = new Thickness(1);
        QualityIcon.Foreground = new SolidColorBrush(statusColor);
        QualityText.Foreground = new SolidColorBrush(statusColor);
        QualityBadge.Visibility = Visibility.Visible;
        Co2AdviceText.Text = ppm < Co2Quality.FairFromPpm
            ? "Comfortable. Nothing to do."
            : ppm < Co2Quality.PoorFromPpm
                ? "Open a window for about 10 minutes."
                : "Open a window or door when you can.";
        Co2AdviceText.Visibility = Visibility.Visible;

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
        Co2Text.Text = TemperatureText.Text = HumidityText.Text = PressureText.Text = BatteryText.Text = FooterBatteryText.Text = DeviceMeasurementAgeText.Text = DeviceIntervalText.Text = RssiText.Text = "—";
        Co2UnitText.Visibility = Visibility.Collapsed;
        Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
        UpdateCo2DaySummary(null, DateTime.Now);
        Co2CaptionText.Visibility = Visibility.Visible;
        Co2AdviceText.Visibility = Visibility.Collapsed;
        Co2Text.Text = "–";
        Co2Text.Opacity = 1;
        TemperatureUnitText.Text = Metrics.Unit(MetricKind.Temperature, preferences.TemperatureDisplayUnit);
        HumidityUnitText.Text = Metrics.Unit(MetricKind.Humidity, preferences.TemperatureDisplayUnit);
        PressureUnitText.Text = Metrics.Unit(MetricKind.Pressure, preferences.TemperatureDisplayUnit);
        HumidityRangeText.Text = string.Empty;
        BatteryBar.Value = 0;
        FooterBatteryBar.Value = 0;
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
        ChartStatsText.Text = "No readings yet";
        HistoryChart.InvalidateVisual();
        UpdateHeaderSensorState();
        UpdateDevicePopover();
        UpdateTray();
    }
}
