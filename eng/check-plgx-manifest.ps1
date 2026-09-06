param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

# Fails when KeeFetch.plgx.csproj (the legacy project KeePass compiles at
# runtime) drifts from the source files tracked in git. Every production
# .cs/.resx/.png must be listed exactly once, and every listed file must exist.
# Test, tooling, and site folders are excluded because they never ship in the PLGX.

$repoRoot = Split-Path -Parent $PSScriptRoot
$plgxProject = Join-Path $repoRoot "KeeFetch.plgx.csproj"

$excludedPrefixes = @("KeeFetch.Tests/", "eng/", "site/", "docs/", "machine-review/", "review-pilot/", ".github/", ".agent/")
$excludedFiles = @("TestCompile.cs")

function Test-Excluded([string]$path) {
    foreach ($prefix in $excludedPrefixes) {
        if ($path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    foreach ($file in $excludedFiles) {
        if ($path.Equals($file, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

Push-Location $repoRoot
try {
    $tracked = git ls-files -- '*.cs' '*.resx' 'Assets/**' 2>$null
    if ($LASTEXITCODE -ne 0) { throw "git ls-files failed; run from a KeeFetch checkout." }
}
finally {
    Pop-Location
}

$expected = @($tracked | Where-Object { -not (Test-Excluded $_) } | ForEach-Object { $_.Replace('/', '\') } | Sort-Object -Unique)

[xml]$xml = Get-Content -LiteralPath $plgxProject -Raw
$listed = @()
foreach ($group in $xml.Project.ItemGroup) {
    foreach ($item in @($group.Compile) + @($group.EmbeddedResource)) {
        if ($null -ne $item -and $item.Include) { $listed += [string]$item.Include }
    }
}

$duplicates = @($listed | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
$listedUnique = @($listed | Sort-Object -Unique)

$missingFromProject = @($expected | Where-Object { $listedUnique -notcontains $_ })
$staleInProject = @($listedUnique | Where-Object { $expected -notcontains $_ })
$nonexistent = @($listedUnique | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repoRoot $_)) })

$problems = @()
if ($missingFromProject.Count) { $problems += "Tracked sources missing from KeeFetch.plgx.csproj: " + ($missingFromProject -join ', ') }
if ($staleInProject.Count) { $problems += "Listed in KeeFetch.plgx.csproj but not tracked production sources: " + ($staleInProject -join ', ') }
if ($nonexistent.Count) { $problems += "Listed in KeeFetch.plgx.csproj but not on disk: " + ($nonexistent -join ', ') }
if ($duplicates.Count) { $problems += "Listed more than once in KeeFetch.plgx.csproj: " + ($duplicates -join ', ') }

if ($problems.Count) {
    $problems | ForEach-Object { Write-Error -Message $_ -ErrorAction Continue }
    exit 1
}

if (-not $Quiet) {
    Write-Output ("PLGX manifest OK: {0} files listed match tracked production sources." -f $listedUnique.Count)
}
exit 0
