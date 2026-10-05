using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>The icon palette + reusable drawing/theme helpers.</summary>
public static class Theme
{
    // palette (from the icon)
    public static readonly Color Bg = Color.FromArgb(0x01, 0x00, 0x01);         // major
    public static readonly Color PanelTop = Color.FromArgb(0x14, 0x10, 0x0B);
    public static readonly Color PanelBottom = Color.FromArgb(0x07, 0x05, 0x03);
    public static readonly Color Edge = Color.FromArgb(0xE1, 0xE0, 0xE1);
    public static readonly Color Minor = Color.FromArgb(0xFA, 0xFB, 0xCA);
    public static readonly Color Accent = Color.FromArgb(0xD9, 0x87, 0x2C);
    public static readonly Color AccentDark = Color.FromArgb(0xAE, 0x49, 0x06);
    public static readonly Color Unlit = Color.FromArgb(0x3A, 0x2A, 0x1A);
    public static readonly Color Track = Color.FromArgb(0x22, 0x18, 0x0E);
    public static readonly Color Muted = Color.FromArgb(0xB8, 0xB4, 0xAC);

    public static readonly Font TitleFont = new("Segoe UI", 11f, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI", 8f, FontStyle.Bold);
    public static readonly Font LabelFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font ValueFont = new("Consolas", 10f, FontStyle.Regular);
    public static readonly Font ButtonFont = new("Segoe UI", 8.5f, FontStyle.Regular);
    public static readonly Font PillFont = new("Segoe UI", 9f, FontStyle.Bold);

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2f;
        var p = new GraphicsPath();
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Min(1, Math.Max(0, t));
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    public static void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = Accent;
        b.FlatAppearance.MouseOverBackColor = Accent;
        b.FlatAppearance.MouseDownBackColor = AccentDark;
        b.BackColor = AccentDark;
        b.ForeColor = Minor;
        b.Font = ButtonFont;
    }

    public static void StyleGrid(DataGridView g)
    {
        g.EnableHeadersVisualStyles = false;
        g.BackgroundColor = Color.FromArgb(0x07, 0x05, 0x03);
        g.BorderStyle = BorderStyle.None;
        g.GridColor = Color.FromArgb(0x2A, 0x1E, 0x12);
        g.DefaultCellStyle.BackColor = Color.FromArgb(0x0B, 0x09, 0x07);
        g.DefaultCellStyle.ForeColor = Edge;
        g.DefaultCellStyle.SelectionBackColor = AccentDark;
        g.DefaultCellStyle.SelectionForeColor = Minor;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0x14, 0x0E, 0x08);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(0x14, 0x0E, 0x08);
    }
}
