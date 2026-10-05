using System.Text.Json;

namespace ZCinemaSound.Core;

/// <summary>
/// User's remote-button → action mapping, stored at
/// %APPDATA%\ZCinemaSound\remote.json as { "Button": "action[:value]" }.
/// </summary>
public static class RemoteMap
{
    public static string Dir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZCinemaSound");

    public static string MapPath() => Path.Combine(Dir(), "remote.json");
    public static string PresetsPath() => Path.Combine(Dir(), "presets.json");

    public static Dictionary<string, string> Default() => new()
    {
        ["Display"] = "gui",
        ["Preset 1"] = "preset:Music",
        ["Preset 2"] = "preset:Movies",
        ["Preset 3"] = "preset:Vocal",
        ["Preset 4"] = "preset:V-Shape",
    };

    public static Dictionary<string, string> Read()
    {
        var path = MapPath();
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = File.ReadAllText(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }

    public static void Save(IReadOnlyDictionary<string, string> map)
    {
        Directory.CreateDirectory(Dir());
        var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(MapPath(), json);
    }
}
