using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aranet4Monitor.Domain.Measurements;

namespace Aranet4Monitor.Models;

public sealed class Aranet4Device : INotifyPropertyChanged
{
    /// <summary>A device counts as "live" if we heard from it within this many seconds.</summary>
    private const double LiveWindowSeconds = 20;

    private DateTime lastSeen;
    private short rssi;
    private string name = "(name pending)";
    private int packets;
    private string firmware = "—";
    private string integrationState = "Waiting for sensor advertisement";
    private int co2Ppm;
    private string temperature = "—";
    private string pressure = "—";
    private string humidity = "—";
    private double humidityValue;
    private string battery = "—";
    private double batteryValue;
    private string measurementAge = "—";
    private string measurementInterval = "—";
    private string lastAdvertisement = "No advertisement packet captured yet.";
    private string lastScanResponse = "No scan response captured yet.";

    public required string Address { get; init; }

    /// <summary>Decoded CO₂ readings, oldest first. Only touched from the UI thread.</summary>
    public List<Co2Sample> History { get; } = [];

    /// <summary>Seeds the presentation model's history from the application's history state.</summary>
    public void LoadHistory(IEnumerable<Co2Sample> saved)
    {
        ReplaceHistory(saved);
        // Show the newest real CO₂ value until a live beacon arrives (temperature-only history rows have Ppm 0).
        if (History.LastOrDefault(sample => sample.Ppm > 0) is { } latest)
        {
            Co2Ppm = latest.Ppm;
        }
    }

    public void ReplaceHistory(IEnumerable<Co2Sample> samples)
    {
        History.Clear();
        History.AddRange(samples);
    }

    public DateTime LastSeen
    {
        get => lastSeen;
        set
        {
            if (SetField(ref lastSeen, value))
            {
                Tick();
            }
        }
    }

    public short Rssi
    {
        get => rssi;
        set
        {
            if (SetField(ref rssi, value))
            {
                OnPropertyChanged(nameof(SignalBars));
            }
        }
    }

    public string Name
    {
        get => name; set => SetField(ref name, value);
    }
    public int Packets
    {
        get => packets; set => SetField(ref packets, value);
    }
    public string Firmware
    {
        get => firmware; set => SetField(ref firmware, value);
    }
    public string IntegrationState
    {
        get => integrationState; set => SetField(ref integrationState, value);
    }
    public decimal? TemperatureCelsius { get; set; }

    /// <summary>CO₂ concentration in ppm; 0 means "no reading yet".</summary>
    public int Co2Ppm
    {
        get => co2Ppm; set => SetField(ref co2Ppm, value);
    }
    public string Temperature
    {
        get => temperature; set => SetField(ref temperature, value);
    }
    public string Pressure
    {
        get => pressure; set => SetField(ref pressure, value);
    }
    public string Humidity
    {
        get => humidity; set => SetField(ref humidity, value);
    }
    public double HumidityValue
    {
        get => humidityValue; set => SetField(ref humidityValue, value);
    }
    public string Battery
    {
        get => battery; set => SetField(ref battery, value);
    }
    public double BatteryValue
    {
        get => batteryValue; set => SetField(ref batteryValue, value);
    }
    public string MeasurementAge
    {
        get => measurementAge; set => SetField(ref measurementAge, value);
    }
    public string MeasurementInterval
    {
        get => measurementInterval; set => SetField(ref measurementInterval, value);
    }
    public string LastAdvertisement
    {
        get => lastAdvertisement; set => SetField(ref lastAdvertisement, value);
    }
    public string LastScanResponse
    {
        get => lastScanResponse; set => SetField(ref lastScanResponse, value);
    }

    /// <summary>0–4 bars derived from RSSI.</summary>
    public int SignalBars => packets == 0 ? 0 : rssi switch
    {
        >= -60 => 4,
        >= -70 => 3,
        >= -80 => 2,
        >= -90 => 1,
        _ => 0,
    };

    public bool IsLive => lastSeen != default && (DateTime.Now - lastSeen).TotalSeconds <= LiveWindowSeconds;

    public string LastSeenAgo
    {
        get
        {
            if (lastSeen == default)
            {
                return "—";
            }

            var seconds = Math.Max(0, (DateTime.Now - lastSeen).TotalSeconds);
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
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
