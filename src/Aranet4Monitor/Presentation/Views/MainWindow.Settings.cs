using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Storage;
using Aranet4Monitor.Windows;

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
        var lastReading = GetLastReadingTime(device);
        var stale = sensorMonitor.IsStale(device.Address, lastReading, DateTime.Now);
        DeviceStatusText.Text = lastReading == default
            ? "Waiting for live readings"
            : stale
                ? "No recent readings"
                : "Receiving live data";
        DeviceStatusText.Foreground = lastReading == default
            ? (Brush)FindResource("TextSecondary")
            : stale
                ? (Brush)FindResource("Warning")
                : (Brush)FindResource("Positive");
        DeviceStatusDot.Fill = lastReading == default
            ? DotIdle
            : stale
                ? DotStale
                : DotLive;
        DeviceLastMeasurementText.Text = lastReading == default
            ? "No measurement yet"
            : lastReading.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        DeviceAddressText.Text = device.Address;
        DeviceLastSyncedText.Text = historySyncService.LoadSyncCursor(device.Address) is { } syncedThrough
            ? syncedThrough.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Not synced yet";
        BatteryText.Foreground = device.BatteryValue switch
        {
            <= 15 => (Brush)FindResource("Danger"),
            <= 35 => (Brush)FindResource("Warning"),
            _ => (Brush)FindResource("TextPrimary"),
        };
        FooterBatteryText.Foreground = BatteryText.Foreground;
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

    private void LiveNavigation_Click(object sender, RoutedEventArgs e)
    {
        DashboardGrid.Visibility = Visibility.Visible;
        HistoryView.Visibility = Visibility.Collapsed;
        LiveNavigationButton.Style = (Style)FindResource("HeaderNavigationSelectedButton");
        HistoryNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        DashboardScrollViewer.ScrollToTop();
        Co2Tab.Focus();
    }

    private void HistoryNavigation_Click(object sender, RoutedEventArgs e)
    {
        DashboardGrid.Visibility = Visibility.Collapsed;
        HistoryView.Visibility = Visibility.Visible;
        LiveNavigationButton.Style = (Style)FindResource("HeaderNavigationButton");
        HistoryNavigationButton.Style = (Style)FindResource("HeaderNavigationSelectedButton");
        RefreshHistoryView();
        DashboardScrollViewer.ScrollToTop();
        LongHistoryChart.Focus();
    }

    private void SettingsNavigation_Click(object sender, RoutedEventArgs e)
    {
        MoreMenu.PlacementTarget = SettingsNavigationButton;
        MoreMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        MoreMenu.IsOpen = true;
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void ToggleWindowState_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void ListenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ListenMenuItem.IsChecked)
        {
            StartListening();
        }
        else
        {
            if (!sensorSource.IsActive)
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
        preferencesStore.Save(preferences);
        notifications.SetShowNumber(ShowNumberMenuItem.IsChecked);
    }

    private void LargePopupsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        preferences.LargePopups = LargePopupsMenuItem.IsChecked;
        preferencesStore.Save(preferences);
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
        preferencesStore.Save(preferences);
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
