using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Aranet4Monitor.Alerts;

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
        PauseAlertsButton.Content = AlertsPaused ? "Resume alerts" : "Pause 1 h";
        if (AlertsPaused)
        {
            SetAlertStatus($"Alerts paused until {alertsPausedUntil:t}.", alertStatusWarning);
        }
        else
        {
            SetAlertStatus("Alerts are on.", alertStatusNeutral);
        }
    }

    private void SetAlertStatus(string text, Brush foreground)
    {
        AlertStatusText.Text = text;
        AlertStatusText.Foreground = foreground;
    }

    private void PauseAlerts_Click(object sender, RoutedEventArgs e) => ToggleAlertPause();

    private void SendTestAlert_Click(object sender, RoutedEventArgs e)
    {
        // Preview the real notification, using a level just above the user's threshold.
        notifications.NotifyHighCo2(AlertThreshold + 80);
        SetAlertStatus("Test notification sent.", alertStatusSuccess);
    }

    private int AlertThreshold => int.TryParse(AlertThresholdTextBox.Text, out var value)
        ? Math.Clamp(value, 800, 5_000)
        : Co2AlertService.RecommendedVentilationThresholdPpm;

    private int AlertDurationMinutes => int.TryParse(AlertDurationTextBox.Text, out var value)
        ? Math.Clamp(value, 1, 60)
        : 10;

    private void AlertThreshold_LostFocus(object sender, RoutedEventArgs e)
    {
        preferences.AlertThresholdPpm = AlertThreshold;
        preferences.AlertDurationMinutes = AlertDurationMinutes;
        AlertThresholdTextBox.Text = preferences.AlertThresholdPpm.ToString(CultureInfo.InvariantCulture);
        AlertDurationTextBox.Text = preferences.AlertDurationMinutes.ToString(CultureInfo.InvariantCulture);
        preferences.Save();
    }
}
