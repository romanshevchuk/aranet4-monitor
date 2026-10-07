using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aranet4Monitor.Domain.AirQuality;
using Aranet4Monitor.Domain.Measurements;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation.Controls;
using Aranet4Monitor.Presentation.Formatting;
using WpfEllipse = System.Windows.Shapes.Ellipse;

namespace Aranet4Monitor.Presentation.Views.History;

public partial class HistoryPage : System.Windows.Controls.UserControl
{
    public event RoutedEventHandler? SyncHistoryRequested;

    public HistoryPage()
    {
        InitializeComponent();
        HistorySidebarControl.ExportRequested += (_, args) => ExportRequested?.Invoke(this, args);
    }

    public event RoutedEventHandler? ExportRequested;

    public Grid HistoryView => HistoryViewControl;

    public TextBlock HistoryHeaderSummaryText => HistoryHeaderSummaryTextControl;

    public TextBlock HistoryLastSyncText => HistoryLastSyncTextControl;

    public System.Windows.Controls.Button SyncHistoryButton => SyncHistoryButtonControl;

    public Grid HistoryContentGrid => HistoryContentGridControl;

    public Border HistoryMainCard => HistoryMainCardControl;

    public TextBlock HistoryChartStatsText => HistoryChartStatsTextControl;

    public MetricChart LongHistoryChart => LongHistoryChartControl;

    public DayStripSection DayStripSection => DayStripSectionControl;

    public HistorySidebar HistorySidebar => HistorySidebarControl;

    public void ScrollToTop() => HistoryScrollViewer.ScrollToTop();

    public void ApplyNarrowLayout(bool useNarrowLayout)
    {
        HistoryContentGrid.ColumnDefinitions.Clear();
        HistoryContentGrid.RowDefinitions.Clear();

        if (useNarrowLayout)
        {
            HistoryView.Margin = new Thickness(16, 14, 16, 14);
            HistoryContentGrid.ColumnDefinitions.Add(new ColumnDefinition());
            HistoryContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            HistoryContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(HistoryMainCard, 0);
            Grid.SetColumn(HistoryMainCard, 0);
            HistoryMainCard.Margin = new Thickness(0, 0, 0, 14);
            Grid.SetRow(HistorySidebar, 1);
            Grid.SetColumn(HistorySidebar, 0);
            HistorySidebar.Margin = new Thickness(0, 0, 0, 14);
            return;
        }

        HistoryView.Margin = new Thickness(26, 24, 26, 18);
        HistoryContentGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star),
            MinWidth = 420,
        });
        HistoryContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        HistoryContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(HistoryMainCard, 0);
        Grid.SetColumn(HistoryMainCard, 0);
        HistoryMainCard.Margin = new Thickness(0, 0, 14, 0);
        Grid.SetRow(HistorySidebar, 0);
        Grid.SetColumn(HistorySidebar, 1);
        HistorySidebar.Margin = new Thickness(0);
    }

    public void RefreshHistory(Aranet4Device? device, TimeSpan selectedRange, DateTime? syncCursor, bool isSyncing)
    {
        if (LongHistoryChart is null)
        {
            return;
        }

        var now = DateTime.Now;
        var allCo2 = device?.History.Where(sample => sample.Ppm > 0).ToArray() ?? [];
        var recentCo2 = allCo2
            .Where(sample => sample.Time >= now - TimeSpan.FromHours(24) && sample.Time <= now)
            .ToArray();
        LongHistoryChart.Metric = MetricKind.Co2;
        LongHistoryChart.Samples = device?.History;
        LongHistoryChart.Range = selectedRange;
        LongHistoryChart.TimeAxisLabels = HistoryTimeAxisLabels.ForRange(now, selectedRange);
        LongHistoryChart.InvalidateVisual();

        HistoryHeaderSummaryText.Text = recentCo2.Length == 0
            ? device is null
                ? "Select a sensor to explore its saved readings."
                : "No readings in the last 24 hours"
            : DescribeDayQuality(Share(recentCo2.Count(sample => sample.Ppm < 1_000), recentCo2.Length));
        HistoryLastSyncText.Text = device is null
            ? "Not synced yet"
            : syncCursor is { } syncedThrough
                ? $"Last synced {DescribeAge(now - syncedThrough.ToLocalTime())} · {allCo2.Length:N0} readings"
                : $"Not synced yet · {allCo2.Length:N0} readings";
        SyncHistoryButton.IsEnabled = device is not null && !isSyncing;
        SyncHistoryButton.Content = isSyncing ? "Syncing…" : "Sync history";
        SyncHistoryButton.ToolTip = isSyncing
            ? "History is being downloaded from the selected sensor."
            : device is null
                ? "Select a sensor to sync its stored history."
                : "Sync stored CO₂, temperature, humidity and pressure history for the selected sensor";
        HistorySidebar.SetExportEnabled(allCo2.Length > 0);
        HistoryChartStatsText.Text = device is null
            ? "No readings yet"
            : Metrics.Describe(device.History, selectedRange, now, MetricKind.Co2);

        RefreshDayAtAGlance(device, now);
    }

    private void RefreshDayAtAGlance(Aranet4Device? device, DateTime now)
    {
        const int bucketCount = 72;
        var start = now - TimeSpan.FromHours(24);
        var daySamples = device?.History
            .Where(sample => sample.Ppm > 0 && sample.Time >= start && sample.Time <= now)
            .OrderBy(sample => sample.Time)
            .ToArray() ?? [];
        var airingEvents = AiringDetector.Detect(daySamples);

        DayStripSection.ClearCells();
        for (var index = 0; index < bucketCount; index++)
        {
            var bucketStart = start + TimeSpan.FromMinutes(index * 20);
            var bucketEnd = bucketStart + TimeSpan.FromMinutes(20);
            var bucket = daySamples
                .Where(sample => sample.Time >= bucketStart && sample.Time < bucketEnd)
                .ToArray();

            var cell = new Grid
            {
                Margin = new Thickness(1.5, 0, 1.5, 0),
            };
            var stripe = new Border
            {
                Background = (Brush)FindResource("SurfaceSecondary"),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 8, 0, 0),
                ToolTip = $"{bucketStart:HH:mm} · no reading",
            };
            cell.Children.Add(stripe);
            if (bucket.Length > 0)
            {
                var average = (int)Math.Round(bucket.Average(sample => sample.Ppm));
                var level = Co2Quality.Classify(average);
                stripe.Background = (Brush)FindResource(level switch
                {
                    Co2Level.Good => "Co2GoodZone",
                    Co2Level.Fair => "Co2FairZone",
                    _ => "Co2PoorZone",
                });
                stripe.ToolTip = $"{bucketStart:HH:mm} · {average:N0} ppm";
            }

            if (airingEvents.Any(item => item.Start >= bucketStart && item.Start < bucketEnd))
            {
                cell.Children.Add(new WpfEllipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = (Brush)FindResource("TextPrimary"),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    ToolTip = "Airing detected",
                });
            }

            DayStripSection.AddCell(cell);
        }

        DayStripSection.SetTimeLabels(
            "Yesterday " + start.ToString("HH:mm", CultureInfo.CurrentCulture),
            (start + TimeSpan.FromHours(6)).ToString("HH:mm", CultureInfo.CurrentCulture),
            (start + TimeSpan.FromHours(12)).ToString("HH:mm", CultureInfo.CurrentCulture),
            (start + TimeSpan.FromHours(18)).ToString("HH:mm", CultureInfo.CurrentCulture));

        HistorySidebar.ClearZoneShare();
        if (daySamples.Length == 0)
        {
            HistorySidebar.SetZoneShareText("No readings in the last 24 hours.");
            HistorySidebar.SetOvernight(": no readings yet.");
            HistorySidebar.SetAiring(" was not detected in the last 24 hours.");
            HistorySidebar.SetPeak(": no readings yet.");
            return;
        }

        var zoneShares = HistoryZoneShareCalculator.Calculate(daySamples, now);
        var total = zoneShares.Total;
        if (total <= TimeSpan.Zero)
        {
            HistorySidebar.SetZoneShareText("Not enough time-spaced readings to estimate zone share.");
        }

        var zones = new[]
        {
            (Duration: zoneShares.Good, Percent: zoneShares.GoodPercent, Resource: "Co2GoodZone"),
            (Duration: zoneShares.Elevated, Percent: zoneShares.ElevatedPercent, Resource: "Co2FairZone"),
            (Duration: zoneShares.High, Percent: zoneShares.HighPercent, Resource: "Co2PoorZone"),
        };
        foreach (var zone in zones.Where(zone => zone.Duration > TimeSpan.Zero))
        {
            HistorySidebar.AddZoneShareSegment(
                (Brush)FindResource(zone.Resource),
                zone.Duration.TotalSeconds,
                $"{zone.Percent}% of observed time");
        }

        if (total > TimeSpan.Zero)
        {
            HistorySidebar.SetZoneShareText(
                $"{zoneShares.GoodPercent}% good · {zoneShares.ElevatedPercent}% elevated · {zoneShares.HighPercent}% high");
        }

        var overnight = daySamples
            .Where(sample => sample.Time.Hour >= 23 || sample.Time.Hour < 7)
            .ToArray();
        var overnightDetail = overnight.Length == 0
            ? " (23:00–07:00): no readings yet."
            : $" (23:00–07:00): average {overnight.Average(sample => sample.Ppm):N0} ppm, peaking at {overnight.Max(sample => sample.Ppm):N0} ppm.";
        HistorySidebar.SetOvernight(overnightDetail);

        AiringEvent? latestAiring = airingEvents.Count > 0 ? airingEvents[^1] : null;
        var airingDetail = latestAiring is { } airing
            ? $" detected {airingEvents.Count} time{(airingEvents.Count == 1 ? string.Empty : "s")}. Latest at {airing.Start:t}: CO₂ fell about {airing.DropPpm:N0} ppm over {DescribeDuration(airing.Duration)}."
            : " was not detected in the last 24 hours.";
        HistorySidebar.SetAiring(airingDetail);

        var peak = daySamples.MaxBy(sample => sample.Ppm)!;
        HistorySidebar.SetPeak($": {peak.Ppm:N0} ppm at {peak.Time:t}.");
    }

    private static int Share(int count, int total) => total == 0 ? 0 : (int)Math.Round(count * 100.0 / total);

    private static string DescribeDayQuality(int goodPercent) =>
        $"{(goodPercent >= 80 ? "Mostly good" : goodPercent >= 50 ? "Mixed" : "Mostly elevated")}: {goodPercent}% of the last 24 h stayed below 1,000 ppm.";

    private static string DescribeDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{duration.TotalHours:0.#} hours"
        : $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} minutes";

    private static string DescribeAge(TimeSpan age) => age.TotalMinutes < 1
        ? "just now"
        : age.TotalHours < 1
            ? $"{(int)age.TotalMinutes} min ago"
            : age.TotalDays < 1
                ? $"{(int)age.TotalHours} h ago"
                : $"{(int)age.TotalDays} d ago";

    private void SyncHistory_Click(object sender, RoutedEventArgs e) => SyncHistoryRequested?.Invoke(sender, e);
}
