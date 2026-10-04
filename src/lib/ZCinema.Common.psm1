# ZCinema.Common.psm1 - shared helpers for the ZCinema Sound scripts.
Set-StrictMode -Version Latest

$script:MmdevicesRender = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render"
$script:PkeyDeviceName  = "{b3f8fa53-0004-438e-9003-51a46e139bfc},6"   # "Z Cinéma"
$script:PkeyEndpoint    = "{a45c254e-df1c-4efd-8020-67d146a850e0},2"   # "Speakers"
$script:PkeyFxTitle     = "{b725f130-47ef-101a-a5f1-02608c9eebac},10"  # "Equalizer APO" when attached
$script:PkeyFxPreMix    = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},1"
$script:PkeyFxPostMix   = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},2"

function Write-EqApoFile {
    <# Equalizer APO reads ANSI text: write ASCII, no BOM, CRLF line endings. #>
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Text)
    $clean = $Text -replace "^\uFEFF", ""
    $enc = New-Object System.Text.ASCIIEncoding
    [System.IO.File]::WriteAllText($Path, $clean, $enc)
}

function Test-IsAdmin {
    $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-ZCinemaEndpoint {
    <# Returns the render endpoint for the Z Cinema, or $null. #>
    if (-not (Test-Path $script:MmdevicesRender)) { return $null }
    foreach ($k in Get-ChildItem $script:MmdevicesRender -ErrorAction SilentlyContinue) {
        $guid = $k.PSChildName
        try { $props = Get-ItemProperty "$script:MmdevicesRender\$guid\Properties" -ErrorAction Stop }
        catch { continue }
        $dev = $props.$($script:PkeyDeviceName)
        $nm  = $props.$($script:PkeyEndpoint)
        if ($dev -like "*Z Cin*" -or $nm -like "*Z Cin*") {
            $state = (Get-ItemProperty "$script:MmdevicesRender\$guid" -ErrorAction SilentlyContinue).DeviceState
            return [pscustomobject]@{
                Guid       = $guid
                EndpointId = "{0.0.0.00000000}.$guid"
                DeviceName = $dev
                Name       = $nm
                State      = $state
                FxKey      = "$script:MmdevicesRender\$guid\FxProperties"
            }
        }
    }
    return $null
}

function Get-EqualizerApoRoot {
    $candidates = @(
        (Join-Path $env:ProgramFiles "EqualizerAPO"),
        (Join-Path ${env:ProgramFiles(x86)} "EqualizerAPO")
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { return $c }
    }
    return $null
}

function Get-EqualizerApoDeviceTool {
    <# Path to the Equalizer APO device-attach tool (Configurator or DeviceSelector). #>
    $apo = Get-EqualizerApoRoot
    if (-not $apo) { return $null }
    foreach ($n in @("Configurator.exe", "DeviceSelector.exe")) {
        $p = Join-Path $apo $n
        if (Test-Path $p) { return $p }
    }
    return $null
}

function Test-EqualizerApoAttached {
    <#
      Equalizer APO 1.4+ records attachment under
        HKLM\SOFTWARE\EqualizerAPO\Child APOs\<endpoint guid>
      Older builds wrote a slot directly into the endpoint FxProperties.
      Returns $true if either is found.
    #>
    param(
        [string]$EndpointGuid,
        [string]$FxKey
    )
    if ($EndpointGuid) {
        $child = "HKLM:\SOFTWARE\EqualizerAPO\Child APOs\$EndpointGuid"
        if (Test-Path $child) {
            $c = Get-ItemProperty $child -ErrorAction SilentlyContinue
            if ($null -ne $c -and ($c.PSObject.Properties.Name -contains "PreMixChild")) { return $true }
            if ($null -ne $c) { return $true }
        }
    }
    if ($FxKey -and (Test-Path $FxKey)) {
        $p = Get-ItemProperty $FxKey -ErrorAction SilentlyContinue
        if ($null -ne $p) {
            if ($p.$($script:PkeyFxTitle) -like "*Equalizer APO*") { return $true }
            $names = $p.PSObject.Properties.Name
            if (($names -contains $script:PkeyFxPreMix) -or ($names -contains $script:PkeyFxPostMix)) { return $true }
        }
    }
    return $false
}

function Set-EqApoInclude {
    <#
      Writes the Equalizer APO config.txt to include our profile.
      Default: replaces config.txt with a clean include (previous file backed up).
      -Merge : keeps existing lines, neutralizes any Preamp: lines (they SUM in
               Equalizer APO), and appends the include at the end.
      Returns the backup path.
    #>
    param(
        [Parameter(Mandatory)][string]$ConfigPath,
        [string]$IncludeFile = "ZCinema.txt",
        [switch]$Merge
    )
    $backup = "$ConfigPath.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"
    if (Test-Path $ConfigPath) { Copy-Item -LiteralPath $ConfigPath -Destination $backup -Force }

    $header = @(
        "# Managed by ZCinema Sound (src\Install-ZCinema.ps1)",
        "# Previous config backed up beside this file as config.txt.bak-<timestamp>",
        ""
    )

    if (-not $Merge) {
        $content = ($header + @("Include: $IncludeFile", "")) -join "`r`n"
        Write-EqApoFile -Path $ConfigPath -Text $content
        return $backup
    }

    $lines = if (Test-Path $ConfigPath) { Get-Content -LiteralPath $ConfigPath } else { @() }
    $pattern = "^\s*Include:\s*" + [regex]::Escape($IncludeFile) + "\s*$"
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($l in $lines) {
        if ($l -match "^\s*Preamp:") { $out.Add("# " + $l + "   # neutralized by ZCinema Sound (Preamp values sum)"); continue }
        if ($l -match $pattern) { continue }
        $out.Add($l)
    }
    $out.Add("")
    $out.Add("Include: $IncludeFile")
    Write-EqApoFile -Path $ConfigPath -Text ($out -join "`r`n")
    return $backup
}

function Remove-EqApoInclude {
    <# Removes 'Include: ZCinema.txt' from config.txt. Returns the backup path. #>
    param(
        [Parameter(Mandatory)][string]$ConfigPath,
        [string]$IncludeFile = "ZCinema.txt"
    )
    if (-not (Test-Path $ConfigPath)) { return $null }
    $lines = Get-Content -LiteralPath $ConfigPath
    $backup = "$ConfigPath.bak-$(Get-Date -Format yyyyMMdd-HHmmss)"
    Copy-Item -LiteralPath $ConfigPath -Destination $backup -Force
    $pattern = "^\s*Include:\s*" + [regex]::Escape($IncludeFile) + "\s*$"
    $kept = $lines | Where-Object { $_ -notmatch $pattern }
    Write-EqApoFile -Path $ConfigPath -Text ($kept -join "`r`n")
    return $backup
}

function Set-PreampInProfile {
    <# Rewrites the first 'Preamp:' line of a profile file to the given dB value. #>
    param(
        [Parameter(Mandatory)][string]$ProfilePath,
        [Parameter(Mandatory)][double]$PreampDb
    )
    if (-not (Test-Path $ProfilePath)) { throw "Profile not found: $ProfilePath" }
    $text = Get-Content -LiteralPath $ProfilePath -Raw
    $replacement = "Preamp: $PreampDb dB"
    if ($text -match "(?m)^\s*Preamp:.*$") {
        $text = [regex]::Replace($text, "(?m)^\s*Preamp:.*$", $replacement, 1)
    } else {
        $text = "$replacement`r`n$text"
    }
    Write-EqApoFile -Path $ProfilePath -Text $text
}

function Get-ZCinemaProfileParams {
    <# Reads the editable values out of a generated profile. #>
    param([Parameter(Mandatory)][string]$ProfilePath)
    $t = Get-Content -LiteralPath $ProfilePath -Raw
    $eqFreqs = @(60, 170, 470, 1200, 2400, 4700, 10000, 14000)
    $eq = New-Object System.Collections.Generic.List[double]
    foreach ($f in $eqFreqs) {
        if ($t -match ("(?m)PK\s+Fc {0} Hz Gain\s*(-?[\d.]+)" -f $f)) { $eq.Add([double]$Matches[1]) } else { $eq.Add(0.0) }
    }
    function Get-One([string]$re, [double]$def) { if ($t -match $re) { return [double]$Matches[1] } else { return $def } }
    $w = Get-One '(?m)Copy:\s*L=L\+(-?[\d.]+)\*R' 0.0
    return [pscustomobject]@{
        PreampDb   = Get-One '(?m)^\s*Preamp:\s*(-?[\d.]+)' -8
        BassGain   = Get-One '(?m)PK\s+Fc 100 Hz Gain\s*(-?[\d.]+)' 6
        SubGain    = Get-One '(?m)PK\s+Fc 45 Hz Gain\s*(-?[\d.]+)' 4
        TrebleGain = Get-One '(?m)PK\s+Fc 8000 Hz Gain\s*(-?[\d.]+)' 4
        DialogGain = Get-One '(?m)PK\s+Fc 3000 Hz Gain\s*(-?[\d.]+)' 3
        Width      = [math]::Abs($w)
        EqFreqs    = $eqFreqs
        EqGains    = $eq.ToArray()
    }
}

function New-ZCinemaProfileText {
    <# Generates the profile text from parameter values (invariant decimal point). #>
    param(
        [double]$PreampDb = -8,
        [double]$BassGain = 6,
        [double]$SubGain = 4,
        [double]$TrebleGain = 4,
        [double]$DialogGain = 3,
        [double]$Width = 0.10,
        [double[]]$EqGains = @(0, 0, 0, 0, 0, 0, 0, 0),
        [int[]]$EqFreqs = @(60, 170, 470, 1200, 2400, 4700, 10000, 14000)
    )
    $ci = [System.Globalization.CultureInfo]::InvariantCulture
    function N1([double]$d) { return $d.ToString('0.#', $ci) }
    function N2([double]$d) { return $d.ToString('0.00', $ci) }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# ZCinema Sound - profile edited by ZCinema-GUI")
    [void]$sb.AppendLine("# Saved: " + (Get-Date -Format s))
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Preamp: " + (N1 $PreampDb) + " dB")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("# Bass (broad low peaking band, works like a bass control)")
    [void]$sb.AppendLine("Filter: ON PK Fc 100 Hz Gain " + (N1 $BassGain) + " dB Q 0.70")
    [void]$sb.AppendLine("Filter: ON PK Fc 45 Hz Gain " + (N1 $SubGain) + " dB Q 1.20")
    [void]$sb.AppendLine("Filter: ON PK Fc 250 Hz Gain -2 dB Q 1.00")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("# Treble (broad high peaking band, works like a treble control)")
    [void]$sb.AppendLine("Filter: ON PK Fc 8000 Hz Gain " + (N1 $TrebleGain) + " dB Q 0.70")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("# Dialogue clarity")
    [void]$sb.AppendLine("Filter: ON PK Fc 3000 Hz Gain " + (N1 $DialogGain) + " dB Q 1.50")
    [void]$sb.AppendLine("")
    if ([math]::Abs($Width) -gt 0.001) {
        [void]$sb.AppendLine("# Width (negative crossfeed)")
        [void]$sb.AppendLine("Copy: L=L+-" + (N2 ([math]::Abs($Width))) + "*R")
        [void]$sb.AppendLine("Copy: R=R+-" + (N2 ([math]::Abs($Width))) + "*L")
        [void]$sb.AppendLine("")
    }
    [void]$sb.AppendLine("# EQ")
    for ($i = 0; $i -lt $EqFreqs.Count; $i++) {
        $g = if ($i -lt $EqGains.Count) { $EqGains[$i] } else { 0 }
        [void]$sb.AppendLine("Filter: ON PK Fc " + $EqFreqs[$i] + " Hz Gain " + (N1 $g) + " dB Q 1.0")
    }
    return $sb.ToString()
}

function Save-ZCinemaProfile {
    <# Writes generated profile text as ASCII (no BOM) so Equalizer APO reloads cleanly. #>
    param(
        [Parameter(Mandatory)][string]$ProfilePath,
        [Parameter(Mandatory)][string]$Text
    )
    Write-EqApoFile -Path $ProfilePath -Text $Text
}

Export-ModuleMember -Function Test-IsAdmin, Get-ZCinemaEndpoint, Get-EqualizerApoRoot,
    Get-EqualizerApoDeviceTool, Test-EqualizerApoAttached, Set-EqApoInclude, Remove-EqApoInclude,
    Set-PreampInProfile, Get-ZCinemaProfileParams, New-ZCinemaProfileText, Save-ZCinemaProfile
