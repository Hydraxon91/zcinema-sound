# Bypass-ZCinema.ps1 - immediately disable all ZCinema Sound processing.
# Use this if audio breaks: it sets config.txt to a harmless no-op and restarts
# the audio service. Your previous config.txt is backed up.
#   powershell -ExecutionPolicy Bypass -File .\src\Bypass-ZCinema.ps1
#   powershell -ExecutionPolicy Bypass -File .\src\Bypass-ZCinema.ps1 -Restore
[CmdletBinding()]
param(
    [switch]$Restore
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "lib\ZCinema.Common.psm1") -Force

if (-not (Test-IsAdmin)) { throw "Please run this from an Administrator PowerShell." }

$apo = Get-EqualizerApoRoot
if (-not $apo) { throw "Equalizer APO not found." }
$configTxt = Join-Path $apo "config\config.txt"

function Write-Ascii([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.ASCIIEncoding
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

if ($Restore) {
    $backups = Get-ChildItem "$configTxt.bak-*" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if (-not $backups) { Write-Host "No backups found; nothing to restore." ; return }
    Copy-Item -LiteralPath $configTxt -Destination "$configTxt.bak-before-restore-$(Get-Date -Format yyyyMMdd-HHmmss)" -Force
    Copy-Item -LiteralPath $backups[0].FullName -Destination $configTxt -Force
    Write-Host ("Restored config.txt from {0}" -f $backups[0].Name) -ForegroundColor Green
} else {
    Copy-Item -LiteralPath $configTxt -Destination "$configTxt.bak-bypass-$(Get-Date -Format yyyyMMdd-HHmmss)" -Force
    Write-Ascii $configTxt "# ZCinema Sound bypassed - no processing active.`r`n"
    Write-Host "ZCinema Sound processing disabled (config.txt replaced with a no-op)." -ForegroundColor Yellow
}

Write-Host "Restarting Windows Audio service..."
Restart-Service Audiosrv -Force
Write-Host "Done. Test audio now." -ForegroundColor Green
