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
    private readonly DeviceProfiles _deviceProfiles = new();
    private readonly ThemedDropDown _deviceCombo = new();
    private List<AudioEndpointDevice> _devices = new();
    private bool _deviceLoading;
    private AppSettings _settings = AppSettings.Load();
    private readonly System.Windows.Forms.Timer _deviceTimer = new() { Interval = 2000 };
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
    private bool _balloonShown;

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
            if (!_exiting) { e.Cancel = true; HideToTray(); }
        };

        LoadProfileIntoUi();
        _volume = AudioEndpointVolume.Open();
        PopulateDevices();
        SyncVolumeFromDevice();
        _volTimer.Tick += (_, _) => SyncVolumeFromDevice();
        _volTimer.Start();
        _deviceTimer.Tick += (_, _) => RefreshDevices();
        _deviceTimer.Start();
        _showTimer.Tick += (_, _) => { if (_showEvent.WaitOne(0)) ShowApp(); };
        _showTimer.Start();
        _reader.ButtonPressed += OnReaderButton;
        _reader.Start();
        if (_settings.StartMinimized) BeginInvoke(new Action(HideToTray));
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
        close.Click += (_, _) => HideToTray();

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

        var output = new GlassPanel { Left = 14, Top = 6, Width = 652, Height = 146, Caption = "Output" };
        RowVolume(output, 28);
        Row(output, "Ceiling", _preamp, 64);
        _chkMute.Left = 14; _chkMute.Top = 100; _chkMute.BackColor = Color.Transparent; _chkMute.FlatStyle = FlatStyle.Flat;
        _chkMute.CheckedChanged += (_, _) => { if (!_muting && _volume is not null) _volume.SetMute(_chkMute.Checked); };
        output.Controls.Add(_chkMute);
        var btnCal = new Button { Text = "Calibrate ceiling...", Left = 478, Top = 100, Width = 164, Height = 26 };
        Theme.StyleButton(btnCal);
        btnCal.Click += (_, _) => Calibrate();
        output.Controls.Add(btnCal);

        // device selector lives in the panel caption strip (no extra row needed)
        _deviceCombo.Left = 338; _deviceCombo.Top = 3; _deviceCombo.Width = 300; _deviceCombo.Height = 22;
        _deviceCombo.SelectedIndexChanged += (_, _) => { if (!_deviceLoading) SelectDevice(_deviceCombo.SelectedIndex); };
        output.Controls.Add(_deviceCombo);

        var tone = new GlassPanel { Left = 14, Top = 160, Width = 652, Height = 176, Caption = "Tone" };
        Row(tone, "Bass", _bass, 28);
        Row(tone, "Treble", _treble, 62);
        Row(tone, "Dialogue", _dialog, 96);
        Row(tone, "Width", _width, 130);

        var eq = new GlassPanel { Left = 14, Top = 344, Width = 652, Height = 240, Caption = "Equalizer" };
        BuildEq(eq);

        _soundRoot.Controls.Add(output);
        _soundRoot.Controls.Add(tone);
        _soundRoot.Controls.Add(eq);

        _presetSeg.Items = PresetNames;
        _presetSeg.SelectedIndex = -1; // show none until the user picks one
        _presetSeg.Left = 14; _presetSeg.Top = 592; _presetSeg.Width = 340; _presetSeg.Height = 34;
        _presetSeg.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _presetSeg.SelectedIndex < 0) return;
            ApplyPreset(PresetNames[_presetSeg.SelectedIndex]);
        };
        _soundRoot.Controls.Add(_presetSeg);

        var lblSave = new Label { Text = "Save:", Left = 362, Top = 600, Width = 40, ForeColor = Theme.Edge, BackColor = Color.Transparent };
        _soundRoot.Controls.Add(lblSave);
        for (int i = 1; i <= 3; i++)
        {
            int slot = i;
            var b = new Button { Text = slot.ToString(), Left = 406 + (i - 1) * 34, Top = 595, Width = 30, Height = 28 };
            Theme.StyleButton(b);
            b.Click += (_, _) => SaveCustomSlot(slot.ToString());
            _soundRoot.Controls.Add(b);
        }
        var lblLoad = new Label { Text = "Load:", Left = 512, Top = 600, Width = 36, ForeColor = Theme.Edge, BackColor = Color.Transparent };
        _soundRoot.Controls.Add(lblLoad);
        for (int i = 1; i <= 3; i++)
        {
            int slot = i;
            var b = new Button { Text = slot.ToString(), Left = 550 + (i - 1) * 34, Top = 595, Width = 30, Height = 28 };
            Theme.StyleButton(b);
            b.Click += (_, _) => LoadCustomSlot(slot.ToString());
            _soundRoot.Controls.Add(b);
        }

        _status.Left = 18; _status.Top = 630; _status.Width = 644; _status.Height = 44;
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
        Persist(p);
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

    private void LoadCustomSlot(string slot)
    {
        if (!ApplyCustomSlot(slot)) { _status.Text = $"Custom {slot} is empty."; return; }
        _status.Text = $"Loaded Custom {slot}";
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
            var freq = new Label { Text = names[i], Left = x, Top = 26, Height = 16, Width = colW, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Edge, BackColor = Color.Transparent, Font = Theme.SectionFont };
            var track = new LedSlider { Orientation = Orientation.Vertical, Minimum = -12, Maximum = 12, Segments = 13, Left = x + 5, Top = 44, Width = 38, Height = 160 };
            var val = new Label { Text = "0", Left = x, Top = 210, Width = colW, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Minor, BackColor = Color.Transparent, Font = Theme.ValueFont };
            track.Tag = val;
            track.ValueChanged += (_, _) => { if (!_loading) { UpdateLabels(); _presetSeg.SelectedIndex = -1; _debounce.Stop(); _debounce.Start(); } };
            _eq[i] = track; _eqValue[i] = val;
            parent.Controls.Add(freq); parent.Controls.Add(track); parent.Controls.Add(val);
            track.BringToFront(); val.BringToFront();
        }
    }

    // ---------------------------------------------------------------- remote
    private void BuildRemoteRoot()
    {
        _remoteRoot.Left = 3; _remoteRoot.Top = ContentTop; _remoteRoot.Width = W - 6; _remoteRoot.Height = H - ContentTop - 3;
        _remoteRoot.BackColor = Theme.Bg;
        _remoteRoot.Visible = false;

        var panel = new GlassPanel { Left = 14, Top = 6, Width = 652, Height = 652, Caption = "Remote mapping" };
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
        _grid.CellPainting += PaintComboCell;
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

        var bar = new FlowLayoutPanel { Left = 14, Top = 498, Width = 624, Height = 64, BackColor = Color.Transparent };
        var btnLearn = Mk("Learn (press a button)", 150);
        var btnSave = Mk("Save", 66);
        var btnDefaults = Mk("Restore defaults", 108);
        var btnReload = Mk("Reload file", 86);
        var btnBrowse = Mk("Browse...", 78);
        var btnExport = Mk("Export...", 80);
        var btnImport = Mk("Import...", 80);
        var btnExit = Mk("Exit", 56);
        bar.Controls.AddRange(new Control[] { btnLearn, btnSave, btnDefaults, btnReload, btnBrowse, btnExport, btnImport, _chkRemote, btnExit });
        panel.Controls.Add(bar);

        _remoteStatus.Left = 14; _remoteStatus.Top = 566; _remoteStatus.Width = 624; _remoteStatus.Height = 76;
        _remoteStatus.ForeColor = Theme.Muted; _remoteStatus.BackColor = Color.Transparent; _remoteStatus.Font = Theme.ValueFont;
        panel.Controls.Add(_remoteStatus);

        btnLearn.Click += (_, _) => { _learn = true; _remoteStatus.Text = "Listening... press a remote button now."; };
        btnSave.Click += (_, _) => SaveRemoteGrid();
        btnDefaults.Click += (_, _) => { RemoteMap.Save(RemoteMap.Default()); LoadRemoteGrid(); _remoteStatus.Text = "Defaults restored."; };
        btnReload.Click += (_, _) => { LoadRemoteGrid(); _remoteStatus.Text = "Reloaded from file."; };
        btnExit.Click += (_, _) => ExitApp();
        btnBrowse.Click += (_, _) => BrowseIntoSelectedRow();
        btnExport.Click += (_, _) => ExportRemote();
        btnImport.Click += (_, _) => ImportRemote();
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

    /// <summary>Repaint combo cells with a themed button (the default one is white).</summary>
    private void PaintComboCell(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex] is not DataGridViewComboBoxColumn) return;
        if (e.Graphics is null) return;

        e.PaintBackground(e.CellBounds, true);
        var r = e.CellBounds;
        string text = e.FormattedValue?.ToString() ?? "";
        TextRenderer.DrawText(e.Graphics, text, Theme.ButtonFont,
            new Rectangle(r.X + 5, r.Y, r.Width - 26, r.Height), Theme.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var btn = new Rectangle(r.Right - 21, r.Y + 2, 19, Math.Max(10, r.Height - 3));
        using (var b = new SolidBrush(Theme.AccentDark)) e.Graphics.FillRectangle(b, btn);
        TextRenderer.DrawText(e.Graphics, "\u25BC", Theme.ButtonFont, btn, Theme.Minor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.Handled = true;
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

    private Dictionary<string, string> BuildMapFromGrid(out List<string> warns)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        warns = new List<string>();
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
        return map;
    }

    private void SaveRemoteGrid()
    {
        var map = BuildMapFromGrid(out var warns);
        RemoteMap.Save(map);
        _remoteStatus.Text = warns.Count > 0 ? "Saved with notes: " + string.Join("; ", warns) : "Saved to " + RemoteMap.MapPath();
    }

    private void ExportRemote()
    {
        var map = BuildMapFromGrid(out _);
        using var dlg = new SaveFileDialog
        {
            Title = "Export remote mapping",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = "zcinema-remote.json",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try { RemoteMap.ExportTo(dlg.FileName, map); _remoteStatus.Text = "Exported " + Path.GetFileName(dlg.FileName); }
        catch (Exception ex) { _remoteStatus.Text = "Export failed: " + ex.Message; }
    }

    private void ImportRemote()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import remote mapping",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            var map = RemoteMap.ImportFrom(dlg.FileName);
            RemoteMap.Save(map);
            LoadRemoteGrid();
            _remoteStatus.Text = $"Imported {map.Count} mappings from {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex) { _remoteStatus.Text = "Import failed: " + ex.Message; }
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

    private void HideToTray()
    {
        Hide(); ShowInTaskbar = false;
        if (!_balloonShown)
        {
            _tray.ShowBalloonTip(3000, "ZCinema Sound", "Still running in the tray - remote mappings stay active.", ToolTipIcon.Info);
            _balloonShown = true;
        }
    }

    // --------------------------------------------------------------- tray
    private void BuildTray()
    {
        _tray.Icon = Icon; _tray.Text = "ZCinema Sound"; _tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open control panel", null, (_, _) => ShowApp());

        var presets = new ToolStripMenuItem("Presets");
        foreach (var name in PresetNames)
        {
            string n = name;
            presets.DropDownItems.Add(n, null, (_, _) => { ApplyPreset(n); _status.Text = $"Preset: {n}"; });
        }
        presets.DropDownItems.Add(new ToolStripSeparator());
        for (int i = 1; i <= 3; i++)
        {
            int slot = i;
            presets.DropDownItems.Add("Custom " + slot, null, (_, _) =>
            {
                if (ApplyCustomSlot(slot.ToString())) _status.Text = $"Loaded Custom {slot}";
                else _status.Text = $"Custom {slot} is empty.";
            });
        }
        menu.Items.Add(presets);

        menu.Items.Add("Install / update profile", null, (_, _) => RunElevated("install"));
        menu.Items.Add("Bypass processing", null, (_, _) => ActionRunner.Run("bypass", this));
        menu.Items.Add("Open Equalizer APO Device Selector", null, (_, _) =>
        {
            var ds = EqualizerApo.DeviceSelectorPath();
            if (ds is not null) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ds) { UseShellExecute = true, Verb = "runas" }); } catch { } }
        });
        menu.Items.Add(new ToolStripSeparator());

        var miRemote = new ToolStripMenuItem("Remote mapping") { CheckOnClick = true, Checked = true };
        miRemote.CheckedChanged += (_, _) => { _remoteEnabled = miRemote.Checked; _chkRemote.Checked = miRemote.Checked; };
        menu.Items.Add(miRemote);
        var miAuto = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = GetAutostart() };
        miAuto.CheckedChanged += (_, _) => SetAutostart(miAuto.Checked);
        menu.Items.Add(miAuto);
        var miMin = new ToolStripMenuItem("Start minimized") { CheckOnClick = true, Checked = _settings.StartMinimized };
        miMin.CheckedChanged += (_, _) => { _settings.StartMinimized = miMin.Checked; _settings.Save(); };
        menu.Items.Add(miMin);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Back up settings...", null, (_, _) => ExportBackup());
        menu.Items.Add("Restore settings...", null, (_, _) => ImportBackup());
        menu.Items.Add("Open data folder", null, (_, _) => OpenDataFolder());
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Uninstall", null, (_, _) => RunElevated("uninstall"));
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowApp();
    }

    private void ExportBackup()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Back up ZCinema Sound settings",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = "zcinema-backup.json",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try { Backup.ExportTo(dlg.FileName); Info("Backup saved to\n" + dlg.FileName); }
        catch (Exception ex) { Info("Backup failed: " + ex.Message); }
    }

    private void ImportBackup()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Restore ZCinema Sound settings",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            Backup.ImportFrom(dlg.FileName).Apply();
            _settings = AppSettings.Load();
            LoadRemoteGrid();
            PopulateDevices();
            Info("Restored from\n" + dlg.FileName);
        }
        catch (Exception ex) { Info("Restore failed: " + ex.Message); }
    }

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(RemoteMap.Dir());
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + RemoteMap.Dir() + "\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void ShowAbout()
    {
        var ver = typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "?";
        Info($"ZCinema Sound {ver}\n\nEqualizer APO does the audio processing (GPLv2, not bundled).\n" +
             $"Remote mappings, per-device profiles and backups live in:\n{RemoteMap.Dir()}\n\n" +
             "https://github.com/Hydraxon91/zcinema-sound");
    }

    private void Info(string text) => MessageBox.Show(text, "ZCinema Sound", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static void RunElevated(string arg)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, arg)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
        }
        catch { /* user declined UAC */ }
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
            Persist(FromUi());
            _status.Text = $"Saved {DateTime.Now:HH:mm:ss}  ->  {_volume?.Name}";
        }
        catch (Exception ex) { _status.Text = "Write failed: " + ex.Message; }
    }

    /// <summary>Write the profile to Equalizer APO and remember it for the active device.</summary>
    private void Persist(ZCinemaProfile p)
    {
        EqualizerApo.SaveProfile(p);
        if (_volume is not null && _volume.EndpointGuid.Length > 0)
            _deviceProfiles.Save(_volume.EndpointGuid, p);
    }

    // ------------------------------------------------------ device selection
    private void PopulateDevices()
    {
        _devices = AudioEndpointVolume.ListRenderDevices().ToList();
        int idx = -1;
        if (!string.IsNullOrEmpty(_settings.ActiveDeviceGuid))
            idx = _devices.FindIndex(d => string.Equals(d.Guid, _settings.ActiveDeviceGuid, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 && _volume is not null)
            idx = _devices.FindIndex(d => string.Equals(d.Guid, _volume.EndpointGuid, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 && _devices.Count > 0) idx = 0;
        _deviceLoading = true;
        _deviceCombo.SetItems(_devices.Select(d => d.Name), idx);
        _deviceLoading = false;
        if (idx >= 0) SelectDevice(idx);
    }

    private void SelectDevice(int index)
    {
        if (index < 0 || index >= _devices.Count) return;
        var d = _devices[index];
        if (_volume is null || !string.Equals(_volume.EndpointGuid, d.Guid, StringComparison.OrdinalIgnoreCase))
        {
            try { _volume?.Dispose(); } catch { }
            _volume = AudioEndpointVolume.OpenById(d.Id);
        }
        SyncVolumeFromDevice();
        _settings.ActiveDeviceGuid = d.Guid;
        _settings.Save();
        ApplyDeviceProfile();
    }

    /// <summary>Load this device's stored profile, or adopt the current settings as its first one.</summary>
    private void ApplyDeviceProfile()
    {
        var guid = _volume?.EndpointGuid ?? "";
        if (guid.Length == 0) return;
        var stored = _deviceProfiles.Load(guid);
        if (stored is not null)
        {
            EqualizerApo.SaveProfile(stored);
            ApplyToUi(stored);
            _status.Text = $"Profile for {_volume!.Name}";
        }
        else
        {
            var current = EqualizerApo.LoadProfile();
            _deviceProfiles.Save(guid, current);
            ApplyToUi(current);
            _status.Text = $"New per-device profile for {_volume!.Name}";
        }
        UpdateAttachmentStatus();
    }

    /// <summary>Warn (in the status line) when APO isn't attached to the selected device.</summary>
    private void UpdateAttachmentStatus()
    {
        if (_volume is null) return;
        if (!EqualizerApo.IsAttached(_volume.EndpointGuid))
        {
            _status.Text = $"Equalizer APO isn't attached to {_volume.Name} - open its Device Selector, then reboot.";
            _status.ForeColor = Theme.Accent;
        }
    }

    /// <summary>Re-scan endpoints; refresh the selector and recover if the current device vanished.</summary>
    private void RefreshDevices()
    {
        var current = AudioEndpointVolume.ListRenderDevices();
        var newGuids = current.Select(d => d.Guid).OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToArray();
        var oldGuids = _devices.Select(d => d.Guid).OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToArray();
        if (newGuids.SequenceEqual(oldGuids)) return;

        string wanted = _volume?.EndpointGuid ?? "";
        _devices = current.ToList();
        int idx = _devices.FindIndex(d => string.Equals(d.Guid, wanted, StringComparison.OrdinalIgnoreCase));
        bool vanished = idx < 0;
        if (idx < 0) idx = _devices.FindIndex(d => d.Name.Contains("Z Cin", StringComparison.OrdinalIgnoreCase));
        if (idx < 0 && _devices.Count > 0) idx = 0;

        _deviceLoading = true;
        _deviceCombo.SetItems(_devices.Select(d => d.Name), idx);
        _deviceLoading = false;
        if (idx >= 0 && vanished) SelectDevice(idx);
    }

    /// <summary>The active device's profile (falls back to the Equalizer APO file).</summary>
    private ZCinemaProfile CurrentDeviceProfile()
    {
        var guid = _volume?.EndpointGuid ?? "";
        var p = guid.Length > 0 ? _deviceProfiles.Load(guid) : null;
        return p ?? EqualizerApo.LoadProfile();
    }

    private void ApplyPreset(string name)
    {
        var p = CurrentDeviceProfile();
        Presets.Apply(p, name);
        Persist(p);
        ApplyToUi(p);
    }

    private bool ApplyCustomSlot(string slot)
    {
        var slots = RemoteMap.ReadCustomSlots();
        if (!slots.TryGetValue(slot, out var c)) return false;
        var p = CurrentDeviceProfile();
        p.PreampDb = c.Preamp; p.BassGain = c.Bass; p.SubGain = Math.Round(c.Bass * 0.6, 1);
        p.TrebleGain = c.Treble; p.DialogGain = c.Dialog; p.Width = c.Width / 100.0; p.EqGains = c.Eq;
        Persist(p);
        ApplyToUi(p);
        return true;
    }

    private static Icon LoadAppIcon()
    {
        // 1) assets\ next to the repo during development
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "ZCinemaSound.ico");
            if (File.Exists(candidate)) { try { return new Icon(candidate); } catch { } }
        }
        // 2) embedded resource (works when published standalone)
        try
        {
            using var s = typeof(MainForm).Assembly.GetManifestResourceStream("ZCinemaSound.ico");
            if (s is not null) return new Icon(s);
        }
        catch { }
        return SystemIcons.Application;
    }
}
