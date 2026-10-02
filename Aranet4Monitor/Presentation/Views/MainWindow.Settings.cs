using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void DeviceChip_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePopup.IsOpen)
        {
            DevicePopup.IsOpen = false;
            return;
        }
        // The same click that dismissed the popup (it closes on any outside press) must not reopen it.
        if ((DateTime.UtcNow - devicePopupClosedAt).TotalMilliseconds < 250)
        {
            return;
        }

        DevicePopup.IsOpen = true;
    }

    private void DevicePopup_Closed(object? sender, EventArgs e)
    {
        devicePopupClosedAt = DateTime.UtcNow;
        DeviceChipButton.Focus();
    }

    private void DevicePopup_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DevicePopup.IsOpen = false;
            DeviceChipButton.Focus();
            e.Handled = true;
        }
    }

    private void UpdateDevicePopover()
    {
        var count = Dashboard.Devices.Count;
        DevicesCountText.Text = count.ToString(CultureInfo.InvariantCulture);
        DevicesTitleText.Text = count > 1 ? "YOUR SENSORS" : "SENSOR";
        DevicesList.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHintText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PacketCountText.Text = Dashboard.SelectedDevice is { } current
            ? $"{current.Packets:N0} packets received"
            : "0 packets received";

        if (Dashboard.SelectedDevice is not { } device)
        {
            DeviceDetailsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DeviceDetailsPanel.Visibility = Visibility.Visible;
        DeviceStatusText.Text = device.LastSeen == default
            ? "Waiting for live readings"
            : SensorFreshness.IsStale(device.LastSeen, measurementIntervals.GetValueOrDefault(device), DateTime.Now)
                ? "No recent readings"
                : "Receiving live data";
        DeviceStatusDot.Fill = device.LastSeen == default
            ? DotIdle
            : SensorFreshness.IsStale(device.LastSeen, measurementIntervals.GetValueOrDefault(device), DateTime.Now)
                ? DotStale
                : DotLive;
        DeviceLastMeasurementText.Text = device.LastSeen == default ? "No measurement yet" : device.LastSeenAgo;
        DeviceAddressText.Text = device.Address;
        DeviceLastSyncedText.Text = HistoryStore.LoadSyncCursor(device.Address) is { } syncedThrough
            ? syncedThrough.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Not synced yet";
    }

    private void CopyAddress_Click(object sender, RoutedEventArgs e)
    {
        if (Dashboard.SelectedDevice is not { } device)
        {
            return;
        }

        try
        {
            Clipboard.SetText(device.Address);
        }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy */ }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = MoreButton;
        MoreMenu.IsOpen = true;
    }

    private void OpenAlertSettings_Click(object sender, RoutedEventArgs e) => AlertSettingsPopup.IsOpen = true;

    private void ListenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ListenMenuItem.IsChecked)
        {
            StartListening();
        }
        else
        {
            if (watcher is null)
            {
                SetStatus("Stopped", StatusKind.Idle);
            }
            else
            {
                StopWatching();
            }
        }
    }

    private void ShowNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        preferences.TrayShowNumber = ShowNumberMenuItem.IsChecked;
        preferences.Save();
        notifications.SetShowNumber(ShowNumberMenuItem.IsChecked);
    }

    private void LargePopupsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        preferences.LargePopups = LargePopupsMenuItem.IsChecked;
        preferences.Save();
        notifications.SetLargePopups(LargePopupsMenuItem.IsChecked);
    }

    private void StartWithWindowsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!StartupRegistration.SetEnabled(StartWithWindowsMenuItem.IsChecked))
        {
            StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled;
        }

        notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
    }

    private void BluetoothDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsExpander.IsExpanded = true;
        DevicePopup.IsOpen = true;
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => ExitFromTray();

    private void NoticeAction_Click(object sender, RoutedEventArgs e) => StartListening();

    private void BluetoothSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            NoticeBar.ToolTip = ex.Message;
        }
    }

    private void RetrySync_Click(object sender, RoutedEventArgs e) => SyncHistory_Click(sender, e);

    private void DismissSyncProblem_Click(object sender, RoutedEventArgs e) =>
        SyncProblemBanner.Visibility = Visibility.Collapsed;

    private void TemperatureUnit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string tag }
            || !Enum.TryParse<TemperatureUnit>(tag, ignoreCase: true, out var temperatureUnit))
        {
            return;
        }

        preferences.TemperatureDisplayUnit = temperatureUnit;
        preferences.Save();
        CelsiusUnitMenuItem.IsChecked = temperatureUnit == TemperatureUnit.Celsius;
        FahrenheitUnitMenuItem.IsChecked = temperatureUnit == TemperatureUnit.Fahrenheit;
        HistoryChart.TemperatureDisplayUnit = temperatureUnit;

        foreach (var device in Dashboard.Devices)
        {
            if (device.TemperatureCelsius is { } celsius)
            {
                device.Temperature = Metrics.FormatWithUnit((double)celsius, MetricKind.Temperature, temperatureUnit);
            }
        }

        if (Dashboard.SelectedDevice is { } selected)
        {
            ShowDetails(selected);
        }
        else
        {
            RefreshChart();
        }
    }

    private void RangeButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag })
        {
            return;
        }

        var hours = int.Parse(tag, CultureInfo.InvariantCulture);
        Dashboard.HistoryRange = hours == 0 ? null : TimeSpan.FromHours(hours);
        if (HistoryChart is null)
        {
            return; // Checked fires once while the XAML is still being loaded
        }

        RefreshChart();
    }
}
