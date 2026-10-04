# Remote-Probe.ps1 - user-mode HID sniffer for the Logitech Z Cinema remote.
# Shows which HID collection each remote button arrives on, and the raw report.
# No driver, no elevation needed. Press buttons while it runs.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Remote-Probe.ps1
#   powershell -ExecutionPolicy Bypass -File .\tools\Remote-Probe.ps1 -Seconds 45
[CmdletBinding()]
param(
    [int]$Seconds = 30
)

$ErrorActionPreference = "Stop"

$cs = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

public static class HidProbe
{
    const uint GENERIC_READ = 0x80000000;
    const uint FILE_SHARE_READ = 0x00000001;
    const uint FILE_SHARE_WRITE = 0x00000002;
    const uint OPEN_EXISTING = 3;
    const int IOCTL_HID_GET_REPORT_DESCRIPTOR = 0x000B0192;
    const int HIDP_STATUS_SUCCESS = 0x00110000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr templ);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadFile(IntPtr h, byte[] buf, int toRead, out int read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CancelIoEx(IntPtr h, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr h, int code, IntPtr inBuf, int inSize, byte[] outBuf, int outSize, out int ret, IntPtr ov);

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
    static void Log(string s) { lock (_lock) { Console.WriteLine(s); } }

    public static void Info(string label, string path)
    {
        IntPtr h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == new IntPtr(-1)) { Log("  " + label + ": open failed, err=" + Marshal.GetLastWin32Error()); return; }
        IntPtr pp;
        if (HidD_GetPreparsedData(h, out pp))
        {
            HIDP_CAPS c;
            if (HidP_GetCaps(pp, out c) == HIDP_STATUS_SUCCESS)
                Log(string.Format("  {0}: usagePage=0x{1:X2} usage=0x{2:X2} inputLen={3}", label, c.UsagePage, c.Usage, c.InputReportByteLength));
            HidD_FreePreparsedData(pp);
        }
        CloseHandle(h);
    }

    public static void Run(string[] labels, string[] paths, int seconds)
    {
        _run = true;
        var handles = new List<IntPtr>();
        var threads = new List<Thread>();
        for (int i = 0; i < paths.Length; i++)
        {
            string label = labels[i];
            IntPtr h = CreateFileW(paths[i], GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) { Log(label + ": open for read failed, err=" + Marshal.GetLastWin32Error()); continue; }
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
                        Log(string.Format("{0}  {1,-26} {2}", DateTime.Now.ToString("HH:mm:ss.fff"), label, sb.ToString().Trim()));
                    }
                }
            });
            t.IsBackground = true; t.Start(); threads.Add(t);
        }
        Log("");
        Log("Listening for " + seconds + "s - press the remote buttons now (one at a time)...");
        Thread.Sleep(seconds * 1000);
        _run = false;
        foreach (var h in handles) { CancelIoEx(h, IntPtr.Zero); CloseHandle(h); }
        Log("Done.");
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
        "COL02" { "COL02 Vendor FFBC:0088" }
        "COL03" { "COL03 Keyboard" }
        default { $n }
    }
    $names.Add($label); $paths.Add($path)
}

Write-Host "Z Cinema remote HID collections:" -ForegroundColor Cyan
for ($i = 0; $i -lt $paths.Count; $i++) { Write-Host ("  {0}" -f $names[$i]); Write-Host ("     {0}" -f $paths[$i]) }
Write-Host ""
Write-Host "Caps:" -ForegroundColor Cyan
for ($i = 0; $i -lt $paths.Count; $i++) { [HidProbe]::Info($names[$i], $paths[$i]) }

$isAdmin = (New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host ""
    Write-Host "Tip: some collections (usually the Keyboard one) can only be read as Administrator." -ForegroundColor Yellow
    Write-Host "If a button shows no output, re-run this from an Administrator PowerShell." -ForegroundColor Yellow
}

Write-Host ""
[HidProbe]::Run($names.ToArray(), $paths.ToArray(), $Seconds)
