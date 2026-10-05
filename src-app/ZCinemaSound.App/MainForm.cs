using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ZCinemaSound.Core;

namespace ZCinemaSound.App;

public sealed class MainForm : Form, IActionHost
{
    private const int W = 680, H = 780, TitleH = 42, ContentTop = 96;

    private readonly LedSlider _preamp = Slider(-60, 0, 16);
    private readonly LedSlider _bass = Slider(-12, 12, 13);
    private readonly LedSlider _treble = Slider(-12, 12, 13);
    private readonly LedSlider _dialog = Slider(-9, 9, 13);
    private readonly LedSlider _width = Slider(0, 30, 13);
    private readonly LedSlider _volSlider = new() { Minimum = 0, Maximum = 100, Segments = 20, Width = 430, Height = 28 };
    private readonly CheckBox _chkMute = new() { Text = "Mute", Width = 80, Height = 24, ForeColor = Theme.Accent, BackColor = Theme.Bg };
    private bool _muting;
    private readonly List<Label> _rowValue = new();

    private AudioEndpointVolume? _volume;
    private readonly System.Windows.Forms.Timer _volTimer = new() { Interval = 350 };
    private bool _volSyncing;
    private readonly EventWaitHandle _showEvent = new(false, EventResetMode.AutoReset, @"Local\ZCinema_Show");
    private readonly System.Windows.Forms.Timer _showTimer = new() { Interval = 400 };

    private readonly LedSlider[] _eq = new LedSlider[ZCinemaProfile.EqFreqs.Length];
    private readonly Label[] _eqValue = new Label[ZCinemaProfile.EqFreqs.Length];

    private readonly Label _status = new();
    private readonly SegmentedControl _tabsSeg = new();
    private readonly SegmentedControl _presetSeg = new();
    private readonly Panel _soundRoot = new();
    private readonly Panel _remoteRoot = new();
    private readonly Panel _content = new();
    private readonly Panel _frame = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 300 };
    private readonly NotifyIcon _tray = new();
    private bool _loading;
    private bool _exiting;

    // remote
    private readonly HidReader _reader = new();
    private readonly DataGridView _grid = new();
    private readonly Label _remoteStatus = new();
    private readonly CheckBox _chkRemote = new() { Text = "Remote on", Checked = true, Width = 96, ForeColor = Theme.Accent, BackColor = Theme.Bg };
    private readonly Dictionary<string, DateTime> _lastPress = new();
    private bool _learn;
    private bool _remoteEnabled = true;

    private static readonly string[] PresetNames = { "Flat", "Music", "Movies", "Night", "Vocal", "V-Shape" };

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
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(W, H);
        BackColor = Theme.Bg;
        DoubleBuffered = true;
        Icon = LoadAppIcon();

        _content.Left = 0; _content.Top = 0; _content.Width = W; _content.Height = H; _content.BackColor = Theme.Bg;
        Controls.Add(_content);

        BuildTitleBar();

        _tabsSeg.Items = new[] { "Sound", "Remote" };
        _tabsSeg.Left = 14; _tabsSeg.Top = 52; _tabsSeg.Width = 300; _tabsSeg.Height = 34;
        _tabsSeg.SelectedIndexChanged += (_, _) => SwitchTab();
        _content.Controls.Add(_tabsSeg);

        BuildSoundRoot();
        BuildRemoteRoot();
        _content.Controls.Add(_soundRoot);
        _content.Controls.Add(_remoteRoot);
        remoteVisible(false);
        SetContentRegion();

        // top-most frame overlay: region = ring (outer rounded minus inner), painted amber,
        // so the border sits above all content and connects at the corners.
        _frame.Left = 0; _frame.Top = 0; _frame.Width = W; _frame.Height = H;
        _frame.BackColor = Theme.Accent;
        Controls.Add(_frame);
        _frame.BringToFront();
        SetFrameRegion();

        BuildTray();

        FormClosing += (_, e) =>
        {
            if (!_exiting)
            {
                e.Cancel = true; Hide();
                _tray.ShowBalloonTip(3000, "ZCinema Sound", "Still running in the tray.", ToolTipIcon.Info);
            }
        };

        LoadProfileIntoUi();
        _volume = AudioEndpointVolume.Open();
        SyncVolumeFromDevice();
        _volTimer.Tick += (_, _) => SyncVolumeFromDevice();
        _volTimer.Start();
        _showTimer.Tick += (_, _) => { if (_showEvent.WaitOne(0)) ShowApp(); };
        _showTimer.Start();
        _reader.ButtonPressed += OnReaderButton;
        _reader.Start();
    }

    private static LedSlider Slider(int min, int max, int segments) => new()
    {
        Minimum = min, Maximum = max, Segments = segments, Width = 430, Height = 28,
    };

    private void SwitchTab()
    {
        bool sound = _tabsSeg.SelectedIndex == 0;
        _soundRoot.Visible = sound;
        _remoteRoot.Visible = !sound;
    }

    private void remoteVisible(bool v) { _soundRoot.Visible = !v; _remoteRoot.Visible = v; }

    // ------------------------------------------------------------- title bar
    private void BuildTitleBar()
    {
        var bar = new Panel { Left = 3, Top = 3, Width = W - 6, Height = TitleH, BackColor = Theme.PanelTop };
        _content.Controls.Add(bar);

        Icon? icon = Icon;
        var pic = new PictureBox { Left = 14, Top = 11, Width = 20, Height = 20, SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.Transparent };
        try { if (icon is not null) pic.Image = icon.ToBitmap(); } catch { }
        bar.Controls.Add(pic);

        var title = new Label { Text = "Z Cinema Sound", Left = 42, Top = 9, Width = 300, Height = 24, ForeColor = Theme.Accent, BackColor = Color.Transparent, Font = Theme.TitleFont };
        bar.Controls.Add(title);

        var min = new Label { Text = "–", Left = W - 70, Top = 7, Width = 28, Height = 26, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Accent, BackColor = Color.Transparent, Font = new Font("Segoe UI", 10f) };
        var close = new Label { Text = "✕", Left = W - 38, Top = 7, Width = 28, Height = 26, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Accent, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f) };
        bar.Controls.Add(min); bar.Controls.Add(close);

        min.MouseEnter += (_, _) => min.ForeColor = Theme.Minor; min.MouseLeave += (_, _) => min.ForeColor = Theme.Accent;
        close.MouseEnter += (_, _) => close.ForeColor = Theme.Minor; close.MouseLeave += (_, _) => close.ForeColor = Theme.Accent;
        min.Click += (_, _) => WindowState = FormWindowState.Minimized;
        close.Click += (_, _) => ExitApp();

        foreach (Control c in new Control[] { bar, pic, title }) c.MouseDown += DragWindow;
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // CS_DROPSHADOW
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SetRegion(); }
    protected override void OnShown(EventArgs e) { base.OnShown(e); SetRegion(); SetContentRegion(); SetFrameRegion(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); SetRegion(); }

    private void SetContentRegion()
    {
        try { using var p = Theme.RoundedRect(new RectangleF(3, 3, W - 6, H - 6), 11); _content.Region = new Region(p); } catch { }
    }

    private void SetFrameRegion()
    {
        try
        {
            // outer = full square; the WINDOW region clips it to the rounded shape, so the
            // amber bleeds exactly to the window edge (no gap at the corners).
            var ring = new Region(new Rectangle(0, 0, W, H));
            using var inner = Theme.RoundedRect(new RectangleF(3, 3, W - 6, H - 6), 11);
            ring.Exclude(inner);
            _frame.Region = ring;
        }
        catch { }
    }
    private void SetRegion()
    {
        try { using var p = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), 14); Region = new Region(p); } catch { }
    }

    protected override void OnPaint(PaintEventArgs e) => base.OnPaint(e);

    // ------------------------------------------------------------------ root
    private void BuildSoundRoot()
    {
        _soundRoot.Left = 3; _soundRoot.Top = ContentTop; _soundRoot.Width = W - 6; _soundRoot.Height = H - ContentTop - 3;
        _soundRoot.BackColor = Theme.Bg;

        var output = new GlassPanel { Left = 14, Top = 6, Width = 652, Height = 124, Caption = "Output" };
        RowVolume(output, 30);
        Row(output, "Ceiling", _preamp, 70);
        var btnCal = new Button { Text = "Calibrate ceiling...", Left = 482, Top = 90, Width = 160, Height = 26 };
        Theme.StyleButton(btnCal);
        btnCal.Click += (_, _) => Calibrate();
        output.Controls.Add(btnCal);
        _chkMute.Left = 330; _chkMute.Top = 90;
        _chkMute.CheckedChanged += (_, _) => { if (!_muting && _volume is not null) _volume.SetMute(_chkMute.Checked); };
        output.Controls.Add(_chkMute);

        var tone = new GlassPanel { Left = 14, Top = 136, Width = 652, Height = 192, Caption = "Tone" };
        Row(tone, "Bass", _bass, 30);
        Row(tone, "Treble", _treble, 70);
        Row(tone, "Dialogue", _dialog, 110);
        Row(tone, "Width", _width, 150);

        var eq = new GlassPanel { Left = 14, Top = 336, Width = 652, Height = 236, Caption = "Equalizer" };
        BuildEq(eq);

        _soundRoot.Controls.Add(output);
        _soundRoot.Controls.Add(tone);
        _soundRoot.Controls.Add(eq);

        _presetSeg.Items = PresetNames;
        _presetSeg.SelectedIndex = -1; // show none until the user picks one
        _presetSeg.Left = 14; _presetSeg.Top = 580; _presetSeg.Width = 440; _presetSeg.Height = 34;
        _presetSeg.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _presetSeg.SelectedIndex < 0) return;
            var p = EqualizerApo.LoadProfile();
            Presets.Apply(p, PresetNames[_presetSeg.SelectedIndex]);
            ApplyToUi(p);
            Save();
        };
        _soundRoot.Controls.Add(_presetSeg);

        var lblSave = new Label { Text = "Save:", Left = 462, Top = 586, Width = 40, ForeColor = Theme.Edge, BackColor = Color.Transparent };
        _soundRoot.Controls.Add(lblSave);
        for (int i = 1; i <= 3; i++)
        {
            int slot = i;
            var b = new Button { Text = slot.ToString(), Left = 500 + (i - 1) * 46, Top = 582, Width = 40, Height = 28 };
            Theme.StyleButton(b);
            b.Click += (_, _) => SaveCustomSlot(slot.ToString());
            _soundRoot.Controls.Add(b);
        }

        _status.Left = 18; _status.Top = 622; _status.Width = 644; _status.Height = 44;
        _status.ForeColor = Theme.Muted; _status.Font = Theme.ValueFont;
        _soundRoot.Controls.Add(_status);

        _debounce.Tick += (_, _) => { _debounce.Stop(); Save(); };
    }

    private void RowVolume(Control parent, int y)
    {
        var label = new Label { Text = "Windows", Left = 14, Top = y + 4, Width = 96, ForeColor = Theme.Edge, BackColor = Color.Transparent, Font = Theme.LabelFont };
        int pw = parent.Width;
        _volSlider.Left = 112; _volSlider.Top = y; _volSlider.Width = pw - 112 - 104; _volSlider.Height = 28;
        var value = new Label { Left = pw - 96, Top = y + 4, Width = 82, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Minor, BackColor = Color.Transparent, Font = Theme.ValueFont };
        _volSlider.Tag = value;
        _volSlider.ValueChanged += (_, _) =>
        {
            value.Text = _volSlider.Value + " %";
            if (_volSyncing || _volume is null) return;
            _volume.SetScalar(_volSlider.Value / 100f);
        };
        parent.Controls.Add(label); parent.Controls.Add(_volSlider); parent.Controls.Add(value);
    }

    private void SyncVolumeFromDevice()
    {
        if (_volume is null || _volSlider.Tag is not Label lbl) return;
        int v = (int)Math.Round(_volume.GetScalar() * 100);
        if (Math.Abs(v - _volSlider.Value) >= 1)
        {
            _volSyncing = true;
            _volSlider.Value = v;
            _volSyncing = false;
        }
        lbl.Text = _volSlider.Value + " %";
        _muting = true;
        _chkMute.Checked = _volume.GetMute();
        _muting = false;
    }

    private void Calibrate()
    {
        using var dlg = new CalibrateDialog(_volume);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var p = EqualizerApo.LoadProfile();
        p.PreampDb = dlg.PreampDb;
        EqualizerApo.SaveProfile(p);
        ApplyToUi(p);
        _status.Text = $"Ceiling set to {dlg.PreampDb} dB";
    }

    private void SaveCustomSlot(string slot)
    {
        var p = FromUi();
        var slots = RemoteMap.ReadCustomSlots();
        slots[slot] = new CustomPreset(p.PreampDb, p.BassGain, p.TrebleGain, p.DialogGain, p.Width * 100.0, p.EqGains);
        RemoteMap.SaveCustomSlots(slots);
        _status.Text = $"Saved current settings to Custom {slot}";
    }

    private void Row(Control parent, string name, LedSlider track, int y)
    {
        var label = new Label { Text = name, Left = 14, Top = y + 4, Width = 96, ForeColor = Theme.Edge, BackColor = Color.Transparent, Font = Theme.LabelFont };
        int pw = parent.Width;
        track.Left = 112; track.Top = y; track.Width = pw - 112 - 104; track.Height = 28;
        var value = new Label { Left = pw - 96, Top = y + 4, Width = 82, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Minor, BackColor = Color.Transparent, Font = Theme.ValueFont };
        track.Tag = value;
        _rowValue.Add(value);
        track.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _presetSeg.SelectedIndex = -1; _debounce.Stop(); _debounce.Start(); } };
        parent.Controls.Add(label); parent.Controls.Add(track); parent.Controls.Add(value);
    }

    private void BuildEq(Control parent)
    {
        string[] names = { "60", "170", "470", "1.2k", "2.4k", "4.7k", "10k", "14k" };
        int start = 50, step = 72, colW = 48;
        for (int i = 0; i < _eq.Length; i++)
        {
            int x = start + i * step;
            var freq = new Label { Text = names[i], Left = x, Top = 34, Width = colW, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Edge, BackColor = Color.Transparent, Font = Theme.SectionFont };
            var track = new LedSlider { Orientation = Orientation.Vertical, Minimum = -12, Maximum = 12, Segments = 13, Left = x, Top = 56, Width = colW, Height = 150 };
            var val = new Label { Text = "0", Left = x, Top = 210, Width = colW, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Minor, BackColor = Color.Transparent, Font = Theme.ValueFont };
            track.Tag = val;
            track.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _presetSeg.SelectedIndex = -1; _debounce.Stop(); _debounce.Start(); } };
            _eq[i] = track; _eqValue[i] = val;
            parent.Controls.Add(freq); parent.Controls.Add(track); parent.Controls.Add(val);
        }
    }

    // ---------------------------------------------------------------- remote
    private void BuildRemoteRoot()
    {
        _remoteRoot.Left = 3; _remoteRoot.Top = ContentTop; _remoteRoot.Width = W - 6; _remoteRoot.Height = H - ContentTop - 3;
        _remoteRoot.BackColor = Theme.Bg;
        _remoteRoot.Visible = false;

        var panel = new GlassPanel { Left = 14, Top = 6, Width = 652, Height = 616, Caption = "Remote mapping" };
        _remoteRoot.Controls.Add(panel);

        var help = new Label
        {
            Left = 14, Top = 34, Width = 624, Height = 34,
            Text = "Map the remote's free buttons. app / script / url / keys use the Value box. Click Learn, then press a remote button.",
            ForeColor = Theme.Edge, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8.5f),
        };
        panel.Controls.Add(help);

        _grid.Left = 14; _grid.Top = 72; _grid.Width = 624; _grid.Height = 420;
        _grid.AllowUserToAddRows = false;
        _grid.RowHeadersVisible = false;
        _grid.RowTemplate.Height = 20;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Remote button", ReadOnly = true, FillWeight = 130 });
        var combo = new DataGridViewComboBoxColumn { HeaderText = "Action", FillWeight = 210, FlatStyle = FlatStyle.Flat };
        combo.Items.AddRange(ActionItems);
        combo.DefaultCellStyle.BackColor = Color.FromArgb(0x0B, 0x09, 0x07);
        combo.DefaultCellStyle.ForeColor = Theme.Accent;
        combo.DefaultCellStyle.SelectionBackColor = Theme.AccentDark;
        combo.DefaultCellStyle.SelectionForeColor = Theme.Minor;
        _grid.Columns.Add(combo);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value (app / script / url / keys)", FillWeight = 240 });
        Theme.StyleGrid(_grid);
        _grid.EditingControlShowing += (_, e) =>
        {
            if (e.Control is ComboBox cb)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Color.FromArgb(0x0B, 0x09, 0x07);
                cb.ForeColor = Theme.Accent;
                cb.DrawMode = DrawMode.OwnerDrawFixed;
                cb.ItemHeight = 20;
                cb.DrawItem -= ComboDraw;
                cb.DrawItem += ComboDraw;
            }
        };
        panel.Controls.Add(_grid);

        var bar = new FlowLayoutPanel { Left = 14, Top = 504, Width = 624, Height = 36, BackColor = Color.Transparent };
        var btnLearn = Mk("Learn (press a button)", 150);
        var btnSave = Mk("Save", 66);
        var btnDefaults = Mk("Restore defaults", 108);
        var btnReload = Mk("Reload file", 86);
        var btnBrowse = Mk("Browse...", 78);
        var btnExit = Mk("Exit", 56);
        bar.Controls.AddRange(new Control[] { btnLearn, btnSave, btnDefaults, btnReload, btnBrowse, _chkRemote, btnExit });
        panel.Controls.Add(bar);

        _remoteStatus.Left = 14; _remoteStatus.Top = 548; _remoteStatus.Width = 624; _remoteStatus.Height = 52;
        _remoteStatus.ForeColor = Theme.Muted; _remoteStatus.BackColor = Color.Transparent; _remoteStatus.Font = Theme.ValueFont;
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

    private static Button Mk(string text, int width)
    {
        var b = new Button { Text = text, Width = width, Height = 26 };
        Theme.StyleButton(b);
        return b;
    }

    private static void ComboDraw(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || sender is not ComboBox cb) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(sel ? Theme.AccentDark : Color.FromArgb(0x0B, 0x09, 0x07));
        e.Graphics.FillRectangle(back, e.Bounds);
        TextRenderer.DrawText(e.Graphics, cb.Items[e.Index]?.ToString() ?? "", Theme.PillFont, e.Bounds,
            sel ? Theme.Minor : Theme.Accent, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
        _tray.Icon = Icon; _tray.Text = "ZCinema Sound"; _tray.Visible = true;
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
        try { _volTimer.Stop(); } catch { }
        try { _volume?.Dispose(); } catch { }
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
