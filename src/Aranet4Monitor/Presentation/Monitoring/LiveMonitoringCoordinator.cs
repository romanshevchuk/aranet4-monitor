using System.Windows.Threading;
using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Presentation.ViewModels;
using Aranet4Monitor.Storage;

namespace Aranet4Monitor.Presentation.Monitoring;

/// <summary>Routes sensor-source lifecycle and readings through the presentation view models.</summary>
public sealed class LiveMonitoringCoordinator : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly ISensorSource sensorSource;
    private readonly SensorMonitor sensorMonitor;
    private readonly AppPreferences preferences;
    private readonly LiveViewModel live;
    private readonly HistoryViewModel history;
    private readonly SettingsViewModel settings;
    private readonly Action<string, ListenerStatusKind, string?> setStatus;
    private readonly Action<Aranet4Device> selectedDeviceUpdated;
    private readonly Action<Aranet4Device> newlySelectedDevice;
    private readonly Action<string> setEmptyHint;
    private bool disposed;

    public LiveMonitoringCoordinator(
        Dispatcher dispatcher,
        ISensorSource sensorSource,
        SensorMonitor sensorMonitor,
        AppPreferences preferences,
        LiveViewModel live,
        HistoryViewModel history,
        SettingsViewModel settings,
        Action<string, ListenerStatusKind, string?> setStatus,
        Action<Aranet4Device> selectedDeviceUpdated,
        Action<Aranet4Device> newlySelectedDevice,
        Action<string> setEmptyHint)
    {
        this.dispatcher = dispatcher;
        this.sensorSource = sensorSource;
        this.sensorMonitor = sensorMonitor;
        this.preferences = preferences;
        this.live = live;
        this.history = history;
        this.settings = settings;
        this.setStatus = setStatus;
        this.selectedDeviceUpdated = selectedDeviceUpdated;
        this.newlySelectedDevice = newlySelectedDevice;
        this.setEmptyHint = setEmptyHint;

        sensorSource.AdvertisementReceived += SensorSource_AdvertisementReceived;
        sensorSource.Stopped += SensorSource_Stopped;
    }

    public void StartListening()
    {
        var update = live.StartListening();
        if (update is null)
        {
            return;
        }

        settings.SetListeningState(update.IsListening);
        setStatus(update.StatusText, update.StatusKind, update.Details);
        if (update.IsListening)
        {
            setEmptyHint("Looking for your Aranet4… Make sure Smart Home Integration is enabled in the Aranet Home app.");
        }
    }

    public void StopListening()
    {
        var update = live.StopListening();
        if (update is null)
        {
            return;
        }

        settings.SetListeningState(update.IsListening);
        setStatus(update.StatusText, update.StatusKind, update.Details);
    }

    public bool SetListeningFromSettings(bool enabled)
    {
        if (enabled)
        {
            StartListening();
        }
        else if (sensorSource.IsActive)
        {
            StopListening();
        }
        else
        {
            setStatus("Stopped", ListenerStatusKind.Idle, null);
        }

        return sensorSource.IsActive;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        StopListening();
        sensorSource.AdvertisementReceived -= SensorSource_AdvertisementReceived;
        sensorSource.Stopped -= SensorSource_Stopped;
    }

    private void SensorSource_Stopped(object? sender, SensorSourceStoppedEventArgs args) => dispatcher.InvokeAsync(() =>
    {
        var update = live.DescribeStoppedSource(args);
        settings.SetListeningState(false);
        setStatus(update.StatusText, update.StatusKind, update.Details);
    });

    private void SensorSource_AdvertisementReceived(object? sender, SensorAdvertisementReceivedEventArgs args) =>
        dispatcher.InvokeAsync(() => ProcessAdvertisement(args));

    private void ProcessAdvertisement(SensorAdvertisementReceivedEventArgs args)
    {
        var update = live.ProcessAdvertisement(
            args,
            sensorMonitor,
            preferences,
            settings.AlertThresholdPpm,
            settings.AlertDurationMinutes,
            settings.AlertsPaused);
        var device = update.Device;
        if (update.IsNewDevice && update.BecameSelected)
        {
            newlySelectedDevice(device);
        }

        if (args.Measurement is { } measurement
            && update.MeasurementResult is { SampleAdded: true, Monitoring: { } monitoring })
        {
            settings.HandleMeasurementAlert(measurement.Co2, monitoring, args.Timestamp);
        }

        // Keep a sync progress/result message visible while the user is reading it.
        if (!history.IsSyncing && sensorSource.IsActive)
        {
            setStatus("Listening", ListenerStatusKind.Listening, null);
        }

        if (live.SelectedDevice == device)
        {
            selectedDeviceUpdated(device);
        }
    }
}
