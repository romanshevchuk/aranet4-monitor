using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Aranet4Monitor.Presentation.Controls;
using Aranet4Monitor.Presentation.Formatting;

namespace Aranet4Monitor.Presentation.Views.Live;

public partial class LivePage : System.Windows.Controls.UserControl
{
    public LivePage()
    {
        InitializeComponent();
    }

    public Grid DashboardGrid => DashboardGridControl;

    public Grid Co2HeroPanel => Co2HeroPanelControl;

    public System.Windows.Controls.RadioButton Co2Tab => Co2TabControl;

    public StackPanel QualityBadge => QualityBadgeControl;

    public System.Windows.Shapes.Ellipse QualityIcon => QualityIconControl;

    public TextBlock QualityText => QualityTextControl;

    public TextBlock Co2Text => Co2TextControl;

    public TextBlock Co2UnitText => Co2UnitTextControl;

    public TextBlock TrendText => TrendTextControl;

    public TextBlock Co2CaptionText => Co2CaptionTextControl;

    public TextBlock Co2AdviceText => Co2AdviceTextControl;

    public Grid GaugeBar => GaugeBarControl;

    public Grid GaugeMarkerGrid => GaugeMarkerGridControl;

    public ColumnDefinition GaugeLeft => GaugeLeftControl;

    public ColumnDefinition GaugeRight => GaugeRightControl;

    public Canvas GaugeNumberCanvas => GaugeNumberCanvasControl;

    public TextBlock Gauge400Label => Gauge400LabelControl;

    public TextBlock Gauge1000Label => Gauge1000LabelControl;

    public TextBlock Gauge1400Label => Gauge1400LabelControl;

    public TextBlock Gauge2000Label => Gauge2000LabelControl;

    public TextBlock GoodZoneText => GoodZoneTextControl;

    public TextBlock FairZoneText => FairZoneTextControl;

    public TextBlock PoorZoneText => PoorZoneTextControl;

    public TextBlock Co2DayShareText => Co2DayShareTextControl;

    public TextBlock Co2DayShareDetailText => Co2DayShareDetailTextControl;

    public TextBlock Co2DayPeakText => Co2DayPeakTextControl;

    public TextBlock Co2DayPeakTimeText => Co2DayPeakTimeTextControl;

    public TextBlock Co2AiringTimeText => Co2AiringTimeTextControl;

    public TextBlock Co2AiringDetailText => Co2AiringDetailTextControl;

    public Border HistoryChartCard => HistoryChartCardControl;

    public System.Windows.Shapes.Ellipse ChartDot => ChartDotControl;

    public TextBlock ChartTitleText => ChartTitleTextControl;

    public TextBlock ChartStatsText => ChartStatsTextControl;

    public MetricChart HistoryChart => HistoryChartControl;

    public System.Windows.Shapes.Ellipse ChartContextDot => ChartContextDotControl;

    public TextBlock ChartContextTitleText => ChartContextTitleTextControl;

    public TextBlock ChartContextInfo => ChartContextInfoControl;

    public TextBlock ChartContextText => ChartContextTextControl;

    public Grid SecondaryMetricsGrid => SecondaryMetricsGridControl;

    public System.Windows.Controls.RadioButton TemperatureTab => TemperatureTabControl;

    public TextBlock TemperatureText => TemperatureTextControl;

    public TextBlock TemperatureUnitText => TemperatureUnitTextControl;

    public TextBlock TemperatureRangeText => TemperatureRangeTextControl;

    public System.Windows.Controls.RadioButton HumidityTab => HumidityTabControl;

    public TextBlock HumidityText => HumidityTextControl;

    public TextBlock HumidityUnitText => HumidityUnitTextControl;

    public TextBlock HumidityRangeText => HumidityRangeTextControl;

    public System.Windows.Controls.RadioButton PressureTab => PressureTabControl;

    public TextBlock PressureText => PressureTextControl;

    public TextBlock PressureUnitText => PressureUnitTextControl;

    public TextBlock PressureRangeText => PressureRangeTextControl;

    public void ScrollToTop() => LiveScrollViewer.ScrollToTop();

    public void Activate()
    {
        ScrollToTop();
        Co2Tab.Focus();
    }

    public void ApplyNarrowLayout(bool useNarrowLayout)
    {
        DashboardGrid.RowDefinitions.Clear();
        DashboardGrid.ColumnDefinitions.Clear();

        if (useNarrowLayout)
        {
            DashboardGrid.ColumnDefinitions.Add(new ColumnDefinition());
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(Co2HeroPanel, 0);
            Grid.SetColumn(Co2HeroPanel, 0);
            Grid.SetRow(HistoryChartCard, 1);
            Grid.SetColumn(HistoryChartCard, 0);
            Grid.SetRow(SecondaryMetricsGrid, 2);
            Grid.SetColumn(SecondaryMetricsGrid, 0);
            Grid.SetColumnSpan(SecondaryMetricsGrid, 1);
            Co2HeroPanel.Margin = new Thickness(0, 0, 0, 12);
            HistoryChartCard.Margin = new Thickness(0, 0, 0, 12);
            HistoryChart.MinHeight = 190;
            DashboardGrid.Margin = new Thickness(16, 14, 16, 14);
            return;
        }

        DashboardGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(0.82, GridUnitType.Star),
            MinWidth = 330,
        });
        DashboardGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1.45, GridUnitType.Star),
            MinWidth = 420,
        });
        DashboardGrid.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star),
            MinHeight = 300,
        });
        DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(Co2HeroPanel, 0);
        Grid.SetColumn(Co2HeroPanel, 0);
        Grid.SetRow(HistoryChartCard, 0);
        Grid.SetColumn(HistoryChartCard, 1);
        Grid.SetRow(SecondaryMetricsGrid, 1);
        Grid.SetColumn(SecondaryMetricsGrid, 0);
        Grid.SetColumnSpan(SecondaryMetricsGrid, 2);
        Co2HeroPanel.Margin = new Thickness(0, 0, 16, 14);
        HistoryChartCard.Margin = new Thickness(0, 0, 0, 14);
        HistoryChart.MinHeight = 150;
        DashboardGrid.Margin = new Thickness(26, 24, 26, 18);
    }

    public void SetMetricValueAndUnit(TextBlock valueText, TextBlock unitText, string display, MetricKind kind, TemperatureUnit temperatureUnit)
    {
        var unit = Metrics.Unit(kind, temperatureUnit);
        valueText.Text = display.EndsWith(unit, StringComparison.Ordinal)
            ? display[..^unit.Length].TrimEnd()
            : display;
        unitText.Text = unit;
    }

    public void ApplyTemperatureUnit(TemperatureUnit temperatureUnit)
    {
        HistoryChart.TemperatureDisplayUnit = temperatureUnit;
        TemperatureUnitText.Text = Metrics.Unit(MetricKind.Temperature, temperatureUnit);
    }

    public void ShowDeviceDetails(
        Aranet4Device device,
        TemperatureUnit temperatureUnit,
        DateTime lastReading,
        bool isStale,
        DateTime now)
    {
        var hasReading = device.Co2Ppm > 0;
        Co2Text.Text = hasReading ? device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture) : "—";
        Co2UnitText.Visibility = hasReading ? Visibility.Visible : Visibility.Collapsed;
        RefreshCo2Summary(device, lastReading, isStale, now);

        SetMetricValueAndUnit(TemperatureText, TemperatureUnitText, device.Temperature, MetricKind.Temperature, temperatureUnit);
        SetMetricValueAndUnit(HumidityText, HumidityUnitText, device.Humidity, MetricKind.Humidity, temperatureUnit);
        SetMetricValueAndUnit(PressureText, PressureUnitText, device.Pressure, MetricKind.Pressure, temperatureUnit);
        AutomationProperties.SetName(TemperatureTab, $"Temperature, {device.Temperature}, show history");
        AutomationProperties.SetName(HumidityTab, $"Humidity, {device.Humidity}, show history");
        AutomationProperties.SetName(PressureTab, $"Pressure, {device.Pressure}, show history");
        AutomationProperties.SetName(Co2Tab, $"CO₂, {(hasReading ? $"{device.Co2Ppm:N0} ppm" : "no reading")}, show history");
    }

    public void RefreshCo2Summary(Aranet4Device device, DateTime lastReading, bool isStale, DateTime now) =>
        UpdateCo2Hero(device, lastReading, isStale, now);

    public void ClearDeviceDetails(TemperatureUnit temperatureUnit, DateTime now)
    {
        Co2Text.Text = "–";
        Co2Text.Opacity = 1;
        Co2UnitText.Visibility = Visibility.Collapsed;
        Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
        Co2CaptionText.Visibility = Visibility.Visible;
        Co2AdviceText.Visibility = Visibility.Collapsed;
        Co2ExplanationText.Visibility = Visibility.Collapsed;
        TrendText.Visibility = Visibility.Collapsed;
        UpdateCo2DaySummary(null, now);
        TemperatureText.Text = HumidityText.Text = PressureText.Text = "—";
        TemperatureUnitText.Text = Metrics.Unit(MetricKind.Temperature, temperatureUnit);
        HumidityUnitText.Text = Metrics.Unit(MetricKind.Humidity, temperatureUnit);
        PressureUnitText.Text = Metrics.Unit(MetricKind.Pressure, temperatureUnit);
        TemperatureRangeText.Text = HumidityRangeText.Text = PressureRangeText.Text = "No readings yet";
        HumidityRangeText.Text = string.Empty;
        ShowQuality(0);
        AutomationProperties.SetName(Co2Tab, "CO₂, no reading, show history");
        AutomationProperties.SetName(TemperatureTab, "Temperature, no reading, show history");
        AutomationProperties.SetName(HumidityTab, "Humidity, no reading, show history");
        AutomationProperties.SetName(PressureTab, "Pressure, no reading, show history");
        HistoryChart.Samples = null;
        ChartStatsText.Text = "No readings yet";
        HistoryChart.InvalidateVisual();
    }

    private void UpdateCo2Hero(Aranet4Device device, DateTime lastReading, bool isStale, DateTime now)
    {
        UpdateCo2DaySummary(device, now);
        if (device.Co2Ppm <= 0)
        {
            Co2Text.Text = "–";
            Co2Text.Opacity = 1;
            Co2UnitText.Visibility = Visibility.Collapsed;
            Co2CaptionText.Text = "Waiting for your first live reading, or sync history to download stored readings.";
            Co2CaptionText.Visibility = Visibility.Visible;
            Co2AdviceText.Visibility = Visibility.Collapsed;
            Co2ExplanationText.Visibility = Visibility.Collapsed;
            TrendText.Visibility = Visibility.Collapsed;
            ShowQuality(0);
            AutomationProperties.SetName(Co2Tab, "CO₂, no reading, show history");
            return;
        }

        Co2Text.Text = device.Co2Ppm.ToString("N0", CultureInfo.CurrentCulture);
        Co2Text.Opacity = isStale ? 0.6 : 1;
        Co2UnitText.Visibility = Visibility.Visible;
        ShowQuality(device.Co2Ppm);

        if (isStale)
        {
            var elapsed = now - lastReading;
            var age = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours} hr ago"
                : $"{Math.Max(1, (int)elapsed.TotalMinutes)} min ago";
            TrendText.Text = $"Last reading {age}";
            TrendText.Foreground = ThemeService.GetBrush("Warning");
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

    public void RefreshChart(Aranet4Device? device, MetricKind selectedMetric, TimeSpan? range, TemperatureUnit temperatureUnit, DateTime now)
    {
        UpdateCo2DaySummary(device, now);
        HistoryChart.Metric = selectedMetric;
        HistoryChart.TemperatureDisplayUnit = temperatureUnit;
        HistoryChart.Samples = device?.History;
        HistoryChart.Range = range;
        HistoryChart.InvalidateVisual();
        AutomationProperties.SetName(HistoryChart, $"{Metrics.Title(selectedMetric)} history chart");

        ChartTitleText.Text = Metrics.Title(selectedMetric);
        ChartDot.Fill = ThemeService.AccentBrush(selectedMetric);
        ChartContextDot.Fill = ThemeService.AccentBrush(selectedMetric);
        UpdateChartContext(device, selectedMetric, temperatureUnit);
        ChartStatsText.Text = device is null
            ? "No readings yet"
            : DescribeChartSummary(device.History, range, now, selectedMetric, temperatureUnit);

        TemperatureRangeText.Text = MetricSummary(device, MetricKind.Temperature, now, temperatureUnit);
        HumidityRangeText.Text = MetricSummary(device, MetricKind.Humidity, now, temperatureUnit);
        PressureRangeText.Text = MetricSummary(device, MetricKind.Pressure, now, temperatureUnit);
    }

    private static string MetricSummary(Aranet4Device? device, MetricKind kind, DateTime now, TemperatureUnit temperatureUnit)
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
            .Select(sample => (sample.Time, Value: Metrics.Value(sample, kind, temperatureUnit)))
            .Where(item => item.Value.HasValue)
            .Select(item => (item.Time, Value: item.Value!.Value))
            .OrderBy(item => item.Time)
            .ToArray();
        if (readings.Length < 2)
        {
            return "No recent trend";
        }

        var delta = readings[^1].Value - readings[0].Value;
        var threshold = kind == MetricKind.Temperature && temperatureUnit == TemperatureUnit.Fahrenheit ? 0.9 : kind == MetricKind.Temperature ? 0.5 : 1;
        var trend = Math.Abs(delta) < threshold ? "Steady" : delta > 0 ? "Rising" : "Falling";

        return $"{trend} this hour";
    }

    public void UpdateCo2DaySummary(Aranet4Device? device, DateTime now)
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
            Co2AiringDetailText.ToolTip = "No clear drop detected";
            Co2AiringTimeText.ToolTip = Co2AiringDetailText.ToolTip;
            return;
        }

        var zoneShares = HistoryZoneShareCalculator.Calculate(samples, now);
        Co2DayShareText.Text = $"{zoneShares.GoodPercent}%";
        Co2DayShareDetailText.Text = "Good · 24 h";

        var peak = samples.MaxBy(sample => sample.Ppm)!;
        Co2DayPeakText.Text = $"{peak.Ppm:N0} ppm";
        Co2DayPeakTimeText.Text = $"Peak · {peak.Time.ToString("t", CultureInfo.CurrentCulture)}";

        var airingEvents = AiringDetector.Detect(samples);
        if (airingEvents.Count == 0)
        {
            Co2AiringTimeText.Text = "—";
            Co2AiringDetailText.ToolTip = "No clear drop detected";
            Co2AiringTimeText.ToolTip = Co2AiringDetailText.ToolTip;
            return;
        }

        var latestAiring = airingEvents[^1];
        Co2AiringTimeText.Text = latestAiring.Start.ToString("t", CultureInfo.CurrentCulture);
        Co2AiringDetailText.ToolTip = $"Down {latestAiring.DropPpm:N0} ppm over {DescribeDuration(latestAiring.Duration)}";
        Co2AiringTimeText.ToolTip = Co2AiringDetailText.ToolTip;
    }

    private static string DescribeChartSummary(
        IReadOnlyList<Co2Sample> samples,
        TimeSpan? range,
        DateTime now,
        MetricKind kind,
        TemperatureUnit temperatureUnit)
    {
        var from = range is null ? DateTime.MinValue : now - range.Value;
        var values = samples
            .Where(sample => sample.Time >= from && sample.Time <= now)
            .Select(sample => Metrics.Value(sample, kind, temperatureUnit))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        if (values.Length == 0)
        {
            return Metrics.Describe(samples, range, now, kind, temperatureUnit);
        }

        var unit = kind == MetricKind.Humidity
            ? "%"
            : $" {Metrics.Unit(kind, temperatureUnit)}";
        return $"Low {Metrics.Format(values.Min(), kind)} · average {Metrics.Format(values.Average(), kind)} · high {Metrics.Format(values.Max(), kind)}{unit}";
    }

    private void UpdateChartContext(Aranet4Device? device, MetricKind metric, TemperatureUnit unit)
    {
        Brush dot = ThemeService.AccentBrush(metric);
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

    private static string DescribeDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{duration.TotalHours:0.#} hours"
        : $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} minutes";

    public void ShowQuality(int ppm)
    {
        if (ppm <= 0)
        {
            Co2Tab.Foreground = ThemeService.GetBrush("Border");
            QualityBadge.Visibility = Visibility.Collapsed;
            Co2ExplanationText.Visibility = Visibility.Collapsed;
            GaugeMarkerGrid.Visibility = Visibility.Collapsed;
            return;
        }

        var level = Co2Quality.Classify(ppm);
        var color = level switch
        {
            Co2Level.Good => (Color)FindResource("Co2GoodColor"),
            Co2Level.Fair => (Color)FindResource("Co2FairColor"),
            _ => (Color)FindResource("Co2PoorColor"),
        };
        QualityText.Text = (level switch
        {
            Co2Level.Good => "Good",
            Co2Level.Fair => "Elevated",
            _ => "High",
        }).ToUpperInvariant();
        var statusColor = level switch
        {
            Co2Level.Good => (Color)FindResource("PositiveColor"),
            Co2Level.Fair => (Color)FindResource("WarningColor"),
            _ => (Color)FindResource("DangerColor"),
        };
        Co2Tab.Foreground = ThemeService.GetBrush(level switch
        {
            Co2Level.Good => "Co2GoodStroke",
            Co2Level.Fair => "Co2FairStroke",
            _ => "Co2PoorStroke",
        });
        QualityIcon.Fill = new SolidColorBrush(statusColor);
        QualityText.Foreground = ThemeService.GetBrush(level switch
        {
            Co2Level.Good => "Co2GoodText",
            Co2Level.Fair => "Co2FairText",
            _ => "Co2PoorText",
        });
        QualityBadge.Visibility = Visibility.Visible;
        Co2ExplanationText.Text = ppm < Co2Quality.FairFromPpm
            ? "CO₂ is in a comfortable range."
            : ppm < Co2Quality.PoorFromPpm
                ? "CO₂ is above the ideal indoor level."
                : "CO₂ is well above the recommended indoor level.";
        Co2ExplanationText.Visibility = Visibility.Visible;
        Co2AdviceText.Text = ppm < Co2Quality.FairFromPpm
            ? "Nothing to do."
            : ppm < Co2Quality.PoorFromPpm
                ? "Open a window for about 10 minutes."
                : "Open a window or door when you can.";
        Co2AdviceText.Visibility = Visibility.Visible;

        var fraction = Math.Clamp((ppm - 400) / 1600.0, 0.0, 1.0);
        GaugeLeft.Width = new GridLength(Math.Max(fraction, 0.001), GridUnitType.Star);
        GaugeRight.Width = new GridLength(Math.Max(1 - fraction, 0.001), GridUnitType.Star);
        GaugeMarkerGrid.Visibility = Visibility.Visible;
    }

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

    private void GaugeNumberCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => PositionGaugeNumberLabels();

    private void GaugeNumberLabel_SizeChanged(object sender, SizeChangedEventArgs e) => PositionGaugeNumberLabels();
}
