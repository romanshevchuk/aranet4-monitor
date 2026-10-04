using System.Windows;

namespace Aranet4Monitor;

public partial class MainWindow
{
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    /// <summary>Hides the window; the first time ever, explains where the app went.</summary>
    private void HideToTray()
    {
        Hide();
        if (preferences.TrayHintShown)
        {
            return;
        }

        preferences.TrayHintShown = true;
        preferencesStore.Save(preferences);
        notifications.NotifyHint("🫧 Still here, in the tray", "I'll keep listening quietly. Click the tray icon to open me, right-click for options.");
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        allowClose = true;
        Close();
    }
}
