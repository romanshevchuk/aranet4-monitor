using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Storage;
using Aranet4Monitor.Windows;

namespace Aranet4Monitor.Presentation.Tray;

/// <summary>Keeps tray menu preferences and their settings-page view model values in sync.</summary>
public sealed class TraySettingsCoordinator : IDisposable
{
    private readonly TrayIconService trayIcon;
    private readonly SettingsViewModel settings;
    private readonly AppPreferences preferences;
    private readonly IPreferencesStore preferencesStore;
    private bool disposed;

    public TraySettingsCoordinator(
        TrayIconService trayIcon,
        SettingsViewModel settings,
        AppPreferences preferences,
        IPreferencesStore preferencesStore)
    {
        this.trayIcon = trayIcon;
        this.settings = settings;
        this.preferences = preferences;
        this.preferencesStore = preferencesStore;

        trayIcon.PauseToggleRequested += PauseToggleRequested;
        trayIcon.StartWithWindowsToggled += StartWithWindowsToggled;
        trayIcon.ShowNumberToggled += ShowNumberToggled;
        trayIcon.LargePopupsToggled += LargePopupsToggled;

        trayIcon.SetStartWithWindows(StartupRegistration.IsEnabled);
        trayIcon.SetShowNumber(preferences.TrayShowNumber);
        trayIcon.SetLargePopups(preferences.LargePopups);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        trayIcon.PauseToggleRequested -= PauseToggleRequested;
        trayIcon.StartWithWindowsToggled -= StartWithWindowsToggled;
        trayIcon.ShowNumberToggled -= ShowNumberToggled;
        trayIcon.LargePopupsToggled -= LargePopupsToggled;
    }

    private void PauseToggleRequested(object? sender, EventArgs args) => settings.ToggleAlertPause(DateTime.Now);

    private void StartWithWindowsToggled(object? sender, bool enabled)
    {
        // Restore the menu to the actual system state if Windows rejects the requested change.
        var actual = StartupRegistration.SetEnabled(enabled)
            ? enabled
            : StartupRegistration.IsEnabled;
        trayIcon.SetStartWithWindows(actual);
        settings.SetStartWithWindowsFromSystem(actual);
    }

    private void ShowNumberToggled(object? sender, bool enabled)
    {
        preferences.TrayShowNumber = enabled;
        preferencesStore.Save(preferences);
        settings.SetShowNumberFromTray(enabled);
    }

    private void LargePopupsToggled(object? sender, bool enabled)
    {
        preferences.LargePopups = enabled;
        preferencesStore.Save(preferences);
        settings.SetLargePopupsFromTray(enabled);
    }
}
