using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.Monitoring;
using Aranet4Monitor.Presentation.Tray;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Storage;
using Aranet4Monitor.Windows;

namespace Aranet4Monitor;

public partial class MainWindow : Window
{
    private readonly IPreferencesStore preferencesStore;
    private readonly AppPreferences preferences;
    private readonly ISensorSource sensorSource;
    private readonly SensorMonitor sensorMonitor;
    private readonly TrayIconService notifications = new();
    private readonly TrayWindowCoordinator trayWindow;
    private readonly TraySettingsCoordinator traySettings;
    private readonly SettingsViewModel settings;
    private readonly LiveMonitoringCoordinator monitoring;
    private readonly DashboardPresentationCoordinator dashboard;
    private readonly ThemePresentationCoordinator themePresentation;
    private readonly HistoryPresentationCoordinator historyPresentation;
    private readonly LivePresentationCoordinator livePresentation;
    private readonly ShellPresentationCoordinator shellPresentation;
    public ShellViewModel Shell { get; }
    public LiveViewModel Live => Shell.Live;
    public HistoryViewModel History => Shell.History;
    public StatusBarViewModel StatusBar { get; } = new();
    private bool startHiddenInTray = Environment.GetCommandLineArgs().Contains(StartupRegistration.TrayArgument);

    private static readonly AccentBrushSet AccentBrushes = new();

    private sealed class AccentBrushSet
    {
        public Brush this[MetricKind kind] => ThemeService.AccentBrush(kind);
    }
    public MainWindow(AppServices services)
    {
        preferencesStore = services.PreferencesStore;
        preferences = services.Preferences;
        sensorSource = services.SensorSource;
        sensorMonitor = services.SensorMonitor;
        Shell = new ShellViewModel(services.HistorySyncService, sensorMonitor, sensorSource: sensorSource);

        InitializeComponent();
#if DEBUG
        PreviewKeyDown += DemoStates_PreviewKeyDown;
#endif
        trayWindow = new TrayWindowCoordinator(this, preferences, preferencesStore, notifications);
        dashboard = new DashboardPresentationCoordinator(
            Live,
            History,
            preferences,
            LivePageControl,
            HistoryPageControl,
            DevicePopupControl,
            StatusBarControl,
            StatusBar,
            HeaderBar,
            notifications,
            title => Title = title);
        themePresentation = new ThemePresentationCoordinator(StatusBar, dashboard, Live);
        historyPresentation = new HistoryPresentationCoordinator(
            History,
            HistoryPageControl,
            StatusBar,
            StatusBarControl,
            DevicePopupControl,
            Live,
            dashboard);
        HeaderBar.DeviceChipRequested += (_, _) => DevicePopupControl.TogglePopup();
        HeaderBar.MinimizeRequested += (_, args) => MinimizeWindow_Click(this, args);
        HeaderBar.ToggleWindowStateRequested += (_, args) => ToggleWindowState_Click(this, args);
        HeaderBar.CloseRequested += (_, args) => CloseWindow_Click(this, args);
        StatusBarControl.DeviceChipRequested += (_, _) => DevicePopupControl.TogglePopup();
        DevicePopupControl.DevicesSelectionChanged += (_, _) => dashboard.DevicesSelectionChanged();
        SettingsPage.LiveViewModel = Live;
        SettingsPage.DevicesForgotten += dashboard.ClearDetails;
        DevicePopupControl.SyncHistoryRequested += (_, _) => HistoryPageControl.BeginSync();
        DevicePopupControl.ManageDevicesRequested += (_, _) => Shell.NavigateCommand.Execute(AppPage.Settings);
        BannerHost.StartListeningRequested += (_, args) => NoticeAction_Click(this, args);
        BannerHost.BluetoothSettingsRequested += (_, args) => BluetoothSettings_Click(this, args);
        BannerHost.RetrySyncRequested += (_, args) => RetrySync_Click(this, args);
        BannerHost.DismissSyncProblemRequested += (_, args) => DismissSyncProblem_Click(this, args);
        Live.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(LiveViewModel.SelectedMetric) or nameof(LiveViewModel.HistoryRange))
            {
                dashboard.RefreshChart();
            }
        };
        DataContext = Live;
        HistoryPageControl.DataContext = History;
        HistoryPageControl.LiveViewModel = Live;
        StatusBarControl.HistoryViewModel = History;
        settings = new SettingsViewModel(
            preferences,
            preferencesStore.Save,
            new SettingsActions(
                dashboard.ApplyTemperatureUnit,
                ThemeService.SetMode,
                SetListeningFromSettings,
                enabled => notifications.SetShowNumber(enabled),
                enabled => notifications.SetLargePopups(enabled),
                enabled =>
                {
                    var actual = StartupRegistration.SetEnabled(enabled)
                        ? enabled
                        : StartupRegistration.IsEnabled;
                    notifications.SetStartWithWindows(actual);
                    return actual;
                },
                ppm => notifications.NotifyHighCo2(ppm),
                (effect, ppm) =>
                {
                    if (effect == MeasurementAlertEffect.HighCo2)
                    {
                        notifications.NotifyHighCo2(ppm);
                    }
                    else if (effect == MeasurementAlertEffect.Recovered)
                    {
                        notifications.NotifyRecovered(ppm);
                    }
                },
                (isPaused, pausedUntil) => notifications.SetAlertsPaused(isPaused, pausedUntil),
                SettingsPage.ConfirmForgetDetectedDevices,
                DevicePopupControl.OpenDiagnostics),
            sensorSource.IsActive,
            StartupRegistration.IsEnabled);
        traySettings = new TraySettingsCoordinator(notifications, settings, preferences, preferencesStore);
        monitoring = new LiveMonitoringCoordinator(
            Dispatcher,
            sensorSource,
            sensorMonitor,
            preferences,
            Live,
            History,
            settings,
            dashboard.SetListenerStatus,
            device =>
            {
                dashboard.ShowDetails(device);
                dashboard.UpdateTray();
            },
            device => DevicePopupControl.DevicesList.ScrollIntoView(device),
            hint => DevicePopupControl.EmptyHintText.Text = hint);
        livePresentation = new LivePresentationCoordinator(Live, History, settings, DevicePopupControl, dashboard);
        DevicePopupControl.ScanDevicesRequested += (_, _) => monitoring.StartListening();
        SettingsPage.DataContext = settings;
        shellPresentation = new ShellPresentationCoordinator(
            this,
            Shell,
            HeaderBar,
            LivePageControl,
            HistoryPageControl,
            SettingsPage);
        LivePageControl.GoodZoneText.Text = "Good";
        LivePageControl.FairZoneText.Text = "Elevated";
        LivePageControl.PoorZoneText.Text = "High";
        LivePageControl.HistoryChart.TemperatureDisplayUnit = preferences.TemperatureDisplayUnit;
        settings.AlertStatus = preferences.LastCo2AlertAt is { } lastAlert
            ? $"Last notification: {preferences.LastCo2AlertPpm:N0} ppm at {lastAlert:t}"
            : "No high-CO₂ alerts sent yet.";
        if (startHiddenInTray)
        {
            // Started by Windows: come up minimized so no window flashes; Loaded then hides it in the tray.
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
        }
        Closed += (_, _) =>
        {
            History.CancelSync();
            monitoring.Dispose();
            historyPresentation.Dispose();
            livePresentation.Dispose();
            shellPresentation.Dispose();
            traySettings.Dispose();
            themePresentation.Dispose();
            sensorSource.Dispose();
            notifications.Dispose();
        };
        StateChanged += (_, _) =>
        {
            WindowSurface.Padding = WindowState == WindowState.Maximized
                ? SystemParameters.WindowResizeBorderThickness
                : new Thickness(0);
            if (WindowState == WindowState.Minimized && !startHiddenInTray)
            {
                trayWindow.HideToTray();
            }
        };

        livePresentation.Start();

        // The app exists to listen, so start right away (Stop still works as before).
        Loaded += (_, _) =>
        {
            monitoring.StartListening();
            if (!startHiddenInTray)
            {
                return;
            }

            Hide();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            startHiddenInTray = false; // from now on minimizing hides to the tray as usual
        };
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleWindowState_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private bool SetListeningFromSettings(bool enabled)
    {
        return monitoring.SetListeningFromSettings(enabled);
    }

    private void NoticeAction_Click(object sender, RoutedEventArgs e) => monitoring.StartListening();

    private void BluetoothSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            StatusBar.NoticeToolTip = exception.Message;
        }
    }

    private void RetrySync_Click(object sender, RoutedEventArgs e) => HistoryPageControl.BeginSync();

    private void DismissSyncProblem_Click(object sender, RoutedEventArgs e) => StatusBar.HideSyncProblem();

#if DEBUG
    // Ctrl+Shift+1/2/3 force Good/Elevated/High CO₂, Ctrl+Shift+0 forces "no reading".
    // Turn off "Listen for live readings" first, otherwise the next real packet overwrites the value.
    private void DemoStates_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (System.Windows.Input.Keyboard.Modifiers != (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
        {
            return;
        }

        int? ppm = e.Key switch
        {
            System.Windows.Input.Key.D1 => 650,
            System.Windows.Input.Key.D2 => 1150,
            System.Windows.Input.Key.D3 => 1750,
            System.Windows.Input.Key.D0 => 0,
            _ => null,
        };
        if (ppm is null || Live.SelectedDevice is not { } device)
        {
            return;
        }

        device.Co2Ppm = ppm.Value;
        dashboard.ShowDetails(device);
        e.Handled = true;
    }
#endif
}
