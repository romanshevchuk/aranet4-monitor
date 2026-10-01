using Aranet4Monitor.Alerts;
using Aranet4Monitor.Models;

namespace Aranet4Monitor.Presentation.Tray;

/// <summary>
/// The system-tray presence: a live CO₂ number as the icon, a context menu, and the notifications.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string AppName = "Aranet4 Monitor";
    private const int MaxTooltipLength = 127; // NotifyIcon.Text throws above this

    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly System.Windows.Forms.ToolStripMenuItem readingItem;
    private readonly System.Windows.Forms.ToolStripMenuItem pauseItem;
    private readonly System.Windows.Forms.ToolStripMenuItem startupItem;
    private readonly System.Windows.Forms.ToolStripMenuItem numberItem;
    private readonly System.Windows.Forms.ToolStripMenuItem popupItem;
    private readonly System.Drawing.Icon defaultIcon;
    private System.Drawing.Icon? readingIcon;
    private int lastPpm = -1;
    private bool lastStale;
    private bool showNumber = true;
    private bool largePopups = true;
    private ToastWindow? toast;
    private int rotation = Random.Shared.Next(0, 1_000);

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
        defaultIcon = LoadDefaultIcon();
        icon = new System.Windows.Forms.NotifyIcon { Icon = defaultIcon, Text = AppName, Visible = true };

        readingItem = new System.Windows.Forms.ToolStripMenuItem("Waiting for a reading…") { Enabled = false };
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open Aranet4 Monitor", null, (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty));
        pauseItem = new System.Windows.Forms.ToolStripMenuItem("Pause alerts for 1 hour", null, (_, _) => PauseToggleRequested?.Invoke(this, EventArgs.Empty));
        startupItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        startupItem.Click += (_, _) => StartWithWindowsToggled?.Invoke(this, startupItem.Checked);
        numberItem = new System.Windows.Forms.ToolStripMenuItem("Show number on tray icon") { CheckOnClick = true, Checked = true };
        numberItem.Click += (_, _) =>
        {
            showNumber = numberItem.Checked;
            Redraw();
            ShowNumberToggled?.Invoke(this, showNumber);
        };
        popupItem = new System.Windows.Forms.ToolStripMenuItem("Large pop-up notifications") { CheckOnClick = true, Checked = true };
        popupItem.Click += (_, _) =>
        {
            largePopups = popupItem.Checked;
            LargePopupsToggled?.Invoke(this, largePopups);
        };
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(readingItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(numberItem);
        menu.Items.Add(popupItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        icon.ContextMenuStrip = menu;

        // One click opens the dashboard; right-click still shows the menu.
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                RestoreRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        icon.BalloonTipClicked += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the current CO₂ value as the tray icon. Cheap to call often: it only redraws when something changed.</summary>
    /// <param name="stale">True when the sensor hasn't been heard from for a while (icon turns grey).</param>
    public void SetReading(int ppm, bool stale)
    {
        if (ppm == lastPpm && stale == lastStale)
        {
            return;
        }

        lastPpm = ppm;
        lastStale = stale;

        if (ppm <= 0)
        {
            icon.Icon = defaultIcon;
            readingIcon?.Dispose();
            readingIcon = null;
            icon.Text = AppName;
            readingItem.Text = "Waiting for a reading…";
            return;
        }

        var newIcon = TrayIconRenderer.Create(ppm, stale, showNumber);
        icon.Icon = newIcon;
        readingIcon?.Dispose();
        readingIcon = newIcon;

        var summary = $"{ppm:N0} ppm · {Co2Quality.Describe(Co2Quality.Classify(ppm))}";
        readingItem.Text = stale ? $"CO₂ {summary} (sensor out of range)" : $"CO₂ {summary}";
        icon.Text = Truncate(stale ? $"{AppName} · last {summary}" : $"{AppName} · {summary}", MaxTooltipLength);
    }

    public void NotifyHighCo2(int ppm)
    {
        var message = AlertMessages.Create(ppm, rotation++);
        Show(message.Title, message.Body, ppm >= 2_000 ? ToastKind.Danger : ToastKind.Warning);
    }

    public void NotifyRecovered(int ppm)
    {
        var message = AlertMessages.CreateRecovered(ppm, rotation++);
        Show(message.Title, message.Body, ToastKind.Success);
    }

    public void NotifyHint(string title, string body) => Show(title, body, ToastKind.Info);

    public void SetShowNumber(bool enabled)
    {
        showNumber = enabled;
        numberItem.Checked = enabled;
        Redraw();
    }

    public void SetLargePopups(bool enabled)
    {
        largePopups = enabled;
        popupItem.Checked = enabled;
    }

    /// <summary>Forces the icon to be drawn again with the current settings.</summary>
    private void Redraw()
    {
        var (ppm, stale) = (lastPpm, lastStale);
        lastPpm = -1;
        SetReading(ppm, stale);
    }

    public void SetAlertsPaused(bool paused, DateTime? until) =>
        pauseItem.Text = paused ? $"Resume alerts (paused until {until:t})" : "Pause alerts for 1 hour";

    public void SetStartWithWindows(bool enabled) => startupItem.Checked = enabled;

    public void Dispose()
    {
        try
        {
            toast?.Close();
        }
        catch (InvalidOperationException) { /* already closed */ }
        icon.Visible = false;
        icon.Dispose();
        readingIcon?.Dispose();
        defaultIcon.Dispose();
    }

    private void Show(string title, string body, ToastKind kind)
    {
        if (largePopups)
        {
            try
            {
                this.toast?.Dismiss();
                var toast = new ToastWindow(title, body, kind);
                toast.Clicked += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
                this.toast = toast;
                toast.Show();
                if (kind != ToastKind.Info)
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }

                return;
            }
            catch (Exception)
            {
                // Fall back to the standard Windows notification below.
            }
        }

        var toolTipIcon = kind switch
        {
            ToastKind.Warning => System.Windows.Forms.ToolTipIcon.Warning,
            ToastKind.Danger => System.Windows.Forms.ToolTipIcon.Error,
            _ => System.Windows.Forms.ToolTipIcon.Info,
        };
        icon.ShowBalloonTip(8_000, title, body, toolTipIcon);
    }

    private static System.Drawing.Icon LoadDefaultIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } path && System.Drawing.Icon.ExtractAssociatedIcon(path) is { } icon)
            {
                return icon;
            }
        }
        catch (Exception) { /* fall back below */ }

        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
