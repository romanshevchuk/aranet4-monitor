using System;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor.Presentation.ViewModels;

public sealed record SettingsActions(
    Action<TemperatureUnit> ApplyTemperatureUnit,
    Action<AppTheme> ApplyTheme,
    Func<bool, bool> SetListening,
    Action<bool> SetTrayShowNumber,
    Action<bool> SetLargePopups,
    Func<bool, bool> SetStartWithWindows,
    Action<int> SendTestNotification,
    Action<MeasurementAlertEffect, int> NotifyMeasurementAlert,
    Action<bool, DateTime?> UpdateAlertsPaused,
    Action ForgetDetectedDevices,
    Action OpenBluetoothDiagnostics);

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppPreferences preferences;
    private readonly Action<AppPreferences> savePreferences;
    private readonly SettingsActions actions;
    private TemperatureUnit temperatureDisplayUnit;
    private AppTheme theme;
    private int alertThresholdPpm;
    private int alertDurationMinutes;
    private bool listenForLiveReadings;
    private bool showNumberInTray;
    private bool largePopups;
    private bool notificationsEnabled;
    private bool startWithWindows;
    private string alertStatus = string.Empty;
    private DateTime? alertsPausedUntil;

    public SettingsViewModel(
        AppPreferences preferences,
        Action<AppPreferences> savePreferences,
        SettingsActions actions,
        bool listenForLiveReadings,
        bool startWithWindows)
    {
        this.preferences = preferences;
        this.savePreferences = savePreferences;
        this.actions = actions;
        temperatureDisplayUnit = preferences.TemperatureDisplayUnit;
        theme = preferences.Theme;
        alertThresholdPpm = preferences.AlertThresholdPpm;
        alertDurationMinutes = preferences.AlertDurationMinutes;
        showNumberInTray = preferences.TrayShowNumber;
        largePopups = preferences.LargePopups;
        notificationsEnabled = preferences.NotificationsEnabled;
        this.listenForLiveReadings = listenForLiveReadings;
        this.startWithWindows = startWithWindows;

        TestNotificationCommand = new RelayCommand(_ => SendTestNotification());
        SnoozeCommand = new RelayCommand(_ => ToggleAlertPause(DateTime.Now));
        ForgetDetectedDevicesCommand = new RelayCommand(_ => actions.ForgetDetectedDevices());
        OpenBluetoothDiagnosticsCommand = new RelayCommand(_ => actions.OpenBluetoothDiagnostics());
    }

    public TemperatureUnit TemperatureDisplayUnit
    {
        get => temperatureDisplayUnit;
        set
        {
            if (!SetProperty(ref temperatureDisplayUnit, value))
            {
                return;
            }

            preferences.TemperatureDisplayUnit = value;
            savePreferences(preferences);
            actions.ApplyTemperatureUnit(value);
            OnPropertyChanged(nameof(IsCelsius));
            OnPropertyChanged(nameof(IsFahrenheit));
        }
    }

    public bool IsCelsius
    {
        get => TemperatureDisplayUnit == TemperatureUnit.Celsius;
        set
        {
            if (value)
            {
                TemperatureDisplayUnit = TemperatureUnit.Celsius;
            }
        }
    }

    public bool IsFahrenheit
    {
        get => TemperatureDisplayUnit == TemperatureUnit.Fahrenheit;
        set
        {
            if (value)
            {
                TemperatureDisplayUnit = TemperatureUnit.Fahrenheit;
            }
        }
    }

    public AppTheme Theme
    {
        get => theme;
        set
        {
            if (!SetProperty(ref theme, value))
            {
                return;
            }

            preferences.Theme = value;
            savePreferences(preferences);
            actions.ApplyTheme(value);
            OnPropertyChanged(nameof(IsAutoTheme));
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
        }
    }

    public bool IsAutoTheme
    {
        get => Theme == AppTheme.Auto;
        set
        {
            if (value)
            {
                Theme = AppTheme.Auto;
            }
        }
    }

    public bool IsLightTheme
    {
        get => Theme == AppTheme.Light;
        set
        {
            if (value)
            {
                Theme = AppTheme.Light;
            }
        }
    }

    public bool IsDarkTheme
    {
        get => Theme == AppTheme.Dark;
        set
        {
            if (value)
            {
                Theme = AppTheme.Dark;
            }
        }
    }

    public int AlertThresholdPpm
    {
        get => alertThresholdPpm;
        set
        {
            var normalized = Math.Clamp(value, 800, 5_000);
            if (!SetProperty(ref alertThresholdPpm, normalized))
            {
                return;
            }

            preferences.AlertThresholdPpm = normalized;
            savePreferences(preferences);
        }
    }

    public int AlertDurationMinutes
    {
        get => alertDurationMinutes;
        set
        {
            var normalized = Math.Clamp(value, 1, 60);
            if (!SetProperty(ref alertDurationMinutes, normalized))
            {
                return;
            }

            preferences.AlertDurationMinutes = normalized;
            savePreferences(preferences);
        }
    }

    public bool ListenForLiveReadings
    {
        get => listenForLiveReadings;
        set => SetListeningState(actions.SetListening(value));
    }

    public bool ShowNumberInTray
    {
        get => showNumberInTray;
        set
        {
            if (!SetProperty(ref showNumberInTray, value))
            {
                return;
            }

            preferences.TrayShowNumber = value;
            savePreferences(preferences);
            actions.SetTrayShowNumber(value);
        }
    }

    public bool LargePopups
    {
        get => largePopups;
        set
        {
            if (!SetProperty(ref largePopups, value))
            {
                return;
            }

            preferences.LargePopups = value;
            savePreferences(preferences);
            actions.SetLargePopups(value);
        }
    }

    public bool NotificationsEnabled
    {
        get => notificationsEnabled;
        set
        {
            if (!SetProperty(ref notificationsEnabled, value))
            {
                return;
            }

            preferences.NotificationsEnabled = value;
            savePreferences(preferences);
            OnPropertyChanged(nameof(IsAlertConfigurationEnabled));
        }
    }

    public bool IsAlertConfigurationEnabled => NotificationsEnabled;

    public bool StartWithWindows
    {
        get => startWithWindows;
        set => SetStartWithWindowsState(actions.SetStartWithWindows(value));
    }

    public string AlertStatus
    {
        get => alertStatus;
        set => SetProperty(ref alertStatus, value);
    }

    public string SnoozeButtonText
    {
        get => AlertsPaused ? "Resume alerts" : "Pause 1 h";
    }

    public DateTime? AlertsPausedUntil
    {
        get => alertsPausedUntil;
        private set
        {
            if (!SetProperty(ref alertsPausedUntil, value))
            {
                return;
            }

            OnPropertyChanged(nameof(AlertsPaused));
            OnPropertyChanged(nameof(SnoozeButtonText));
        }
    }

    public bool AlertsPaused => AlertsPausedUntil is { } until && DateTime.Now < until;

    public void ToggleAlertPause(DateTime now)
    {
        var wasPaused = AlertsPausedUntil is { } until && now < until;
        AlertsPausedUntil = wasPaused ? null : now.AddHours(1);
        AlertStatus = AlertsPausedUntil is not null
            ? $"Alerts paused until {AlertsPausedUntil:t}."
            : "Alerts are on.";
        actions.UpdateAlertsPaused(AlertsPaused, AlertsPausedUntil);
    }

    public bool ExpireAlertPause(DateTime now)
    {
        if (AlertsPausedUntil is not { } until || now < until)
        {
            return false;
        }

        AlertsPausedUntil = null;
        AlertStatus = "Alerts are on.";
        actions.UpdateAlertsPaused(false, null);
        return true;
    }

    private void SendTestNotification()
    {
        actions.SendTestNotification(AlertThresholdPpm + 80);
        AlertStatus = "Test notification sent.";
    }

    public MeasurementAlertEffect HandleMeasurementAlert(int co2Ppm, SensorMonitorResult monitoring, DateTime observedAt)
    {
        if (monitoring.NotificationReady && NotificationsEnabled)
        {
            preferences.LastCo2AlertAt = observedAt;
            preferences.LastCo2AlertPpm = co2Ppm;
            savePreferences(preferences);
            AlertStatus = $"Alert sent: {co2Ppm:N0} ppm at {observedAt:t}";
            return NotifyMeasurementAlert(MeasurementAlertEffect.HighCo2, co2Ppm);
        }

        if (monitoring.NotificationDeferred)
        {
            AlertStatus = $"Above your alert level, but notifications are paused until {AlertsPausedUntil:t}.";
            return MeasurementAlertEffect.None;
        }

        if (co2Ppm > AlertThresholdPpm)
        {
            AlertStatus = $"Above your alert level; waiting for {AlertDurationMinutes} minutes of sustained readings.";
            return MeasurementAlertEffect.None;
        }

        if (co2Ppm <= monitoring.ResetThresholdPpm)
        {
            AlertStatus = preferences.LastCo2AlertAt is { } previousAlert
                ? $"Recovered below {monitoring.ResetThresholdPpm:N0} ppm. Last alert {previousAlert:t}."
                : "No active high-CO₂ alert.";
            return monitoring.RecoveryNotificationDue && NotificationsEnabled
                ? NotifyMeasurementAlert(MeasurementAlertEffect.Recovered, co2Ppm)
                : MeasurementAlertEffect.None;
        }

        return MeasurementAlertEffect.None;
    }

    private MeasurementAlertEffect NotifyMeasurementAlert(MeasurementAlertEffect effect, int co2Ppm)
    {
        actions.NotifyMeasurementAlert(effect, co2Ppm);
        return effect;
    }

    public RelayCommand TestNotificationCommand { get; }

    public RelayCommand SnoozeCommand { get; }

    public RelayCommand ForgetDetectedDevicesCommand { get; }

    public RelayCommand OpenBluetoothDiagnosticsCommand { get; }

    public void SetListeningState(bool value)
    {
        if (!SetProperty(ref listenForLiveReadings, value, nameof(ListenForLiveReadings)))
        {
            OnPropertyChanged(nameof(ListenForLiveReadings));
        }
    }

    public void SetShowNumberFromTray(bool value) => SetProperty(ref showNumberInTray, value, nameof(ShowNumberInTray));

    public void SetLargePopupsFromTray(bool value) => SetProperty(ref largePopups, value, nameof(LargePopups));

    public void SetStartWithWindowsFromSystem(bool value) => SetProperty(ref startWithWindows, value, nameof(StartWithWindows));

    private void SetStartWithWindowsState(bool value)
    {
        if (!SetProperty(ref startWithWindows, value, nameof(StartWithWindows)))
        {
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }
}

public enum MeasurementAlertEffect
{
    None,
    HighCo2,
    Recovered,
}
