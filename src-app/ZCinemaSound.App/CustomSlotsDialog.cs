using System.Drawing;
using System.Windows.Forms;
using ZCinemaSound.Core;

namespace ZCinemaSound.App;

/// <summary>Rename or clear the three custom slots.</summary>
public sealed class CustomSlotsDialog : Form
{
    private static readonly string[] Slots = { "1", "2", "3" };
    private readonly TextBox[] _name = new TextBox[3];

    public Dictionary<string, string> Names { get; } = new();
    public List<string> Cleared { get; } = new();

    public CustomSlotsDialog(IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, CustomPreset> slots)
    {
        Text = "Custom slots";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 210);
        BackColor = Theme.Bg;
        ForeColor = Theme.Edge;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        for (int i = 0; i < 3; i++)
        {
            int y = 16 + i * 40;
            var lbl = new Label { Left = 16, Top = y + 4, Width = 70, Text = "Custom " + (i + 1), ForeColor = Theme.Edge, BackColor = Color.Transparent };
            var tb = new TextBox
            {
                Left = 92, Top = y, Width = 190,
                Text = names.TryGetValue(Slots[i], out var n) ? n : "",
                BackColor = Theme.PanelTop, ForeColor = Theme.Accent, BorderStyle = BorderStyle.FixedSingle,
            };
            var clr = new Button { Text = "Clear", Left = 294, Top = y - 1, Width = 88, Height = 26, Enabled = slots.ContainsKey(Slots[i]) };
            Theme.StyleButton(clr);
            int idx = i;
            clr.Click += (_, _) => { _clear[idx] = true; tb.Text = ""; tb.Enabled = false; clr.Enabled = false; };
            _name[i] = tb;
            Controls.Add(lbl); Controls.Add(tb); Controls.Add(clr);
        }

        var hint = new Label
        {
            Left = 16, Top = 138, Width = 366,
            Text = "Leave a name blank to keep the default (\"Custom 1\" etc.).",
            ForeColor = Theme.Muted, BackColor = Color.Transparent, Font = Theme.ButtonFont,
        };
        Controls.Add(hint);

        var ok = new Button { Text = "OK", Left = 210, Top = 166, Width = 80, Height = 28 };
        var cancel = new Button { Text = "Cancel", Left = 296, Top = 166, Width = 88, Height = 28 };
        Theme.StyleButton(ok); Theme.StyleButton(cancel);
        ok.Click += (_, _) =>
        {
            for (int i = 0; i < 3; i++)
            {
                if (_clear[i]) Cleared.Add(Slots[i]);
                else Names[Slots[i]] = _name[i].Text.Trim();
            }
        };
        ok.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private readonly bool[] _clear = new bool[3];
}
