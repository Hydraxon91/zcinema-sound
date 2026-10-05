# Remote-Bridge.ps1 - maps the Z Cinema remote's free buttons to actions.
# User-mode HID only; no kernel driver, no test-signing.
# Mapping file: %APPDATA%\ZCinemaSound\remote.json (edit it, or use the GUI Remote tab).
# Changes to the mapping are picked up without restarting.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Remote-Bridge.ps1
#   powershell -ExecutionPolicy Bypass -File .\tools\Remote-Bridge.ps1 -Seconds 20
[CmdletBinding()]
param(
    [int]$Seconds = 0   # 0 = run until Q/Enter
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $repoRoot "src\lib\ZCinema.Remote.psm1") -Force

# Don't run two bridges (or fight the tray app's built-in bridge).
if (-not (Lock-ZCinemaMutex -Name "Bridge")) { Write-Host "A bridge is already running."; exit }
if (Test-ZCinemaMutex -Name "App") { Write-Host "The tray app is running its own bridge - use that, or close it first."; Unlock-ZCinemaMutexes; exit }

$mapPath = Get-ZCinemaRemoteMapPath
if (-not (Test-Path $mapPath)) { Save-ZCinemaRemoteMap -Map (Get-ZCinemaDefaultRemoteMap) }
$map = Read-ZCinemaRemoteMap
$mapStamp = (Get-Item $mapPath).LastWriteTime

Start-ZCinemaHidReader
Write-Host "Z Cinema remote bridge running. Press remote buttons. Press Q then Enter to stop." -ForegroundColor Cyan
Write-Host ("Mapping: {0}" -f $mapPath) -ForegroundColor DarkGray
Write-Host ""

$last = @{}
$canKey = $true
try { $null = [Console]::KeyAvailable } catch { $canKey = $false }
$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    while ($true) {
        $name = $null
        foreach ($name in (Get-ZCinemaHidEvents)) {
            if (-not $name) { continue }
            $now = Get-Date
            if ($last.ContainsKey($name) -and ($now - $last[$name]).TotalMilliseconds -lt 700) { continue }
            $last[$name] = $now
            $action = $map[$name]
            if ($action -and $action -ne "none") {
                Write-Host ("[{0:HH:mm:ss}] {1}  ->  {2}" -f $now, $name, $action)
                try { Invoke-ZCinemaRemoteAction -Action $action } catch { Write-Host ("  action failed: {0}" -f $_.Exception.Message) -ForegroundColor Yellow }
            } else {
                Write-Host ("[{0:HH:mm:ss}] {1}" -f $now, $name) -ForegroundColor DarkGray
            }
        }
        # live-reload the mapping when the file changes (e.g. edits from the GUI)
        $stamp = (Get-Item $mapPath -ErrorAction SilentlyContinue).LastWriteTime
        if ($stamp -ne $mapStamp) { $map = Read-ZCinemaRemoteMap; $mapStamp = $stamp; Write-Host "  (mapping reloaded)" -ForegroundColor DarkGray }
        if ($Seconds -gt 0 -and $sw.Elapsed.TotalSeconds -ge $Seconds) { break }
        if ($canKey -and [Console]::KeyAvailable) {
            $k = [Console]::ReadKey($true)
            if ($k.Key -eq [ConsoleKey]::Q -or $k.Key -eq [ConsoleKey]::Enter) { break }
        }
        Start-Sleep -Milliseconds 40
    }
} finally {
    Stop-ZCinemaHidReader
    Unlock-ZCinemaMutexes
}
Write-Host "Stopped."
