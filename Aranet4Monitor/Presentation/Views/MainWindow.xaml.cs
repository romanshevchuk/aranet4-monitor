using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aranet4Monitor.Models;
using Aranet4Monitor.Presentation.Tray;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer tickTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly AppPreferences preferences = AppPreferences.Load();
    private readonly TrayIconService notifications = new();
    public DashboardViewModel Dashboard { get; } = new();
    private bool startHiddenInTray = Environment.GetCommandLineArgs().Contains(StartupRegistration.TrayArgument);
    private DateTime? alertsPausedUntil;
    private CancellationTokenSource? syncCancellation;
    private DateTime devicePopupClosedAt;
    private int tickCount;
    private bool allowClose;

    private static readonly Brush DotLive = Frozen(Color.FromRgb(0x2E, 0xC2, 0x7E));
    private static readonly Brush DotStale = Frozen(Color.FromRgb(0xF5, 0xB9, 0x42));
    private static readonly Brush DotIdle = Frozen(Color.FromRgb(0x8F, 0xA3, 0xBF));
    private static readonly Brush SeenLive = Frozen(Color.FromRgb(0x9D, 0xB8, 0xE0));
    private static readonly Brush SeenStale = Frozen(Color.FromRgb(0xFF, 0xD2, 0x7A));
    private static readonly Dictionary<MetricKind, Brush> AccentBrushes =
        Enum.GetValues<MetricKind>().ToDictionary(kind => kind, kind => Frozen(Metrics.Accent(kind)));
    private static readonly Brush TrendRising = Frozen(Color.FromRgb(0xFF, 0xD2, 0x7A));
    private static readonly Brush TrendFalling = Frozen(Color.FromRgb(0x7B, 0xE0, 0xAE));
    private static readonly Brush TrendSteady = Frozen(Color.FromRgb(0x9D, 0xB8, 0xE0));
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = Dashboard;
        CelsiusUnitMenuItem.IsChecked = preferences.TemperatureDisplayUnit == TemperatureUnit.Celsius;
        FahrenheitUnitMenuItem.IsChecked = preferences.TemperatureDisplayUnit == TemperatureUnit.Fahrenheit;
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
            }
        };
        notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
        notifications.SetShowNumber(preferences.TrayShowNumber);
        notifications.SetLargePopups(preferences.LargePopups);
        notifications.ShowNumberToggled += (_, enabled) => { preferences.TrayShowNumber = enabled; preferences.Save(); };
        notifications.LargePopupsToggled += (_, enabled) => { preferences.LargePopups = enabled; preferences.Save(); };

        if (startHiddenInTray)
        {
            // Started by Windows: come up minimized so no window flashes; Loaded then hides it in the tray.
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
        }
        Closing += MainWindow_Closing;
        Closed += (_, _) => { tickTimer.Stop(); syncCancellation?.Cancel(); StopWatching(); notifications.Dispose(); };
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
        };

        // Keeps "3s ago" labels and the live dots fresh between packets.
        tickTimer.Tick += (_, _) =>
        {
            foreach (var device in Dashboard.Devices)
            {
                device.Tick();
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

    private enum StatusKind
    {
        Idle, Listening, Error
    }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusText.ToolTip = text;
        StatusDot.Tag = kind switch
        {
            StatusKind.Listening => "on",
            StatusKind.Error => "error",
            _ => null
        };
    }

    private void SetSyncStatus(string text, string? details = null)
    {
        SyncStatusText.Text = text;
        SyncStatusText.ToolTip = details ?? text;
        SyncStatusText.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
