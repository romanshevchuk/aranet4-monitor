using Aranet4Monitor.Protocol.Aranet4;

namespace Aranet4Monitor.Abstractions;

public interface ISensorSource : IDisposable
{
    event EventHandler<SensorAdvertisementReceivedEventArgs>? AdvertisementReceived;

    event EventHandler<SensorSourceStoppedEventArgs>? Stopped;

    bool IsActive { get; }

    void RegisterKnownAddress(ulong bluetoothAddress);

    void ClearKnownAddresses();

    void Start();

    void Stop();
}

public sealed class SensorAdvertisementReceivedEventArgs(
    ulong bluetoothAddress,
    string address,
    DateTime timestamp,
    short rssi,
    string? localName,
    bool isScanResponse,
    string packet,
    Aranet4Measurement? measurement,
    string decodeMessage) : EventArgs
{
    public ulong BluetoothAddress { get; } = bluetoothAddress;

    public string Address { get; } = address;

    public DateTime Timestamp { get; } = timestamp;

    public short Rssi { get; } = rssi;

    public string? LocalName { get; } = localName;

    public bool IsScanResponse { get; } = isScanResponse;

    public string Packet { get; } = packet;

    public Aranet4Measurement? Measurement { get; } = measurement;

    public string DecodeMessage { get; } = decodeMessage;
}

public sealed class SensorSourceStoppedEventArgs(bool radioUnavailable, bool successful, string error) : EventArgs
{
    public bool RadioUnavailable { get; } = radioUnavailable;

    public bool Successful { get; } = successful;

    public string Error { get; } = error;
}
