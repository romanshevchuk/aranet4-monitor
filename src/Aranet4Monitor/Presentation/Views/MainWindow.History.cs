using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private TimeSpan selectedHistoryRange = TimeSpan.FromDays(1);

    private void HistoryRangeButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }
            || !int.TryParse(tag, CultureInfo.InvariantCulture, out var hours))
        {
            return;
        }

        selectedHistoryRange = TimeSpan.FromHours(hours);
        RefreshHistoryView();
    }

    private void RefreshHistoryView()
    {
        if (LongHistoryChart is null)
        {
            return;
        }

        var device = Dashboard.SelectedDevice;
        var now = DateTime.Now;
        var allCo2 = device?.History.Where(sample => sample.Ppm > 0).ToArray() ?? [];
        var recentCo2 = allCo2
            .Where(sample => sample.Time >= now - TimeSpan.FromHours(24) && sample.Time <= now)
            .ToArray();
        LongHistoryChart.Metric = MetricKind.Co2;
        LongHistoryChart.Samples = device?.History;
        LongHistoryChart.Range = selectedHistoryRange;
        LongHistoryChart.InvalidateVisual();

        HistoryHeaderSummaryText.Text = recentCo2.Length == 0
            ? device is null
                ? "Select a sensor to explore its saved readings."
                : "No readings in the last 24 hours"
            : DescribeDayQuality(Share(recentCo2.Count(sample => sample.Ppm < 1_000), recentCo2.Length));
        HistoryLastSyncText.Text = device is null
            ? "Not synced yet"
            : historySyncService.LoadSyncCursor(device.Address) is { } syncedThrough
                ? $"Last synced {DescribeAge(now - syncedThrough.ToLocalTime())} · {allCo2.Length:N0} readings"
                : $"Not synced yet · {allCo2.Length:N0} readings";
        SyncHistoryButton.IsEnabled = device is not null && syncCancellation is null;
        HistoryExportButton.IsEnabled = allCo2.Length > 0;
        HistoryChartStatsText.Text = device is null
            ? "No readings yet"
            : Metrics.Describe(device.History, selectedHistoryRange, now, MetricKind.Co2);

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

        HistoryDayStrip.Children.Clear();
        for (var index = 0; index < bucketCount; index++)
        {
            var bucketStart = start + TimeSpan.FromMinutes(index * 20);
            var bucketEnd = bucketStart + TimeSpan.FromMinutes(20);
            var bucket = daySamples
                .Where(sample => sample.Time >= bucketStart && sample.Time < bucketEnd)
                .ToArray();

            var border = new Border
            {
                Background = (Brush)FindResource("SurfaceSecondary"),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1),
                ToolTip = $"{bucketStart:t}–{bucketEnd:t}: no CO₂ readings",
            };
            if (bucket.Length > 0)
            {
                var average = (int)Math.Round(bucket.Average(sample => sample.Ppm));
                var level = Co2Quality.Classify(average);
                border.Background = (Brush)FindResource(level switch
                {
                    Co2Level.Good => "Co2GoodZone",
                    Co2Level.Fair => "Co2FairZone",
                    _ => "Co2PoorZone",
                });
                border.ToolTip = $"{bucketStart:t}–{bucketEnd:t}: average {average:N0} ppm · {Co2Quality.Describe(level)}";

                if (airingEvents.Any(item => item.Start >= bucketStart && item.Start < bucketEnd))
                {
                    border.BorderBrush = (Brush)FindResource("TextPrimary");
                    border.BorderThickness = new Thickness(1);
                    border.ToolTip += " · airing detected";
                }
            }

            HistoryDayStrip.Children.Add(border);
        }

        HistoryStartTimeText.Text = start.ToString("ddd HH:mm", CultureInfo.CurrentCulture);
        HistoryQuarterTimeText.Text = (start + TimeSpan.FromHours(6)).ToString("HH:mm", CultureInfo.CurrentCulture);
        HistoryHalfTimeText.Text = (start + TimeSpan.FromHours(12)).ToString("HH:mm", CultureInfo.CurrentCulture);
        HistoryThreeQuarterTimeText.Text = (start + TimeSpan.FromHours(18)).ToString("HH:mm", CultureInfo.CurrentCulture);

        var good = daySamples.Count(sample => Co2Quality.Classify(sample.Ppm) == Co2Level.Good);
        var elevated = daySamples.Count(sample => Co2Quality.Classify(sample.Ppm) == Co2Level.Fair);
        var high = daySamples.Count(sample => Co2Quality.Classify(sample.Ppm) == Co2Level.Poor);
        var total = daySamples.Length;
        HistoryZoneShareBar.Children.Clear();
        HistoryZoneShareBar.ColumnDefinitions.Clear();
        if (total == 0)
        {
            HistoryZoneShareText.Text = "No readings in the last 24 hours.";
            HistoryOvernightText.Text = "Overnight: no readings yet.";
            HistoryAiringText.Text = "No clear airing detected in the last 24 hours.";
            HistoryPeakText.Text = "Highest: no readings yet.";
            return;
        }

        var zones = new[]
        {
            (Count: good, Resource: "Co2GoodZone"),
            (Count: elevated, Resource: "Co2FairZone"),
            (Count: high, Resource: "Co2PoorZone"),
        };
        var column = 0;
        foreach (var zone in zones.Where(zone => zone.Count > 0))
        {
            HistoryZoneShareBar.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(zone.Count, GridUnitType.Star),
            });
            var segment = new Border
            {
                Background = (Brush)FindResource(zone.Resource),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(1, 0, 1, 0),
                ToolTip = $"{Math.Round(zone.Count * 100.0 / total):0}% of readings",
            };
            Grid.SetColumn(segment, column++);
            HistoryZoneShareBar.Children.Add(segment);
        }

        HistoryZoneShareText.Text = $"{Share(good, total)}% good · {Share(elevated, total)}% elevated · {Share(high, total)}% high · of readings";

        var overnight = daySamples
            .Where(sample => sample.Time.Hour >= 23 || sample.Time.Hour < 7)
            .ToArray();
        HistoryOvernightText.Text = overnight.Length == 0
            ? "Overnight (23:00–07:00): no readings yet."
            : $"Overnight (23:00–07:00): average {overnight.Average(sample => sample.Ppm):N0} ppm, peaking at {overnight.Max(sample => sample.Ppm):N0} ppm.";

        AiringEvent? latestAiring = airingEvents.Count > 0 ? airingEvents[^1] : null;
        HistoryAiringText.Text = latestAiring is { } airing
            ? $"Airing detected {airingEvents.Count} time{(airingEvents.Count == 1 ? string.Empty : "s")}. Latest at {airing.Start:t}: CO₂ fell about {airing.DropPpm:N0} ppm over {DescribeDuration(airing.Duration)}."
            : "No clear airing detected in the last 24 hours.";

        var peak = daySamples.MaxBy(sample => sample.Ppm)!;
        HistoryPeakText.Text = $"Highest: {peak.Ppm:N0} ppm at {peak.Time:t}.";
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

    private static string DescribeExportRange(TimeSpan? range) => range switch
    {
        null => "all",
        { TotalHours: < 48 } shortRange => $"{(int)shortRange.TotalHours}h",
        { } longRange => $"{(int)longRange.TotalDays}d",
    };

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (Dashboard.SelectedDevice is not { History.Count: > 0 } device)
        {
            MessageBox.Show(this, "There are no readings to export yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IEnumerable<Co2Sample> samples = device.History;
        TimeSpan? visibleRange = sender switch
        {
            Button { Tag: "visible" } => selectedHistoryRange,
            System.Windows.Controls.MenuItem { Tag: "visible" } => Dashboard.HistoryRange,
            _ => null,
        };
        if (visibleRange is { } range)
        {
            var earliest = DateTime.Now - range;
            samples = samples.Where(sample => sample.Time >= earliest);
            if (!samples.Any())
            {
                MessageBox.Show(this, "There are no readings in the visible range yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"aranet4-{device.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? device.Address}-{DescribeExportRange(visibleRange)}",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var csv = new StringBuilder("time,co2_ppm,temperature_c,humidity_percent,pressure_hpa\n");
        foreach (var sample in samples)
        {
            csv.Append(sample.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.Ppm > 0 ? sample.Ppm.ToString(CultureInfo.InvariantCulture) : string.Empty).Append(',')
                .Append(sample.TemperatureCelsius?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(sample.HumidityPercent?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(sample.PressureHpa?.ToString("0.0", CultureInfo.InvariantCulture) ?? string.Empty).Append('\n');
        }
        try
        {
            File.WriteAllText(dialog.FileName, csv.ToString());
            ShowSyncToast($"Saved {Path.GetFileName(dialog.FileName)}");
        }
        catch (IOException ex) { MessageBox.Show(this, ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void SyncHistory_Click(object sender, RoutedEventArgs e)
    {
        if (Dashboard.SelectedDevice is not { } device)
        {
            return;
        }

        var bluetoothAddress = devicesByAddress.FirstOrDefault(entry => ReferenceEquals(entry.Value, device)).Key;
        if (bluetoothAddress == 0)
        {
            ShowSyncProblem("Sensor unavailable – move closer and try again.", "Could not identify the selected sensor.");
            return;
        }

        SyncProblemBanner.Visibility = Visibility.Collapsed;
        SyncToast.Visibility = Visibility.Collapsed;
        syncToastTimer.Stop();
        SetSyncStatus(string.Empty);
        SyncHistoryButton.IsEnabled = false;
        SyncHistoryButton.Content = "Syncing…";
        SyncHistoryButton.ToolTip = "History is being downloaded from the selected sensor.";
        CancelHistorySyncButton.IsEnabled = true;
        CancelHistorySyncButton.Visibility = Visibility.Visible;
        CancelHistorySyncButton.Content = "Cancel";
        var cancellation = new CancellationTokenSource();
        syncCancellation = cancellation;
        try
        {
            var progress = new Progress<string>(message => SetSyncStatus(message));
            var result = await historySyncService.SyncAsync(
                device.Address,
                bluetoothAddress,
                progress,
                cancellation.Token);
            sensorMonitor.ReplaceHistory(device.Address, result.Samples);
            device.ReplaceHistory(result.Samples);

            if (Dashboard.SelectedDevice == device)
            {
                ShowDetails(device);
            }
            UpdateDevicePopover();

            if (result.MissingRecords > 0)
            {
                ShowSyncProblem(
                    "Incomplete – retry needed",
                    $"History sync was incomplete: {result.AddedSamples:N0} new readings saved, {result.MissingRecords:N0} records weren't received.");
            }
            else if (result.AddedSamples > 0)
            {
                ShowSyncToast($"Synced {result.AddedSamples:N0} new readings");
            }
            else
            {
                ShowSyncToast("Already up to date");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ShowSyncToast("Sync cancelled");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("nearby", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("took too long", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("DeviceNotConnected", StringComparison.OrdinalIgnoreCase))
            {
                ShowSyncProblem("Sensor unavailable – move closer and try again.", ex.Message);
                return;
            }

            var reason = ex is IOException
                ? "History could not be saved locally. Check available disk space and try again."
                : "The history transfer did not complete. Move closer to the sensor and try again.";
            ShowSyncProblem($"Sync failed: {reason}", ex.Message);
        }
        finally
        {
            syncCancellation = null;
            cancellation.Dispose();
            SetSyncStatus(string.Empty);
            SyncHistoryButton.Content = "Sync history";
            SyncHistoryButton.IsEnabled = Dashboard.SelectedDevice is not null;
            SyncHistoryButton.ToolTip = Dashboard.SelectedDevice is null
                ? "Select a sensor to sync its stored history."
                : "Sync stored CO₂, temperature, humidity and pressure history for the selected sensor";
            CancelHistorySyncButton.Content = "Cancel";
            CancelHistorySyncButton.IsEnabled = false;
            CancelHistorySyncButton.Visibility = Visibility.Collapsed;
            UpdateDevicePopover();
        }
    }

    private void CancelHistorySync_Click(object sender, RoutedEventArgs e)
    {
        CancelHistorySyncButton.IsEnabled = false;
        CancelHistorySyncButton.Content = "Cancelling…";
        syncCancellation?.Cancel();
    }
}
