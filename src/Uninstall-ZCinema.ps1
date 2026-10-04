# Uninstall-ZCinema.ps1 - remove the ZCinema Sound profile from Equalizer APO.
# Restores the pre-install config.txt when possible.
# Run from an Administrator PowerShell:
#   powershell -ExecutionPolicy Bypass -File .\src\Uninstall-ZCinema.ps1
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "lib\ZCinema.Common.psm1") -Force

Write-Host "ZCinema Sound - uninstaller" -ForegroundColor Cyan

if (-not (Test-IsAdmin)) { throw "Please run this from an Administrator PowerShell." }

$apo = Get-EqualizerApoRoot
if (-not $apo) { throw "Equalizer APO not found." }
$configDir = Join-Path $apo "config"
$configTxt = Join-Path $configDir "config.txt"
$profileDst = Join-Path $configDir "ZCinema.txt"

# 1. Restore the newest pre-install backup of config.txt, if we can find one.
$configNow = if (Test-Path $configTxt) { Get-Content -LiteralPath $configTxt -Raw } else { "" }
$restored = $false
if ($configNow -match "Managed by ZCinema Sound") {
    $backups = Get-ChildItem "$configTxt.bak-*" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    foreach ($b in $backups) {
        $content = Get-Content -LiteralPath $b.FullName -Raw
        if ($content -notmatch "Managed by ZCinema Sound") {
            Copy-Item -LiteralPath $configTxt -Destination "$configTxt.bak-uninstall-$(Get-Date -Format yyyyMMdd-HHmmss)" -Force
            Copy-Item -LiteralPath $b.FullName -Destination $configTxt -Force
            Write-Host ("Restored pre-install config.txt from {0}" -f $b.Name) -ForegroundColor Green
            $restored = $true
            break
        }
    }
}
if (-not $restored) {
    # Fallback: just drop the include line.
    $backup = Remove-EqApoInclude -ConfigPath $configTxt -IncludeFile "ZCinema.txt"
    if ($backup) { Write-Host ("config.txt updated (backup: {0})" -f (Split-Path -Leaf $backup)) }
}

# 2. Remove the profile (keep a copy for safety).
if (Test-Path $profileDst) {
    Copy-Item -LiteralPath $profileDst -Destination "$profileDst.bak-$(Get-Date -Format yyyyMMdd-HHmmss)" -Force
    Remove-Item -LiteralPath $profileDst -Force
    Write-Host "Removed $profileDst"
}

Write-Host "Done. Equalizer APO itself is untouched." -ForegroundColor Green
