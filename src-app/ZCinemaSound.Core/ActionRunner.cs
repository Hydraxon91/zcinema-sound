using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ZCinemaSound.Core;

/// <summary>Host callbacks the app provides for actions that need the UI/process.</summary>
public interface IActionHost
{
    void ShowApp();
    void SendKeys(string text);
}

public sealed record CustomPreset(double Preamp, double Bass, double Treble, double Dialog, double Width, double[] Eq);

/// <summary>Executes a mapped action string ("verb[:value]").</summary>
public static class ActionRunner
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    private static readonly Dictionary<string, byte> MediaKeys = new()
    {
        ["playpause"] = 0xB3, ["next"] = 0xB0, ["prev"] = 0xB1, ["stop"] = 0xB2,
        ["volup"] = 0xAF, ["voldown"] = 0xAE, ["mute"] = 0xAD,
    };

    public static void Run(string action, IActionHost host)
    {
        if (string.IsNullOrWhiteSpace(action) || action == "none") return;
        int i = action.IndexOf(':');
        string verb = i >= 0 ? action[..i] : action;
        string val = i >= 0 ? action[(i + 1)..] : "";

        switch (verb)
        {
            case "preset": { var p = EqualizerApo.LoadProfile(); Presets.Apply(p, val); EqualizerApo.SaveProfile(p); break; }
            case "custom": ApplyCustom(val); break;
            case "gui": host.ShowApp(); break;
            case "bypass": ToggleBypass(); break;
            case "sound": AdjustSound(val); break;
            case "media": SendMedia(val); break;
            case "app":
            case "url": StartProcess(val); break;
            case "script": RunScript(val); break;
            case "keys": if (val.Length > 0) host.SendKeys(val); break;
        }
    }

    private static void ToggleBypass()
    {
        var dir = RemoteMap.Dir();
        Directory.CreateDirectory(dir);
        var flag = Path.Combine(dir, "bypassed.flag");
        var save = Path.Combine(dir, "last-profile.txt");
        var profile = EqualizerApo.ProfilePath();
        if (File.Exists(flag))
        {
            if (File.Exists(save)) File.Copy(save, profile, true);
            File.Delete(flag);
        }
        else
        {
            if (File.Exists(profile)) File.Copy(profile, save, true);
            EqualizerApo.WriteAscii(profile, "# ZCinema Sound bypassed (remote)\r\n");
            File.WriteAllText(flag, "");
        }
    }

    private static void AdjustSound(string val)
    {
        var p = EqualizerApo.LoadProfile();
        switch (val)
        {
            case "dialogue+": p.DialogGain = Math.Min(9, p.DialogGain + 1); break;
            case "dialogue-": p.DialogGain = Math.Max(-9, p.DialogGain - 1); break;
            case "width+": p.Width = Math.Min(0.30, p.Width + 0.02); break;
            case "width-": p.Width = Math.Max(0, p.Width - 0.02); break;
            case "ceiling+": p.PreampDb = Math.Min(0, p.PreampDb + 1); break;
            case "ceiling-": p.PreampDb = Math.Max(-60, p.PreampDb - 1); break;
            default: return;
        }
        EqualizerApo.SaveProfile(p);
    }

    private static void SendMedia(string val)
    {
        if (!MediaKeys.TryGetValue(val, out byte vk)) return;
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
    }

    private static void ApplyCustom(string slot)
    {
        if (!int.TryParse(slot, out _)) return;
        var path = RemoteMap.PresetsPath();
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var all = JsonSerializer.Deserialize<Dictionary<string, CustomPreset>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (all is null || !all.TryGetValue(slot, out var c)) return;
            var p = EqualizerApo.LoadProfile();
            p.PreampDb = c.Preamp; p.BassGain = c.Bass; p.SubGain = Math.Round(c.Bass * 0.6, 1);
            p.TrebleGain = c.Treble; p.DialogGain = c.Dialog; p.Width = c.Width / 100.0;
            p.EqGains = c.Eq;
            EqualizerApo.SaveProfile(p);
        }
        catch { /* ignore malformed presets */ }
    }

    private static void StartProcess(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }

    private static void RunScript(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo("powershell",
                $"-NoProfile -ExecutionPolicy Bypass -File \"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }
}
