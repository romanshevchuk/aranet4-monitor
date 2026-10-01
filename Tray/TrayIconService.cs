namespace BleListener;

/// <summary>
/// The system-tray presence: a live CO₂ number as the icon, a context menu, and the notifications.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string AppName = "Aranet4 Home";
    private const int MaxTooltipLength = 127; // NotifyIcon.Text throws above this

    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ToolStripMenuItem _readingItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _pauseItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _startupItem;
    private readonly System.Drawing.Icon _defaultIcon;
    private System.Drawing.Icon? _readingIcon;
    private int _lastPpm = -1;
    private bool _lastStale;
    private int _rotation = Random.Shared.Next(0, 1_000);

    public event EventHandler? RestoreRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? PauseToggleRequested;

    /// <summary>Raised with the new checked state when the user toggles "Start with Windows".</summary>
    public event EventHandler<bool>? StartWithWindowsToggled;

    public TrayIconService()
    {
        _defaultIcon = LoadDefaultIcon();
        _icon = new System.Windows.Forms.NotifyIcon { Icon = _defaultIcon, Text = AppName, Visible = true };

        _readingItem = new System.Windows.Forms.ToolStripMenuItem("Waiting for a reading…") { Enabled = false };
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open Aranet4 Home", null, (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty));
        _pauseItem = new System.Windows.Forms.ToolStripMenuItem("Pause alerts for 1 hour", null, (_, _) => PauseToggleRequested?.Invoke(this, EventArgs.Empty));
        _startupItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        _startupItem.Click += (_, _) => StartWithWindowsToggled?.Invoke(this, _startupItem.Checked);
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(_readingItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        _icon.ContextMenuStrip = menu;

        // One click opens the dashboard; right-click still shows the menu.
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) RestoreRequested?.Invoke(this, EventArgs.Empty);
        };
        _icon.BalloonTipClicked += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the current CO₂ value as the tray icon. Cheap to call often: it only redraws when something changed.</summary>
    /// <param name="stale">True when the sensor hasn't been heard from for a while (icon turns grey).</param>
    public void SetReading(int ppm, bool stale)
    {
        if (ppm == _lastPpm && stale == _lastStale) return;
        _lastPpm = ppm;
        _lastStale = stale;

        if (ppm <= 0)
        {
            _icon.Icon = _defaultIcon;
            _readingIcon?.Dispose();
            _readingIcon = null;
            _icon.Text = AppName;
            _readingItem.Text = "Waiting for a reading…";
            return;
        }

        var newIcon = TrayIconRenderer.Create(ppm, stale);
        _icon.Icon = newIcon;
        _readingIcon?.Dispose();
        _readingIcon = newIcon;

        var summary = $"{ppm:N0} ppm · {Co2Quality.Describe(Co2Quality.Classify(ppm))}";
        _readingItem.Text = stale ? $"CO₂ {summary} (sensor out of range)" : $"CO₂ {summary}";
        _icon.Text = Truncate(stale ? $"{AppName} · last {summary}" : $"{AppName} · {summary}", MaxTooltipLength);
    }

    public void NotifyHighCo2(int ppm)
    {
        var message = AlertMessages.Create(ppm, _rotation++);
        Show(message.Title, message.Body);
    }

    public void NotifyRecovered(int ppm)
    {
        var message = AlertMessages.CreateRecovered(ppm, _rotation++);
        Show(message.Title, message.Body);
    }

    public void NotifyHint(string title, string body) => Show(title, body);

    public void SetAlertsPaused(bool paused, DateTime? until) =>
        _pauseItem.Text = paused ? $"Resume alerts (paused until {until:t})" : "Pause alerts for 1 hour";

    public void SetStartWithWindows(bool enabled) => _startupItem.Checked = enabled;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _readingIcon?.Dispose();
        _defaultIcon.Dispose();
    }

    private void Show(string title, string body) =>
        _icon.ShowBalloonTip(8_000, title, body, System.Windows.Forms.ToolTipIcon.None);

    private static System.Drawing.Icon LoadDefaultIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } path && System.Drawing.Icon.ExtractAssociatedIcon(path) is { } icon) return icon;
        }
        catch (Exception) { /* fall back below */ }

        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
