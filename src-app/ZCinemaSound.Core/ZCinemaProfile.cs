using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ZCinemaSound.Core;

/// <summary>
/// The editable Equalizer APO profile for the Z Cinema (bass/treble/dialogue/
/// width/ceiling + 8 EQ bands). Port of the PowerShell generator/parser.
/// Write as ASCII, no BOM, CRLF — Equalizer APO is picky about encoding.
/// </summary>
public sealed class ZCinemaProfile
{
    public double PreampDb { get; set; } = -8;
    public double BassGain { get; set; } = 6;    // broad peaking band @ 100 Hz
    public double SubGain { get; set; } = 4;     // peaking band @ 45 Hz
    public double TrebleGain { get; set; } = 4;  // peaking band @ 8000 Hz
    public double DialogGain { get; set; } = 3;  // peaking band @ 3000 Hz
    public double Width { get; set; } = 0.10;    // negative crossfeed 0..0.30

    public static readonly int[] EqFreqs = { 60, 170, 470, 1200, 2400, 4700, 10000, 14000 };
    public double[] EqGains { get; set; } = new double[EqFreqs.Length];

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string N1(double d) => d.ToString("0.#", Inv);
    private static string N2(double d) => d.ToString("0.00", Inv);

    /// <summary>Generate the profile text (CRLF, ASCII-safe).</summary>
    public string Generate()
    {
        var sb = new StringBuilder();
        sb.Append("# ZCinema Sound - profile\r\n");
        sb.Append("\r\n");
        sb.Append("Preamp: ").Append(N1(PreampDb)).Append(" dB\r\n");
        sb.Append("\r\n");
        sb.Append("# Bass / TruBass\r\n");
        sb.Append("Filter: ON PK Fc 100 Hz Gain ").Append(N1(BassGain)).Append(" dB Q 0.70\r\n");
        sb.Append("Filter: ON PK Fc 45 Hz Gain ").Append(N1(SubGain)).Append(" dB Q 1.20\r\n");
        sb.Append("Filter: ON PK Fc 250 Hz Gain -2 dB Q 1.00\r\n");
        sb.Append("\r\n");
        sb.Append("# Treble\r\n");
        sb.Append("Filter: ON PK Fc 8000 Hz Gain ").Append(N1(TrebleGain)).Append(" dB Q 0.70\r\n");
        sb.Append("\r\n");
        sb.Append("# Dialogue clarity\r\n");
        sb.Append("Filter: ON PK Fc 3000 Hz Gain ").Append(N1(DialogGain)).Append(" dB Q 1.50\r\n");
        sb.Append("\r\n");
        if (Math.Abs(Width) > 0.001)
        {
            sb.Append("# Width (negative crossfeed)\r\n");
            sb.Append("Copy: L=L+-").Append(N2(Math.Abs(Width))).Append("*R\r\n");
            sb.Append("Copy: R=R+-").Append(N2(Math.Abs(Width))).Append("*L\r\n");
            sb.Append("\r\n");
        }
        sb.Append("# EQ\r\n");
        for (int i = 0; i < EqFreqs.Length; i++)
        {
            double g = i < EqGains.Length ? EqGains[i] : 0;
            sb.Append("Filter: ON PK Fc ").Append(EqFreqs[i]).Append(" Hz Gain ").Append(N1(g)).Append(" dB Q 1.0\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Parse profile text; missing values fall back to defaults.</summary>
    public static ZCinemaProfile Parse(string text)
    {
        var p = new ZCinemaProfile();
        p.PreampDb = Get(text, @"^\s*Preamp:\s*(-?[\d.]+)", p.PreampDb);
        p.BassGain = Get(text, @"PK\s+Fc 100 Hz Gain\s*(-?[\d.]+)", p.BassGain);
        p.SubGain = Get(text, @"PK\s+Fc 45 Hz Gain\s*(-?[\d.]+)", p.SubGain);
        p.TrebleGain = Get(text, @"PK\s+Fc 8000 Hz Gain\s*(-?[\d.]+)", p.TrebleGain);
        p.DialogGain = Get(text, @"PK\s+Fc 3000 Hz Gain\s*(-?[\d.]+)", p.DialogGain);
        p.Width = Math.Abs(Get(text, @"Copy:\s*L=L\+(-?[\d.]+)\*R", 0.0));
        var eq = new double[EqFreqs.Length];
        for (int i = 0; i < EqFreqs.Length; i++)
            eq[i] = Get(text, $@"PK\s+Fc {EqFreqs[i]} Hz Gain\s*(-?[\d.]+)", 0.0);
        p.EqGains = eq;
        return p;
    }

    private static double Get(string text, string pattern, double fallback)
    {
        var m = Regex.Match(text, pattern, RegexOptions.Multiline);
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, Inv, out double v)) return v;
        return fallback;
    }
}
