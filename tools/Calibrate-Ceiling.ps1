# Calibrate-Ceiling.ps1 - measures the Z Cinema volume taper and tells you the
# 'Preamp:' value that makes Windows 100% equal your comfortable maximum.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Calibrate-Ceiling.ps1
#   powershell -ExecutionPolicy Bypass -File .\tools\Calibrate-Ceiling.ps1 -ComfortablePercent 35
#   powershell -ExecutionPolicy Bypass -File .\tools\Calibrate-Ceiling.ps1 -ComfortablePercent 35 -Apply
#
# It briefly moves the volume, measures, then restores it. Safe to re-run.
[CmdletBinding()]
param(
    # The Windows slider % where it already sounds as loud as you want.
    [ValidateRange(1, 100)][int]$ComfortablePercent = 40,
    # Write the result into the installed profile's Preamp line.
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $repoRoot "src\lib\ZCinema.Common.psm1") -Force
Add-Type -Path (Join-Path $PSScriptRoot "lib\ZCinemaAudio.cs")

Write-Host "ZCinema Sound - volume ceiling calibration" -ForegroundColor Cyan

$ep = Get-ZCinemaEndpoint
if ($ep) {
    Write-Host ("Endpoint: {0}" -f $ep.EndpointId)
    $vol = [ZCinemaEndpointVolume]::OpenById($ep.EndpointId)
} else {
    Write-Host "Z Cinema endpoint not found; using the default render device." -ForegroundColor Yellow
    $vol = [ZCinemaEndpointVolume]::OpenDefault()
}

$saved = $vol.GetScalar()
$savedMute = $vol.GetMute()
if ($savedMute) { $vol.SetMute($false) }

$comfortableDb = 0.0
try {
    $scalar = $ComfortablePercent / 100.0
    Write-Host ("Setting volume to {0}% (scalar {1:N3}) and measuring..." -f $ComfortablePercent, $scalar)
    $vol.SetScalar($scalar)
    Start-Sleep -Milliseconds 200
    $comfortableDb = $vol.GetDb()
} finally {
    $vol.SetScalar($saved)
    if ($savedMute) { $vol.SetMute($true) }
}

$preamp = [math]::Round($comfortableDb, 1)
Write-Host ""
Write-Host ("At Windows {0}% the endpoint is {1:N1} dB below full scale." -f $ComfortablePercent, $comfortableDb)
Write-Host ("=> Use:  Preamp: {0} dB" -f $preamp) -ForegroundColor Green
Write-Host ("   That makes Windows 100% approximately your old {0}% loudness, so the whole slider is usable." -f $ComfortablePercent)
Write-Host ("   (Your volume was {0:N3}; restored.)" -f $saved)

if ($Apply) {
    $apo = Get-EqualizerApoRoot
    if (-not $apo) { throw "Equalizer APO not found; cannot -Apply." }
    $profile = Join-Path $apo "config\ZCinema.txt"
    if (-not (Test-Path $profile)) { throw "Profile not installed: $profile. Run src\Install-ZCinema.ps1 first." }
    Set-PreampInProfile -ProfilePath $profile -PreampDb $preamp
    Write-Host "Applied to $profile" -ForegroundColor Green
}
