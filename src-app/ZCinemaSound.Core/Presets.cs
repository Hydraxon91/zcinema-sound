namespace ZCinemaSound.Core;

public sealed record PresetDef(
    double Bass,
    double Treble,
    double Dialog,
    double Width,
    double[] Eq);

/// <summary>Built-in sound presets (mirrors the PowerShell GUI presets).</summary>
public static class Presets
{
    public static readonly Dictionary<string, PresetDef> All = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Flat"] = new(0, 0, 0, 0, new double[] { 0, 0, 0, 0, 0, 0, 0, 0 }),
        ["Music"] = new(6, 4, 0, 10, new double[] { 3, 2, 1, 0, 1, 2, 3, 3 }),
        ["Movies"] = new(4, 2, 5, 20, new double[] { 2, 1, 0, 1, 2, 3, 2, 1 }),
        ["Night"] = new(-2, 2, 3, 10, new double[] { -3, -2, 0, 0, 1, 0, 0, 0 }),
        ["Vocal"] = new(-2, 2, 7, 5, new double[] { -2, -1, 1, 2, 3, 2, 1, 0 }),
        ["V-Shape"] = new(8, 6, -2, 15, new double[] { 5, 3, 0, -2, -1, 2, 4, 5 }),
    };

    /// <summary>Apply a preset to a profile, keeping the ceiling (PreampDb).</summary>
    public static ZCinemaProfile Apply(ZCinemaProfile profile, string name)
    {
        if (!All.TryGetValue(name, out var p)) return profile;
        profile.BassGain = p.Bass;
        profile.SubGain = Math.Round(p.Bass * 0.6, 1);
        profile.TrebleGain = p.Treble;
        profile.DialogGain = p.Dialog;
        profile.Width = p.Width / 100.0;
        profile.EqGains = (double[])p.Eq.Clone();
        return profile;
    }
}
