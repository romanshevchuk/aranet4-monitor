using System.ComponentModel;
using System.Windows;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor.Presentation.Tray;

public sealed class TrayWindowCoordinator
{
    private readonly Window window;
    private readonly AppPreferences preferences;
    private readonly IPreferencesStore preferencesStore;
    private readonly TrayIconService trayIcon;
    private bool allowClose;

    public TrayWindowCoordinator(
        Window window,
        AppPreferences preferences,
        IPreferencesStore preferencesStore,
        TrayIconService trayIcon)
    {
        this.window = window;
        this.preferences = preferences;
        this.preferencesStore = preferencesStore;
        this.trayIcon = trayIcon;

        window.Closing += Window_Closing;
        trayIcon.RestoreRequested += (_, _) => Restore();
        trayIcon.ExitRequested += (_, _) => Exit();
    }

    public void HideToTray()
    {
        window.Hide();
        if (preferences.TrayHintShown)
        {
            return;
        }

        preferences.TrayHintShown = true;
        preferencesStore.Save(preferences);
        trayIcon.NotifyHint("🫧 Still here, in the tray", "I'll keep listening quietly. Click the tray icon to open me, right-click for options.");
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void Restore()
    {
        window.Show();
        window.ShowInTaskbar = true;
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void Exit()
    {
        allowClose = true;
        window.Close();
    }
}
