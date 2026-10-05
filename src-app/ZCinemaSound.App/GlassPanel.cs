using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>A rounded "glass" section panel with a small accent caption.</summary>
public sealed class GlassPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = "";
    public const int CaptionHeight = 28;

    public GlassPanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(r, 10);
        using (var grad = new LinearGradientBrush(r, Theme.PanelTop, Theme.PanelBottom, 90f))
            g.FillPath(grad, path);
        using (var pen = new Pen(Color.FromArgb(0x66, Theme.Accent), 1f))
            g.DrawPath(pen, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!string.IsNullOrEmpty(Caption))
        {
            using var brush = new SolidBrush(Theme.Accent);
            e.Graphics.DrawString(Caption.ToUpperInvariant(), Theme.SectionFont, brush, 14, 9);
        }
    }
}
