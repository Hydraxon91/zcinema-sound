# Get-EqualizerApoLog.ps1 - copy Equalizer APO's log somewhere readable and print it.
# Run from an ADMIN PowerShell (the log lives under a protected service profile):
#   powershell -ExecutionPolicy Bypass -File .\tools\Get-EqualizerApoLog.ps1
# Optional: -Trace then restart audio to capture detailed parse messages.
[CmdletBinding()]
param(
    [switch]$Trace,
    [int]$Tail = 120
)

$ErrorActionPreference = "Continue"
$log = "C:\Windows\ServiceProfiles\LocalService\AppData\Local\Temp\EqualizerAPO.log"
$dest = Join-Path $env:USERPROFILE "Desktop\EqualizerAPO.log"

if ($Trace) {
    New-ItemProperty -Path "HKLM:\SOFTWARE\EqualizerAPO" -Name "EnableTrace" -Value 1 -PropertyType String -Force | Out-Null
    Write-Host "Trace enabled. Restarting audio to capture details..."
    Restart-Service Audiosrv -Force
    Start-Sleep -Seconds 4
}

if (-not (Test-Path $log)) { Write-Host "Log not found: $log"; exit 1 }
Copy-Item -LiteralPath $log -Destination $dest -Force -ErrorAction SilentlyContinue
Write-Host ("Saved copy to: {0}" -f $dest)
Write-Host ("---- last {0} lines ----" -f $Tail)
Get-Content -LiteralPath $log -Tail $Tail

if ($Trace) {
    New-ItemProperty -Path "HKLM:\SOFTWARE\EqualizerAPO" -Name "EnableTrace" -Value 0 -PropertyType String -Force | Out-Null
    Write-Host "Trace disabled again."
}
