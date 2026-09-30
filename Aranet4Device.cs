using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BleListener;

public sealed class Aranet4Device : INotifyPropertyChanged
{
    private DateTime _lastSeen;
    private short _rssi;
    private string _name = "(name pending)";
    private int _packets;
    private string _firmware = "—";
    private string _integrationState = "Waiting for sensor advertisement";
    private string _co2 = "—";
    private string _temperature = "—";
    private string _pressure = "—";
    private string _humidity = "—";
    private string _battery = "—";
    private string _measurementAge = "—";
    private string _measurementInterval = "—";
    private string _lastAdvertisement = "No advertisement packet captured yet.";
    private string _lastScanResponse = "No scan response captured yet.";

    public required string Address { get; init; }
    public DateTime LastSeen { get => _lastSeen; set => SetField(ref _lastSeen, value); }
    public short Rssi { get => _rssi; set => SetField(ref _rssi, value); }
    public string Name { get => _name; set => SetField(ref _name, value); }
    public int Packets { get => _packets; set => SetField(ref _packets, value); }
    public string Firmware { get => _firmware; set => SetField(ref _firmware, value); }
    public string IntegrationState { get => _integrationState; set => SetField(ref _integrationState, value); }
    public string Co2 { get => _co2; set => SetField(ref _co2, value); }
    public string Temperature { get => _temperature; set => SetField(ref _temperature, value); }
    public string Pressure { get => _pressure; set => SetField(ref _pressure, value); }
    public string Humidity { get => _humidity; set => SetField(ref _humidity, value); }
    public string Battery { get => _battery; set => SetField(ref _battery, value); }
    public string MeasurementAge { get => _measurementAge; set => SetField(ref _measurementAge, value); }
    public string MeasurementInterval { get => _measurementInterval; set => SetField(ref _measurementInterval, value); }
    public string LastAdvertisement { get => _lastAdvertisement; set => SetField(ref _lastAdvertisement, value); }
    public string LastScanResponse { get => _lastScanResponse; set => SetField(ref _lastScanResponse, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
