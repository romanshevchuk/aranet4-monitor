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
        foreach (var sample in device.History)
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
            SetSyncStatus("Sensor unavailable", "Could not identify the selected sensor.");
            return;
        }

        SetSyncStatus(string.Empty);
        SyncHistoryButton.IsEnabled = false;
        SyncHistoryButton.Content = "Syncing…";
        CancelHistorySyncButton.IsEnabled = true;
        CancelHistorySyncButton.Visibility = Visibility.Visible;
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

            if (result.MissingRecords > 0)
            {
                SetSyncStatus(
                    "Incomplete · retry needed",
                    $"History sync incomplete: {added:N0} new readings saved, {result.MissingRecords:N0} records weren't received. Sync again to fill the gap.");
            }
            else if (added > 0)
            {
                SetSyncStatus($"Synced {added:N0} readings");
            }
            else
            {
                SetSyncStatus("No new readings");
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SetSyncStatus("Sync cancelled", "History sync cancelled. Retry to resume from the last completed sync.");
        }
        catch (Exception ex)
        {
            SetSyncStatus("Sync failed", ex.Message);
            MessageBox.Show(this, ex.Message, "History sync", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            syncCancellation = null;
            cancellation.Dispose();
            SyncHistoryButton.Content = "Sync now";
            SyncHistoryButton.IsEnabled = Dashboard.SelectedDevice is not null;
            CancelHistorySyncButton.Content = "Cancel sync";
            CancelHistorySyncButton.IsEnabled = false;
            CancelHistorySyncButton.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelHistorySync_Click(object sender, RoutedEventArgs e)
    {
        CancelHistorySyncButton.IsEnabled = false;
        CancelHistorySyncButton.Content = "Cancelling…";
        syncCancellation?.Cancel();
    }
}
