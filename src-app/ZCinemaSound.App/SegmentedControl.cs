using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>A row of rounded "pill" segments; exactly one active (or none).</summary>
public sealed class SegmentedControl : Control
{
    private string[] _items = Array.Empty<string>();
    private int _selected;
    private int _hover = -1;

    public SegmentedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        Height = 34;
        BackColor = Theme.Bg;
        Cursor = Cursors.Hand;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(true); }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] Items
    {
        get => _items;
        set { _items = value ?? Array.Empty<string>(); _selected = _items.Length > 0 ? 0 : -1; Invalidate(); }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = (value < -1 || value >= _items.Length) ? -1 : value;
            if (v == _selected) return;
            _selected = v;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

    private Rectangle ItemRect(int i)
    {
        if (_items.Length == 0) return Rectangle.Empty;
        int w = Width / _items.Length;
        return new Rectangle(i * w + 3, 3, w - 6, Height - 6);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        using var bg = new SolidBrush(BackColor);
        g.FillRectangle(bg, ClientRectangle);
        for (int i = 0; i < _items.Length; i++)
        {
            var rect = ItemRect(i);
            bool active = i == _selected;
            bool hover = i == _hover && !active;
            using var path = Theme.RoundedRect(rect, (Height - 6) / 2f);
            Color fill = active ? Theme.AccentDark
                       : hover ? Color.FromArgb(0x33, Theme.Accent)
                       : Color.FromArgb(0x16, 0x11, 0x0B);
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(active ? 200 : 90, Theme.Accent), 1f)) g.DrawPath(pen, path);
            TextRenderer.DrawText(g, _items[i], Theme.PillFont, rect,
                active ? Theme.Minor : Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = _items.Length == 0 ? -1 : Math.Min(_items.Length - 1, e.X * _items.Length / Math.Max(1, Width));
        if (h != _hover) { _hover = h; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (_items.Length > 0) SelectedIndex = Math.Min(_items.Length - 1, e.X * _items.Length / Math.Max(1, Width));
        base.OnMouseDown(e);
    }
}
