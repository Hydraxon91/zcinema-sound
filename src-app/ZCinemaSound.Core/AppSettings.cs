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

    /// <summary>Start hidden in the tray instead of showing the window.</summary>
    public bool StartMinimized { get; set; }

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
