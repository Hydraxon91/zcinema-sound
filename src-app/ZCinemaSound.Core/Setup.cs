namespace ZCinemaSound.Core;

/// <summary>Install / uninstall the Equalizer APO wiring (writes config.txt include).</summary>
public static class Setup
{
    public static string Install()
    {
        var dir = EqualizerApo.ConfigDir();
        if (!Directory.Exists(dir)) return "Equalizer APO config folder not found:\r\n" + dir;

        // create a default profile if one isn't present yet
        var profile = Path.Combine(dir, EqualizerApo.ProfileFileName);
        if (!File.Exists(profile)) EqualizerApo.WriteAscii(profile, new ZCinemaProfile().Generate());

        var cfg = Path.Combine(dir, EqualizerApo.ConfigFileName);
        var backup = cfg + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if (File.Exists(cfg)) File.Copy(cfg, backup, true);

        EqualizerApo.WriteAscii(cfg, "# Managed by ZCinema Sound\r\nInclude: ZCinema.txt\r\n");
        return $"Installed.\r\nProfile: {profile}\r\nconfig.txt now includes ZCinema.txt.\r\nBackup: {backup}";
    }

    public static string Uninstall()
    {
        var dir = EqualizerApo.ConfigDir();
        var cfg = Path.Combine(dir, EqualizerApo.ConfigFileName);
        if (!File.Exists(cfg)) return "config.txt not found; nothing to do.";

        // restore the newest backup that isn't one of ours
        foreach (var b in Directory.GetFiles(dir, "config.txt.bak-*").OrderByDescending(f => f))
        {
            try
            {
                if (!File.ReadAllText(b).Contains("ZCinema Sound"))
                {
                    File.Copy(b, cfg, true);
                    return "Restored config.txt from " + Path.GetFileName(b);
                }
            }
            catch { /* skip unreadable */ }
        }

        // fallback: strip the include line
        var kept = File.ReadAllLines(cfg).Where(l => !System.Text.RegularExpressions.Regex.IsMatch(l, @"^\s*Include:\s*ZCinema\.txt\s*$"));
        EqualizerApo.WriteAscii(cfg, string.Join("\r\n", kept) + "\r\n");
        return "Removed the ZCinema include from config.txt.";
    }
}
