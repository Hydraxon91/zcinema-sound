using Microsoft.Win32;

namespace ZCinemaSound.Core;

/// <summary>
/// Z Cinema remote HID protocol (see docs/REMOTE-CODES.md). One press may emit a
/// bitmask report and a "B1" code report; both decode to the same button name so
/// callers can de-duplicate and fire exactly one action.
/// </summary>
public static class RemoteProtocol
{
    public sealed record Collection(string Label, string Path);

    // COL02 vendor report 0x02, byte1 = bit per Media Center button
    private static readonly string[] VendorBits =
        { "Music", "Videos", "Pictures", "Media Player (Windows)", "DVD menu", "Live TV", "Recorded TV" };

    // COL01 report 0x01, bytes[8]==0xB1, bytes[9]==code
    private static readonly Dictionary<byte, string> B1Codes = new()
    {
        [0xC2] = "Forward", [0xC3] = "Rewind", [0xC4] = "Skip", [0xC5] = "Replay",
        [0xC6] = "Play", [0xC7] = "Stop", [0xC8] = "Ch+", [0xC9] = "Ch-",
        [0xCA] = "Mute", [0xCB] = "Pause",
        [0xD0] = "Back", [0xD1] = "Guide", [0xD2] = "Record", [0xD3] = "Info", [0xD6] = "Full screen",
        [0xD8] = "Music", [0xD9] = "Videos", [0xDA] = "Pictures", [0xDB] = "Media Player (Windows)",
        [0xDC] = "DVD menu", [0xDD] = "Live TV", [0xDE] = "Recorded TV",
        [0x11] = "Display",
        [0x2E] = "Preset 1", [0x2F] = "Preset 2", [0x30] = "Preset 3", [0x31] = "Preset 4",
    };

    // COL01 remote-button bitmask (bytes 1..3)
    private static readonly string?[][] Bitmask =
    {
        new string?[] { null, null, "Forward", "Rewind", "Skip", "Replay", "Play", "Stop" },        // byte1
        new string?[] { "Ch+", "Ch-", "Mute", "Pause", null, null, null, null },                    // byte2
        new string?[] { "Back", "Guide", "Record", "Info", null, null, "Full screen", null },        // byte3
    };

    public static readonly string[] FreeButtons =
    {
        "Preset 1", "Preset 2", "Preset 3", "Preset 4", "Display",
        "Guide", "Info", "Record", "Full screen", "Ch+", "Ch-",
        "DVD menu", "Live TV", "Recorded TV", "Music", "Videos", "Pictures",
        "Media Player (Windows)",
    };

    public static readonly HashSet<string> NativeButtons = new(StringComparer.OrdinalIgnoreCase)
        { "Play", "Pause", "Stop", "Skip", "Replay", "Rewind", "Forward", "Mute", "Back" };

    /// <summary>Decode an input report to a comma-joined button name, or null.</summary>
    public static string? Decode(string collectionLabel, byte[] report, int length)
    {
        var names = new List<string>();
        if (collectionLabel.StartsWith("COL02", StringComparison.OrdinalIgnoreCase))
        {
            if (length >= 2 && report[0] == 0x02 && report[1] != 0)
                for (int i = 0; i < VendorBits.Length; i++)
                    if ((report[1] & (1 << i)) != 0) names.Add(VendorBits[i]);
            return names.Count > 0 ? string.Join(",", names) : null;
        }

        if (collectionLabel.StartsWith("COL01", StringComparison.OrdinalIgnoreCase) && length >= 10 && report[0] == 0x01)
        {
            if (report[8] == 0xB1 && report[9] != 0)
            {
                names.Add(B1Codes.TryGetValue(report[9], out var n) ? n : $"media 0x{report[9]:X2}");
                return string.Join(",", names);
            }
            for (int i = 0; i < Bitmask.Length; i++)
            {
                int idx = 1 + i;
                if (idx >= length) break;
                for (int bit = 0; bit < 8; bit++)
                    if ((report[idx] & (1 << bit)) != 0 && Bitmask[i][bit] is { } bn) names.Add(bn);
            }
            return names.Count > 0 ? string.Join(",", names) : null;
        }
        return null;
    }

    /// <summary>Find the Z Cinema HID collections (COL01 Consumer, COL02 Vendor; keyboard skipped).</summary>
    public static IReadOnlyList<Collection> FindCollections()
    {
        var result = new List<Collection>();
        const string baseKey = @"SYSTEM\CurrentControlSet\Control\DeviceClasses\{4d1e55b2-f16f-11cf-88cb-001111000030}";
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(baseKey);
            if (root is null) return result;
            foreach (var name in root.GetSubKeyNames())
            {
                if (name.IndexOf("046D&PID_0A0F", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (name.IndexOf("Col03", StringComparison.OrdinalIgnoreCase) >= 0) continue; // keyboard needs admin
                int idx = name.IndexOf("Col0", StringComparison.OrdinalIgnoreCase);
                string label = idx >= 0 && idx + 4 < name.Length ? "COL0" + name[idx + 4] : "COL?";
                string path = name.StartsWith("##?#") ? @"\\?\" + name[4..] : name;
                result.Add(new Collection(label, path));
            }
        }
        catch { /* ignore */ }
        return result;
    }
}
