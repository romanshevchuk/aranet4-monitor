using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Storage;
using Aranet4Monitor.Windows;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void DeviceChip_Click(object sender, RoutedEventArgs e)
    {
        if (DevicePopupControl.DevicePopup.IsOpen)
        {
            DevicePopupControl.DevicePopup.IsOpen = false;
            return;
        }
        // The same click that dismissed the popup (it closes on any outside press) must not reopen it.
        if ((DateTime.UtcNow - devicePopupClosedAt).TotalMilliseconds < 250)
        {
            return;
        }

        DevicePopupControl.DevicePopup.IsOpen = true;
    }

    private void ScanDevices_Click(object sender, RoutedEventArgs e)
    {
        DevicePopupControl.DevicePopup.IsOpen = false;
        StartListening();
    }

    private void ManageDevices_Click(object sender, RoutedEventArgs e)
    {
        DevicePopupControl.DevicePopup.IsOpen = false;
        SettingsNavigation_Click(sender, e);
    }

    private void DevicePopup_Closed(object? sender, EventArgs e)
    {
        devicePopupClosedAt = DateTime.UtcNow;
        HeaderBar.DeviceChipButton.Focus();
    }

    private void DevicePopup_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DevicePopupControl.DevicePopup.IsOpen = false;
            HeaderBar.DeviceChipButton.Focus();
            e.Handled = true;
        }
    }

    private void UpdateDevicePopover()
    {
        var count = Live.Devices.Count;
        DevicePopupControl.DevicesCountText.Text = count.ToString(CultureInfo.InvariantCulture);
        DevicePopupControl.DevicesTitleText.Text = count > 1 ? "YOUR SENSORS" : "SENSORS";
        DevicePopupControl.DevicesList.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DevicePopupControl.EmptyHintText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DevicePopupControl.PacketCountText.Text = Live.SelectedDevice is { } current
            ? $"{current.Packets:N0} packets received"
            : "0 packets received";

        if (Live.SelectedDevice is not { } device)
        {
            DevicePopupControl.DeviceDetailsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DevicePopupControl.DeviceDetailsPanel.Visibility = Visibility.Visible;
        var lastReading = GetLastReadingTime(device);
        var stale = sensorMonitor.IsStale(device.Address, lastReading, DateTime.Now);
        DevicePopupControl.DeviceStatusText.Text = lastReading == default
            ? "Waiting for live readings"
            : stale
                ? "No recent readings"
                : "Receiving live data";
        DevicePopupControl.DeviceStatusText.Foreground = lastReading == default
            ? (Brush)FindResource("TextSecondary")
            : stale
                ? (Brush)FindResource("Warning")
                : (Brush)FindResource("Positive");
        DevicePopupControl.DeviceStatusDot.Fill = lastReading == default
            ? DotIdle
            : stale
                ? DotStale
                : DotLive;
        DevicePopupControl.DeviceLastMeasurementText.Text = lastReading == default
            ? "No measurement yet"
            : lastReading.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        DevicePopupControl.DeviceAddressText.Text = device.Address;
        DevicePopupControl.DeviceLastSyncedText.Text = historySyncService.LoadSyncCursor(device.Address) is { } syncedThrough
            ? syncedThrough.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Not synced yet";
        DevicePopupControl.BatteryText.Foreground = device.BatteryValue switch
        {
            <= 15 => (Brush)FindResource("Danger"),
            <= 35 => (Brush)FindResource("Warning"),
            _ => (Brush)FindResource("TextPrimary"),
        };
        StatusBarControl.FooterBatteryText.Foreground = DevicePopupControl.BatteryText.Foreground;
    }

    private void CopyAddress_Click(object sender, RoutedEventArgs e)
    {
        if (Live.SelectedDevice is not { } device)
        {
            return;
        }

        try
        {
            Clipboard.SetText(device.Address);
        }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy */ }
    }

    private void LiveNavigation_Click(object sender, RoutedEventArgs e)
    {
        HeaderBar.LiveNavigationButton.Style = (Style)FindResource("HeaderNavigationSelectedButton");
        HeaderBar.HistoryNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        HeaderBar.SettingsNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        LivePageControl.ScrollToTop();
        LivePageControl.Co2Tab.Focus();
    }

    private void HistoryNavigation_Click(object sender, RoutedEventArgs e)
    {
        HeaderBar.LiveNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        HeaderBar.HistoryNavigationButton.Style = (Style)FindResource("HeaderNavigationSelectedButton");
        HeaderBar.SettingsNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        RefreshHistoryView();
        HistoryPageControl.ScrollToTop();
        HistoryPageControl.LongHistoryChart.Focus();
    }

    private void SettingsNavigation_Click(object sender, RoutedEventArgs e)
    {
        HeaderBar.LiveNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        HeaderBar.HistoryNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        HeaderBar.SettingsNavigationButton.Style = (Style)FindResource("HeaderNavigationSelectedButton");
        SettingsPage.ScrollToTop();
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplyTheme(AppTheme mode)
    {
        ThemeService.SetMode(mode);
    }

    private void ThemeService_Changed(object? sender, EventArgs e)
    {
        StatusBar.RefreshTheme();
        UpdateHeaderSensorState();
        if (Live.SelectedDevice is { } device)
        {
            ShowDetails(device);
        }
        else
        {
            RefreshChart();
        }
    }

    private void ToggleWindowState_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private bool SetListeningFromSettings(bool enabled)
    {
        if (enabled)
        {
            StartListening();
        }
        else if (sensorSource.IsActive)
        {
            StopWatching();
        }
        else
        {
            SetStatus("Stopped", ListenerStatusKind.Idle);
        }

        return sensorSource.IsActive;
    }

    private void SetShowNumberFromSettings(bool enabled)
    {
        notifications.SetShowNumber(enabled);
    }

    private void SetLargePopupsFromSettings(bool enabled)
    {
        notifications.SetLargePopups(enabled);
    }

    private bool SetStartWithWindowsFromSettings(bool enabled)
    {
        var actual = StartupRegistration.SetEnabled(enabled)
            ? enabled
            : StartupRegistration.IsEnabled;
        notifications.SetStartWithWindows(actual);
        return actual;
    }

    private void OpenBluetoothDiagnostics()
    {
        DevicePopupControl.DiagnosticsExpander.IsExpanded = true;
        DevicePopupControl.DevicePopup.IsOpen = true;
    }

    private void NoticeAction_Click(object sender, RoutedEventArgs e) => StartListening();

    private void BluetoothSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusBar.NoticeToolTip = ex.Message;
        }
    }

    private void RetrySync_Click(object sender, RoutedEventArgs e) => SyncHistory_Click(sender, e);

    private void DismissSyncProblem_Click(object sender, RoutedEventArgs e) =>
        StatusBar.HideSyncProblem();

    private void ApplyTemperatureUnit(TemperatureUnit temperatureUnit)
    {
        LivePageControl.HistoryChart.TemperatureDisplayUnit = temperatureUnit;

        foreach (var device in Live.Devices)
        {
            if (device.TemperatureCelsius is { } celsius)
            {
                device.Temperature = Metrics.FormatWithUnit((double)celsius, MetricKind.Temperature, temperatureUnit);
            }
        }

        if (Live.SelectedDevice is { } selected)
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
        Live.HistoryRange = hours == 0 ? null : TimeSpan.FromHours(hours);
        if (LivePageControl.HistoryChart is null)
        {
            return; // Checked fires once while the XAML is still being loaded
        }

        RefreshChart();
    }
}
