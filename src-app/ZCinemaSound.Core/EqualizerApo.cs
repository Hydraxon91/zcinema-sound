using System.Text;
using Microsoft.Win32;

namespace ZCinemaSound.Core;

/// <summary>
/// Locates Equalizer APO and reads/writes the Z Cinema profile as ASCII (no BOM).
/// </summary>
public static class EqualizerApo
{
    public const string ProfileFileName = "ZCinema.txt";
    public const string ConfigFileName = "config.txt";

    /// <summary>Equalizer APO's config folder (registry ConfigPath, else default).</summary>
    public static string ConfigDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");
            if (key?.GetValue("ConfigPath") is string p && !string.IsNullOrWhiteSpace(p))
                return p;
        }
        catch { /* fall through */ }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO", "config");
    }

    public static string ProfilePath() => Path.Combine(ConfigDir(), ProfileFileName);
    public static string ConfigTxtPath() => Path.Combine(ConfigDir(), ConfigFileName);

    public static bool IsInstalled() => Directory.Exists(ConfigDir());

    public static string? InstallDir()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EqualizerAPO");
            return key?.GetValue("InstallPath") as string;
        }
        catch { return null; }
    }

    public static string? DeviceSelectorPath()
    {
        var dir = InstallDir();
        if (string.IsNullOrEmpty(dir)) return null;
        foreach (var n in new[] { "DeviceSelector.exe", "Configurator.exe" })
        {
            var p = Path.Combine(dir, n);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    /// <summary>True if Equalizer APO is attached to this endpoint (Child APO record).</summary>
    public static bool IsAttached(string endpointGuid)
    {
        if (string.IsNullOrEmpty(endpointGuid)) return false;
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\EqualizerAPO\Child APOs\{endpointGuid}");
            return k is not null;
        }
        catch { return false; }
    }

    /// <summary>Write text as ASCII with no BOM (Equalizer APO requirement).</summary>
    public static void WriteAscii(string path, string text)
        => File.WriteAllText(path, text, new ASCIIEncoding());

    public static ZCinemaProfile LoadProfile()
    {
        var path = ProfilePath();
        if (!File.Exists(path)) return new ZCinemaProfile();
        return ZCinemaProfile.Parse(File.ReadAllText(path));
    }

    public static void SaveProfile(ZCinemaProfile profile)
        => WriteAscii(ProfilePath(), profile.Generate());
}
