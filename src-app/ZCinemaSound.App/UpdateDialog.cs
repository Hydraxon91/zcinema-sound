using System.Drawing;
using System.Windows.Forms;

namespace ZCinemaSound.App;

/// <summary>Small modal progress window shown while the update downloads.</summary>
public sealed class UpdateDialog : Form
{
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Left = 16, Top = 44, Width = 368, Height = 18 };
    private readonly Label _label = new() { Left = 16, Top = 16, Width = 368, Height = 20, ForeColor = Theme.Edge, BackColor = Color.Transparent };

    public CancellationTokenSource Cts { get; } = new();

    public UpdateDialog(string version)
    {
        Text = "Downloading update";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 120);
        BackColor = Theme.Bg;
        ForeColor = Theme.Edge;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;

        _label.Text = $"Downloading ZCinema Sound {version}...";
        var cancel = new Button { Text = "Cancel", Left = 304, Top = 76, Width = 80, Height = 28 };
        Theme.StyleButton(cancel);
        cancel.Click += (_, _) => Cts.Cancel();

        Controls.Add(_label);
        Controls.Add(_bar);
        Controls.Add(cancel);
    }

    public void SetProgress(int pct)
    {
        if (IsDisposed) return;
        _bar.Value = Math.Max(0, Math.Min(100, pct));
        _label.Text = $"Downloading... {_bar.Value}%";
    }
}
