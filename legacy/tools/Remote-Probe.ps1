# Remote-Probe.ps1 - live, user-mode HID sniffer + decoder for the Logitech Z Cinema remote.
# - No time limit (press Enter to stop)
# - Clears the screen on each new press and shows recent presses grouped by press
# - Decodes the known vendor bitmask and consumer media/preset codes
# - Writes the full log to a file so you can paste it afterwards
# COL01/COL02 need no elevation; COL03 (Keyboard) needs Administrator.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Remote-Probe.ps1
#   powershell -ExecutionPolicy Bypass -File .\tools\Remote-Probe.ps1 -Seconds 30
[CmdletBinding()]
param(
    [int]$Seconds = 0,   # 0 = run until Enter
    [string]$LogPath = (Join-Path $env:TEMP "ZCinema-remote-probe.log")
)

$ErrorActionPreference = "Stop"

$cs = @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

public static class HidProbe
{
    const uint GENERIC_READ = 0x80000000;
    const uint FILE_SHARE_READ = 0x00000001;
    const uint FILE_SHARE_WRITE = 0x00000002;
    const uint OPEN_EXISTING = 3;
    const int HIDP_STATUS_SUCCESS = 0x00110000;
    const int HIST = 40;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr templ);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr h, byte[] buf, int toRead, out int read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CancelIoEx(IntPtr h, IntPtr ov);

    [StructLayout(LayoutKind.Sequential)]
    struct HIDP_CAPS
    {
        public ushort Usage; public ushort UsagePage;
        public ushort InputReportByteLength; public ushort OutputReportByteLength; public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps; public ushort NumberInputValueCaps; public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps; public ushort NumberOutputValueCaps; public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps; public ushort NumberFeatureValueCaps; public ushort NumberFeatureDataIndices;
    }
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr p);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr p, out HIDP_CAPS c);

    static volatile bool _run = true;
    static readonly object _lock = new object();
    static readonly List<string> _hist = new List<string>();
    static DateTime _last = DateTime.MinValue;
    static int _group = 0;
    static string _logPath = "";

    // COL02 vendor report 0x02: byte1 = one bit per Media Center button
    static readonly string[] VendorBits = { "Music", "Videos", "Pictures", "Media Player (Windows)", "DVD menu", "Live TV", "Recorded TV" };
    // COL01 consumer media/preset codes at bytes [8]=0xB1,[9]=code
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
            return names.Count > 0 ? string.Join(", ", names) : null;
        }
        if (label.StartsWith("COL01") && read >= 10 && b[0] == 0x01)
        {
            if (b[8] == 0xB1 && b[9] != 0)
            {
                if (MediaCodes.ContainsKey(b[9])) names.Add(MediaCodes[b[9]]);
                else names.Add("media code 0x" + b[9].ToString("X2"));
                return string.Join(", ", names);
            }
            // remote button bitmask (bytes 1..3 observed)
            string[][] tbl = new string[][] {
                new string[] { null, null, "Forward", "Rewind", "Skip", "Replay", "Play", "Stop" },              // byte1
                new string[] { "Ch+", "Ch-", "Mute", "Pause", null, null, null, null },                          // byte2
                new string[] { "Back", "Guide", "Record", "Info", null, null, "Full screen", null }              // byte3
            };
            for (int i = 0; i < tbl.Length; i++)
            {
                int idx = 1 + i;
                if (idx >= read) break;
                for (int bit = 0; bit < 8; bit++)
                    if ((b[idx] & (1 << bit)) != 0 && tbl[i][bit] != null) names.Add(tbl[i][bit]);
            }
            return names.Count > 0 ? string.Join(", ", names) : null;
        }
        return null;
    }

    static void Redraw()
    {
        try { Console.Clear(); } catch { }
        Console.WriteLine("Z Cinema remote probe   -   newest at the bottom   -   press Enter to stop");
        Console.WriteLine("Full log: " + _logPath);
        Console.WriteLine(new string('-', 78));
        foreach (var l in _hist) Console.WriteLine(l);
    }

    static void Report(string label, string hex, string decoded)
    {
        lock (_lock)
        {
            DateTime now = DateTime.Now;
            if ((now - _last).TotalMilliseconds > 600)
            {
                _group++;
                _hist.Add("");
                _hist.Add("--- press " + _group + "   " + now.ToString("HH:mm:ss") + " ---");
            }
            _last = now;
            string full = now.ToString("HH:mm:ss.fff") + "  " + label + "  " + hex + (decoded == null ? "" : "   <- " + decoded);
            _hist.Add(full);
            while (_hist.Count > HIST) _hist.RemoveAt(0);
            try { File.AppendAllText(_logPath, full + Environment.NewLine); } catch { }
            Redraw();
        }
    }

    public static void Info(string label, string path)
    {
        IntPtr h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == new IntPtr(-1))
        {
            Console.WriteLine("  " + label + ": open failed, err=" + Marshal.GetLastWin32Error());
            return;
        }
        IntPtr pp;
        if (HidD_GetPreparsedData(h, out pp))
        {
            HIDP_CAPS c;
            if (HidP_GetCaps(pp, out c) == HIDP_STATUS_SUCCESS)
                Console.WriteLine(string.Format("  {0}: usagePage=0x{1:X2} usage=0x{2:X2} inputLen={3}", label, c.UsagePage, c.Usage, c.InputReportByteLength));
            HidD_FreePreparsedData(pp);
        }
        CloseHandle(h);
    }

    public static void Run(string[] labels, string[] paths, int seconds, string logPath)
    {
        _logPath = logPath;
        _run = true; _last = DateTime.MinValue; _group = 0;
        lock (_lock) { _hist.Clear(); }
        try { File.AppendAllText(_logPath, "=== session " + DateTime.Now.ToString("s") + " ===" + Environment.NewLine); } catch { }

        var handles = new List<IntPtr>();
        var threads = new List<Thread>();
        for (int i = 0; i < paths.Length; i++)
        {
            string label = labels[i];
            IntPtr h = CreateFileW(paths[i], GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) { Console.WriteLine(label + ": open for read failed, err=" + Marshal.GetLastWin32Error()); continue; }
            handles.Add(h);
            Thread t = new Thread(() =>
            {
                var buf = new byte[128];
                while (_run)
                {
                    int read;
                    if (!ReadFile(h, buf, buf.Length, out read, IntPtr.Zero)) break;
                    if (read > 0)
                    {
                        var sb = new System.Text.StringBuilder();
                        for (int k = 0; k < read; k++) sb.Append(buf[k].ToString("X2")).Append(' ');
                        Report(label, sb.ToString().Trim(), Decode(label, buf, read));
                    }
                }
            });
            t.IsBackground = true; t.Start(); threads.Add(t);
        }

        Redraw();
        if (seconds > 0)
        {
            Thread.Sleep(seconds * 1000);
        }
        else
        {
            Console.WriteLine("");
            Console.WriteLine("Listening... press remote buttons one at a time, then press Enter here to stop.");
            try { Console.ReadLine(); } catch { Thread.Sleep(1000000000); }
        }
        _run = false;
        foreach (var h in handles) { CancelIoEx(h, IntPtr.Zero); CloseHandle(h); }
        Console.WriteLine("Done. Full log saved to: " + _logPath);
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp

$hidGuid = "{4d1e55b2-f16f-11cf-88cb-001111000030}"
$base = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceClasses\$hidGuid"
$cols = Get-ChildItem $base -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match "046D&PID_0A0F" }
if (-not $cols) { throw "Z Cinema HID collections not found. Is the remote/speaker connected?" }

$names = New-Object System.Collections.Generic.List[string]
$paths = New-Object System.Collections.Generic.List[string]
foreach ($c in $cols) {
    $raw = $c.PSChildName
    $path = $raw -replace '^##\?#', '\\?\'
    $n = "COL?"
    if ($raw -match '&Col0?(\d)') { $n = "COL0" + $Matches[1] }
    $label = switch ($n) {
        "COL01" { "COL01 Consumer" }
        "COL02" { "COL02 Vendor" }
        "COL03" { "COL03 Keyboard" }
        default { $n }
    }
    $names.Add($label); $paths.Add($path)
}

Write-Host "Z Cinema remote HID collections:" -ForegroundColor Cyan
for ($i = 0; $i -lt $paths.Count; $i++) { Write-Host ("  {0}" -f $names[$i]) }
Write-Host "Caps:" -ForegroundColor Cyan
for ($i = 0; $i -lt $paths.Count; $i++) { [HidProbe]::Info($names[$i], $paths[$i]) }

$isAdmin = (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { Write-Host "Note: COL03 (Keyboard) needs Administrator; re-run elevated if a button shows nothing." -ForegroundColor Yellow }
Write-Host ""

[HidProbe]::Run($names.ToArray(), $paths.ToArray(), $Seconds, $LogPath)
