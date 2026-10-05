# ZCinema-GUI.ps1 - the Z Cinema control panel (Sound + Remote tabs).
# Hosts the remote bridge in-process and lives in the notification tray:
# closing the window hides to tray; Exit (tray menu or Remote tab) quits.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\ZCinema-GUI.ps1
#   powershell -ExecutionPolicy Bypass -File .\tools\ZCinema-GUI.ps1 -Tray   # start hidden (autostart)
#
# Equalizer APO hot-reloads the profile, so changes are heard immediately.
[CmdletBinding()]
param(
    [switch]$Tray   # start hidden in the tray (used by autostart)
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $repoRoot "src\lib\ZCinema.Common.psm1") -Force
Import-Module (Join-Path $repoRoot "src\lib\ZCinema.Remote.psm1") -Force
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$apo = Get-EqualizerApoRoot
if (-not $apo) { [System.Windows.Forms.MessageBox]::Show("Equalizer APO not found. Install it first."); exit 1 }
$profilePath = Join-Path $apo "config\ZCinema.txt"
if (-not (Test-Path $profilePath)) {
    [System.Windows.Forms.MessageBox]::Show("Profile not installed:`n$profilePath`n`nRun src\Install-ZCinema.ps1 first.")
    exit 1
}

# If we cannot write the profile, relaunch elevated once (UAC).
try { $fs = [System.IO.File]::Open($profilePath, 'Open', 'ReadWrite'); $fs.Close() }
catch {
    Start-Process powershell -Verb RunAs -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    exit
}

$p = Get-ZCinemaProfileParams -ProfilePath $profilePath

# Is the profile actually enabled in config.txt?
$configTxt = Join-Path $apo "config\config.txt"
$cfgText = if (Test-Path $configTxt) { Get-Content -LiteralPath $configTxt -Raw } else { "" }
$enabled = ($cfgText -match '(?m)^\s*Include:\s*ZCinema\.txt')

# ---- single instance: focus the running app, or stand down ----
if (-not (Lock-ZCinemaMutex -Name "App")) {
    try { [System.Threading.EventWaitHandle]::OpenExisting("Local\ZCinema_Show").Set() | Out-Null } catch { }
    exit
}
$bridgeAlreadyRunning = Test-ZCinemaMutex -Name "Bridge"

# ---- helpers ---------------------------------------------------------------
$script:dirty = $false
function Save-Now {
    try {
        $text = New-ZCinemaProfileText `
            -PreampDb $tbPreamp.Value -BassGain $tbBass.Value -SubGain ([math]::Round($tbBass.Value * 0.6, 1)) `
            -TrebleGain $tbTreble.Value -DialogGain $tbDialog.Value -Width ($tbWidth.Value / 100.0) `
            -EqGains @($eqSliders | ForEach-Object { [double]$_.Value })
        Save-ZCinemaProfile -ProfilePath $profilePath -Text $text
        $lblStatus.Text = "Saved " + (Get-Date -Format HH:mm:ss) + "  ->  " + $profilePath
        $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
    } catch {
        $lblStatus.Text = "Write failed: " + $_.Exception.Message
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
    }
}

$debounce = New-Object System.Windows.Forms.Timer
$debounce.Interval = 300
$debounce.Add_Tick({ $debounce.Stop(); Save-Now })

function Touch {
    $lblPreampV.Text = "{0} dB" -f $tbPreamp.Value
    $lblBassV.Text   = "{0} dB" -f $tbBass.Value
    $lblTrebleV.Text = "{0} dB" -f $tbTreble.Value
    $lblDialogV.Text = "{0} dB" -f $tbDialog.Value
    $lblWidthV.Text  = "{0}%" -f $tbWidth.Value
    for ($i = 0; $i -lt $eqSliders.Count; $i++) { $eqValueLabels[$i].Text = "{0}" -f $eqSliders[$i].Value }
    $debounce.Stop(); $debounce.Start()
}

function New-Row([string]$name, [int]$y, [int]$min, [int]$max) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $name; $l.Left = 12; $l.Top = $y + 6; $l.Width = 90
    $t = New-Object System.Windows.Forms.TrackBar
    $t.Left = 108; $t.Top = $y; $t.Width = 600; $t.Minimum = $min; $t.Maximum = $max; $t.TickFrequency = [math]::Max(1, [int](($max - $min) / 12))
    $v = New-Object System.Windows.Forms.Label
    $v.Left = 714; $v.Top = $y + 6; $v.Width = 70; $v.TextAlign = 'MiddleRight'
    $form.Controls.AddRange(@($l, $t, $v))
    return @{ Track = $t; Value = $v }
}

# ---- form ------------------------------------------------------------------
$form = New-Object System.Windows.Forms.Form
$form.Text = "Z Cinema Sound Control"
$form.ClientSize = New-Object System.Drawing.Size(800, 748)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedSingle"
$form.MaximizeBox = $false

$r = New-Row "Ceiling" 10 -60 0;  $tbPreamp = $r.Track; $lblPreampV = $r.Value
$r = New-Row "Bass"    56 -12 12; $tbBass   = $r.Track; $lblBassV   = $r.Value
$r = New-Row "Treble"  102 -12 12; $tbTreble = $r.Track; $lblTrebleV = $r.Value
$r = New-Row "Dialogue" 148 -9 9;  $tbDialog = $r.Track; $lblDialogV = $r.Value
$r = New-Row "Width"   194 0 30;  $tbWidth  = $r.Track; $lblWidthV  = $r.Value

$lblEq = New-Object System.Windows.Forms.Label
$lblEq.Text = "Graphic EQ (dB)"; $lblEq.Left = 12; $lblEq.Top = 244; $lblEq.Width = 200
$lblEq.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($lblEq)

$eqSliders = @(); $eqValueLabels = @(); $eqFreqLabels = @()
$eqFreqs = @(60, 170, 470, 1200, 2400, 4700, 10000, 14000)
$eqLabels = @("60", "170", "470", "1.2k", "2.4k", "4.7k", "10k", "14k")
for ($i = 0; $i -lt 8; $i++) {
    $x = 40 + $i * 92
    $f = New-Object System.Windows.Forms.Label
    $f.Text = $eqLabels[$i]; $f.Left = $x; $f.Top = 272; $f.Width = 60; $f.TextAlign = 'MiddleCenter'
    $t = New-Object System.Windows.Forms.TrackBar
    $t.Orientation = 'Vertical'; $t.Left = $x; $t.Top = 296; $t.Height = 220; $t.Width = 60
    $t.Minimum = -12; $t.Maximum = 12; $t.TickFrequency = 4
    $t.RightToLeftLayout = $true   # put +12 at the top
    $t.Add_ValueChanged({ Touch })
    $v = New-Object System.Windows.Forms.Label
    $v.Text = "0"; $v.Left = $x; $v.Top = 520; $v.Width = 60; $v.TextAlign = 'MiddleCenter'
    $form.Controls.AddRange(@($f, $t, $v))
    $eqSliders += $t; $eqValueLabels += $v; $eqFreqLabels += $f
}

# ---- built-in presets (each also sets the EQ) -------------------------------
$presets = [ordered]@{
    "Flat"    = @{ Bass = 0;  Treble = 0; Dialog = 0;  Width = 0;  Eq = @(0, 0, 0, 0, 0, 0, 0, 0) }
    "Music"   = @{ Bass = 6;  Treble = 4; Dialog = 0;  Width = 10; Eq = @(3, 2, 1, 0, 1, 2, 3, 3) }
    "Movies"  = @{ Bass = 4;  Treble = 2; Dialog = 5;  Width = 20; Eq = @(2, 1, 0, 1, 2, 3, 2, 1) }
    "Night"   = @{ Bass = -2; Treble = 2; Dialog = 3;  Width = 10; Eq = @(-3, -2, 0, 0, 1, 0, 0, 0) }
    "Vocal"   = @{ Bass = -2; Treble = 2; Dialog = 7;  Width = 5;  Eq = @(-2, -1, 1, 2, 3, 2, 1, 0) }
    "V-Shape" = @{ Bass = 8;  Treble = 6; Dialog = -2; Width = 15; Eq = @(5, 3, 0, -2, -1, 2, 4, 5) }
}

function Apply-State($preamp, $bass, $treble, $dialog, $widthPct, $eq) {
    $tbPreamp.Value = [int]$preamp; $tbBass.Value = [int]$bass; $tbTreble.Value = [int]$treble
    $tbDialog.Value = [int]$dialog; $tbWidth.Value = [int]$widthPct
    for ($i = 0; $i -lt 8; $i++) { $eqSliders[$i].Value = [int]$eq[$i] }
    Touch; $debounce.Stop(); Save-Now
}

$tooltip = New-Object System.Windows.Forms.ToolTip
$y1 = 548; $y2 = 586; $bw = 86
$i = 0
foreach ($name in $presets.Keys) {
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $name; $b.Tag = $name; $b.Left = 12 + $i * $bw; $b.Top = $y1; $b.Width = $bw - 4; $b.Height = 26
    $b.Add_Click({
        param($sender, $ev)
        $pr = $presets[$sender.Tag]
        Apply-State $tbPreamp.Value $pr.Bass $pr.Treble $pr.Dialog $pr.Width $pr.Eq
    })
    $form.Controls.Add($b); $i++
}

# ---- custom presets, saved to %APPDATA%\ZCinemaSound\presets.json -----------
$customPath = Join-Path $env:APPDATA "ZCinemaSound\presets.json"
$script:custom = @{}
if (Test-Path $customPath) {
    try {
        $j = Get-Content -LiteralPath $customPath -Raw | ConvertFrom-Json
        foreach ($n in @("1", "2", "3")) { if ($null -ne $j.$n) { $script:custom[$n] = $j.$n } }
    } catch { }
}

function Save-Custom([string]$slot) {
    $state = [ordered]@{
        Preamp = [int]$tbPreamp.Value; Bass = [int]$tbBass.Value; Treble = [int]$tbTreble.Value
        Dialog = [int]$tbDialog.Value; Width = [int]$tbWidth.Value
        Eq     = @($eqSliders | ForEach-Object { [int]$_.Value })
    }
    $script:custom[$slot] = $state
    $dir = Split-Path -Parent $customPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    ($script:custom | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $customPath -Encoding UTF8
    $lblStatus.Text = "Saved current sliders to Custom $slot  ($customPath)"
    $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
}

function Load-Custom([string]$slot) {
    $c = $script:custom[$slot]
    if ($null -eq $c) {
        $lblStatus.Text = "Custom $slot is empty - right-click the '$slot' button to save the current sliders."
        $lblStatus.ForeColor = [System.Drawing.Color]::Firebrick
        return
    }
    Apply-State $c.Preamp $c.Bass $c.Treble $c.Dialog $c.Width @($c.Eq)
    $lblStatus.Text = "Loaded Custom $slot"
    $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
}

$lblCustom = New-Object System.Windows.Forms.Label
$lblCustom.Text = "Custom:"; $lblCustom.Left = 12; $lblCustom.Top = $y2 + 4; $lblCustom.Width = 70
$form.Controls.Add($lblCustom)
$ci = 0
foreach ($slot in @("1", "2", "3")) {
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $slot; $b.Tag = $slot; $b.Left = 84 + $ci * 54; $b.Top = $y2; $b.Width = 46; $b.Height = 26
    $b.Add_Click({ param($sender, $ev) Load-Custom $sender.Tag })
    $b.Add_MouseUp({ param($sender, $ev) if ($ev.Button -eq [System.Windows.Forms.MouseButtons]::Right) { Save-Custom $sender.Tag } })
    $tooltip.SetToolTip($b, "Left-click: load. Right-click: save the current sliders.")
    $form.Controls.Add($b); $ci++
}

$lblCustomHint = New-Object System.Windows.Forms.Label
$lblCustomHint.Text = "click = load,  right-click = save current sliders"
$lblCustomHint.Left = 256; $lblCustomHint.Top = $y2 + 4; $lblCustomHint.Width = 340
$lblCustomHint.ForeColor = [System.Drawing.Color]::Gray
$form.Controls.Add($lblCustomHint)

$btnOpen = New-Object System.Windows.Forms.Button
$btnOpen.Text = "Open config folder"; $btnOpen.Left = 620; $btnOpen.Top = $y2; $btnOpen.Width = 164; $btnOpen.Height = 26
$btnOpen.Add_Click({ Start-Process explorer.exe (Split-Path -Parent $profilePath) })
$form.Controls.Add($btnOpen)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Left = 12; $lblStatus.Top = 622; $lblStatus.Width = 772; $lblStatus.Height = 40
$form.Controls.Add($lblStatus)

$lblHint = New-Object System.Windows.Forms.Label
$lblHint.Left = 12; $lblHint.Top = 662; $lblHint.Width = 772
$lblHint.Text = "Bass also lifts the sub. Width = stereo widening (0 = mono-safe). Ceiling = volume cap. Values save automatically."
$lblHint.ForeColor = [System.Drawing.Color]::Gray
$form.Controls.Add($lblHint)

foreach ($t in @($tbPreamp, $tbBass, $tbTreble, $tbDialog, $tbWidth)) { $t.Add_ValueChanged({ Touch }) }

# initialise from the current profile
$eqInit = @($p.EqGains)
Apply-State $p.PreampDb $p.BassGain $p.TrebleGain $p.DialogGain ([int]($p.Width * 100)) $eqInit
$lblStatus.Text = if ($enabled) { "Loaded from " + $profilePath } else { "Profile is NOT enabled: config.txt has no 'Include: ZCinema.txt'. Run src\Install-ZCinema.ps1 (or Bypass-ZCinema.ps1 -Restore)." }
$lblStatus.ForeColor = if ($enabled) { [System.Drawing.Color]::DimGray } else { [System.Drawing.Color]::Firebrick }

# ---- tabs: move the sound controls into a 'Sound' tab, add a 'Remote' tab ----
$tabs = New-Object System.Windows.Forms.TabControl
$tabs.Dock = 'Fill'
$tabSound = New-Object System.Windows.Forms.TabPage
$tabSound.Text = 'Sound'
$tabRemote = New-Object System.Windows.Forms.TabPage
$tabRemote.Text = 'Remote'
[void]$tabs.TabPages.Add($tabSound)
[void]$tabs.TabPages.Add($tabRemote)

$existing = @($form.Controls)
$form.Controls.Clear()
foreach ($c in $existing) { $tabSound.Controls.Add($c) }
$form.Controls.Add($tabs)
$form.ClientSize = New-Object System.Drawing.Size(800, 748)

# ---- Remote tab ----
$remoteHelp = New-Object System.Windows.Forms.Label
$remoteHelp.Text = 'Map the remote free buttons below. Pick an Action; fill Value for app/script/url/keys/custom. Click Learn, then press a remote button.'
$remoteHelp.Left = 8; $remoteHelp.Top = 6; $remoteHelp.Width = 780; $remoteHelp.Height = 28

$grid = New-Object System.Windows.Forms.DataGridView
$grid.Left = 8; $grid.Top = 38; $grid.Width = 780; $grid.Height = 428
$grid.AllowUserToAddRows = $false
$grid.RowHeadersVisible = $false
$grid.AutoSizeColumnsMode = 'Fill'
$grid.EditMode = 'EditOnEnter'
$grid.SelectionMode = 'FullRowSelect'

$colBtn = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colBtn.HeaderText = 'Remote button'; $colBtn.ReadOnly = $true; $colBtn.FillWeight = 130
$colAct = New-Object System.Windows.Forms.DataGridViewComboBoxColumn
$colAct.HeaderText = 'Action'; $colAct.FillWeight = 200
$colVal = New-Object System.Windows.Forms.DataGridViewTextBoxColumn
$colVal.HeaderText = 'Value (app / script / url / keys / custom)'; $colVal.FillWeight = 240
[void]$grid.Columns.Add($colBtn)
[void]$grid.Columns.Add($colAct)
[void]$grid.Columns.Add($colVal)

$actionItems = @(
    'none', 'gui', 'bypass',
    'preset:Flat', 'preset:Music', 'preset:Movies', 'preset:Night', 'preset:Vocal', 'preset:V-Shape',
    'custom:1', 'custom:2', 'custom:3',
    'sound:dialogue+', 'sound:dialogue-', 'sound:width+', 'sound:width-', 'sound:ceiling+', 'sound:ceiling-',
    'media:playpause', 'media:next', 'media:prev', 'media:stop', 'media:volup', 'media:voldown', 'media:mute',
    'app', 'script', 'url', 'keys'
)
foreach ($a in $actionItems) { [void]$colAct.Items.Add($a) }

function Set-RemoteGrid {
    $map = Read-ZCinemaRemoteMap
    $grid.Rows.Clear()
    foreach ($b in (Get-ZCinemaRemoteCatalog).Name) {
        $act = 'none'; $val = ''
        if ($map.ContainsKey($b) -and $map[$b]) {
            $a = [string]$map[$b]
            if ($a -match '^(app|script|url|keys):(.+)$') { $act = $Matches[1]; $val = $Matches[2] }
            elseif ($actionItems -contains $a) { $act = $a }
        }
        [void]$grid.Rows.Add($b, $act, $val)
    }
}

$btnLearn    = New-Object System.Windows.Forms.Button; $btnLearn.Text = 'Learn (press a button)'; $btnLearn.Left = 8;   $btnLearn.Top = 472; $btnLearn.Width = 150
$btnSave     = New-Object System.Windows.Forms.Button; $btnSave.Text = 'Save';                    $btnSave.Left = 162;  $btnSave.Top = 472; $btnSave.Width = 70
$btnDefaults = New-Object System.Windows.Forms.Button; $btnDefaults.Text = 'Restore defaults';     $btnDefaults.Left = 236; $btnDefaults.Top = 472; $btnDefaults.Width = 110
$btnReload   = New-Object System.Windows.Forms.Button; $btnReload.Text = 'Reload file';            $btnReload.Left = 350; $btnReload.Top = 472; $btnReload.Width = 80
$btnBrowse   = New-Object System.Windows.Forms.Button; $btnBrowse.Text = 'Browse...';              $btnBrowse.Left = 434; $btnBrowse.Top = 472; $btnBrowse.Width = 90
$chkRemote   = New-Object System.Windows.Forms.CheckBox; $chkRemote.Text = 'Remote on';            $chkRemote.Left = 532; $chkRemote.Top = 474; $chkRemote.Width = 110; $chkRemote.Checked = $true
$btnExit     = New-Object System.Windows.Forms.Button; $btnExit.Text = 'Exit';                     $btnExit.Left = 690; $btnExit.Top = 472; $btnExit.Width = 90
$remoteStatus = New-Object System.Windows.Forms.Label; $remoteStatus.Left = 8; $remoteStatus.Top = 504; $remoteStatus.Width = 780; $remoteStatus.Height = 30

$remoteRef = New-Object System.Windows.Forms.Label
$remoteRef.Left = 8; $remoteRef.Top = 538; $remoteRef.Width = 780; $remoteRef.Height = 170
$remoteRef.Font = New-Object System.Drawing.Font("Consolas", 8.5)
$remoteRef.Text = @'
How to map (choose Action, then fill Value where needed):

  app     open a program or a file     Value: C:\Windows\System32\notepad.exe
                                              D:\Videos\clip.mp4
  url     open a web link              Value: https://example.com
  script  run a PowerShell .ps1        Value: C:\tools\myscript.ps1
  keys    send keystrokes (SendKeys)   Value: {PRTSC} = Print Screen
                                              ^c = Ctrl+C   %{F4} = Alt+F4   {ENTER} = Enter
  preset:X   load a sound preset       e.g. preset:Movies   preset:Music
  custom:1/2/3  load a GUI custom slot  (fixed choices)
  sound:X    adjust live               sound:dialogue+  sound:width-  sound:ceiling-
  bypass     toggle processing off/on   gui   open the control panel

Tips:
  - For a web link choose Action url (not custom); for a program/file choose app.
  - No conversion needed: a button is "pressed" when you press it on the remote.
  - Browse... fills Value for the selected row (program, script or file).
  - Print Screen: use keys {PRTSC}; or app  ms-screenclip:  opens the Snipping Tool overlay.
  - Save writes %APPDATA%\ZCinemaSound\remote.json. Remote mappings run while this app is
    open (it stays in the tray); the standalone bridge (ZCinema.bat option 10) is for headless use.
'@

$tabRemote.Controls.AddRange(@($remoteHelp, $grid, $btnLearn, $btnSave, $btnDefaults, $btnReload, $btnBrowse, $chkRemote, $btnExit, $remoteStatus, $remoteRef))

$nativeNames = @('Play', 'Pause', 'Stop', 'Skip', 'Replay', 'Rewind', 'Forward', 'Mute', 'Back')

$btnLearn.Add_Click({
    $script:learnMode = $true
    $remoteStatus.Text = 'Listening... press a remote button now.'
    $remoteStatus.ForeColor = [System.Drawing.Color]::DarkOrange
})

$btnSave.Add_Click({
    $m = [ordered]@{}; $warn = New-Object System.Collections.Generic.List[string]
    foreach ($r in $grid.Rows) {
        $b = $r.Cells[0].Value; $a = [string]$r.Cells[1].Value; $v = [string]$r.Cells[2].Value
        if (-not $b) { continue }
        if ($a -in @('app', 'script', 'url', 'keys')) {
            if (-not $v) { $warn.Add("$b needs a Value for $a") }
            elseif ($a -eq 'url' -and $v -notmatch '^[a-zA-Z]+://') { $warn.Add("$b value '$v' is not a link (use url)") }
            elseif (($a -eq 'app' -or $a -eq 'script') -and -not (Test-Path -LiteralPath $v)) { $warn.Add("$b path not found") }
            $m[$b] = if ($v) { "${a}:${v}" } else { 'none' }
        }
        elseif ($a) { $m[$b] = $a }
    }
    Save-ZCinemaRemoteMap -Map $m
    if ($warn.Count) { $remoteStatus.Text = 'Saved with notes: ' + ($warn -join '; '); $remoteStatus.ForeColor = [System.Drawing.Color]::DarkOrange }
    else { $remoteStatus.Text = "Saved to $(Get-ZCinemaRemoteMapPath)"; $remoteStatus.ForeColor = [System.Drawing.Color]::DarkGreen }
})

$btnDefaults.Add_Click({
    Save-ZCinemaRemoteMap -Map (Get-ZCinemaDefaultRemoteMap)
    Set-RemoteGrid
    $remoteStatus.Text = 'Defaults restored (Preset 1-4 to presets).'
    $remoteStatus.ForeColor = [System.Drawing.Color]::DarkGreen
})
$btnReload.Add_Click({ Set-RemoteGrid; $remoteStatus.Text = 'Reloaded from file.' })

$btnBrowse.Add_Click({
    if ($grid.SelectedRows.Count -eq 0) {
        $remoteStatus.Text = 'Select a remote button row first, then Browse.'
        $remoteStatus.ForeColor = [System.Drawing.Color]::DarkOrange
        return
    }
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Title = 'Pick a program, script or file'
    $dlg.Filter = 'Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|PowerShell (*.ps1)|*.ps1|All files (*.*)|*.*'
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $row = $grid.SelectedRows[0]
        $path = $dlg.FileName
        $row.Cells[2].Value = $path
        if ($path -like '*.ps1') { $row.Cells[1].Value = 'script' } else { $row.Cells[1].Value = 'app' }
        $remoteStatus.Text = "Value set for $($row.Cells[0].Value)."
        $remoteStatus.ForeColor = [System.Drawing.Color]::DarkGreen
    }
})

Set-RemoteGrid

# ---- in-process remote bridge (this app is the bridge) ----------------------
$script:remoteEnabled = $true
$script:learnMode = $false
$script:debounceNames = @{}
Start-ZCinemaHidReader

$remoteTimer = New-Object System.Windows.Forms.Timer
$remoteTimer.Interval = 150
$remoteTimer.Add_Tick({
    foreach ($name in (Get-ZCinemaHidEvents)) {
        if (-not $name) { continue }
        if ($script:learnMode) {
            $script:learnMode = $false
            if ($nativeNames -contains $name) {
                $remoteStatus.Text = "'$name' is handled by Windows and hidden here."
                $remoteStatus.ForeColor = [System.Drawing.Color]::Firebrick
            } else {
                $row = $grid.Rows | Where-Object { $_.Cells[0].Value -eq $name } | Select-Object -First 1
                if (-not $row) { [void]$grid.Rows.Add($name, 'none', ''); $row = $grid.Rows[$grid.Rows.Count - 1] }
                $grid.CurrentCell = $row.Cells[0]
                $remoteStatus.Text = "Detected: $name"
                $remoteStatus.ForeColor = [System.Drawing.Color]::DarkGreen
            }
            continue
        }
        if (-not $script:remoteEnabled) { continue }
        $now = Get-Date
        if ($script:debounceNames.ContainsKey($name) -and ($now - $script:debounceNames[$name]).TotalMilliseconds -lt 700) { continue }
        $script:debounceNames[$name] = $now
        $action = (Read-ZCinemaRemoteMap)[$name]
        if ($action -and $action -ne 'none') {
            try { Invoke-ZCinemaRemoteAction -Action $action } catch { }
            $remoteStatus.Text = "Remote: $name -> $action"
            $remoteStatus.ForeColor = [System.Drawing.Color]::DarkGreen
        }
    }
})
$remoteTimer.Start()

# ---- tray icon, close-to-tray, exit ----------------------------------------
$appIcon = $null
$iconPath = Join-Path $repoRoot "..\assets\ZCinemaSound.ico"
if (Test-Path $iconPath) { try { $appIcon = New-Object System.Drawing.Icon($iconPath) } catch { $appIcon = $null } }
if (-not $appIcon) { $appIcon = [System.Drawing.SystemIcons]::Application }   # fallback when no custom .ico
$form.Icon = $appIcon

$trayIcon = New-Object System.Windows.Forms.NotifyIcon
$trayIcon.Icon = $appIcon
$trayIcon.Text = "ZCinema Sound"
$trayIcon.Visible = $true

$trayMenu = New-Object System.Windows.Forms.ContextMenuStrip
$miOpen = $trayMenu.Items.Add("Open control panel")
$miRemote = $trayMenu.Items.Add("Remote mapping")
$miRemote.CheckOnClick = $true
$miRemote.Checked = $true
$miAuto = $trayMenu.Items.Add("Start with Windows")
$miAuto.CheckOnClick = $true
$miAuto.Checked = Get-ZCinemaAutostart
[void]$trayMenu.Items.Add("-")
$miExit = $trayMenu.Items.Add("Exit")
$trayIcon.ContextMenuStrip = $trayMenu

function Show-App { $form.ShowInTaskbar = $true; $form.Show(); $form.WindowState = 'Normal'; $form.Activate() }
function Exit-App {
    $script:reallyExit = $true
    try { $remoteTimer.Stop() } catch { }
    try { Stop-ZCinemaHidReader } catch { }
    try { Unlock-ZCinemaMutexes } catch { }
    try { $trayIcon.Visible = $false } catch { }
    try { $form.Close() } catch { }
    try { $context.ExitThread() } catch { }
}

$trayIcon.Add_MouseDoubleClick({ Show-App })
$miOpen.Add_Click({ Show-App })
$miRemote.Add_Click({ $script:remoteEnabled = $miRemote.Checked; $chkRemote.Checked = $miRemote.Checked })
$miAuto.Add_Click({ Set-ZCinemaAutostart -Enable $miAuto.Checked; $remoteStatus.Text = "Start with Windows: $($miAuto.Checked)"; $remoteStatus.ForeColor = [System.Drawing.Color]::DimGray })
$miExit.Add_Click({ Exit-App })
$btnExit.Add_Click({ Exit-App })

$chkRemote.Add_CheckedChanged({
    $script:remoteEnabled = $chkRemote.Checked
    $miRemote.Checked = $chkRemote.Checked
    $remoteStatus.Text = if ($chkRemote.Checked) { 'Remote mapping enabled.' } else { 'Remote mapping disabled.' }
    $remoteStatus.ForeColor = [System.Drawing.Color]::DimGray
})

$script:reallyExit = $false
$script:balloonShown = $false
$form.Add_FormClosing({
    param($sender, $e)
    if (-not $script:reallyExit) {
        $e.Cancel = $true
        $form.Hide(); $form.ShowInTaskbar = $false
        if (-not $script:balloonShown) {
            $trayIcon.ShowBalloonTip(3000, "ZCinema Sound", "Still running in the tray - remote mappings stay active.", [System.Windows.Forms.ToolTipIcon]::Info)
            $script:balloonShown = $true
        }
    }
})

# start hidden (-Tray) when launched by autostart, otherwise show now
$context = New-Object System.Windows.Forms.ApplicationContext
if (-not $Tray) { $form.Show() }

# a second launch signals this named event so we can show the window
$showEvent = New-Object System.Threading.EventWaitHandle($false, [System.Threading.EventResetMode]::AutoReset, "Local\ZCinema_Show")
$showTimer = New-Object System.Windows.Forms.Timer
$showTimer.Interval = 400
$showTimer.Add_Tick({ if ($showEvent.WaitOne(0)) { Show-App } })
$showTimer.Start()

if ($bridgeAlreadyRunning) { $remoteStatus.Text = 'Note: the standalone bridge is also running - stop it to avoid double actions.'; $remoteStatus.ForeColor = [System.Drawing.Color]::DarkOrange }

[System.Windows.Forms.Application]::Run($context)
try { $trayIcon.Dispose() } catch { }
