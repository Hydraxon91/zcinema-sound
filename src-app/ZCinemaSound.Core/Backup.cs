using System.Text.Json;

namespace ZCinemaSound.Core;

/// <summary>
/// A portable snapshot of everything the app owns: the current profile, custom
/// slots, remote bindings, per-device profiles and user settings. One JSON file,
/// for backup and moving between PCs.
/// </summary>
public sealed class Backup
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public ZCinemaProfile? Profile { get; set; }
    public Dictionary<string, CustomPreset> CustomSlots { get; set; } = new();
    public Dictionary<string, string> Bindings { get; set; } = new();
    public Dictionary<string, ZCinemaProfile> Devices { get; set; } = new();
    public AppSettings? Settings { get; set; }

    public static Backup Capture()
    {
        return new Backup
        {
            Profile = EqualizerApo.LoadProfile(),
            CustomSlots = RemoteMap.ReadCustomSlots(),
            Bindings = RemoteMap.Read(),
            Devices = new DeviceProfiles().All(),
            Settings = AppSettings.Load(),
        };
    }

    public void Apply()
    {
        if (Profile is not null) EqualizerApo.SaveProfile(Profile);
        if (CustomSlots.Count > 0) RemoteMap.SaveCustomSlots(CustomSlots);
        if (Bindings.Count > 0) RemoteMap.Save(Bindings);

        var store = new DeviceProfiles();
        foreach (var kv in Devices) store.Save(kv.Key, kv.Value);

        Settings?.Save();
    }

    public static void ExportTo(string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(Capture(), Options));

    public static Backup ImportFrom(string path)
        => JsonSerializer.Deserialize<Backup>(File.ReadAllText(path), Options)
           ?? throw new InvalidDataException("Not a ZCinema Sound backup.");
}
