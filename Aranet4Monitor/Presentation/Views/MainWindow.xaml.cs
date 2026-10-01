using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aranet4Monitor.Presentation.Tray;
using Aranet4Monitor.Storage;
using Aranet4Monitor.Models;

namespace Aranet4Monitor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _tickTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly AppPreferences _preferences = AppPreferences.Load();
    private readonly TrayIconService _notifications = new();
    private bool _startHiddenInTray = Environment.GetCommandLineArgs().Contains(StartupRegistration.TrayArgument);
    private DateTime? _alertsPausedUntil;
    private CancellationTokenSource? _syncCancellation;
    private TimeSpan? _range = TimeSpan.FromHours(6); // null = show all recorded history
    private MetricKind _metric = MetricKind.Co2;          // which metric the chart shows
    private DateTime _devicePopupClosedAt;
    private DateTime _statusHoldUntil;
    private int _tickCount;
    private bool _allowClose;

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

    private static Brush Frozen(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        DevicesList.ItemsSource = Devices;
        CelsiusUnitMenuItem.IsChecked = _preferences.TemperatureDisplayUnit == TemperatureUnit.Celsius;
        FahrenheitUnitMenuItem.IsChecked = _preferences.TemperatureDisplayUnit == TemperatureUnit.Fahrenheit;
        HistoryChart.TemperatureDisplayUnit = _preferences.TemperatureDisplayUnit;
        AlertThresholdTextBox.Text = _preferences.AlertThresholdPpm.ToString(CultureInfo.InvariantCulture);
        AlertDurationTextBox.Text = _preferences.AlertDurationMinutes.ToString(CultureInfo.InvariantCulture);
        AlertStatusText.Text = _preferences.LastCo2AlertAt is { } lastAlert
            ? $"Last notification: {_preferences.LastCo2AlertPpm:N0} ppm at {lastAlert:t}"
            : "No high-CO₂ alerts sent yet.";
        _notifications.RestoreRequested += (_, _) => RestoreFromTray();
        _notifications.ExitRequested += (_, _) => ExitFromTray();
        _notifications.PauseToggleRequested += (_, _) => ToggleAlertPause();
        _notifications.StartWithWindowsToggled += (_, enabled) =>
        {
            // If Windows refuses the change, put the menu check mark back to the real state.
            if (!StartupRegistration.SetEnabled(enabled)) _notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
        };
        _notifications.SetStartWithWindows(StartupRegistration.IsEnabled);
        _notifications.SetShowNumber(_preferences.TrayShowNumber);
        _notifications.SetLargePopups(_preferences.LargePopups);
        _notifications.ShowNumberToggled += (_, enabled) => { _preferences.TrayShowNumber = enabled; _preferences.Save(); };
        _notifications.LargePopupsToggled += (_, enabled) => { _preferences.LargePopups = enabled; _preferences.Save(); };

        if (_startHiddenInTray)
        {
            // Started by Windows: come up minimized so no window flashes; Loaded then hides it in the tray.
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;
        }
        Closing += MainWindow_Closing;
        Closed += (_, _) => { _tickTimer.Stop(); _syncCancellation?.Cancel(); StopWatching(); _notifications.Dispose(); };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && !_startHiddenInTray) HideToTray();
        };

        Devices.CollectionChanged += (_, _) =>
        {
            DevicesCountText.Text = Devices.Count.ToString(CultureInfo.InvariantCulture);
            EmptyHintText.Visibility = Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };

        // Keeps "3s ago" labels and the live dots fresh between packets.
        _tickTimer.Tick += (_, _) =>
        {
            foreach (var device in Devices) device.Tick();
            if (DevicesList.SelectedItem is Aranet4Device selected) ShowLastSeen(selected);
            if (_alertsPausedUntil is { } until && DateTime.Now >= until)
            {
                _alertsPausedUntil = null;
                UpdatePauseUi();
            }

            if (++_tickCount % 10 == 0)
            {
                RefreshChart(); // the window "now" keeps moving even without new data
                UpdateTray();   // turns the tray icon grey if the sensor has gone quiet
            }
        };
        _tickTimer.Start();

        // The app exists to listen, so start right away (Stop still works as before).
        Loaded += (_, _) =>
        {
            StartListening();
            if (!_startHiddenInTray) return;
            Hide();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            _startHiddenInTray = false; // from now on minimizing hides to the tray as usual
        };
    }

    private enum StatusKind { Idle, Listening, Error }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusText.ToolTip = text;
        StatusDot.Tag = kind switch { StatusKind.Listening => "on", StatusKind.Error => "error", _ => null };
    }
}
