using Xunit;

namespace Aranet4Monitor.Tests.Alerts;

using System.Globalization;
using Aranet4Monitor.Alerts;

public sealed class AlertMessagesTests
{
    [Theory]
    [InlineData(1300)]   // mild
    [InlineData(1600)]   // stuffy
    [InlineData(2400)]   // very stuffy
    public void EveryAlertFitsInAWindowsBalloonAndShowsTheNumber(int ppm)
    {
        var number = ppm.ToString("N0", CultureInfo.CurrentCulture);

        for (var rotation = 0; rotation < 20; rotation++)
        {
            var message = AlertMessages.Create(ppm, rotation);

            Assert.False(string.IsNullOrWhiteSpace(message.Title));
            Assert.InRange(message.Title.Length, 1, AlertMessages.MaxTitleLength);
            Assert.InRange(message.Body.Length, 1, AlertMessages.MaxBodyLength);
            Assert.Contains(number, message.Body);
            Assert.DoesNotContain("{ppm}", message.Body);
        }
    }

    [Theory]
    [InlineData(1300)]
    [InlineData(1600)]
    [InlineData(2400)]
    public void ConsecutiveAlertsUseDifferentWording(int ppm)
    {
        for (var rotation = 0; rotation < 10; rotation++)
        {
            Assert.NotEqual(AlertMessages.Create(ppm, rotation).Title, AlertMessages.Create(ppm, rotation + 1).Title);
        }
    }

    [Fact]
    public void ToneEscalatesWithTheReading()
    {
        Assert.NotEqual(AlertMessages.Create(1300, 0).Title, AlertMessages.Create(2400, 0).Title);
    }

    [Fact]
    public void TitlesStartWithTextInsteadOfAnEmojiGlyph()
    {
        foreach (var ppm in new[] { 1300, 1600, 2400 })
        {
            for (var rotation = 0; rotation < 10; rotation++)
            {
                var title = AlertMessages.Create(ppm, rotation).Title;
                Assert.True(char.IsLetter(title[0]), title);
            }
        }

        for (var rotation = 0; rotation < 10; rotation++)
        {
            var title = AlertMessages.CreateRecovered(820, rotation).Title;
            Assert.True(char.IsLetter(title[0]), title);
        }
    }

    [Fact]
    public void NegativeRotationDoesNotThrow()
    {
        Assert.NotNull(AlertMessages.Create(1600, -7));
        Assert.NotNull(AlertMessages.CreateRecovered(900, int.MinValue));
    }

    [Fact]
    public void RecoveryMessageMentionsTheNewReadingAndFitsABalloon()
    {
        for (var rotation = 0; rotation < 10; rotation++)
        {
            var message = AlertMessages.CreateRecovered(820, rotation);

            Assert.InRange(message.Title.Length, 1, AlertMessages.MaxTitleLength);
            Assert.Contains("820", message.Body);
        }
    }
}
