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

    /// <summary>
    /// Generate a multi-device config: an <c>If</c>/<c>ElseIf</c> block per configured
    /// device (matched by endpoint GUID via <c>deviceGuid</c>), with an <c>Else</c>
    /// fallback to the active device's profile — so audio can never go silent, even if
    /// the GUID never matches (in which case it behaves like the single-profile config).
    /// </summary>
    public static string GenerateMulti(string fallbackGuid, IReadOnlyDictionary<string, ZCinemaProfile> devices, ZCinemaProfile fallback)
    {
        var others = devices
            .Where(kv => !string.Equals(kv.Key, fallbackGuid, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.Append("# ZCinema Sound - per-device profiles\r\n");
        sb.Append("# Each block applies only to the matching audio device.\r\n\r\n");

        bool first = true;
        foreach (var kv in others)
        {
            sb.Append(first ? "If: " : "ElseIf: ")
              .Append("sizeof(regexSearch(\"").Append(NormalizeGuid(kv.Key))
              .Append("\", tolower(deviceGuid))) > 0\r\n");
            first = false;
            sb.Append(kv.Value.Generate());
            sb.Append("\r\n");
        }

        if (!first) sb.Append("Else:\r\n");
        sb.Append(fallback.Generate());
        if (!first) sb.Append("EndIf:\r\n");
        return sb.ToString();
    }

    public static void SaveMultiProfile(string fallbackGuid, IReadOnlyDictionary<string, ZCinemaProfile> devices, ZCinemaProfile fallback)
        => WriteAscii(ProfilePath(), GenerateMulti(fallbackGuid, devices, fallback));

    /// <summary>Bare, lowercase GUID used as the regex pattern for device matching.</summary>
    private static string NormalizeGuid(string guid) => guid.Trim('{', '}').ToLowerInvariant();
}
