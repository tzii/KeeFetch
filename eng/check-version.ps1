param(
    # Tag being released, e.g. v1.4.0 or refs/tags/v1.4.0. Omit to check only internal consistency.
    [string]$Tag
)

$ErrorActionPreference = "Stop"

# KeePass polls version.txt from master for update checks, so version.txt,
# AssemblyVersion, AssemblyFileVersion, and (on release) the git tag must agree.

$repoRoot = Split-Path -Parent $PSScriptRoot

# KeePass's version information format: a header line holding only the
# separator, "<AssemblyTitle><separator><AssemblyFileVersion>" lines, and a
# footer line holding only the separator. KeePass ignores any other layout.
$feedLines = @(Get-Content -LiteralPath (Join-Path $repoRoot "version.txt") |
    ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
if ($feedLines.Count -lt 3 -or $feedLines[0] -ne ':' -or $feedLines[-1] -ne ':') {
    throw "version.txt must start and end with a line containing only ':'."
}

$assemblyInfo = Get-Content -LiteralPath (Join-Path $repoRoot "Properties\AssemblyInfo.cs") -Raw
$title = [regex]::Match($assemblyInfo, 'AssemblyTitle\("([^"]+)"\)').Groups[1].Value
if (-not $title) { throw "AssemblyInfo.cs is missing AssemblyTitle." }
$componentLines = @($feedLines[1..($feedLines.Count - 2)] | Where-Object { $_ -like "$($title):*" })
if ($componentLines.Count -ne 1) { throw "version.txt must contain exactly one '$($title):<version>' line." }
$versionTxt = $componentLines[0].Substring($title.Length + 1)
if ($versionTxt -notmatch '^\d+(\.\d+){1,3}$') { throw "version.txt has an invalid version '$versionTxt'." }

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
