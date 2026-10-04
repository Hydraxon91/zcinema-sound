# ZCinema-GUI.ps1 - sliders for bass, treble, dialogue, stereo width, preamp and
# an 8-band graphic EQ. Writes the Equalizer APO profile live.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\ZCinema-GUI.ps1
#
# Equalizer APO hot-reloads the profile, so changes are heard immediately.
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $repoRoot "src\lib\ZCinema.Common.psm1") -Force
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

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
$form.ClientSize = New-Object System.Drawing.Size(800, 724)
$form.StartPosition = "CenterScreen"
$form.FormBorderStyle = "FixedSingle"
$form.MaximizeBox = $false

$r = New-Row "Ceiling" 10 -20 0;  $tbPreamp = $r.Track; $lblPreampV = $r.Value
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

[void]$form.ShowDialog()
