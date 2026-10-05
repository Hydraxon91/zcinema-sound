using System.Drawing;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>The icon palette + helpers to theme standard WinForms controls.</summary>
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(0x01, 0x00, 0x01);        // major
    public static readonly Color Panel = Color.FromArgb(0x0C, 0x0A, 0x08);
    public static readonly Color Edge = Color.FromArgb(0xE1, 0xE0, 0xE1);      // light grey
    public static readonly Color Minor = Color.FromArgb(0xFA, 0xFB, 0xCA);     // pale cream
    public static readonly Color Accent = Color.FromArgb(0xD9, 0x87, 0x2C);    // amber
    public static readonly Color AccentDark = Color.FromArgb(0xAE, 0x49, 0x06); // deep amber
    public static readonly Color Unlit = Color.FromArgb(0x3A, 0x2A, 0x1A);     // dark brown

    public static void Apply(Control root)
    {
        root.BackColor = Bg;
        root.ForeColor = Accent;
        foreach (Control c in root.Controls) Style(c);
    }

    private static void Style(Control c)
    {
        switch (c)
        {
            case TabControl tabs:
                tabs.BackColor = Bg;
                tabs.ForeColor = Accent;
                StyleTabs(tabs);
                break;
            case TabPage page:
                page.BackColor = Bg;
                page.ForeColor = Accent;
                break;
            case Button b:
                StyleButton(b);
                break;
            case CheckBox cb:
                cb.BackColor = Bg;
                cb.ForeColor = Accent;
                cb.FlatStyle = FlatStyle.Flat;
                break;
            case DataGridView g:
                StyleGrid(g);
                break;
            case ComboBox combo:
                combo.BackColor = Panel;
                combo.ForeColor = Accent;
                combo.FlatStyle = FlatStyle.Flat;
                break;
            case TextBox tb:
                tb.BackColor = Panel;
                tb.ForeColor = Edge;
                tb.BorderStyle = BorderStyle.FixedSingle;
                break;
            case LedSlider:
                c.BackColor = Bg;
                break;
            case Label:
                c.BackColor = Bg;
                c.ForeColor = Accent;
                break;
            case Panel p:
                p.BackColor = Bg;
                p.ForeColor = Accent;
                break;
        }
        foreach (Control child in c.Controls) Style(child);
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
        b.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
    }

    public static void StyleGrid(DataGridView g)
    {
        g.EnableHeadersVisualStyles = false;
        g.BackgroundColor = Bg;
        g.GridColor = Color.FromArgb(40, 30, 20);
        g.DefaultCellStyle.BackColor = Color.FromArgb(12, 10, 8);
        g.DefaultCellStyle.ForeColor = Edge;
        g.DefaultCellStyle.SelectionBackColor = AccentDark;
        g.DefaultCellStyle.SelectionForeColor = Minor;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(20, 14, 8);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(20, 14, 8);
    }

    public static void StyleTabs(TabControl tabs)
    {
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(120, 32);
        tabs.Appearance = TabAppearance.Normal;
        tabs.DrawItem += (s, e) =>
        {
            var tc = (TabControl)s!;
            bool selected = e.Index == tc.SelectedIndex;
            using (var back = new SolidBrush(selected ? AccentDark : Color.FromArgb(16, 12, 8)))
                e.Graphics.FillRectangle(back, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, new Font("Segoe UI", 9.5f, FontStyle.Bold),
                e.Bounds, selected ? Minor : Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
    }
}
