using System.Windows;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.Tray;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Presentation.Views.Chrome;
using Aranet4Monitor.Presentation.Views.History;
using Aranet4Monitor.Presentation.Views.Live;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor.Presentation.Monitoring;

/// <summary>Coordinates selected-device presentation across the dashboard's page and chrome controls.</summary>
public sealed class DashboardPresentationCoordinator
{
    private readonly LiveViewModel live;
    private readonly HistoryViewModel history;
    private readonly AppPreferences preferences;
    private readonly LivePage livePage;
    private readonly HistoryPage historyPage;
    private readonly DevicePopupControl devicePopup;
    private readonly StatusBarControl statusBarControl;
    private readonly StatusBarViewModel statusBar;
    private readonly HeaderBar headerBar;
    private readonly TrayIconService trayIcon;
    private readonly Action<string> setWindowTitle;

    public DashboardPresentationCoordinator(
        LiveViewModel live,
        HistoryViewModel history,
        AppPreferences preferences,
        LivePage livePage,
        HistoryPage historyPage,
        DevicePopupControl devicePopup,
        StatusBarControl statusBarControl,
        StatusBarViewModel statusBar,
        HeaderBar headerBar,
        TrayIconService trayIcon,
        Action<string> setWindowTitle)
    {
        this.live = live;
        this.history = history;
        this.preferences = preferences;
        this.livePage = livePage;
        this.historyPage = historyPage;
        this.devicePopup = devicePopup;
        this.statusBarControl = statusBarControl;
        this.statusBar = statusBar;
        this.headerBar = headerBar;
        this.trayIcon = trayIcon;
        this.setWindowTitle = setWindowTitle;
    }

    public void DevicesSelectionChanged()
    {
        if (live.SelectedDevice is { } device)
        {
            ShowDetails(device);
        }

        devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
        UpdateTray();
    }

    public void ShowDetails(Aranet4Device device)
    {
        var hasReading = device.Co2Ppm > 0;
        var now = DateTime.Now;
        var lastReading = live.GetLastReadingTime(device);
        var isStale = live.IsStale(device, now);
        livePage.ShowDeviceDetails(device, preferences.TemperatureDisplayUnit, lastReading, isStale, now);
        historyPage.SetSyncButtonState(hasDevice: true, history.IsSyncing);
        setWindowTitle(hasReading ? $"{device.Co2Ppm:N0} ppm · Aranet4 Monitor" : "Aranet4 Monitor");

        devicePopup.ShowTelemetry(device);
        statusBarControl.ShowDeviceTelemetry(device);
        headerBar.SetDeviceName(device.Name);
        RefreshLastSeen(device);

        devicePopup.AdvertisementText.Text = device.LastAdvertisement;
        devicePopup.ScanResponseText.Text = device.LastScanResponse;
        devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
        RefreshChart();
    }

    public void RefreshLastSeen(Aranet4Device device)
    {
        UpdateHeaderSensorState();
        var now = DateTime.Now;
        var lastReading = live.GetLastReadingTime(device);
        livePage.RefreshCo2Summary(device, lastReading, live.IsStale(device, now), now);
    }

    public void RefreshChart()
    {
        var now = DateTime.Now;
        livePage.RefreshChart(
            live.SelectedDevice,
            live.SelectedMetric,
            live.HistoryRange,
            preferences.TemperatureDisplayUnit,
            now);
        if (historyPage.Visibility == Visibility.Visible)
        {
            historyPage.RefreshFromLiveDevice();
        }
    }

    public void ApplyTemperatureUnit(TemperatureUnit temperatureUnit)
    {
        live.SetTemperatureUnit(temperatureUnit);
        livePage.ApplyTemperatureUnit(temperatureUnit);

        if (live.SelectedDevice is { } selected)
        {
            ShowDetails(selected);
        }
        else
        {
            RefreshChart();
        }
    }

    public void SetListenerStatus(string text, ListenerStatusKind kind, string? details)
    {
        statusBar.SetListenerStatus(text, kind, details);
        UpdateHeaderSensorState();
    }

    public void ClearDetails()
    {
        livePage.ClearDeviceDetails(preferences.TemperatureDisplayUnit, DateTime.Now);
        devicePopup.ClearTelemetry();
        statusBarControl.ClearDeviceTelemetry();
        historyPage.SetSyncButtonState(hasDevice: false, history.IsSyncing);
        headerBar.SetDeviceName("Searching for sensor…");
        setWindowTitle("Aranet4 Monitor");
        UpdateHeaderSensorState();
        devicePopup.RefreshFromViewModels(live, history, DateTime.Now);
        UpdateTray();
    }

    public void UpdateTray()
    {
        var reading = live.GetTrayReading(DateTime.Now);
        trayIcon.SetReading(reading.Co2Ppm, reading.IsStale);
    }

    public void UpdateHeaderSensorState()
    {
        var device = live.SelectedDevice;
        var lastReading = device is null ? default : live.GetLastReadingTime(device);
        var isStale = device is not null && live.IsStale(device, DateTime.Now);
        statusBar.UpdateSensorState(lastReading != default, isStale);
    }
}
