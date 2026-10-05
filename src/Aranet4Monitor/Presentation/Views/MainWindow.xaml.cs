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
    public DashboardViewModel Dashboard { get; } = new();
    private bool startHiddenInTray = Environment.GetCommandLineArgs().Contains(StartupRegistration.TrayArgument);
    private DateTime? alertsPausedUntil;
    private CancellationTokenSource? syncCancellation;
    private DateTime devicePopupClosedAt;
    private int tickCount;
    private bool allowClose;
    private bool listenerHasBeenStarted;
    private bool isNarrowDashboardLayout;
    private StatusKind statusKind = StatusKind.Idle;

    private static readonly Brush DotLive = Frozen(Color.FromRgb(0x2E, 0xC2, 0x7E));
    private static readonly Brush DotStale = Frozen(Color.FromRgb(0xF5, 0xB9, 0x42));
    private static readonly Brush DotIdle = Frozen(Color.FromRgb(0x8F, 0xA3, 0xBF));
    private static readonly Brush SeenLive = Frozen(Color.FromRgb(0x9D, 0xB8, 0xE0));
    private static readonly Brush SeenStale = Frozen(Color.FromRgb(0xFF, 0xD2, 0x7A));
    private static readonly Dictionary<MetricKind, Brush> AccentBrushes =
        Enum.GetValues<MetricKind>().ToDictionary(kind => kind, kind => Frozen(MetricColors.Accent(kind)));
    private static readonly Brush alertStatusNeutral = Frozen(Color.FromRgb(0x66, 0x73, 0x8A));
    private static readonly Brush alertStatusSuccess = Frozen(Color.FromRgb(0x15, 0x80, 0x3D));
    private static readonly Brush alertStatusWarning = Frozen(Color.FromRgb(0xB4, 0x53, 0x09));
    private static readonly Brush alertStatusDanger = Frozen(Color.FromRgb(0xB4, 0x23, 0x18));

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public MainWindow(AppServices services)
    {
        preferencesStore = services.PreferencesStore;
        preferences = services.Preferences;
        historySyncService = services.HistorySyncService;
        sensorSource = services.SensorSource;
        sensorMonitor = services.SensorMonitor;

        InitializeComponent();
        DataContext = Dashboard;
        GoodZoneText.Text = "Good";
        FairZoneText.Text = "Elevated";
        PoorZoneText.Text = "High";
        CelsiusUnitMenuItem.IsChecked = preferences.TemperatureDisplayUnit == TemperatureUnit.Celsius;
        FahrenheitUnitMenuItem.IsChecked = preferences.TemperatureDisplayUnit == TemperatureUnit.Fahrenheit;
        ShowNumberMenuItem.IsChecked = preferences.TrayShowNumber;
        LargePopupsMenuItem.IsChecked = preferences.LargePopups;
        StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled;
        HistoryChart.TemperatureDisplayUnit = preferences.TemperatureDisplayUnit;
        AlertThresholdTextBox.Text = preferences.AlertThresholdPpm.ToString(CultureInfo.InvariantCulture);
        AlertDurationTextBox.Text = preferences.AlertDurationMinutes.ToString(CultureInfo.InvariantCulture);
        SetAlertStatus(
            preferences.LastCo2AlertAt is { } lastAlert
                ? $"Last notification: {preferences.LastCo2AlertPpm:N0} ppm at {lastAlert:t}"
                : "No high-CO₂ alerts sent yet.",
            alertStatusNeutral);
        notifications.RestoreRequested += (_, _) => RestoreFromTray();
        notifications.ExitRequested += (_, _) => ExitFromTray();
        notifications.PauseToggleRequested += (_, _) => ToggleAlertPause();
        notifications.StartWithWindowsToggled += (_, enabled) =>
        {
            // If Windows refuses the change, put the menu check mark back to the real state.
            if (!StartupRegistration.SetEnabled(enabled))
            {
                notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
                StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled;
            }
            else
            {
                StartWithWindowsMenuItem.IsChecked = enabled;
            }
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
            ShowNumberMenuItem.IsChecked = enabled;
        };
        notifications.LargePopupsToggled += (_, enabled) =>
        {
            preferences.LargePopups = enabled;
            preferencesStore.Save(preferences);
            LargePopupsMenuItem.IsChecked = enabled;
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
            syncCancellation?.Cancel();
            StopWatching();
            sensorSource.AdvertisementReceived -= SensorSource_AdvertisementReceived;
            sensorSource.Stopped -= SensorSource_Stopped;
            sensorSource.Dispose();
            notifications.Dispose();
        };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !startHiddenInTray)
            {
                HideToTray();
            }
        };

        Dashboard.Devices.CollectionChanged += (_, _) =>
        {
            DevicesCountText.Text = Dashboard.Devices.Count.ToString(CultureInfo.InvariantCulture);
            EmptyHintText.Visibility = Dashboard.Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateDevicePopover();
        };

        syncToastTimer.Tick += (_, _) =>
        {
            syncToastTimer.Stop();
            SyncToast.Visibility = Visibility.Collapsed;
        };

        // Keeps "3s ago" labels and the live dots fresh between packets.
        tickTimer.Tick += (_, _) =>
        {
            foreach (var device in Dashboard.Devices)
            {
                device.Tick();
            }

            if (DevicePopup.IsOpen)
            {
                UpdateDevicePopover();
            }

            if (Dashboard.SelectedDevice is { } selected)
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
        if (DashboardGrid is null)
        {
            return;
        }

        var useNarrowLayout = ActualWidth < 1060;
        if (useNarrowLayout == isNarrowDashboardLayout)
        {
            return;
        }

        isNarrowDashboardLayout = useNarrowLayout;
        DashboardGrid.RowDefinitions.Clear();
        DashboardGrid.ColumnDefinitions.Clear();

        if (useNarrowLayout)
        {
            DashboardGrid.ColumnDefinitions.Add(new ColumnDefinition());
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(Co2HeroPanel, 0);
            Grid.SetColumn(Co2HeroPanel, 0);
            Grid.SetRow(HistoryChartCard, 1);
            Grid.SetColumn(HistoryChartCard, 0);
            Grid.SetRow(SecondaryMetricsGrid, 2);
            Grid.SetColumn(SecondaryMetricsGrid, 0);
            Grid.SetColumnSpan(SecondaryMetricsGrid, 1);
            Grid.SetRow(StatusStripGrid, 3);
            Grid.SetColumn(StatusStripGrid, 0);
            Grid.SetColumnSpan(StatusStripGrid, 1);
            Co2HeroPanel.Margin = new Thickness(0, 0, 0, 12);
            HistoryChartCard.Margin = new Thickness(0, 0, 0, 12);
            HistoryChart.MinHeight = 190;
            DashboardGrid.Margin = new Thickness(16, 14, 16, 14);
            StatusStripGrid.Margin = new Thickness(-16, 0, -16, -14);
            HistoryView.Margin = new Thickness(16, 14, 16, 14);
            HistoryContentGrid.ColumnDefinitions.Clear();
            HistoryContentGrid.RowDefinitions.Clear();
            HistoryContentGrid.ColumnDefinitions.Add(new ColumnDefinition());
            HistoryContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            HistoryContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(HistoryMainCard, 0);
            Grid.SetColumn(HistoryMainCard, 0);
            HistoryMainCard.Margin = new Thickness(0, 0, 0, 14);
            Grid.SetRow(HistorySummaryCard, 1);
            Grid.SetColumn(HistorySummaryCard, 0);
        }
        else
        {
            DashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.82, GridUnitType.Star), MinWidth = 330 });
            DashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star), MinWidth = 420 });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 300 });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DashboardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(Co2HeroPanel, 0);
            Grid.SetColumn(Co2HeroPanel, 0);
            Grid.SetRow(HistoryChartCard, 0);
            Grid.SetColumn(HistoryChartCard, 1);
            Grid.SetRow(SecondaryMetricsGrid, 1);
            Grid.SetColumn(SecondaryMetricsGrid, 0);
            Grid.SetColumnSpan(SecondaryMetricsGrid, 2);
            Grid.SetRow(StatusStripGrid, 2);
            Grid.SetColumn(StatusStripGrid, 0);
            Grid.SetColumnSpan(StatusStripGrid, 2);
            Co2HeroPanel.Margin = new Thickness(0, 0, 16, 14);
            HistoryChartCard.Margin = new Thickness(0, 0, 0, 14);
            HistoryChart.MinHeight = 150;
            DashboardGrid.Margin = new Thickness(26, 24, 26, 18);
            StatusStripGrid.Margin = new Thickness(-26, 0, -26, -18);
            HistoryView.Margin = new Thickness(26, 24, 26, 18);
            HistoryContentGrid.ColumnDefinitions.Clear();
            HistoryContentGrid.RowDefinitions.Clear();
            HistoryContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 420 });
            HistoryContentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            HistoryContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(HistoryMainCard, 0);
            Grid.SetColumn(HistoryMainCard, 0);
            HistoryMainCard.Margin = new Thickness(0, 0, 14, 0);
            Grid.SetRow(HistorySummaryCard, 0);
            Grid.SetColumn(HistorySummaryCard, 1);
        }
    }

    private enum StatusKind
    {
        Idle, Listening, Error, BluetoothUnavailable
    }

    private void SetStatus(string text, StatusKind kind, string? details = null)
    {
        statusKind = kind;
        NoticeBar.Visibility = kind == StatusKind.Listening || !listenerHasBeenStarted
            ? Visibility.Collapsed
            : Visibility.Visible;

        switch (kind)
        {
            case StatusKind.Idle:
                NoticeText.Text = "Live readings are off.";
                NoticeActionButton.Content = "Start listening";
                BluetoothSettingsButton.Visibility = Visibility.Collapsed;
                NoticeBar.Background = (Brush)FindResource("SurfaceSecondary");
                NoticeBar.BorderBrush = (Brush)FindResource("Border");
                NoticeBar.ToolTip = null;
                break;
            case StatusKind.Error:
                NoticeText.Text = "Live readings stopped. Check Bluetooth settings or permissions, then try again.";
                NoticeActionButton.Content = "Try again";
                BluetoothSettingsButton.Visibility = Visibility.Collapsed;
                NoticeBar.Background = (Brush)FindResource("DangerBackground");
                NoticeBar.BorderBrush = (Brush)FindResource("Danger");
                NoticeBar.ToolTip = details ?? text;
                break;
            case StatusKind.BluetoothUnavailable:
                NoticeText.Text = "Bluetooth is off. Turn it on in Windows settings to see live readings.";
                NoticeActionButton.Visibility = Visibility.Collapsed;
                BluetoothSettingsButton.Visibility = Visibility.Visible;
                NoticeBar.Background = (Brush)FindResource("WarningBackground");
                NoticeBar.BorderBrush = (Brush)FindResource("Warning");
                NoticeBar.ToolTip = details ?? text;
                break;
            default:
                NoticeActionButton.Visibility = Visibility.Visible;
                NoticeBar.Background = (Brush)FindResource("PositiveBackground");
                NoticeBar.BorderBrush = (Brush)FindResource("Positive");
                NoticeBar.ToolTip = details;
                break;
        }

        NoticeActionButton.Visibility = kind == StatusKind.BluetoothUnavailable
            ? Visibility.Collapsed
            : Visibility.Visible;
        UpdateHeaderSensorState();
    }

    private void SetSyncStatus(string text, string? details = null)
    {
        SyncStatusText.Text = text;
        SyncStatusText.ToolTip = details ?? text;
        SyncProgressPanel.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowSyncToast(string text)
    {
        SyncToastText.Text = text;
        SyncToast.Visibility = Visibility.Visible;
        syncToastTimer.Stop();
        syncToastTimer.Start();
    }

    private void ShowSyncProblem(string text, string details)
    {
        SyncProblemText.Text = text;
        SyncProblemText.ToolTip = details;
        SyncProblemBanner.Visibility = Visibility.Visible;
    }

    private void UpdateHeaderSensorState()
    {
        if (statusKind == StatusKind.Error)
        {
            UpdateFooterStatus("Listener error", "Danger");
            DeviceDot.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            DeviceChipButton.ToolTip = "Live readings stopped. Try starting the listener again.";
            return;
        }

        if (statusKind is StatusKind.Idle or StatusKind.BluetoothUnavailable)
        {
            UpdateFooterStatus(statusKind == StatusKind.BluetoothUnavailable ? "Bluetooth unavailable" : "Stopped", "Muted");
            DeviceDot.Fill = DotIdle;
            DeviceChipButton.ToolTip = statusKind == StatusKind.BluetoothUnavailable
                ? "Bluetooth is unavailable. Turn it on in Windows settings to see live readings."
                : "Live readings are off.";
            return;
        }

        if (Dashboard.SelectedDevice is not { } device || GetLastReadingTime(device) == default)
        {
            UpdateFooterStatus("Searching", "Muted");
            DeviceDot.Fill = DotIdle;
            DeviceChipButton.ToolTip = "Looking for your Aranet4. Make sure Smart Home Integration is enabled in the Aranet Home app.";
            return;
        }

        var stale = sensorMonitor.IsStale(
            device.Address,
            GetLastReadingTime(device),
            DateTime.Now);
        UpdateFooterStatus(stale ? "Waiting for sensor" : "Live", stale ? "Warning" : "Positive");
        DeviceDot.Fill = stale ? DotStale : DotLive;
        DeviceChipButton.ToolTip = stale
            ? "No recent readings from this sensor. Move it closer and check its battery."
            : "Receiving live readings from this sensor.";
    }

    private void UpdateFooterStatus(string text, string brushKey)
    {
        FooterStatusText.Text = text;
        FooterStatusDot.Fill = (Brush)FindResource(brushKey);
    }
}
