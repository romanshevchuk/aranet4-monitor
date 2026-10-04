namespace Aranet4Monitor.Protocol.Aranet4;

public static class Aranet4AdvertisementFilter
{
    public static readonly Guid CurrentServiceUuid = Guid.Parse("0000fce0-0000-1000-8000-00805f9b34fb");

    public static readonly Guid LegacyServiceUuid = Guid.Parse("f0cd1400-95da-4f4b-9ac8-aa55d312af0c");

    public static bool IsCandidate(
        string? localName,
        IEnumerable<Guid> serviceUuids,
        IEnumerable<(ushort CompanyId, int PayloadLength)> manufacturerData)
    {
        ArgumentNullException.ThrowIfNull(serviceUuids);
        ArgumentNullException.ThrowIfNull(manufacturerData);

        if (!string.IsNullOrWhiteSpace(localName)
            && localName.StartsWith("Aranet", StringComparison.OrdinalIgnoreCase))
        {
            return localName.StartsWith("Aranet4", StringComparison.OrdinalIgnoreCase);
        }

        var aranetManufacturerData = manufacturerData
            .Where(block => block.CompanyId == Aranet4BeaconParser.AranetCompanyId)
            .ToArray();

        if (aranetManufacturerData.Length > 0)
        {
            // Match the upstream Aranet4-Python heuristic for advertisements without a usable name.
            return aranetManufacturerData.Any(block => block.PayloadLength is 7 or 22);
        }

        return serviceUuids.Contains(CurrentServiceUuid) || serviceUuids.Contains(LegacyServiceUuid);
    }
}
