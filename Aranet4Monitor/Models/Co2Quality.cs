namespace Aranet4Monitor.Models;

public enum Co2Level { Unknown, Good, Fair, Poor }

/// <summary>Single source of truth for what a CO₂ number means (same bands as the dashboard gauge).</summary>
public static class Co2Quality
{
    public const int FairFromPpm = 1000;
    public const int PoorFromPpm = 1400;

    public static Co2Level Classify(int ppm) => ppm switch
    {
        <= 0 => Co2Level.Unknown,
        < FairFromPpm => Co2Level.Good,
        < PoorFromPpm => Co2Level.Fair,
        _ => Co2Level.Poor,
    };

    public static string Describe(Co2Level level) => level switch
    {
        Co2Level.Good => "Good air",
        Co2Level.Fair => "Getting stuffy",
        Co2Level.Poor => "Poor — ventilate",
        _ => "No reading",
    };
}
