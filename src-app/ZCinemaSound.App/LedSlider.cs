using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>
/// Segmented "LED" slider in the icon palette: a row/column of dots, the lit
/// ones in amber (#D9872C) with a cream (#FAFBCA) tip; unlit in dark brown.
/// </summary>
public sealed class LedSlider : Control
{
    private int _min = -12, _max = 12, _value, _segments = 13;
    private bool _dragging;

    public LedSlider()
    {
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        TabStop = false;
        Cursor = Cursors.Hand;
        Width = 420;
        Height = 30;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get => _min; set { _min = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get => _max; set { _max = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Segments { get => _segments; set { _segments = Math.Max(2, value); Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            int v = Math.Min(_max, Math.Max(_min, value));
            if (v == _value) return;
            _value = v;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ValueChanged;

    private int IndexFromValue()
    {
        if (_max == _min) return 0;
        double frac = (double)(_value - _min) / (_max - _min);
        return (int)Math.Round(frac * (_segments - 1));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        int n = _segments;
        int idx = IndexFromValue();

        if (Orientation == Orientation.Horizontal)
        {
            int r = Math.Max(3, (Height - 8) / 2);
            for (int i = 0; i < n; i++)
            {
                float cx = n == 1 ? Width / 2f : (Width - 2f * r) * i / (n - 1) + r;
                float cy = Height / 2f;
                DrawDot(g, cx, cy, r, i, idx);
            }
        }
        else
        {
            int r = Math.Max(3, (int)((double)Height / n / 2) - 1);
            r = Math.Min(r, Math.Max(3, Width / 2 - 4));
            for (int i = 0; i < n; i++)
            {
                float cy = (Height - 2f * r) * (n - 1 - i) / (n - 1) + r; // highest value on top
                float cx = Width / 2f;
                DrawDot(g, cx, cy, r, i, idx);
            }
        }
    }

    private static void DrawDot(Graphics g, float cx, float cy, int r, int i, int idx)
    {
        bool lit = i <= idx;
        Color fill = lit ? (i == idx ? Theme.Minor : Theme.Accent) : Theme.Unlit;
        using var b = new SolidBrush(fill);
        g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        if (lit)
        {
            using var p = new Pen(Theme.AccentDark, 1f);
            g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
        }
    }

    private void SetFromPoint(Point pt)
    {
        if (_segments < 2) return;
        double frac = Orientation == Orientation.Horizontal
            ? (double)pt.X / Math.Max(1, Width)
            : (double)(Height - pt.Y) / Math.Max(1, Height);
        frac = Math.Min(1, Math.Max(0, frac));
        int i = (int)Math.Round(frac * (_segments - 1));
        Value = _min + (int)Math.Round((double)(_max - _min) * i / (_segments - 1));
    }

    protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; SetFromPoint(e.Location); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_dragging) SetFromPoint(e.Location); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; base.OnMouseUp(e); }
}
