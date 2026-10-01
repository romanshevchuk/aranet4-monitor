using Aranet4Monitor.Models;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aranet4Monitor.Storage;

/// <summary>Saves each device's CO₂ history under the platform's local application-data folder.</summary>
public static class HistoryStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AranetHome", "history");

    private static string PathFor(string address)
    {
        var fileKey = string.Concat(address.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-'));
        if (fileKey.Length > 120)
            fileKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address)));
        return Path.Combine(Folder, fileKey + ".json");
    }

    private static string SyncCursorPathFor(string address) => PathFor(address) + ".sync.json";

    public static DateTime? LoadSyncCursor(string address)
    {
        try
        {
            var path = SyncCursorPathFor(address);
            return File.Exists(path) ? JsonSerializer.Deserialize<DateTime?>(File.ReadAllText(path)) : null;
        }
        catch { return null; }
    }

    public static bool SaveSyncCursor(string address, DateTime syncedThrough)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = SyncCursorPathFor(address);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(syncedThrough));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch { return false; }
    }

    public static List<Co2Sample> Load(string address)
    {
        try
        {
            var path = PathFor(address);
            if (!File.Exists(path)) return [];
            return JsonSerializer.Deserialize<List<Co2Sample>>(File.ReadAllText(path)) ?? [];
        }
        catch { return []; } // corrupt or unreadable history must never break the live view
    }

    public static bool Save(string address, IReadOnlyList<Co2Sample> samples)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = PathFor(address);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(samples));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch { return false; }
    }
}
