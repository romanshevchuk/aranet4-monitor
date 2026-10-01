using System.Globalization;

namespace BleListener.Alerts;

public sealed record AlertMessage(string Title, string Body);

/// <summary>
/// Short, friendly notification texts. The tone gets more urgent as CO₂ climbs, and a rotation
/// counter picks the next line so the same joke isn't repeated back to back.
/// </summary>
public static class AlertMessages
{
    /// <summary>Windows truncates balloon titles after 63 characters and bodies after 255.</summary>
    public const int MaxTitleLength = 63;
    public const int MaxBodyLength = 255;

    private const int StuffyFromPpm = 1_500;
    private const int VeryStuffyFromPpm = 2_000;

    private static readonly (string Title, string Body)[] Mild =
    [
        ("🌿 Psst… the air's getting cozy", "CO₂ is at {ppm} ppm. Crack a window for a minute or two?"),
        ("🪟 Window o'clock?", "{ppm} ppm and creeping up. A little fresh air goes a long way."),
        ("🫧 Quick air check", "{ppm} ppm of CO₂. Your brain would love a breeze."),
    ];

    private static readonly (string Title, string Body)[] Stuffy =
    [
        ("🥱 Feeling sleepy? Blame the air", "{ppm} ppm of CO₂. Fresh air is basically free coffee."),
        ("🧠 Your brain is calling", "It wants more oxygen. CO₂ is {ppm} ppm. Window?"),
        ("🌬️ Fresh air, please!", "{ppm} ppm in here. Open a window and feel the difference."),
        ("🐟 Fish-tank vibes in here", "CO₂ hit {ppm} ppm. Time to let some air in!"),
    ];

    private static readonly (string Title, string Body)[] VeryStuffy =
    [
        ("😵 Okay, this air is a lot", "{ppm} ppm! Open a window wide. We'll wait."),
        ("🚨 CO₂ party, nobody invited it", "{ppm} ppm. Air the room out and take a breather."),
        ("🥵 Seriously stuffy in here", "{ppm} ppm of CO₂. Fresh air, stat!"),
    ];

    private static readonly (string Title, string Body)[] Recovered =
    [
        ("🌱 Fresh air achieved!", "CO₂ is back down to {ppm} ppm. Nicely done."),
        ("✨ Aaah, much better", "{ppm} ppm. Your brain says thanks."),
        ("🎉 Window magic worked", "CO₂ dropped to {ppm} ppm. Breathe easy!"),
    ];

    /// <summary>The "please ventilate" notification for the given level.</summary>
    public static AlertMessage Create(int ppm, int rotation)
    {
        var pool = ppm >= VeryStuffyFromPpm ? VeryStuffy : ppm >= StuffyFromPpm ? Stuffy : Mild;
        return Build(pool, ppm, rotation);
    }

    /// <summary>The "nice, the air is fine again" notification sent after an alert clears.</summary>
    public static AlertMessage CreateRecovered(int ppm, int rotation) => Build(Recovered, ppm, rotation);

    private static AlertMessage Build((string Title, string Body)[] pool, int ppm, int rotation)
    {
        var index = ((rotation % pool.Length) + pool.Length) % pool.Length; // safe for negative rotations
        var number = ppm.ToString("N0", CultureInfo.CurrentCulture);
        return new AlertMessage(pool[index].Title, pool[index].Body.Replace("{ppm}", number));
    }
}
