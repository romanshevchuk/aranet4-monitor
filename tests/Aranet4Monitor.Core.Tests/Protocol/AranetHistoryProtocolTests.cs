using System.Text.Json;
using Xunit;

namespace Aranet4Monitor.Core.Tests.Protocol;

public sealed class AranetHistoryProtocolTests
{
    [Theory]
    [InlineData("history-v1-co2.json", false)]
    [InlineData("history-v2-co2.json", true)]
    public void ParsesFixturePackets(string fixtureName, bool version2)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        var fixture = JsonSerializer.Deserialize<ProtocolFixture>(File.ReadAllText(fixturePath))!;
        var packet = Convert.FromHexString(fixture.PacketHex);

        var page = version2
            ? AranetHistoryProtocol.ParseV2(packet, fixture.Parameter)
            : AranetHistoryProtocol.ParseV1(packet, fixture.Parameter);

        Assert.Equal(fixture.Parameter, page.Parameter);
        Assert.Equal(fixture.ExpectedStartIndex, page.StartIndex);
        Assert.Equal(fixture.ExpectedValues, page.Values);
    }

    [Fact]
    public void RejectsHistoryPacketForUnexpectedMetric()
    {
        var packet = Convert.FromHexString("040100032003DC050080");

        Assert.Throws<FormatException>(() => AranetHistoryProtocol.ParseV1(packet, (byte)AranetHistoryParameter.Temperature));
    }

    [Fact]
    public void IncrementalStartIncludesOneOverlapRecord()
    {
        var first = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Local);

        Assert.Equal(1, AranetHistoryProtocol.GetIncrementalStartIndex(null, first, 300, 100));
        Assert.Equal(10, AranetHistoryProtocol.GetIncrementalStartIndex(first.AddMinutes(50), first, 300, 100));
        Assert.Equal(100, AranetHistoryProtocol.GetIncrementalStartIndex(first.AddDays(2), first, 300, 100));
    }

    private sealed class ProtocolFixture
    {
        public byte Parameter { get; set; }
        public string PacketHex { get; set; } = string.Empty;
        public ushort ExpectedStartIndex { get; set; }
        public ushort[] ExpectedValues { get; set; } = [];
    }
}
