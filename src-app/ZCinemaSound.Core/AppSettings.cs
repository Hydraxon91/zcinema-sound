using System.Text.Json;

namespace ZCinemaSound.Core;

/// <summary>Small user preferences, stored at %APPDATA%\ZCinemaSound\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>GUID of the endpoint the user last selected ("" = auto).</summary>
    public string ActiveDeviceGuid { get; set; } = "";

    /// <summary>Show the control panel on launch (default: start hidden in the tray).</summary>
    public bool ShowOnStart { get; set; }

    /// <summary>Emit per-device <c>If</c> blocks (so each device keeps its own tuning).</summary>
    public bool ScopePerDevice { get; set; } = true;

    /// <summary>Whether the "Equalizer APO isn't installed" prompt has been shown.</summary>
    public bool WarnedNoApo { get; set; }

    /// <summary>Friendly names for the custom slots (key "1".."3"); empty = unnamed.</summary>
    public Dictionary<string, string> CustomSlotNames { get; set; } = new();

    /// <summary>Ctrl+Alt+1..6 map to the built-in presets (and Ctrl+Alt+0 opens the panel).</summary>
    public bool HotkeysEnabled { get; set; }

    /// <summary>Check GitHub Releases for a newer version on launch.</summary>
    public bool CheckUpdates { get; set; } = true;

    private static string PathFor(string? directory)
        => System.IO.Path.Combine(directory ?? RemoteMap.Dir(), "settings.json");

    public static AppSettings Load(string? directory = null)
    {
        try
        {
            var path = PathFor(directory);
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings();
        }
        catch { /* ignore malformed settings */ }
        return new AppSettings();
    }

    public void Save(string? directory = null)
    {
        try
        {
            var path = PathFor(directory);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch { /* ignore */ }
    }
}
