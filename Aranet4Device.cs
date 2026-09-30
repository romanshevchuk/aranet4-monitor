using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BleListener;

public sealed class Aranet4Device : INotifyPropertyChanged
{
    /// <summary>A device counts as "live" if we heard from it within this many seconds.</summary>
    private const double LiveWindowSeconds = 20;

    private const int MaxHistorySamples = 10_000;
    private static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(7);

    private DateTime _lastSeen;
    private short _rssi;
    private string _name = "(name pending)";
    private int _packets;
    private string _firmware = "—";
    private string _integrationState = "Waiting for sensor advertisement";
    private int _co2Ppm;
    private string _temperature = "—";
    private string _pressure = "—";
    private string _humidity = "—";
    private double _humidityValue;
    private string _battery = "—";
    private double _batteryValue;
    private string _measurementAge = "—";
    private string _measurementInterval = "—";
    private string _lastAdvertisement = "No advertisement packet captured yet.";
    private string _lastScanResponse = "No scan response captured yet.";

    public required string Address { get; init; }

    /// <summary>Decoded CO₂ readings, oldest first. Only touched from the UI thread.</summary>
    public List<Co2Sample> History { get; } = [];

    /// <summary>Seeds the history from disk, dropping anything older than the retention window.</summary>
    public void LoadHistory(IEnumerable<Co2Sample> saved)
    {
        var cutoff = DateTime.Now - HistoryRetention;
        History.Clear();
        History.AddRange(saved.Where(sample => sample.Time >= cutoff).OrderBy(sample => sample.Time));
    }

    /// <summary>
    /// Adds a reading unless it is the same measurement we already recorded (the sensor repeats each
    /// reading in many advertisement packets). Returns true when a new point was stored.
    /// </summary>
    public bool TryAddSample(DateTime time, int ppm, TimeSpan minGap)
    {
        if (History.Count > 0 && time - History[^1].Time < minGap) return false;

        History.Add(new Co2Sample(time, ppm));

        var cutoff = time - HistoryRetention;
        var stale = History.FindIndex(sample => sample.Time >= cutoff);
        if (stale > 0) History.RemoveRange(0, stale);
        if (History.Count > MaxHistorySamples) History.RemoveRange(0, History.Count - MaxHistorySamples);
        return true;
    }

    public DateTime LastSeen
    {
        get => _lastSeen;
        set { if (SetField(ref _lastSeen, value)) Tick(); }
    }

    public short Rssi
    {
        get => _rssi;
        set { if (SetField(ref _rssi, value)) OnPropertyChanged(nameof(SignalBars)); }
    }

    public string Name { get => _name; set => SetField(ref _name, value); }
    public int Packets { get => _packets; set => SetField(ref _packets, value); }
    public string Firmware { get => _firmware; set => SetField(ref _firmware, value); }
    public string IntegrationState { get => _integrationState; set => SetField(ref _integrationState, value); }

    /// <summary>CO₂ concentration in ppm; 0 means "no reading yet".</summary>
    public int Co2Ppm { get => _co2Ppm; set => SetField(ref _co2Ppm, value); }
    public string Temperature { get => _temperature; set => SetField(ref _temperature, value); }
    public string Pressure { get => _pressure; set => SetField(ref _pressure, value); }
    public string Humidity { get => _humidity; set => SetField(ref _humidity, value); }
    public double HumidityValue { get => _humidityValue; set => SetField(ref _humidityValue, value); }
    public string Battery { get => _battery; set => SetField(ref _battery, value); }
    public double BatteryValue { get => _batteryValue; set => SetField(ref _batteryValue, value); }
    public string MeasurementAge { get => _measurementAge; set => SetField(ref _measurementAge, value); }
    public string MeasurementInterval { get => _measurementInterval; set => SetField(ref _measurementInterval, value); }
    public string LastAdvertisement { get => _lastAdvertisement; set => SetField(ref _lastAdvertisement, value); }
    public string LastScanResponse { get => _lastScanResponse; set => SetField(ref _lastScanResponse, value); }

    /// <summary>0–4 bars derived from RSSI.</summary>
    public int SignalBars => _packets == 0 ? 0 : _rssi switch
    {
        >= -60 => 4,
        >= -70 => 3,
        >= -80 => 2,
        >= -90 => 1,
        _ => 0,
    };

    public bool IsLive => _lastSeen != default && (DateTime.Now - _lastSeen).TotalSeconds <= LiveWindowSeconds;

    public string LastSeenAgo
    {
        get
        {
            if (_lastSeen == default) return "—";
            var seconds = Math.Max(0, (DateTime.Now - _lastSeen).TotalSeconds);
            return seconds switch
            {
                < 2 => "just now",
                < 60 => $"{(int)seconds}s ago",
                < 3600 => $"{(int)(seconds / 60)}m ago",
                _ => "over 1h ago",
            };
        }
    }

    /// <summary>Re-evaluates time-dependent properties; called once a second by the window.</summary>
    public void Tick()
    {
        OnPropertyChanged(nameof(LastSeenAgo));
        OnPropertyChanged(nameof(IsLive));
        OnPropertyChanged(nameof(SignalBars));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
