param(
    # Tag being released, e.g. v1.3.0 or refs/tags/v1.3.0. Omit to check only internal consistency.
    [string]$Tag
)

$ErrorActionPreference = "Stop"

# KeePass polls version.txt from master for update checks, so version.txt,
# AssemblyVersion, AssemblyFileVersion, and (on release) the git tag must agree.

$repoRoot = Split-Path -Parent $PSScriptRoot

$versionTxt = (Get-Content -LiteralPath (Join-Path $repoRoot "version.txt")) |
    Where-Object { $_ -match '^\d+\.\d+\.\d+(\.\d+)?$' } | Select-Object -First 1
if (-not $versionTxt) { throw "version.txt does not contain a version line." }

$assemblyInfo = Get-Content -LiteralPath (Join-Path $repoRoot "Properties\AssemblyInfo.cs") -Raw
$asmVersion = [regex]::Match($assemblyInfo, 'AssemblyVersion\("([^"]+)"\)').Groups[1].Value
$fileVersion = [regex]::Match($assemblyInfo, 'AssemblyFileVersion\("([^"]+)"\)').Groups[1].Value
if (-not $asmVersion -or -not $fileVersion) { throw "AssemblyInfo.cs is missing AssemblyVersion or AssemblyFileVersion." }

function Normalize([string]$v) {
    $parts = $v.Split('.')
    while ($parts.Count -lt 4) { $parts += '0' }
    return ($parts[0..3] -join '.')
}

$problems = @()
if ((Normalize $versionTxt) -ne (Normalize $asmVersion)) {
    $problems += "version.txt ($versionTxt) != AssemblyVersion ($asmVersion)"
}
if ((Normalize $asmVersion) -ne (Normalize $fileVersion)) {
    $problems += "AssemblyVersion ($asmVersion) != AssemblyFileVersion ($fileVersion)"
}

if ($Tag) {
    $tagVersion = ($Tag -replace '^refs/tags/', '') -replace '^v', ''
    if ((Normalize $tagVersion) -ne (Normalize $versionTxt)) {
        $problems += "Release tag ($Tag) != version.txt ($versionTxt)"
    }
    $changelog = Get-Content -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Raw
    if ($changelog -notmatch ('(?m)^## \[' + [regex]::Escape($tagVersion) + '\]')) {
        $problems += "CHANGELOG.md has no '## [$tagVersion]' section"
    }
}

if ($problems.Count) {
    $problems | ForEach-Object { Write-Error -Message $_ -ErrorAction Continue }
    exit 1
}

Write-Output ("Version OK: {0}" -f $versionTxt)
exit 0
