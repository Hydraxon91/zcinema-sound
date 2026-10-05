# Build the ZCinema Sound installer.
#   powershell -ExecutionPolicy Bypass -File .\installer\build-installer.ps1 [-Version 0.1.3]
# Requires: .NET 10 SDK and Inno Setup 6 (ISCC.exe). Does NOT bundle Equalizer APO.
param(
    [string]$Version = "0.1.3",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$installerDir = $PSScriptRoot
$root = Split-Path -Parent $installerDir
$app = Join-Path $root "src-app\ZCinemaSound.App"

Write-Host "Publishing $Configuration ..." -ForegroundColor Cyan
& dotnet publish $app -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$pub = Join-Path $app "bin\$Configuration\net10.0-windows\win-x64\publish"
if (-not (Test-Path (Join-Path $pub "ZCinemaSound.App.exe"))) { throw "published exe not found in $pub" }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php" }

Write-Host "Building installer with $iscc ..." -ForegroundColor Cyan
& $iscc "/DAppExeDir=$pub" "/DVersion=$Version" (Join-Path $installerDir "zcinema.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

Write-Host ("Done: {0}" -f (Join-Path $installerDir "output\ZCinemaSound-Setup.exe")) -ForegroundColor Green
