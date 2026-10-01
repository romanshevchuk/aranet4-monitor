using Aranet4Monitor.Bluetooth;
using Xunit;

namespace Aranet4Monitor.Tests.Bluetooth;

public sealed class Aranet4AdvertisementFilterTests
{
    [Fact]
    public void AcceptsAranet4NameWithoutManufacturerData()
    {
        Assert.True(Aranet4AdvertisementFilter.IsCandidate("Aranet4 12345", [], []));
    }

    [Theory]
    [InlineData("0000fce0-0000-1000-8000-00805f9b34fb")]
    [InlineData("f0cd1400-95da-4f4b-9ac8-aa55d312af0c")]
    public void AcceptsCurrentAndLegacyServicesWithoutName(string serviceUuid)
    {
        Assert.True(Aranet4AdvertisementFilter.IsCandidate(null, [Guid.Parse(serviceUuid)], []));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(22)]
    public void RecognizesAranet4ManufacturerPayloadLengthsWithoutName(int payloadLength)
    {
        var manufacturerData = new[] { (Aranet4BeaconParser.AranetCompanyId, payloadLength) };

        Assert.True(Aranet4AdvertisementFilter.IsCandidate(null, [], manufacturerData));
    }

    [Fact]
    public void RejectsOtherNamedAranetModelsEvenWithSharedIdentifiers()
    {
        var manufacturerData = new[] { (Aranet4BeaconParser.AranetCompanyId, 22) };

        Assert.False(Aranet4AdvertisementFilter.IsCandidate(
            "Aranet2 12345",
            [Aranet4AdvertisementFilter.LegacyServiceUuid],
            manufacturerData));
    }

    [Fact]
    public void RejectsUnknownManufacturerPayloadLengths()
    {
        var manufacturerData = new[] { (Aranet4BeaconParser.AranetCompanyId, 24) };

        Assert.False(Aranet4AdvertisementFilter.IsCandidate(null, [], manufacturerData));
    }

    [Fact]
    public void RejectsUnknownManufacturerPayloadLengthsEvenWithSharedService()
    {
        var manufacturerData = new[] { (Aranet4BeaconParser.AranetCompanyId, 24) };

        Assert.False(Aranet4AdvertisementFilter.IsCandidate(
            null,
            [Aranet4AdvertisementFilter.LegacyServiceUuid],
            manufacturerData));
    }
}