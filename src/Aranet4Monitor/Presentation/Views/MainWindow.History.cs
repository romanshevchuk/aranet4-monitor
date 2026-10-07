using System.Windows;
using System.Windows.Controls;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void RefreshHistoryView()
    {
        var device = Live.SelectedDevice;
        var syncCursor = device is null ? null : historySyncService.LoadSyncCursor(device.Address);
        HistoryPageControl.RefreshHistory(device, History.SelectedRange, syncCursor, History.IsSyncing);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (Live.SelectedDevice is not { History.Count: > 0 } device)
        {
            MessageBox.Show(this, "There are no readings to export yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TimeSpan? visibleRange = sender switch
        {
            Button { Tag: "visible" } => History.SelectedRange,
            System.Windows.Controls.MenuItem { Tag: "visible" } => Live.HistoryRange,
            _ => null,
        };
        var export = History.CreateCsvExport(device, visibleRange, DateTime.Now);
        if (export is null)
        {
            MessageBox.Show(this, "There are no readings in the visible range yet.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = export.FileName,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, export.Contents);
            ShowSyncToast($"Saved {Path.GetFileName(dialog.FileName)}");
        }
        catch (IOException ex) { MessageBox.Show(this, ex.Message, "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void SyncHistory_Click(object sender, RoutedEventArgs e)
    {
        if (History.IsSyncing || Live.SelectedDevice is not { } device)
        {
            return;
        }

        var bluetoothAddress = devicesByAddress.FirstOrDefault(entry => ReferenceEquals(entry.Value, device)).Key;
        if (bluetoothAddress == 0)
        {
            ShowSyncProblem("Sensor unavailable – move closer and try again.", "Could not identify the selected sensor.");
            return;
        }

        StatusBar.HideSyncProblem();
        StatusBar.HideSyncToast();
        syncToastTimer.Stop();
        SetSyncStatus(string.Empty);
        try
        {
            var result = await History.SyncAsync(device, bluetoothAddress);
            if (result is null)
            {
                ShowSyncToast("Sync cancelled");
                return;
            }

            if (Live.SelectedDevice == device)
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
            SetSyncStatus(string.Empty);
            UpdateDevicePopover();
        }
    }

    private void CancelHistorySync_Click(object sender, RoutedEventArgs e)
    {
        StatusBarControl.CancelHistorySyncButton.IsEnabled = false;
        StatusBarControl.CancelHistorySyncButton.Content = "Cancelling…";
        History.CancelSync();
    }
}
