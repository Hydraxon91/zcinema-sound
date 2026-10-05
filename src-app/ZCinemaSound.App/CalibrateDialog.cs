using System.Drawing;
using System.Windows.Forms;
using ZCinemaSound.Core;

namespace ZCinemaSound.App;

/// <summary>Small dialog: measures the endpoint dB at a "comfortable" Windows % and
/// returns the Preamp value that makes 100% equal that loudness.</summary>
public sealed class CalibrateDialog : Form
{
    private readonly NumericUpDown _pct = new() { Minimum = 1, Maximum = 100, Value = 40, Left = 168, Top = 52, Width = 80, BackColor = Theme.PanelTop, ForeColor = Theme.Accent, BorderStyle = BorderStyle.FixedSingle };
    private readonly Label _result = new() { Left = 14, Top = 92, Width = 372, Height = 60, ForeColor = Theme.Muted, BackColor = Color.Transparent, Font = Theme.ValueFont };
    private readonly AudioEndpointVolume? _vol;

    public double PreampDb { get; private set; }

    public CalibrateDialog(AudioEndpointVolume? vol)
    {
        _vol = vol;
        Text = "Calibrate ceiling";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 190);
        BackColor = Theme.Bg;
        ForeColor = Theme.Edge;
        MaximizeBox = false;
        MinimizeBox = false;

        var lbl = new Label { Left = 14, Top = 55, Width = 150, Text = "Comfortable Windows %", ForeColor = Theme.Edge, BackColor = Color.Transparent };
        var ok = new Button { Text = "Apply", Left = 208, Top = 150, Width = 90, Height = 28 };
        var cancel = new Button { Text = "Cancel", Left = 304, Top = 150, Width = 82, Height = 28 };
        Theme.StyleButton(ok);
        Theme.StyleButton(cancel);
        ok.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;

        Controls.Add(lbl);
        Controls.Add(_pct);
        Controls.Add(_result);
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        _pct.ValueChanged += (_, _) => Measure();
        Measure();
    }

    private void Measure()
    {
        if (_vol is null) { _result.Text = "No endpoint volume available."; return; }
        var orig = _vol.GetScalar();
        _vol.SetScalar((float)_pct.Value / 100f);
        System.Threading.Thread.Sleep(120);
        float db = _vol.GetDb();
        _vol.SetScalar(orig);
        PreampDb = Math.Round(db, 1);
        _result.Text = $"At Windows {_pct.Value}% the endpoint is {db:0.0} dB below max.\r\nUse  Preamp: {PreampDb} dB";
    }
}
