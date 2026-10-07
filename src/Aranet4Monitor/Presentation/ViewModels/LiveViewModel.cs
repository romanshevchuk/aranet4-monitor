using System.Collections.ObjectModel;
using System.Globalization;
using Aranet4Monitor.Abstractions;
using Aranet4Monitor.Application.Monitoring;
using Aranet4Monitor.Storage;
namespace Aranet4Monitor.Presentation.ViewModels;

public sealed class LiveViewModel : ObservableObject
{
    private Aranet4Device? selectedDevice;
    private MetricKind selectedMetric = MetricKind.Co2;
    private TimeSpan? historyRange = TimeSpan.FromHours(1);
    private readonly Dictionary<ulong, Aranet4Device> devicesByBluetoothAddress = new();
    private readonly ISensorSource? sensorSource;
    private readonly SensorMonitor? sensorMonitor;

    public LiveViewModel(ISensorSource? sensorSource = null, SensorMonitor? sensorMonitor = null)
    {
        this.sensorSource = sensorSource;
        this.sensorMonitor = sensorMonitor;
        SelectMetricCommand = new RelayCommand(SelectMetric);
        SelectHistoryRangeCommand = new RelayCommand(SelectHistoryRange);
    }

    public ObservableCollection<Aranet4Device> Devices { get; } = [];

    public RelayCommand SelectMetricCommand { get; }

    public RelayCommand SelectHistoryRangeCommand { get; }

    public Aranet4Device? SelectedDevice
    {
        get => selectedDevice;
        set => SetProperty(ref selectedDevice, value);
    }

    public MetricKind SelectedMetric
    {
        get => selectedMetric;
        set => SetProperty(ref selectedMetric, value);
    }

    public TimeSpan? HistoryRange
    {
        get => historyRange;
        set => SetProperty(ref historyRange, value);
    }

    public DateTime GetLastReadingTime(Aranet4Device device) =>
        device.LastSeen != default
            ? device.LastSeen
            : device.History.LastOrDefault(sample => sample.Ppm > 0)?.Time ?? default;

    public bool IsStale(Aranet4Device device, DateTime now) =>
        (sensorMonitor ?? throw new InvalidOperationException("A sensor monitor is required to check reading freshness."))
            .IsStale(device.Address, GetLastReadingTime(device), now);

    public TrayReading GetTrayReading(DateTime now)
    {
        if (SelectedDevice is not { } device)
        {
            return new TrayReading(0, false);
        }

        return new TrayReading(device.Co2Ppm, IsStale(device, now));
    }

    public ListenerStateUpdate? StartListening()
    {
        var source = GetSensorSource();
        if (source.IsActive)
        {
            return null;
        }

        try
        {
            source.Start();
            return new ListenerStateUpdate(true, ListenerStatusKind.Listening, "Listening for Aranet4 beacon packets…");
        }
        catch (Exception exception)
        {
            source.Stop();
            var bluetoothUnavailable = exception is UnauthorizedAccessException
                || exception.Message.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("radio", StringComparison.OrdinalIgnoreCase);
            return bluetoothUnavailable
                ? new ListenerStateUpdate(false, ListenerStatusKind.BluetoothUnavailable, "Bluetooth unavailable", exception.ToString())
                : new ListenerStateUpdate(false, ListenerStatusKind.Error, $"Could not start: {exception.Message}", exception.ToString());
        }
    }

    public ListenerStateUpdate? StopListening()
    {
        var source = GetSensorSource();
        if (!source.IsActive)
        {
            return null;
        }

        source.Stop();
        return new ListenerStateUpdate(false, ListenerStatusKind.Idle, "Stopped");
    }

    public ListenerStateUpdate DescribeStoppedSource(SensorSourceStoppedEventArgs args)
    {
        if (args.RadioUnavailable)
        {
            return new ListenerStateUpdate(false, ListenerStatusKind.BluetoothUnavailable, "Bluetooth radio unavailable", args.Error);
        }

        return args.Successful
            ? new ListenerStateUpdate(false, ListenerStatusKind.Idle, "Listener stopped")
            : new ListenerStateUpdate(false, ListenerStatusKind.Error, $"Listener stopped: {args.Error}", args.Error);
    }

    private ISensorSource GetSensorSource() => sensorSource
        ?? throw new InvalidOperationException("A sensor source is required for listener lifecycle operations.");

    private void SelectMetric(object? parameter)
    {
        if (parameter is string text && Enum.TryParse<MetricKind>(text, ignoreCase: true, out var metric))
        {
            SelectedMetric = metric;
        }
    }

    private void SelectHistoryRange(object? parameter)
    {
        if (parameter is string text
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var hours))
        {
            HistoryRange = hours == 0 ? null : TimeSpan.FromHours(hours);
        }
    }

    public LiveDeviceUpdate ProcessAdvertisement(
        SensorAdvertisementReceivedEventArgs args,
        SensorMonitor sensorMonitor,
        AppPreferences preferences,
        int alertThresholdPpm,
        int alertDurationMinutes,
        bool alertsPaused)
    {
        var isNewDevice = !devicesByBluetoothAddress.TryGetValue(args.BluetoothAddress, out var knownDevice);
        Aranet4Device device;
        if (isNewDevice)
        {
            device = new Aranet4Device { Address = args.Address };
            device.LoadHistory(sensorMonitor.LoadHistory(device.Address));
            devicesByBluetoothAddress.Add(args.BluetoothAddress, device);
            Devices.Add(device);
            sensorSource?.RegisterKnownAddress(args.BluetoothAddress);
        }
        else
        {
            device = knownDevice!;
        }

        var becameSelected = SelectedDevice is null;
        if (becameSelected)
        {
            SelectedDevice = device;
        }

        device.LastSeen = args.Timestamp;
        device.Rssi = args.Rssi;
        device.Packets++;
        if (!string.IsNullOrWhiteSpace(args.LocalName))
        {
            device.Name = args.LocalName;
        }

        if (args.IsScanResponse)
        {
            device.LastScanResponse = args.Packet;
        }
        else
        {
            device.LastAdvertisement = args.Packet;
        }

        SensorMeasurementResult? measurementResult = null;
        if (args.Measurement is { } measurement)
        {
            device.Firmware = measurement.Firmware;
            device.Co2Ppm = measurement.Co2;
            device.TemperatureCelsius = measurement.TemperatureCelsius;
            device.Temperature = Metrics.FormatWithUnit((double)measurement.TemperatureCelsius, MetricKind.Temperature, preferences.TemperatureDisplayUnit);
            device.Pressure = $"{measurement.PressureHpa:0.0} hPa";
            device.Humidity = $"{measurement.HumidityPercent}%";
            device.HumidityValue = measurement.HumidityPercent;
            device.Battery = measurement.BatteryPercent is null ? "—" : $"{measurement.BatteryPercent}%";
            device.BatteryValue = measurement.BatteryPercent ?? 0;
            device.MeasurementInterval = measurement.IntervalSeconds is null ? "—" : $"{measurement.IntervalSeconds} s";
            device.MeasurementAge = FormatMeasurementAge(measurement.AgeSeconds);
            device.IntegrationState = "Live Smart Home beacon decoded";

            measurementResult = sensorMonitor.ProcessMeasurement(
                device.Address,
                measurement,
                args.Timestamp,
                alertThresholdPpm,
                TimeSpan.FromMinutes(alertDurationMinutes),
                alertsPaused);
            if (measurementResult.SampleAdded)
            {
                device.ReplaceHistory(measurementResult.History);
            }
        }
        else if (args.DecodeMessage != "Waiting for an Aranet manufacturer beacon.")
        {
            device.IntegrationState = args.DecodeMessage;
        }

        return new LiveDeviceUpdate(device, isNewDevice, becameSelected, measurementResult);
    }

    public bool TryGetBluetoothAddress(Aranet4Device device, out ulong bluetoothAddress)
    {
        foreach (var (address, knownDevice) in devicesByBluetoothAddress)
        {
            if (ReferenceEquals(knownDevice, device))
            {
                bluetoothAddress = address;
                return true;
            }
        }

        bluetoothAddress = 0;
        return false;
    }

    public void ClearDetectedDevices()
    {
        devicesByBluetoothAddress.Clear();
        Devices.Clear();
        SelectedDevice = null;
    }

    public void ForgetDetectedDevices()
    {
        GetSensorSource().ClearKnownAddresses();
        (sensorMonitor ?? throw new InvalidOperationException("A sensor monitor is required to forget detected devices."))
            .ClearSensorTracking();
        ClearDetectedDevices();
    }

    public void SetTemperatureUnit(TemperatureUnit temperatureUnit)
    {
        foreach (var device in Devices)
        {
            if (device.TemperatureCelsius is { } celsius)
            {
                device.Temperature = Metrics.FormatWithUnit((double)celsius, MetricKind.Temperature, temperatureUnit);
            }
        }
    }

    private static string FormatMeasurementAge(ushort? ageSeconds)
    {
        if (ageSeconds is not { } seconds)
        {
            return "—";
        }

        if (seconds < 5)
        {
            return "just now";
        }

        if (seconds < 60)
        {
            return $"{seconds}s ago";
        }

        if (seconds < 3_600)
        {
            return $"{seconds / 60}m {seconds % 60}s ago";
        }

        return $"{seconds / 3_600}h {seconds % 3_600 / 60}m ago";
    }
}

public sealed record LiveDeviceUpdate(
    Aranet4Device Device,
    bool IsNewDevice,
    bool BecameSelected,
    SensorMeasurementResult? MeasurementResult);

public sealed record ListenerStateUpdate(
    bool IsListening,
    ListenerStatusKind StatusKind,
    string StatusText,
    string? Details = null);

public sealed record TrayReading(int Co2Ppm, bool IsStale);
