namespace ZCinemaSound.Core;

/// <summary>
/// The global "bypass processing" toggle: swaps the Equalizer APO profile for a
/// neutral one and restores it when toggled back. (Global, not per-device.)
/// </summary>
public static class Bypass
{
    public static string FlagPath => Path.Combine(RemoteMap.Dir(), "bypassed.flag");
    public static string SavedPath => Path.Combine(RemoteMap.Dir(), "last-profile.txt");

    public static bool IsActive => File.Exists(FlagPath);

    public static void Toggle()
    {
        var dir = RemoteMap.Dir();
        Directory.CreateDirectory(dir);
        var profile = EqualizerApo.ProfilePath();

        if (IsActive)
        {
            if (File.Exists(SavedPath)) File.Copy(SavedPath, profile, true);
            File.Delete(FlagPath);
        }
        else
        {
            if (File.Exists(profile)) File.Copy(profile, SavedPath, true);
            EqualizerApo.WriteAscii(profile, "# ZCinema Sound bypassed\r\n");
            File.WriteAllText(FlagPath, "");
        }
    }
}
