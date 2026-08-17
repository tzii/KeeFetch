param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"

# Exports visible managed profiles from the built KeeFetch catalog to
# site/data/profiles.json so the website surfaces the same data the plugin
# ships. Deterministic output: fixed field order, two-space indent, UTF-8
# without BOM, trailing newline. -Check compares without writing.

$repoRoot = Split-Path -Parent $PSScriptRoot
$keepassPath = "C:\Program Files\KeePass Password Safe 2\KeePass.exe"
$keepassPathEnv = [Environment]::GetEnvironmentVariable('KEEFETCH_KEEPASS_PATH')
if (-not [string]::IsNullOrWhiteSpace($keepassPathEnv)) {
    $keepassPath = $keepassPathEnv
}
$assemblyPath = Join-Path $repoRoot "bin\Release\net48\KeeFetch.dll"
$outPath = Join-Path $repoRoot "site\data\profiles.json"

if (-not (Test-Path -LiteralPath $keepassPath)) {
    throw "KeePass.exe not found at $keepassPath (set KEEFETCH_KEEPASS_PATH to override)."
}
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build KeeFetch first. Missing $assemblyPath"
}

[void][Reflection.Assembly]::LoadFrom($keepassPath)
$keefetchAssembly = [Reflection.Assembly]::LoadFrom($assemblyPath)

$catalogType = $keefetchAssembly.GetType("KeeFetch.FetchProfiles.FetchProfileCatalog", $true)
$profilesProperty = $catalogType.GetProperty("ManagedProfiles", [Reflection.BindingFlags] "Public, Static")
if ($null -eq $profilesProperty) {
    throw "ManagedProfiles property not found on FetchProfileCatalog."
}

function ConvertTo-JsonString {
    param([Parameter(Mandatory=$true)][string]$Value)
    $escaped = $Value.Replace("\", "\\").Replace('"', '\"')
    $escaped = $escaped.Replace("`r", "\r").Replace("`n", "\n").Replace("`t", "\t")
    return '"' + $escaped + '"'
}

function ConvertTo-JsonProperty {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$ValueJson,
        [Parameter(Mandatory=$true)][int]$IndentLevel
    )
    $pad = "  " * $IndentLevel
    return $pad + (ConvertTo-JsonString -Value $Name) + ": " + $ValueJson
}

$profiles = @($profilesProperty.GetValue($null, $null))
$visible = @($profiles | Where-Object { [bool]$_.IsVisible })

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("{")
$lines.Add("  " + '"schema": 1,')
$lines.Add("  " + '"source": "FetchProfileCatalog.ManagedProfiles",')
$lines.Add('  "profiles": [')
for ($i = 0; $i -lt $visible.Count; $i++) {
    $p = $visible[$i]
    $isLast = ($i -eq $visible.Count - 1)

    $lines.Add("    {")
    $lines.Add((ConvertTo-JsonProperty -Name "id" -ValueJson (ConvertTo-JsonString -Value ([string]$p.Id)) -IndentLevel 3) + ",")
    $lines.Add((ConvertTo-JsonProperty -Name "displayName" -ValueJson (ConvertTo-JsonString -Value ([string]$p.DisplayName)) -IndentLevel 3) + ",")
    $lines.Add((ConvertTo-JsonProperty -Name "description" -ValueJson (ConvertTo-JsonString -Value ([string]$p.Description)) -IndentLevel 3) + ",")
    $lines.Add((ConvertTo-JsonProperty -Name "intendedUse" -ValueJson (ConvertTo-JsonString -Value ([string]$p.IntendedUse)) -IndentLevel 3) + ",")

    $providerIds = @($p.ProviderIds)
    $lines.Add((ConvertTo-JsonProperty -Name "providerIds" -ValueJson "[" -IndentLevel 3))
    for ($j = 0; $j -lt $providerIds.Count; $j++) {
        $providerJson = (ConvertTo-JsonString -Value ([string]$providerIds[$j]))
        if ($j -lt $providerIds.Count - 1) { $providerJson = $providerJson + "," }
        $lines.Add("        " + $providerJson)
    }
    $lines.Add("      ],")

    $lines.Add((ConvertTo-JsonProperty -Name "primaryTimeoutMs" -ValueJson ([string]$p.PrimaryTimeoutMs) -IndentLevel 3) + ",")
    $lines.Add((ConvertTo-JsonProperty -Name "fallbackTimeoutMs" -ValueJson ([string]$p.FallbackTimeoutMs) -IndentLevel 3) + ",")
    $lines.Add((ConvertTo-JsonProperty -Name "cumulativeTimeoutMs" -ValueJson ([string]$p.CumulativeTimeoutMs) -IndentLevel 3) + ",")
    $syntheticJson = "false"
    if ([bool]$p.AllowSyntheticFallbacks) { $syntheticJson = "true" }
    $lines.Add((ConvertTo-JsonProperty -Name "allowSyntheticFallbacks" -ValueJson $syntheticJson -IndentLevel 3) + ",")
    $visibleJson = "false"
    if ([bool]$p.IsVisible) { $visibleJson = "true" }
    $lines.Add((ConvertTo-JsonProperty -Name "isVisible" -ValueJson $visibleJson -IndentLevel 3) + ",")
    $lines.Add((ConvertTo-JsonProperty -Name "evidenceReport" -ValueJson (ConvertTo-JsonString -Value ([string]$p.EvidenceReport)) -IndentLevel 3))

    if ($isLast) {
        $lines.Add("    }")
    } else {
        $lines.Add("    },")
    }
}
$lines.Add("  ]")
$lines.Add("}")

$content = ($lines -join [Environment]::NewLine) + [Environment]::NewLine

if ($Check) {
    if (-not (Test-Path -LiteralPath $outPath)) {
        Write-Output "profile data check failed: $outPath does not exist. Run eng/export-profile-data.ps1 without -Check to generate it."
        exit 1
    }
    $existing = [System.IO.File]::ReadAllText($outPath)
    if ($existing -ne $content) {
        $existingLines = @($existing -split "\r?\n")
        $contentLines = @($content -split "\r?\n")
        $diffShown = 0
        $maxLines = [Math]::Max($existingLines.Count, $contentLines.Count)
        for ($i = 0; $i -lt $maxLines -and $diffShown -lt 10; $i++) {
            $a = if ($i -lt $existingLines.Count) { $existingLines[$i] } else { "<missing>" }
            $b = if ($i -lt $contentLines.Count) { $contentLines[$i] } else { "<missing>" }
            if ($a -ne $b) {
                Write-Output ("line {0}: committed: {1}" -f ($i + 1), $a)
                Write-Output ("line {0}: catalog:   {1}" -f ($i + 1), $b)
                $diffShown++
            }
        }
        Write-Output "profile data check failed: $outPath does not match FetchProfileCatalog.ManagedProfiles. Regenerate with eng/export-profile-data.ps1."
        exit 1
    }
    Write-Output "profile data check passed: $outPath matches the managed catalog."
    exit 0
}

$outDir = Split-Path -Parent $outPath
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outPath, $content, $utf8NoBom)
Write-Output "profile data written: $outPath ($($visible.Count) visible profiles)"
