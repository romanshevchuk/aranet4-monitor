using System;
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
    Action SendTestNotification,
    Action ToggleSnooze,
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
    private string snoozeButtonText = "Pause 1 h";

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

        TestNotificationCommand = new RelayCommand(_ => actions.SendTestNotification());
        SnoozeCommand = new RelayCommand(_ => actions.ToggleSnooze());
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
        get => snoozeButtonText;
        set => SetProperty(ref snoozeButtonText, value);
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
