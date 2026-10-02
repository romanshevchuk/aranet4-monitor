using System.Globalization;
using System.Text;
using System.Windows;
using Aranet4Monitor.Bluetooth;
using Aranet4Monitor.Models;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (Dashboard.SelectedDevice is not { History.Count: > 0 } device)
        {
            MessageBox.Show(this, "There are no readings to export yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IEnumerable<Co2Sample> samples = device.History;
        if (sender is System.Windows.Controls.MenuItem { Tag: "visible" }
            && Dashboard.HistoryRange is { } visibleRange)
        {
            var earliest = DateTime.Now - visibleRange;
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
            FileName = $"aranet4-co2-{DateTime.Now:yyyyMMdd-HHmm}",
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
        }
        catch (IOException ex) { MessageBox.Show(this, ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void SyncHistory_Click(object sender, RoutedEventArgs e)
    {
        if (Dashboard.SelectedDevice is not { } device)
        {
            return;
        }

        var bluetoothAddress = _devicesByAddress.FirstOrDefault(entry => ReferenceEquals(entry.Value, device)).Key;
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
            var cursor = HistoryStore.LoadSyncCursor(device.Address);
            var result = await Aranet4HistorySync.SyncAsync(bluetoothAddress, cursor, progress, cancellation.Token);
            var added = device.MergeHistory(result.Samples);
            if (!HistoryStore.Save(device.Address, device.History))
            {
                throw new IOException("Downloaded history could not be saved locally. The sync cursor was not advanced; retry after checking disk space.");
            }

            if (result.SyncedThrough is { } syncedThrough && !HistoryStore.SaveSyncCursor(device.Address, syncedThrough))
            {
                throw new IOException("The sync cursor could not be saved. Retrying will safely import the overlap again.");
            }

            if (Dashboard.SelectedDevice == device)
            {
                ShowDetails(device);
            }
            UpdateDevicePopover();

            if (result.MissingRecords > 0)
            {
                ShowSyncProblem(
                    "Incomplete – retry needed",
                    $"History sync was incomplete: {added:N0} new readings saved, {result.MissingRecords:N0} records weren't received.");
            }
            else if (added > 0)
            {
                ShowSyncToast($"Synced {added:N0} readings");
            }
            else
            {
                ShowSyncToast("No new readings");
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
