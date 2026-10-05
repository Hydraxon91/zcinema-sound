using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>
/// A flat, themed dropdown that replaces the OS ComboBox chrome (which draws a
/// white button). Used for the output-device selector.
/// </summary>
public sealed class ThemedDropDown : Control
{
    private readonly List<string> _items = new();
    private int _selected = -1;

    public ThemedDropDown()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        BackColor = Theme.Plate;
        Cursor = Cursors.Hand;
        Font = Theme.ButtonFont;
        Height = 22;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            int v = (value < -1 || value >= _items.Count) ? -1 : value;
            if (v == _selected) return;
            _selected = v;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectedIndexChanged;

    /// <summary>Replace the items (raising <see cref="SelectedIndexChanged"/> once).</summary>
    public void SetItems(IEnumerable<string> items, int selected)
    {
        _items.Clear();
        _items.AddRange(items);
        _selected = (selected >= 0 && selected < _items.Count) ? selected : (_items.Count > 0 ? 0 : -1);
        Invalidate();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = Theme.RoundedRect(r, 6))
        {
            using (var b = new SolidBrush(Theme.Plate)) g.FillPath(b, path);
            using (var pen = new Pen(Color.FromArgb(0x66, Theme.Accent), 1f)) g.DrawPath(pen, path);
        }

        string text = _selected >= 0 && _selected < _items.Count ? _items[_selected] : "";
        TextRenderer.DrawText(g, text, Font, new Rectangle(8, 0, Width - 28, Height), Theme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, "\u25BC", Font, new Rectangle(Width - 22, 0, 18, Height), Theme.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_items.Count > 0) ShowMenu();
    }

    private void ShowMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Theme.Plate,
            ForeColor = Theme.Accent,
            Font = Font,
            ShowImageMargin = false,
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false },
        };
        for (int i = 0; i < _items.Count; i++)
        {
            int idx = i;
            var item = new ToolStripMenuItem(_items[i]) { ForeColor = Theme.Accent, BackColor = Theme.Plate };
            item.Click += (_, _) => SelectedIndex = idx;
            menu.Items.Add(item);
        }
        menu.Closed += (_, _) => Invalidate();
        menu.Show(this, new Point(0, Height));
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.AccentDark;
        public override Color MenuItemSelectedGradientBegin => Theme.AccentDark;
        public override Color MenuItemSelectedGradientEnd => Theme.AccentDark;
        public override Color MenuItemPressedGradientBegin => Theme.AccentDark;
        public override Color MenuItemPressedGradientEnd => Theme.AccentDark;
        public override Color MenuItemBorder => Theme.Accent;
        public override Color MenuBorder => Theme.Accent;
        public override Color ToolStripDropDownBackground => Theme.Plate;
        public override Color ImageMarginGradientBegin => Theme.Plate;
        public override Color ImageMarginGradientMiddle => Theme.Plate;
        public override Color ImageMarginGradientEnd => Theme.Plate;
    }
}
