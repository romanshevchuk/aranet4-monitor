using BleListener.Alerts;
using BleListener.Models;

namespace BleListener.Presentation.Tray;

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
    private readonly System.Windows.Forms.ToolStripMenuItem _numberItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _popupItem;
    private readonly System.Drawing.Icon _defaultIcon;
    private System.Drawing.Icon? _readingIcon;
    private int _lastPpm = -1;
    private bool _lastStale;
    private bool _showNumber = true;
    private bool _largePopups = true;
    private ToastWindow? _toast;
    private int _rotation = Random.Shared.Next(0, 1_000);

    public event EventHandler? RestoreRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? PauseToggleRequested;

    /// <summary>Raised with the new checked state when the user toggles "Start with Windows".</summary>
    public event EventHandler<bool>? StartWithWindowsToggled;

    /// <summary>Raised with the new state when the user toggles the number on the tray icon.</summary>
    public event EventHandler<bool>? ShowNumberToggled;

    /// <summary>Raised with the new state when the user toggles the large pop-up notifications.</summary>
    public event EventHandler<bool>? LargePopupsToggled;

    public TrayIconService()
    {
        _defaultIcon = LoadDefaultIcon();
        _icon = new System.Windows.Forms.NotifyIcon { Icon = _defaultIcon, Text = AppName, Visible = true };

        _readingItem = new System.Windows.Forms.ToolStripMenuItem("Waiting for a reading…") { Enabled = false };
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open Aranet4 Home", null, (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty));
        _pauseItem = new System.Windows.Forms.ToolStripMenuItem("Pause alerts for 1 hour", null, (_, _) => PauseToggleRequested?.Invoke(this, EventArgs.Empty));
        _startupItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        _startupItem.Click += (_, _) => StartWithWindowsToggled?.Invoke(this, _startupItem.Checked);
        _numberItem = new System.Windows.Forms.ToolStripMenuItem("Show number on tray icon") { CheckOnClick = true, Checked = true };
        _numberItem.Click += (_, _) =>
        {
            _showNumber = _numberItem.Checked;
            Redraw();
            ShowNumberToggled?.Invoke(this, _showNumber);
        };
        _popupItem = new System.Windows.Forms.ToolStripMenuItem("Large pop-up notifications") { CheckOnClick = true, Checked = true };
        _popupItem.Click += (_, _) =>
        {
            _largePopups = _popupItem.Checked;
            LargePopupsToggled?.Invoke(this, _largePopups);
        };
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(_readingItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(_numberItem);
        menu.Items.Add(_popupItem);
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

        var newIcon = TrayIconRenderer.Create(ppm, stale, _showNumber);
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
        Show(message.Title, message.Body, ppm >= 2_000 ? ToastKind.Danger : ToastKind.Warning);
    }

    public void NotifyRecovered(int ppm)
    {
        var message = AlertMessages.CreateRecovered(ppm, _rotation++);
        Show(message.Title, message.Body, ToastKind.Success);
    }

    public void NotifyHint(string title, string body) => Show(title, body, ToastKind.Info);

    public void SetShowNumber(bool enabled)
    {
        _showNumber = enabled;
        _numberItem.Checked = enabled;
        Redraw();
    }

    public void SetLargePopups(bool enabled)
    {
        _largePopups = enabled;
        _popupItem.Checked = enabled;
    }

    /// <summary>Forces the icon to be drawn again with the current settings.</summary>
    private void Redraw()
    {
        var (ppm, stale) = (_lastPpm, _lastStale);
        _lastPpm = -1;
        SetReading(ppm, stale);
    }

    public void SetAlertsPaused(bool paused, DateTime? until) =>
        _pauseItem.Text = paused ? $"Resume alerts (paused until {until:t})" : "Pause alerts for 1 hour";

    public void SetStartWithWindows(bool enabled) => _startupItem.Checked = enabled;

    public void Dispose()
    {
        try { _toast?.Close(); } catch (InvalidOperationException) { /* already closed */ }
        _icon.Visible = false;
        _icon.Dispose();
        _readingIcon?.Dispose();
        _defaultIcon.Dispose();
    }

    private void Show(string title, string body, ToastKind kind)
    {
        if (_largePopups)
        {
            try
            {
                _toast?.Dismiss();
                var toast = new ToastWindow(title, body, kind);
                toast.Clicked += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
                _toast = toast;
                toast.Show();
                if (kind != ToastKind.Info) System.Media.SystemSounds.Asterisk.Play();
                return;
            }
            catch (Exception)
            {
                // Fall back to the standard Windows notification below.
            }
        }

        _icon.ShowBalloonTip(8_000, title, body, System.Windows.Forms.ToolTipIcon.None);
    }

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
