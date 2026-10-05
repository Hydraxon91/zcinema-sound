namespace ZCinemaSound.Core;

/// <summary>
/// Single place that turns a device profile change into an Equalizer APO config
/// write, honouring the user's per-device scoping preference. Used by both the GUI
/// and the remote-action path so they can never disagree about the file format.
/// </summary>
public static class ProfileStore
{
    public static void Save(string guid, ZCinemaProfile profile)
    {
        var store = new DeviceProfiles();
        if (!string.IsNullOrEmpty(guid)) store.Save(guid, profile);

        var settings = AppSettings.Load();
        if (settings.ScopePerDevice && !string.IsNullOrEmpty(guid))
            EqualizerApo.SaveMultiProfile(guid, store.All(), profile);
        else
            EqualizerApo.SaveProfile(profile);
    }
}
