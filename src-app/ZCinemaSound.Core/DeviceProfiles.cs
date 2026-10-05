using System.Text.Json;

namespace ZCinemaSound.Core;

/// <summary>
/// Per-device tuning profiles, keyed by audio endpoint GUID and stored at
/// %APPDATA%\ZCinemaSound\devices\profiles.json. Lets each output device keep its
/// own bass/treble/dialogue/width/EQ/ceiling instead of one global profile.
/// </summary>
public sealed class DeviceProfiles
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _dir;

    public DeviceProfiles(string? directory = null)
        => _dir = directory ?? Path.Combine(RemoteMap.Dir(), "devices");

    public string FilePath => Path.Combine(_dir, "profiles.json");

    public Dictionary<string, ZCinemaProfile> All()
    {
        if (!File.Exists(FilePath)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var all = JsonSerializer.Deserialize<Dictionary<string, ZCinemaProfile>>(
                File.ReadAllText(FilePath), Options);
            return all is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ZCinemaProfile>(all, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }

    public ZCinemaProfile? Load(string endpointGuid)
        => string.IsNullOrEmpty(endpointGuid) ? null
           : (All().TryGetValue(endpointGuid, out var p) ? p : null);

    public bool Exists(string endpointGuid)
        => !string.IsNullOrEmpty(endpointGuid) && All().ContainsKey(endpointGuid);

    public void Save(string endpointGuid, ZCinemaProfile profile)
    {
        if (string.IsNullOrEmpty(endpointGuid) || profile is null) return;
        var all = All();
        all[endpointGuid] = profile;
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(all, Options));
    }
}
