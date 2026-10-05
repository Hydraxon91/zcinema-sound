# ZCinema.Remote.psm1 - shared remote-control support for the Z Cinema.
# HID reader (user mode), button catalog, mapping file and the action library.
# Used by tools\Remote-Bridge.ps1 and the GUI Remote tab.
Set-StrictMode -Version Latest

$script:RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Import-Module (Join-Path $PSScriptRoot "ZCinema.Common.psm1")
try { Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue } catch { }

$script:RemoteDir = Join-Path $env:APPDATA "ZCinemaSound"
$script:MapPath   = Join-Path $script:RemoteDir "remote.json"

# ---------------------------------------------------------------- HID reader
if (-not ('ZCinemaHid' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

public static class ZCinemaHid
{
    const uint GENERIC_READ = 0x80000000;
    const uint FILE_SHARE_READ = 0x00000001;
    const uint FILE_SHARE_WRITE = 0x00000002;
    const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr templ);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr h, byte[] buf, int toRead, out int read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CancelIoEx(IntPtr h, IntPtr ov);

    static volatile bool _run;
    static readonly List<IntPtr> _handles = new List<IntPtr>();
    static readonly List<Thread> _threads = new List<Thread>();
    public static readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();

    static readonly string[] VendorBits = { "Music", "Videos", "Pictures", "Media Player (Windows)", "DVD menu", "Live TV", "Recorded TV" };
    static readonly Dictionary<byte, string> MediaCodes = new Dictionary<byte, string> {
        { 0x11, "Display" }, { 0x2E, "Preset 1" }, { 0x2F, "Preset 2" }, { 0x30, "Preset 3" }, { 0x31, "Preset 4" },
        { 0xD8, "Music" }, { 0xD9, "Videos" }, { 0xDA, "Pictures" }, { 0xDB, "Media Player (Windows)" },
        { 0xDC, "DVD menu" }, { 0xDD, "Live TV" }, { 0xDE, "Recorded TV" }
    };

    static string Decode(string label, byte[] b, int read)
    {
        var names = new List<string>();
        if (label.StartsWith("COL02"))
        {
            if (read >= 2 && b[0] == 0x02 && b[1] != 0)
                for (int i = 0; i < 7; i++) if ((b[1] & (1 << i)) != 0) names.Add(VendorBits[i]);
            return names.Count > 0 ? string.Join(",", names) : null;
        }
        if (label.StartsWith("COL01") && read >= 10 && b[0] == 0x01)
        {
            if (b[8] == 0xB1 && b[9] != 0)
            {
                names.Add(MediaCodes.ContainsKey(b[9]) ? MediaCodes[b[9]] : "media 0x" + b[9].ToString("X2"));
                return string.Join(",", names);
            }
            string[][] tbl = new string[][] {
                new string[] { null, null, "Forward", "Rewind", "Skip", "Replay", "Play", "Stop" },
                new string[] { "Ch+", "Ch-", "Mute", "Pause", null, null, null, null },
                new string[] { "Back", "Guide", "Record", "Info", null, null, "Full screen", null }
            };
            for (int i = 0; i < tbl.Length; i++)
            {
                int idx = 1 + i; if (idx >= read) break;
                for (int bit = 0; bit < 8; bit++)
                    if ((b[idx] & (1 << bit)) != 0 && tbl[i][bit] != null) names.Add(tbl[i][bit]);
            }
            return names.Count > 0 ? string.Join(",", names) : null;
        }
        return null;
    }

    public static string[] FindCollections()
    {
        // returns "COL0x|\\?\path" entries for the Z Cinema, excluding the keyboard collection
        var result = new List<string>();
        const string baseKey = @"SYSTEM\CurrentControlSet\Control\DeviceClasses\{4d1e55b2-f16f-11cf-88cb-001111000030}";
        using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(baseKey))
        {
            if (k == null) return result.ToArray();
            foreach (var name in k.GetSubKeyNames())
            {
                if (name.IndexOf("046D&PID_0A0F", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (name.IndexOf("Col03", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string col = "COL?";
                int idx = name.IndexOf("Col0", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && idx + 4 < name.Length) col = "COL0" + name[idx + 4];
                string path = name.StartsWith("##?#") ? ("\\\\?\\" + name.Substring(4)) : name;
                result.Add(col + "|" + path);
            }
        }
        return result.ToArray();
    }

    public static void Start()
    {
        _run = true;
        foreach (var entry in FindCollections())
        {
            string[] parts = entry.Split('|');
            IntPtr h = CreateFileW(parts[1], GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) continue;
            _handles.Add(h);
            string label = parts[0];
            Thread t = new Thread(() =>
            {
                var buf = new byte[128];
                while (_run)
                {
                    int read;
                    if (!ReadFile(h, buf, buf.Length, out read, IntPtr.Zero)) break;
                    if (read > 0) { string n = Decode(label, buf, read); if (n != null) Events.Enqueue(n); }
                }
            });
            t.IsBackground = true; t.Start(); _threads.Add(t);
        }
    }

    public static void Stop()
    {
        _run = false;
        foreach (var h in _handles) { CancelIoEx(h, IntPtr.Zero); CloseHandle(h); }
        _handles.Clear();
    }
}

public static class ZCinemaKeys
{
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    public static void Send(string[] vks)
    {
        foreach (var v in vks)
        {
            byte b = Convert.ToByte(v, 16);
            keybd_event(b, 0, 0, UIntPtr.Zero);
            keybd_event(b, 0, 2, UIntPtr.Zero);
        }
    }
}
'@
}

# ---------------------------------------------------------------- catalog
function Get-ZCinemaRemoteCatalog {
    # Only keys that do nothing in Windows today (native transport/volume/back are hidden).
    $free = @(
        "Preset 1", "Preset 2", "Preset 3", "Preset 4", "Display",
        "Guide", "Info", "Record", "Full screen", "Ch+", "Ch-",
        "DVD menu", "Live TV", "Recorded TV", "Music", "Videos", "Pictures",
        "Media Player (Windows)"
    )
    foreach ($n in $free) { [pscustomobject]@{ Name = $n } }
}

# ---------------------------------------------------------------- mapping
function Get-ZCinemaRemoteMapPath { return $script:MapPath }

function Get-ZCinemaDefaultRemoteMap {
    [ordered]@{
        "Display"   = "gui"
        "Preset 1"  = "preset:Music"
        "Preset 2"  = "preset:Movies"
        "Preset 3"  = "preset:Vocal"
        "Preset 4"  = "preset:V-Shape"
    }
}

function Read-ZCinemaRemoteMap {
    if (-not (Test-Path $script:MapPath)) { return @{} }
    try {
        $j = Get-Content -LiteralPath $script:MapPath -Raw | ConvertFrom-Json
        $h = @{}
        foreach ($p in $j.PSObject.Properties) { $h[$p.Name] = $p.Value }
        return $h
    } catch { return @{} }
}

function Save-ZCinemaRemoteMap {
    param([Parameter(Mandatory)]$Map)
    if (-not (Test-Path $script:RemoteDir)) { New-Item -ItemType Directory -Path $script:RemoteDir | Out-Null }
    ($Map | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath $script:MapPath -Encoding UTF8
}

# ---------------------------------------------------------------- presets
function Get-ZCinemaPresetDefinitions {
    @{
        "Flat"    = @{ Bass = 0;  Treble = 0; Dialog = 0;  Width = 0;  Eq = @(0, 0, 0, 0, 0, 0, 0, 0) }
        "Music"   = @{ Bass = 6;  Treble = 4; Dialog = 0;  Width = 10; Eq = @(3, 2, 1, 0, 1, 2, 3, 3) }
        "Movies"  = @{ Bass = 4;  Treble = 2; Dialog = 5;  Width = 20; Eq = @(2, 1, 0, 1, 2, 3, 2, 1) }
        "Night"   = @{ Bass = -2; Treble = 2; Dialog = 3;  Width = 10; Eq = @(-3, -2, 0, 0, 1, 0, 0, 0) }
        "Vocal"   = @{ Bass = -2; Treble = 2; Dialog = 7;  Width = 5;  Eq = @(-2, -1, 1, 2, 3, 2, 1, 0) }
        "V-Shape" = @{ Bass = 8;  Treble = 6; Dialog = -2; Width = 15; Eq = @(5, 3, 0, -2, -1, 2, 4, 5) }
    }
}

function Get-ZCinemaProfilePath {
    $apo = Get-EqualizerApoRoot
    if (-not $apo) { throw "Equalizer APO not found." }
    return (Join-Path $apo "config\ZCinema.txt")
}

# ---------------------------------------------------------------- actions
function Invoke-ZCinemaRemoteAction {
    param([Parameter(Mandatory)][string]$Action)

    if ([string]::IsNullOrWhiteSpace($Action) -or $Action -eq "none") { return }

    $verb = $Action; $val = ""
    $i = $Action.IndexOf(':')
    if ($i -ge 0) { $verb = $Action.Substring(0, $i); $val = $Action.Substring($i + 1) }

    switch ($verb) {
        "preset" {
            $presets = Get-ZCinemaPresetDefinitions
            if (-not $presets.ContainsKey($val)) { return }
            $prof = Get-ZCinemaProfilePath
            $cur = Get-ZCinemaProfileParams -ProfilePath $prof
            $p = $presets[$val]
            $text = New-ZCinemaProfileText -PreampDb $cur.PreampDb -BassGain $p.Bass -SubGain ([math]::Round($p.Bass * 0.6, 1)) `
                -TrebleGain $p.Treble -DialogGain $p.Dialog -Width ($p.Width / 100.0) -EqGains $p.Eq
            Save-ZCinemaProfile -ProfilePath $prof -Text $text
        }
        "custom" {
            if ($val -notmatch '^\d+$') { return }   # custom expects a slot number, not a URL/path
            $file = Join-Path $script:RemoteDir "presets.json"
            if (-not (Test-Path $file)) { return }
            $j = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
            if (-not ($j.PSObject.Properties.Name -contains $val)) { return }
            $c = $j.$val
            if ($null -eq $c) { return }
            $prof = Get-ZCinemaProfilePath
            $text = New-ZCinemaProfileText -PreampDb $c.Preamp -BassGain $c.Bass -SubGain ([math]::Round($c.Bass * 0.6, 1)) `
                -TrebleGain $c.Treble -DialogGain $c.Dialog -Width ($c.Width / 100.0) -EqGains @($c.Eq)
            Save-ZCinemaProfile -ProfilePath $prof -Text $text
        }
        "gui" {
            # bring the running tray app forward, else launch it
            $signaled = $false
            try { [System.Threading.EventWaitHandle]::OpenExisting("Local\ZCinema_Show").Set() | Out-Null; $signaled = $true } catch { }
            if (-not $signaled) {
                $gui = Join-Path $script:RepoRoot "tools\ZCinema-GUI.ps1"
                Start-Process powershell -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$gui`"")
            }
        }
        "bypass" {
            $prof = Get-ZCinemaProfilePath
            $flag = Join-Path $script:RemoteDir "bypassed.flag"
            $save = Join-Path $script:RemoteDir "last-profile.txt"
            if (Test-Path $flag) {
                if (Test-Path $save) { Copy-Item -LiteralPath $save -Destination $prof -Force }
                Remove-Item -LiteralPath $flag -Force
            } else {
                if (Test-Path $prof) { Copy-Item -LiteralPath $prof -Destination $save -Force }
                Save-ZCinemaProfile -ProfilePath $prof -Text "# ZCinema Sound bypassed (remote)`r`n"
                New-Item -ItemType File -Path $flag -Force | Out-Null
            }
        }
        "sound" {
            $prof = Get-ZCinemaProfilePath
            $pr = Get-ZCinemaProfileParams -ProfilePath $prof
            switch ($val) {
                "dialogue+" { $pr.DialogGain = [math]::Min(9,  $pr.DialogGain + 1) }
                "dialogue-" { $pr.DialogGain = [math]::Max(-9, $pr.DialogGain - 1) }
                "width+"    { $pr.Width      = [math]::Min(0.30, $pr.Width + 0.02) }
                "width-"    { $pr.Width      = [math]::Max(0.0,  $pr.Width - 0.02) }
                "ceiling+"  { $pr.PreampDb   = [math]::Min(0,    $pr.PreampDb + 1) }
                "ceiling-"  { $pr.PreampDb   = [math]::Max(-60,  $pr.PreampDb - 1) }
                default { return }
            }
            $text = New-ZCinemaProfileText -PreampDb $pr.PreampDb -BassGain $pr.BassGain -SubGain $pr.SubGain `
                -TrebleGain $pr.TrebleGain -DialogGain $pr.DialogGain -Width $pr.Width -EqGains @($pr.EqGains)
            Save-ZCinemaProfile -ProfilePath $prof -Text $text
        }
        "media" {
            $map = @{ "playpause" = "B3"; "next" = "B0"; "prev" = "B1"; "stop" = "B2"; "volup" = "AF"; "voldown" = "AE"; "mute" = "AD" }
            if ($map.ContainsKey($val)) { [ZCinemaKeys]::Send(@($map[$val])) }
        }
        "app"    { if ($val) { Start-Process $val } }
        "url"    { if ($val) { Start-Process $val } }
        "script" { if ($val) { Start-Process powershell -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$val`"") } }
        "keys"   { if ($val) { [System.Windows.Forms.SendKeys]::SendWait($val) } }
        default  { }
    }
}

# ---------------------------------------------------------------- reader facade
function Start-ZCinemaHidReader { [ZCinemaHid]::Start() }
function Stop-ZCinemaHidReader  { [ZCinemaHid]::Stop() }
function Get-ZCinemaHidEvents {
    $list = New-Object System.Collections.Generic.List[string]
    $n = $null
    while ([ZCinemaHid]::Events.TryDequeue([ref]$n)) { $list.Add($n) }
    return $list
}

# ---------------------------------------------------------------- single instance & autostart
$script:Mutexes = @{}

function Lock-ZCinemaMutex {
    param([Parameter(Mandatory)][string]$Name)
    $m = New-Object System.Threading.Mutex($false, "Local\ZCinema_$Name")
    if ($m.WaitOne(0)) { $script:Mutexes[$Name] = $m; return $true }
    $m.Dispose(); return $false
}
function Unlock-ZCinemaMutexes {
    foreach ($k in @($script:Mutexes.Keys)) {
        try { $script:Mutexes[$k].ReleaseMutex() } catch { }
        try { $script:Mutexes[$k].Dispose() } catch { }
    }
    $script:Mutexes = @{}
}
function Test-ZCinemaMutex {
    param([Parameter(Mandatory)][string]$Name)
    $m = New-Object System.Threading.Mutex($false, "Local\ZCinema_$Name")
    $free = $m.WaitOne(0)
    if ($free) { try { $m.ReleaseMutex() } catch { } }
    $m.Dispose()
    return (-not $free)
}

function Get-ZCinemaGuiPath { return (Join-Path $script:RepoRoot "tools\ZCinema-GUI.ps1") }

function Get-ZCinemaAutostart {
    $key = Get-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue
    if ($null -eq $key) { return $false }
    $val = $key.GetValue("ZCinemaSound")
    return [bool]$val
}
function Set-ZCinemaAutostart {
    param([Parameter(Mandatory)][bool]$Enable)
    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    if ($Enable) {
        $cmd = 'powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Get-ZCinemaGuiPath) + '" -Tray'
        New-ItemProperty -Path $key -Name "ZCinemaSound" -Value $cmd -PropertyType String -Force | Out-Null
    } else {
        Remove-ItemProperty -Path $key -Name "ZCinemaSound" -ErrorAction SilentlyContinue
    }
}

Export-ModuleMember -Function Get-ZCinemaRemoteCatalog, Get-ZCinemaRemoteMapPath, Get-ZCinemaDefaultRemoteMap,
    Read-ZCinemaRemoteMap, Save-ZCinemaRemoteMap, Get-ZCinemaPresetDefinitions, Invoke-ZCinemaRemoteAction,
    Start-ZCinemaHidReader, Stop-ZCinemaHidReader, Get-ZCinemaHidEvents, Get-ZCinemaProfilePath,
    Lock-ZCinemaMutex, Unlock-ZCinemaMutexes, Test-ZCinemaMutex, Get-ZCinemaGuiPath, Get-ZCinemaAutostart, Set-ZCinemaAutostart
