using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Aranet4Monitor.Application.History;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Presentation.Tray;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Storage;
using Aranet4Monitor.Windows;

namespace Aranet4Monitor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer tickTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer syncToastTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly IPreferencesStore preferencesStore;
    private readonly AppPreferences preferences;
    private readonly HistorySyncService historySyncService;
    private readonly ISensorSource sensorSource;
    private readonly SensorMonitor sensorMonitor;
    private readonly TrayIconService notifications = new();
    private readonly SettingsViewModel settings;
    public ShellViewModel Shell { get; }
    public LiveViewModel Live => Shell.Live;
    public HistoryViewModel History => Shell.History;
    public StatusBarViewModel StatusBar { get; } = new();
    private bool startHiddenInTray = Environment.GetCommandLineArgs().Contains(StartupRegistration.TrayArgument);
    private DateTime? alertsPausedUntil;
    private DateTime devicePopupClosedAt;
    private int tickCount;
    private bool allowClose;
    private bool listenerHasBeenStarted;
    private bool isNarrowDashboardLayout;

    private static Brush DotLive => ThemeService.GetBrush("Co2Good");
    private static Brush DotStale => ThemeService.GetBrush("Co2Fair");
    private static Brush DotIdle => ThemeService.GetBrush("Disabled");
    private static Brush SeenStale => ThemeService.GetBrush("Warning");
    private static readonly AccentBrushSet AccentBrushes = new();

    private sealed class AccentBrushSet
    {
        public Brush this[MetricKind kind] => ThemeService.AccentBrush(kind);
    }
    public MainWindow(AppServices services)
    {
        preferencesStore = services.PreferencesStore;
        preferences = services.Preferences;
        historySyncService = services.HistorySyncService;
        Shell = new ShellViewModel(historySyncService, sensorMonitor);
        sensorSource = services.SensorSource;
        sensorMonitor = services.SensorMonitor;

        InitializeComponent();
        LivePageControl.MetricTabChecked += MetricTab_Checked;
        LivePageControl.RangeButtonChecked += RangeButton_Checked;
        HeaderBar.LiveNavigationRequested += (_, args) => LiveNavigation_Click(this, args);
        HeaderBar.HistoryNavigationRequested += (_, args) => HistoryNavigation_Click(this, args);
        HeaderBar.SettingsNavigationRequested += (_, args) => SettingsNavigation_Click(this, args);
        HeaderBar.DeviceChipRequested += (_, args) => DeviceChip_Click(this, args);
        HeaderBar.MinimizeRequested += (_, args) => MinimizeWindow_Click(this, args);
        HeaderBar.ToggleWindowStateRequested += (_, args) => ToggleWindowState_Click(this, args);
        HeaderBar.CloseRequested += (_, args) => CloseWindow_Click(this, args);
        StatusBarControl.CancelHistorySyncRequested += (_, args) => CancelHistorySync_Click(this, args);
        StatusBarControl.DeviceChipRequested += (_, args) => DeviceChip_Click(this, args);
        DevicePopupControl.PopupClosed += DevicePopup_Closed;
        DevicePopupControl.DevicesSelectionChanged += DevicesList_SelectionChanged;
        DevicePopupControl.CopyAddressRequested += CopyAddress_Click;
        DevicePopupControl.SyncHistoryRequested += SyncHistory_Click;
        DevicePopupControl.CopyPacketRequested += CopyPacket_Click;
        DevicePopupControl.ScanDevicesRequested += ScanDevices_Click;
        DevicePopupControl.ManageDevicesRequested += ManageDevices_Click;
        DevicePopupControl.PopupPreviewKeyDown += DevicePopup_PreviewKeyDown;
        BannerHost.StartListeningRequested += (_, args) => NoticeAction_Click(this, args);
        BannerHost.BluetoothSettingsRequested += (_, args) => BluetoothSettings_Click(this, args);
        BannerHost.RetrySyncRequested += (_, args) => RetrySync_Click(this, args);
        BannerHost.DismissSyncProblemRequested += (_, args) => DismissSyncProblem_Click(this, args);
        HistoryPageControl.ExportRequested += ExportCsv_Click;
        HistoryPageControl.SyncHistoryRequested += SyncHistory_Click;
        History.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(HistoryViewModel.SelectedRange))
            {
                RefreshHistoryView();
            }
            else if (args.PropertyName == nameof(HistoryViewModel.SyncProgressText))
            {
                SetSyncStatus(History.SyncProgressText);
            }
            else if (args.PropertyName == nameof(HistoryViewModel.IsSyncing))
            {
                RefreshHistoryView();
                StatusBarControl.CancelHistorySyncButton.Content = "Cancel";
                StatusBarControl.CancelHistorySyncButton.IsEnabled = History.IsSyncing;
                StatusBarControl.CancelHistorySyncButton.Visibility = History.IsSyncing ? Visibility.Visible : Visibility.Collapsed;
            }
        };
        DataContext = Live;
        settings = new SettingsViewModel(
            preferences,
            preferencesStore.Save,
            new SettingsActions(
                ApplyTemperatureUnit,
                ApplyTheme,
                SetListeningFromSettings,
                SetShowNumberFromSettings,
                SetLargePopupsFromSettings,
                SetStartWithWindowsFromSettings,
                SendTestAlertFromSettings,
                ToggleAlertPause,
                ForgetDetectedDevices,
                OpenBluetoothDiagnostics),
            sensorSource.IsActive,
            StartupRegistration.IsEnabled);
        SettingsPage.DataContext = settings;
        LivePageControl.GoodZoneText.Text = "Good";
        LivePageControl.FairZoneText.Text = "Elevated";
        LivePageControl.PoorZoneText.Text = "High";
        ThemeService.Changed += ThemeService_Changed;
        LivePageControl.HistoryChart.TemperatureDisplayUnit = preferences.TemperatureDisplayUnit;
        SetAlertStatus(
            preferences.LastCo2AlertAt is { } lastAlert
                ? $"Last notification: {preferences.LastCo2AlertPpm:N0} ppm at {lastAlert:t}"
                : "No high-CO₂ alerts sent yet.");
        notifications.RestoreRequested += (_, _) => RestoreFromTray();
        notifications.ExitRequested += (_, _) => ExitFromTray();
        notifications.PauseToggleRequested += (_, _) => ToggleAlertPause();
        notifications.StartWithWindowsToggled += (_, enabled) =>
        {
            // If Windows refuses the change, put the menu check mark back to the real state.
            var actual = StartupRegistration.SetEnabled(enabled)
                ? enabled
                : StartupRegistration.IsEnabled;
            notifications.SetStartWithWindows(actual);
            settings.SetStartWithWindowsFromSystem(actual);
        };
        notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
        notifications.SetShowNumber(preferences.TrayShowNumber);
        notifications.SetLargePopups(preferences.LargePopups);
        sensorSource.AdvertisementReceived += SensorSource_AdvertisementReceived;
        sensorSource.Stopped += SensorSource_Stopped;
        notifications.ShowNumberToggled += (_, enabled) =>
        {
            preferences.TrayShowNumber = enabled;
            preferencesStore.Save(preferences);
            settings.SetShowNumberFromTray(enabled);
        };
        notifications.LargePopupsToggled += (_, enabled) =>
        {
            preferences.LargePopups = enabled;
            preferencesStore.Save(preferences);
            settings.SetLargePopupsFromTray(enabled);
        };

        if (startHiddenInTray)
        {
            // Started by Windows: come up minimized so no window flashes; Loaded then hides it in the tray.
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
        }
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            tickTimer.Stop();
            syncToastTimer.Stop();
            History.CancelSync();
            StopWatching();
            sensorSource.AdvertisementReceived -= SensorSource_AdvertisementReceived;
            sensorSource.Stopped -= SensorSource_Stopped;
            ThemeService.Changed -= ThemeService_Changed;
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
                HideToTray();
            }
        };

        Live.Devices.CollectionChanged += (_, _) =>
        {
            DevicePopupControl.DevicesCountText.Text = Live.Devices.Count.ToString(CultureInfo.InvariantCulture);
            DevicePopupControl.EmptyHintText.Visibility = Live.Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateDevicePopover();
        };

        syncToastTimer.Tick += (_, _) =>
        {
            syncToastTimer.Stop();
            StatusBar.HideSyncToast();
        };

        // Keeps "3s ago" labels and the live dots fresh between packets.
        tickTimer.Tick += (_, _) =>
        {
            foreach (var device in Live.Devices)
            {
                device.Tick();
            }

            if (DevicePopupControl.DevicePopup.IsOpen)
            {
                UpdateDevicePopover();
            }

            if (Live.SelectedDevice is { } selected)
            {
                ShowLastSeen(selected);
            }

            if (alertsPausedUntil is { } until && DateTime.Now >= until)
            {
                alertsPausedUntil = null;
                UpdatePauseUi();
            }

            if (++tickCount % 10 == 0)
            {
                RefreshChart(); // the window "now" keeps moving even without new data
                UpdateTray();   // turns the tray icon grey if the sensor has gone quiet
            }
        };
        tickTimer.Start();

        // The app exists to listen, so start right away (Stop still works as before).
        Loaded += (_, _) =>
        {
            StartListening();
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

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var useNarrowLayout = ActualWidth < 1060;
        if (useNarrowLayout == isNarrowDashboardLayout)
        {
            return;
        }
        isNarrowDashboardLayout = useNarrowLayout;
        LivePageControl.ApplyNarrowLayout(useNarrowLayout);
        HistoryPageControl.ApplyNarrowLayout(useNarrowLayout);
    }

    private void SetStatus(string text, ListenerStatusKind kind, string? details = null)
    {
        StatusBar.SetListenerStatus(text, kind, details, listenerHasBeenStarted);
        UpdateHeaderSensorState();
    }

    private void SetSyncStatus(string text, string? details = null)
    {
        StatusBar.SetSyncStatus(text, details);
    }

    private void ShowSyncToast(string text)
    {
        StatusBar.ShowSyncToast(text);
        syncToastTimer.Stop();
        syncToastTimer.Start();
    }

    private void ShowSyncProblem(string text, string details)
    {
        StatusBar.ShowSyncProblem(text, details);
    }

    private void UpdateHeaderSensorState()
    {
        if (StatusBar.ListenerStatus == ListenerStatusKind.Error)
        {
            UpdateFooterStatus("Listener error", "Danger");
            HeaderBar.DeviceChipButton.ToolTip = "Live readings stopped. Try starting the listener again.";
            return;
        }

        if (StatusBar.ListenerStatus is ListenerStatusKind.Idle or ListenerStatusKind.BluetoothUnavailable)
        {
            UpdateFooterStatus(StatusBar.ListenerStatus == ListenerStatusKind.BluetoothUnavailable ? "Bluetooth unavailable" : "Stopped", "Muted");
            HeaderBar.DeviceChipButton.ToolTip = StatusBar.ListenerStatus == ListenerStatusKind.BluetoothUnavailable
                ? "Bluetooth is unavailable. Turn it on in Windows settings to see live readings."
                : "Live readings are off.";
            return;
        }

        if (Live.SelectedDevice is not { } device || GetLastReadingTime(device) == default)
        {
            UpdateFooterStatus("Searching", "Muted");
            HeaderBar.DeviceChipButton.ToolTip = "Looking for your Aranet4. Make sure Smart Home Integration is enabled in the Aranet Home app.";
            return;
        }

        var stale = sensorMonitor.IsStale(
            device.Address,
            GetLastReadingTime(device),
            DateTime.Now);
        UpdateFooterStatus(stale ? "Waiting for sensor" : "Live", stale ? "Warning" : "Positive");
        HeaderBar.DeviceChipButton.ToolTip = stale
            ? "No recent readings from this sensor. Move it closer and check its battery."
            : "Receiving live readings from this sensor.";
    }

    private void UpdateFooterStatus(string text, string brushKey)
    {
        StatusBar.SetFooterStatus(text, brushKey);
    }
}
