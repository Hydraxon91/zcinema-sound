using System.Drawing;
using System.Windows.Forms;
using ZCinemaSound.Core;

namespace ZCinemaSound.App;

public sealed class MainForm : Form
{
    private readonly TrackBar _preamp = Slider(-60, 0);
    private readonly TrackBar _bass = Slider(-12, 12);
    private readonly TrackBar _treble = Slider(-12, 12);
    private readonly TrackBar _dialog = Slider(-9, 9);
    private readonly TrackBar _width = Slider(0, 30);

    private readonly TrackBar[] _eq = new TrackBar[ZCinemaProfile.EqFreqs.Length];
    private readonly Label[] _eqValue = new Label[ZCinemaProfile.EqFreqs.Length];

    private readonly Label _status = new();
    private readonly ComboBox _preset = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private readonly NotifyIcon _tray = new();
    private bool _loading;
    private bool _exiting;

    public MainForm()
    {
        Text = "Z Cinema Sound";
        ClientSize = new Size(600, 650);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Icon = LoadAppIcon();

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var sound = new TabPage("Sound");
        var remote = new TabPage("Remote");
        tabs.TabPages.Add(sound);
        tabs.TabPages.Add(remote);
        Controls.Add(tabs);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
        sound.Controls.Add(panel);

        int y = 14;
        Row(panel, "Ceiling", _preamp, ref y);
        Row(panel, "Bass", _bass, ref y);
        Row(panel, "Treble", _treble, ref y);
        Row(panel, "Dialogue", _dialog, ref y);
        Row(panel, "Width %", _width, ref y);

        var lblPreset = new Label { Text = "Preset", Left = 12, Top = y + 5, Width = 66 };
        _preset.Left = 84; _preset.Top = y; _preset.Width = 170; _preset.DropDownStyle = ComboBoxStyle.DropDownList;
        _preset.Items.AddRange(new object[] { "(custom)", "Flat", "Music", "Movies", "Night", "Vocal", "V-Shape" });
        _preset.SelectedIndex = 0;
        _preset.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _preset.SelectedItem?.ToString() is not string name || name == "(custom)") return;
            var p = EqualizerApo.LoadProfile();
            Presets.Apply(p, name);
            ApplyToUi(p);
            Save();
        };
        panel.Controls.Add(lblPreset);
        panel.Controls.Add(_preset);
        y += 40;

        // ---- Graphic EQ (same bands as the PowerShell GUI) ----
        var lblEq = new Label { Text = "Graphic EQ (dB)", Left = 12, Top = y, Width = 200, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        panel.Controls.Add(lblEq);
        y += 26;

        string[] eqLabels = { "60", "170", "470", "1.2k", "2.4k", "4.7k", "10k", "14k" };
        for (int i = 0; i < _eq.Length; i++)
        {
            int x = 22 + i * 66;
            var freq = new Label { Text = eqLabels[i], Left = x, Top = y, Width = 50, TextAlign = ContentAlignment.MiddleCenter };
            var track = new TrackBar
            {
                Orientation = Orientation.Vertical,
                Minimum = -12, Maximum = 12, TickFrequency = 4,
                Height = 180, Width = 50, Left = x, Top = y + 20,
                RightToLeftLayout = true, // put +12 at the top
            };
            var val = new Label { Text = "0", Left = x, Top = y + 204, Width = 50, TextAlign = ContentAlignment.MiddleCenter };
            track.Tag = val;
            track.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _debounce.Stop(); _debounce.Start(); } };
            _eq[i] = track;
            _eqValue[i] = val;
            panel.Controls.Add(freq);
            panel.Controls.Add(track);
            panel.Controls.Add(val);
        }
        y += 232;

        _status.Left = 12; _status.Top = y; _status.Width = 560; _status.Height = 40;
        panel.Controls.Add(_status);

        foreach (var t in new[] { _preamp, _bass, _treble, _dialog, _width })
            t.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _debounce.Stop(); _debounce.Start(); } };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Save(); };

        // ---- Remote tab (placeholder for now) ----
        var remoteInfo = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Text = "Remote mapping\n\n"
                 + "Free buttons on the remote:\n  " + string.Join(", ", RemoteProtocol.FreeButtons)
                 + "\n\nMapping file: " + RemoteMap.MapPath()
                 + "\n\n(The full Remote tab — mapping grid, Learn, tray actions — is ported next.)"
        };
        remote.Controls.Add(remoteInfo);

        // ---- tray ----
        _tray.Icon = Icon;
        _tray.Text = "ZCinema Sound";
        _tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open control panel", null, (_, _) => ShowApp());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowApp();

        FormClosing += (_, e) =>
        {
            if (!_exiting) { e.Cancel = true; Hide(); _tray.ShowBalloonTip(3000, "ZCinema Sound", "Still running in the tray.", ToolTipIcon.Info); }
        };

        LoadProfileIntoUi();
    }

    private static TrackBar Slider(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        TickFrequency = Math.Max(1, (max - min) / 12),
        Width = 400,
    };

    private void Row(Control parent, string name, TrackBar track, ref int y)
    {
        var label = new Label { Text = name, Left = 12, Top = y + 6, Width = 66 };
        track.Left = 84; track.Top = y;
        var value = new Label { Left = 494, Top = y + 6, Width = 70, TextAlign = ContentAlignment.MiddleRight };
        track.Tag = value;
        parent.Controls.Add(label);
        parent.Controls.Add(track);
        parent.Controls.Add(value);
        y += 42;
    }

    private void UpdateLabels()
    {
        Set(_preamp, "{0} dB"); Set(_bass, "{0} dB"); Set(_treble, "{0} dB");
        Set(_dialog, "{0} dB"); Set(_width, "{0} %");
        for (int i = 0; i < _eq.Length; i++) _eqValue[i].Text = _eq[i].Value.ToString();
        static void Set(TrackBar t, string fmt) { if (t.Tag is Label l) l.Text = string.Format(fmt, t.Value); }
    }

    private void LoadProfileIntoUi() => ApplyToUi(EqualizerApo.LoadProfile());

    private void ApplyToUi(ZCinemaProfile p)
    {
        _loading = true;
        _preamp.Value = Clamp(_preamp, (int)Math.Round(p.PreampDb));
        _bass.Value = Clamp(_bass, (int)Math.Round(p.BassGain));
        _treble.Value = Clamp(_treble, (int)Math.Round(p.TrebleGain));
        _dialog.Value = Clamp(_dialog, (int)Math.Round(p.DialogGain));
        _width.Value = Clamp(_width, (int)Math.Round(p.Width * 100));
        for (int i = 0; i < _eq.Length; i++)
            _eq[i].Value = Clamp(_eq[i], (int)Math.Round(i < p.EqGains.Length ? p.EqGains[i] : 0));
        _loading = false;
        UpdateLabels();
        _status.Text = "Loaded from " + EqualizerApo.ProfilePath();
    }

    private static int Clamp(TrackBar t, int v) => Math.Min(t.Maximum, Math.Max(t.Minimum, v));

    private ZCinemaProfile FromUi()
    {
        var p = EqualizerApo.LoadProfile();
        p.PreampDb = _preamp.Value;
        p.BassGain = _bass.Value;
        p.SubGain = Math.Round(_bass.Value * 0.6, 1);
        p.TrebleGain = _treble.Value;
        p.DialogGain = _dialog.Value;
        p.Width = _width.Value / 100.0;
        p.EqGains = _eq.Select(t => (double)t.Value).ToArray();
        return p;
    }

    private void Save()
    {
        try
        {
            EqualizerApo.SaveProfile(FromUi());
            _status.Text = $"Saved {DateTime.Now:HH:mm:ss}  ->  {EqualizerApo.ProfilePath()}";
        }
        catch (Exception ex)
        {
            _status.Text = "Write failed: " + ex.Message;
        }
    }

    private void ShowApp() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private void ExitApp()
    {
        _exiting = true;
        _tray.Visible = false;
        Close();
    }

    private static Icon LoadAppIcon()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "ZCinemaSound.ico");
            if (File.Exists(candidate))
            {
                try { return new Icon(candidate); } catch { /* fall through */ }
            }
        }
        return SystemIcons.Application;
    }
}
