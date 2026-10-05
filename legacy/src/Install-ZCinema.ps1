# Install-ZCinema.ps1 - install the ZCinema Sound profile into Equalizer APO.
# Run from an Administrator PowerShell:
#   powershell -ExecutionPolicy Bypass -File .\src\Install-ZCinema.ps1
[CmdletBinding()]
param(
    # Volume ceiling. Lower = quieter max = more usable slider travel. Try -6 .. -13.
    [double]$PreampDb = -8,
    # Keep your existing config.txt (neutralize its Preamp lines) instead of
    # replacing it with a clean include. Your old file is backed up either way.
    [switch]$Merge,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "lib\ZCinema.Common.psm1") -Force

Write-Host "ZCinema Sound - installer" -ForegroundColor Cyan
Write-Host "-------------------------" -ForegroundColor Cyan

if (-not (Test-IsAdmin)) { throw "Please run this from an Administrator PowerShell." }

# 1. Equalizer APO present?
$apo = Get-EqualizerApoRoot
if (-not $apo) {
    Write-Host "Equalizer APO was not found." -ForegroundColor Yellow
    Write-Host "  1. Install it: https://sourceforge.net/projects/equalizerapo/"
    Write-Host "  2. Run its DeviceSelector (or Configurator), tick 'Speakers (Z Cinema)', reboot."
    Write-Host "  3. Run this script again."
    exit 1
}
$configDir = Join-Path $apo "config"
$configTxt = Join-Path $configDir "config.txt"
$profileSrc = Join-Path $repoRoot "config\ZCinema.txt"
$profileDst = Join-Path $configDir "ZCinema.txt"
Write-Host "Equalizer APO: $apo"

# 2. Z Cinema endpoint present?
$ep = Get-ZCinemaEndpoint
if (-not $ep) {
    throw "Z Cinema render endpoint not found. Is the speaker set connected and enabled in Sound settings?"
}
Write-Host ("Endpoint: {0}  ({1})" -f $ep.DeviceName, $ep.EndpointId)

# 3. Is Equalizer APO attached to this endpoint?
if (Test-EqualizerApoAttached -EndpointGuid $ep.Guid -FxKey $ep.FxKey) {
    Write-Host "Equalizer APO is attached to the Z Cinema endpoint." -ForegroundColor Green
} else {
    $tool = Get-EqualizerApoDeviceTool
    Write-Host "Equalizer APO is NOT attached to the Z Cinema endpoint yet." -ForegroundColor Yellow
    Write-Host ("  Run {0}, tick 'Speakers (Z Cinema)', reboot, then re-run." -f $tool)
    if (-not $Force) { throw "Aborting. Use -Force to copy the profile anyway." }
}

# 4. Install profile (with the chosen preamp) + include line.
if ((Test-Path $profileDst) -and -not $Force) {
    Copy-Item -LiteralPath $profileDst -Destination "$profileDst.bak-$(Get-Date -Format yyyyMMdd-HHmmss)" -Force
}
Copy-Item -LiteralPath $profileSrc -Destination $profileDst -Force
Set-PreampInProfile -ProfilePath $profileDst -PreampDb $PreampDb
Write-Host ("Profile installed: {0} (Preamp: {1} dB)" -f $profileDst, $PreampDb) -ForegroundColor Green

# Let the current user edit the profile without admin, so tools\ZCinema-GUI.ps1 works.
try {
    $me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $acl = Get-Acl -LiteralPath $profileDst
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($me, "Modify", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl -LiteralPath $profileDst -AclObject $acl
    Write-Host ("Granted {0} modify rights on the profile (for the GUI)." -f $me)
} catch {
    Write-Host ("Note: could not set profile permissions ({0}). Run the GUI as admin if it cannot save." -f $_.Exception.Message) -ForegroundColor Yellow
}

$backup = Set-EqApoInclude -ConfigPath $configTxt -IncludeFile "ZCinema.txt" -Merge:$Merge
if ($backup) { Write-Host "config.txt backed up: $backup" }
if ($Merge) {
    Write-Host "config.txt merged (existing Preamp lines neutralized; include appended)." -ForegroundColor Green
} else {
    Write-Host "config.txt replaced with a clean ZCinema include (old file backed up)." -ForegroundColor Green
}

# 5. Done.
Write-Host ""
Write-Host "Next:" -ForegroundColor Cyan
Write-Host "  * Set the volume ceiling by measurement:  .\tools\Calibrate-Ceiling.ps1"
Write-Host "  * Or edit:  $profileDst"
Write-Host "  * Windows volume is your everyday control (remote/OSD stay in sync)."
