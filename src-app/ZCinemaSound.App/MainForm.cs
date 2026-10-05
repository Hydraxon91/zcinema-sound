using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using ZCinemaSound.Core;

namespace ZCinemaSound.App;

public sealed class MainForm : Form, IActionHost
{
    private readonly LedSlider _preamp = Slider(-60, 0, 16);
    private readonly LedSlider _bass = Slider(-12, 12, 13);
    private readonly LedSlider _treble = Slider(-12, 12, 13);
    private readonly LedSlider _dialog = Slider(-9, 9, 13);
    private readonly LedSlider _width = Slider(0, 30, 13);
    private readonly List<Label> _rowValue = new();

    private readonly LedSlider[] _eq = new LedSlider[ZCinemaProfile.EqFreqs.Length];
    private readonly Label[] _eqValue = new Label[ZCinemaProfile.EqFreqs.Length];

    private readonly Label _status = new();
    private readonly ComboBox _preset = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private readonly NotifyIcon _tray = new();
    private bool _loading;
    private bool _exiting;

    // remote
    private readonly HidReader _reader = new();
    private readonly DataGridView _grid = new();
    private readonly Label _remoteStatus = new();
    private readonly Label _remoteHelp = new();
    private readonly CheckBox _chkRemote = new() { Text = "Remote on", Checked = true, Width = 100 };
    private readonly Dictionary<string, DateTime> _lastPress = new();
    private bool _learn;
    private bool _remoteEnabled = true;

    private static readonly string[] ActionItems =
    {
        "none", "gui", "bypass",
        "preset:Flat", "preset:Music", "preset:Movies", "preset:Night", "preset:Vocal", "preset:V-Shape",
        "custom:1", "custom:2", "custom:3",
        "sound:dialogue+", "sound:dialogue-", "sound:width+", "sound:width-", "sound:ceiling+", "sound:ceiling-",
        "media:playpause", "media:next", "media:prev", "media:stop", "media:volup", "media:voldown", "media:mute",
        "app", "script", "url", "keys",
    };

    public MainForm()
    {
        Text = "Z Cinema Sound";
        ClientSize = new Size(620, 660);
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

        BuildSoundTab(sound);
        BuildRemoteTab(remote);
        BuildTray();

        Theme.Apply(this);
        _status.ForeColor = Theme.Minor;
        _remoteStatus.ForeColor = Theme.Minor;
        _remoteHelp.ForeColor = Theme.Edge;
        foreach (var l in _rowValue) l.ForeColor = Theme.Minor;
        foreach (var l in _eqValue) l.ForeColor = Theme.Minor;

        FormClosing += (_, e) =>
        {
            if (!_exiting)
            {
                e.Cancel = true; Hide();
                _tray.ShowBalloonTip(3000, "ZCinema Sound", "Still running in the tray.", ToolTipIcon.Info);
            }
        };

        LoadProfileIntoUi();
        _reader.ButtonPressed += OnReaderButton;
        _reader.Start();
    }

    // ------------------------------------------------------------------ Sound
    private void BuildSoundTab(TabPage page)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
        page.Controls.Add(panel);

        int y = 16;
        Row(panel, "Ceiling", _preamp, ref y);
        Row(panel, "Bass", _bass, ref y);
        Row(panel, "Treble", _treble, ref y);
        Row(panel, "Dialogue", _dialog, ref y);
        Row(panel, "Width", _width, ref y);

        var lblPreset = new Label { Text = "Preset", Left = 14, Top = y + 6, Width = 70, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _preset.Left = 96; _preset.Top = y; _preset.Width = 170; _preset.DropDownStyle = ComboBoxStyle.DropDownList;
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
        y += 44;

        var lblEq = new Label { Text = "Graphic EQ (dB)", Left = 14, Top = y, Width = 220, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        panel.Controls.Add(lblEq);
        y += 28;

        string[] eqLabels = { "60", "170", "470", "1.2k", "2.4k", "4.7k", "10k", "14k" };
        for (int i = 0; i < _eq.Length; i++)
        {
            int x = 24 + i * 66;
            var freq = new Label { Text = eqLabels[i], Left = x, Top = y, Width = 50, TextAlign = ContentAlignment.MiddleCenter };
            var track = new LedSlider
            {
                Orientation = Orientation.Vertical,
                Minimum = -12, Maximum = 12, Segments = 13,
                Width = 50, Height = 190, Left = x, Top = y + 20,
            };
            var val = new Label { Text = "0", Left = x, Top = y + 214, Width = 50, TextAlign = ContentAlignment.MiddleCenter };
            track.Tag = val;
            track.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _debounce.Stop(); _debounce.Start(); } };
            _eq[i] = track;
            _eqValue[i] = val;
            panel.Controls.Add(freq); panel.Controls.Add(track); panel.Controls.Add(val);
        }
        y += 244;

        _status.Left = 14; _status.Top = y; _status.Width = 560; _status.Height = 40;
        panel.Controls.Add(_status);

        foreach (var t in new[] { _preamp, _bass, _treble, _dialog, _width })
            t.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _debounce.Stop(); _debounce.Start(); } };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Save(); };
    }

    private static LedSlider Slider(int min, int max, int segments) => new()
    {
        Minimum = min, Maximum = max, Segments = segments, Width = 430, Height = 30,
    };

    private void Row(Control parent, string name, LedSlider track, ref int y)
    {
        var label = new Label { Text = name, Left = 14, Top = y + 6, Width = 74, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        track.Left = 96; track.Top = y;
        var value = new Label { Left = 530, Top = y + 6, Width = 60, TextAlign = ContentAlignment.MiddleRight };
        track.Tag = value;
        _rowValue.Add(value);
        parent.Controls.Add(label); parent.Controls.Add(track); parent.Controls.Add(value);
        y += 40;
    }

    // ----------------------------------------------------------------- Remote
    private void BuildRemoteTab(TabPage page)
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        page.Controls.Add(panel);

        _remoteHelp.Text = "Map the remote's free buttons. app/script/url/keys use the Value box. Click Learn, then press a remote button.";
        _remoteHelp.Left = 8; _remoteHelp.Top = 6; _remoteHelp.Width = 594; _remoteHelp.Height = 24;
        panel.Controls.Add(_remoteHelp);

        _grid.Left = 8; _grid.Top = 32; _grid.Width = 592; _grid.Height = 372;
        _grid.AllowUserToAddRows = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Remote button", ReadOnly = true, FillWeight = 130 });
        var combo = new DataGridViewComboBoxColumn { HeaderText = "Action", FillWeight = 200 };
        combo.Items.AddRange(ActionItems);
        _grid.Columns.Add(combo);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value (app / script / url / keys)", FillWeight = 240 });
        panel.Controls.Add(_grid);

        var bar = new FlowLayoutPanel { Left = 8, Top = 412, Width = 592, Height = 36 };
        var btnLearn = new Button { Text = "Learn (press a button)", Width = 150, Height = 26 };
        var btnSave = new Button { Text = "Save", Width = 66, Height = 26 };
        var btnDefaults = new Button { Text = "Restore defaults", Width = 108, Height = 26 };
        var btnReload = new Button { Text = "Reload file", Width = 86, Height = 26 };
        var btnBrowse = new Button { Text = "Browse...", Width = 78, Height = 26 };
        var btnExit = new Button { Text = "Exit", Width = 56, Height = 26 };
        bar.Controls.AddRange(new Control[] { btnLearn, btnSave, btnDefaults, btnReload, btnBrowse, _chkRemote, btnExit });
        panel.Controls.Add(bar);

        _remoteStatus.Left = 8; _remoteStatus.Top = 452; _remoteStatus.Width = 592; _remoteStatus.Height = 30;
        panel.Controls.Add(_remoteStatus);

        btnLearn.Click += (_, _) => { _learn = true; _remoteStatus.Text = "Listening... press a remote button now."; };
        btnSave.Click += (_, _) => SaveRemoteGrid();
        btnDefaults.Click += (_, _) => { RemoteMap.Save(RemoteMap.Default()); LoadRemoteGrid(); _remoteStatus.Text = "Defaults restored."; };
        btnReload.Click += (_, _) => { LoadRemoteGrid(); _remoteStatus.Text = "Reloaded from file."; };
        btnExit.Click += (_, _) => ExitApp();
        btnBrowse.Click += (_, _) => BrowseIntoSelectedRow();
        _chkRemote.CheckedChanged += (_, _) => _remoteEnabled = _chkRemote.Checked;

        LoadRemoteGrid();
    }

    private void LoadRemoteGrid()
    {
        var map = RemoteMap.Read();
        _grid.Rows.Clear();
        foreach (var b in RemoteProtocol.FreeButtons)
        {
            string act = "none", val = "";
            if (map.TryGetValue(b, out var a) && !string.IsNullOrWhiteSpace(a))
            {
                int i = a.IndexOf(':');
                string verb = i >= 0 ? a[..i] : a;
                if (verb is "app" or "script" or "url" or "keys") { act = verb; val = a[(i + 1)..]; }
                else if (ActionItems.Contains(a)) act = a;
            }
            _grid.Rows.Add(b, act, val);
        }
    }

    private void SaveRemoteGrid()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var warns = new List<string>();
        foreach (DataGridViewRow r in _grid.Rows)
        {
            var b = r.Cells[0].Value?.ToString();
            var a = r.Cells[1].Value?.ToString() ?? "none";
            var v = r.Cells[2].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(b)) continue;
            if (a is "app" or "script" or "url" or "keys")
            {
                if (string.IsNullOrEmpty(v)) warns.Add($"{b} needs a Value");
                else if (a == "url" && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[a-zA-Z]+://")) warns.Add($"{b} value is not a link");
                else if ((a == "app" || a == "script") && !File.Exists(v)) warns.Add($"{b} path not found");
                map[b] = string.IsNullOrEmpty(v) ? "none" : $"{a}:{v}";
            }
            else if (a != "none") map[b] = a;
        }
        RemoteMap.Save(map);
        _remoteStatus.Text = warns.Count > 0 ? "Saved with notes: " + string.Join("; ", warns) : "Saved to " + RemoteMap.MapPath();
    }

    private void BrowseIntoSelectedRow()
    {
        if (_grid.SelectedRows.Count == 0) { _remoteStatus.Text = "Select a row first."; return; }
        using var dlg = new OpenFileDialog
        {
            Title = "Pick a program, script or file",
            Filter = "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|PowerShell (*.ps1)|*.ps1|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var row = _grid.SelectedRows[0];
        row.Cells[2].Value = dlg.FileName;
        row.Cells[1].Value = dlg.FileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ? "script" : "app";
        _remoteStatus.Text = "Value set for " + row.Cells[0].Value;
    }

    private void OnReaderButton(string name)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(() =>
        {
            if (_learn)
            {
                _learn = false;
                if (RemoteProtocol.NativeButtons.Contains(name)) { _remoteStatus.Text = $"'{name}' is handled by Windows and hidden here."; return; }
                var row = _grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (string?)r.Cells[0].Value == name);
                if (row is null) { _grid.Rows.Add(name, "none", ""); row = _grid.Rows[^1]; }
                _grid.CurrentCell = row.Cells[0];
                _remoteStatus.Text = "Detected: " + name;
                return;
            }
            if (!_remoteEnabled) return;
            var now = DateTime.Now;
            if (_lastPress.TryGetValue(name, out var last) && (now - last).TotalMilliseconds < 700) return;
            _lastPress[name] = now;
            var map = RemoteMap.Read();
            if (map.TryGetValue(name, out var action) && action != "none")
            {
                ActionRunner.Run(action, this);
                _remoteStatus.Text = $"Remote: {name} -> {action}";
            }
        });
    }

    // ------------------------------------------------------------ IActionHost
    public void ShowApp() => BeginInvoke(() => { Show(); WindowState = FormWindowState.Normal; Activate(); });
    public void SendKeys(string text) => System.Windows.Forms.SendKeys.SendWait(text);

    // --------------------------------------------------------------- tray
    private void BuildTray()
    {
        _tray.Icon = Icon;
        _tray.Text = "ZCinema Sound";
        _tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open control panel", null, (_, _) => ShowApp());
        var miRemote = new ToolStripMenuItem("Remote mapping") { CheckOnClick = true, Checked = true };
        miRemote.CheckedChanged += (_, _) => { _remoteEnabled = miRemote.Checked; _chkRemote.Checked = miRemote.Checked; };
        menu.Items.Add(miRemote);
        var miAuto = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = GetAutostart() };
        miAuto.CheckedChanged += (_, _) => SetAutostart(miAuto.Checked);
        menu.Items.Add(miAuto);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowApp();
    }

    private const string RunKeyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "ZCinemaSound";

    private static bool GetAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKeyName);
        return k?.GetValue(RunValueName) is string s && s.Length > 0;
    }

    private static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKeyName);
        if (on) k.SetValue(RunValueName, "\"" + Environment.ProcessPath + "\"");
        else k.DeleteValue(RunValueName, false);
    }

    private void ExitApp()
    {
        _exiting = true;
        try { _reader.Stop(); } catch { }
        _tray.Visible = false;
        Close();
    }

    // ------------------------------------------------------------- sound I/O
    private void UpdateLabels()
    {
        Set(_preamp, "{0} dB"); Set(_bass, "{0} dB"); Set(_treble, "{0} dB");
        Set(_dialog, "{0} dB"); Set(_width, "{0} %");
        for (int i = 0; i < _eq.Length; i++) _eqValue[i].Text = _eq[i].Value.ToString();
        static void Set(LedSlider t, string fmt) { if (t.Tag is Label l) l.Text = string.Format(fmt, t.Value); }
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

    private static int Clamp(LedSlider t, int v) => Math.Min(t.Maximum, Math.Max(t.Minimum, v));

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
        catch (Exception ex) { _status.Text = "Write failed: " + ex.Message; }
    }

    private static Icon LoadAppIcon()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "ZCinemaSound.ico");
            if (File.Exists(candidate)) { try { return new Icon(candidate); } catch { } }
        }
        return SystemIcons.Application;
    }
}
