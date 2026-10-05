using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>
/// Segmented "LED" slider: a faint track with a row/column of dots; lit dots fade
/// #AE4906 -> #D9872C with a soft glow and a cream tip, unlit are dark brown.
/// </summary>
public sealed class LedSlider : Control
{
    private int _min = -12, _max = 12, _value, _segments = 13;
    private bool _dragging;
    private int _hover = -1;

    public LedSlider()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        TabStop = false;
        Cursor = Cursors.Hand;
        Width = 430;
        Height = 28;
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

    private const int R = 5;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int n = _segments;
        int idx = IndexFromValue();
        float s = Theme.Scale(this);
        float r = R * s;
        float margin = Orientation == Orientation.Horizontal ? r + 13 * s : r + 11 * s;

        // rounded "capsule" plate behind the bar (the dark bar, now with rounded ends)
        RectangleF plate = Orientation == Orientation.Horizontal
            ? new RectangleF(0f, 2f * s, Width, Height - 4f * s)
            : new RectangleF(2f * s, 1f * s, Width - 4f * s, Height - 2f * s);
        // rounded plate behind the bar. Horizontal = capsule ends; vertical = modest corners.
        float radius = Orientation == Orientation.Horizontal ? (Height - 4f * s) / 2f : 9f * s;
        using (var platePath = Theme.RoundedRect(plate, radius))
        {
            using (var fill = new SolidBrush(Theme.Plate)) g.FillPath(fill, platePath);
            using (var edge = new Pen(Color.FromArgb(0x80, Theme.Unlit), 1f)) g.DrawPath(edge, platePath);
        }

        // track line with rounded (capsule) ends
        using (var pen = new Pen(Theme.Track, 2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (Orientation == Orientation.Horizontal)
                g.DrawLine(pen, margin, Height / 2f, Width - margin, Height / 2f);
            else
                g.DrawLine(pen, Width / 2f, margin, Width / 2f, Height - margin);
        }

        for (int i = 0; i < n; i++)
        {
            PointF c = Center(i, n, margin);
            bool lit = i <= idx;
            if (lit)
            {
                using var glow = new SolidBrush(Color.FromArgb(55, Theme.Accent));
                g.FillEllipse(glow, c.X - r - 3 * s, c.Y - r - 3 * s, 2 * (r + 3 * s), 2 * (r + 3 * s));
            }
            Color fill = lit ? (i == idx ? Theme.Minor : Theme.Lerp(Theme.AccentDark, Theme.Accent, (double)i / (n - 1))) : Theme.Unlit;
            using (var b = new SolidBrush(fill)) g.FillEllipse(b, c.X - r, c.Y - r, 2 * r, 2 * r);
            using (var pen = new Pen(lit ? Theme.AccentDark : Color.FromArgb(0x2A, 0x1E, 0x12), 1f))
                g.DrawEllipse(pen, c.X - r, c.Y - r, 2 * r, 2 * r);
            if (i == _hover)
            {
                using var hp = new Pen(Color.FromArgb(150, Theme.Minor), 1.5f);
                g.DrawEllipse(hp, c.X - r - 2 * s, c.Y - r - 2 * s, 2 * (r + 2 * s), 2 * (r + 2 * s));
            }
        }
    }

    private PointF Center(int i, int n, float margin)
    {
        if (Orientation == Orientation.Horizontal)
        {
            float span = Width - 2f * margin;
            float x = n == 1 ? Width / 2f : margin + span * i / (n - 1);
            return new PointF(x, Height / 2f);
        }
        float spanV = Height - 2f * margin;
        float y = n == 1 ? Height / 2f : margin + spanV * (n - 1 - i) / (n - 1); // top = highest
        return new PointF(Width / 2f, y);
    }

    private int IndexFromPoint(Point pt)
    {
        int n = _segments;
        float s = Theme.Scale(this);
        float margin = R * s + 3 * s;
        double frac = Orientation == Orientation.Horizontal
            ? (double)(pt.X - margin) / Math.Max(1, Width - 2 * margin)
            : (double)((Height - margin) - pt.Y) / Math.Max(1, Height - 2 * margin);
        frac = Math.Min(1, Math.Max(0, frac));
        return (int)Math.Round(frac * (n - 1));
    }

    private void SetFromPoint(Point pt)
    {
        int i = IndexFromPoint(pt);
        Value = _min + (int)Math.Round((double)(_max - _min) * i / (_segments - 1));
    }

    protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; SetFromPoint(e.Location); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = IndexFromPoint(e.Location);
        if (h != _hover) { _hover = h; Invalidate(); }
        if (_dragging) SetFromPoint(e.Location);
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; base.OnMouseUp(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }
}
