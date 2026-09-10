param([string]$Experiment = (Join-Path $PSScriptRoot 'experiments/profile-candidates-v13-complete.json'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Import-Module (Join-Path $PSScriptRoot 'BenchmarkHarness.psm1') -Force
$definition = Read-KeeFetchExperiment -ExperimentPath $Experiment
$raw = Get-Content -Raw -LiteralPath $Experiment | ConvertFrom-Json
$ids = @($raw.candidates | ForEach-Object { [string]$_.id })
if ($ids.Count -ne 19 -or @($ids | Select-Object -Unique).Count -ne 19) {
    throw 'Complete study requires exactly 19 unique candidates.'
}
if (($ids -join '|') -cne (@($raw.profiles) -join '|')) { throw 'Ordered candidate/profile IDs differ.' }
if ($raw.experiment_id -ne 'profile-candidates-v13-complete' -or
    $raw.output_root -ne 'eng/benchmark-runs/profile-candidates-v13-complete') { throw 'Study identity/output root mismatch.' }
if ($raw.repetitions -ne 3 -or $raw.concurrency -ne 8 -or
    (@($raw.cache_modes) -join '|') -ne 'cold|warm' -or $raw.schedule_seed -ne 20260827) {
    throw 'Study schedule contract changed.'
}
if ($raw.corpus -ne 'KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv') { throw 'Corpus path changed.' }
$corpus = Join-Path $repoRoot $raw.corpus
Test-KeeFetchCorpus -CsvPath $corpus -VocabularyPath (Join-Path (Split-Path $corpus) 'categories.json') | Out-Null
if (@(Import-Csv -LiteralPath $corpus).Count -ne 300) { throw 'Expected 300 fixtures.' }
$original = Get-Content -Raw (Join-Path $PSScriptRoot 'experiments/profile-candidates-v13.json') | ConvertFrom-Json
foreach ($old in $original.candidates) {
    $current = @($raw.candidates | Where-Object { $_.id -eq $old.id })
    if ($current.Count -ne 1 -or ($current[0] | ConvertTo-Json -Depth 10 -Compress) -cne
        ($old | ConvertTo-Json -Depth 10 -Compress)) { throw "Original candidate changed: $($old.id)" }
}
$missing = @($raw.candidates | Where-Object { $_.id -eq 'cand-full-minus-yandex-thorough-synth' })
if ($missing.Count -ne 1 -or (@($missing[0].providerIds) -join '|') -ne
    'direct-site|twenty-icons|duckduckgo|google|favicone|icon-horse') { throw 'Missing Yandex ablation has wrong chain.' }
if ($missing[0].primaryTimeout -ne 10000 -or $missing[0].fallbackTimeout -ne 5000 -or
    $missing[0].cumulativeTimeout -ne 45000 -or !$missing[0].allowSynthetic -or
    $missing[0].stopAfterStrongResolved -or !$missing[0].allowAndroidStoreLookup) { throw 'Yandex ablation policy mismatch.' }

# Use the production constructor: it validates provider IDs through the catalog.
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'bin/Release/net48/KeeFetch.dll'))
$policyType = $assembly.GetType('KeeFetch.FetchProfiles.FetchExecutionPolicy', $true)
$ctor = $policyType.GetConstructors()[0]
$fingerprints = @{}
foreach ($candidate in $raw.candidates) {
    $policy = $ctor.Invoke([object[]]@([string[]]$candidate.providerIds,
        [int]$candidate.primaryTimeout, [int]$candidate.fallbackTimeout, [int]$candidate.cumulativeTimeout,
        [bool]$candidate.allowSynthetic, [bool]$candidate.stopAfterStrongResolved, [bool]$candidate.allowAndroidStoreLookup))
    $fingerprints[$candidate.id] = [string]$policyType.GetMethod('Fingerprint').Invoke($policy, $null)
}
$ablationHash = $fingerprints['cand-full-minus-yandex-thorough-synth']
foreach ($id in $ids) {
    if ($id -ne 'cand-full-minus-yandex-thorough-synth' -and $fingerprints[$id] -eq $ablationHash) {
        throw "Yandex ablation collides with $id"
    }
}
$canonicalBefore = $raw | ConvertTo-Json -Depth 20 -Compress
$raw.candidates = @($raw.candidates | Where-Object { $_.id -ne 'cand-full-minus-yandex-thorough-synth' })
$raw.profiles = @($raw.profiles | Where-Object { $_ -ne 'cand-full-minus-yandex-thorough-synth' })
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $before = [Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonicalBefore)))
    $after = [Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($raw | ConvertTo-Json -Depth 20 -Compress))))
    if ($before -eq $after) { throw 'Removing the ablation did not change experiment identity.' }
} finally { $sha.Dispose() }
Write-Output "Complete study contract passed: 19 candidates, 300 fixtures, distinct Yandex policy $ablationHash."
