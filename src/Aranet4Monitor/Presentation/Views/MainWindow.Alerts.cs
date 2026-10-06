namespace Aranet4Monitor;

public partial class MainWindow
{
    private bool AlertsPaused => alertsPausedUntil is { } until && DateTime.Now < until;

    private void ToggleAlertPause()
    {
        alertsPausedUntil = AlertsPaused ? null : DateTime.Now.AddHours(1);
        UpdatePauseUi();
    }

    private void UpdatePauseUi()
    {
        notifications.SetAlertsPaused(AlertsPaused, alertsPausedUntil);
        settings.SnoozeButtonText = AlertsPaused ? "Resume alerts" : "Pause 1 h";
        if (AlertsPaused)
        {
            SetAlertStatus($"Alerts paused until {alertsPausedUntil:t}.");
        }
        else
        {
            SetAlertStatus("Alerts are on.");
        }
    }

    private void SetAlertStatus(string text) => settings.AlertStatus = text;

    private int AlertThreshold => settings.AlertThresholdPpm;

    private int AlertDurationMinutes => settings.AlertDurationMinutes;

    private void SendTestAlertFromSettings()
    {
        notifications.NotifyHighCo2(settings.AlertThresholdPpm + 80);
        SetAlertStatus("Test notification sent.");
    }
}
