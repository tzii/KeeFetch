param(
    [switch]$KeepOutput
)

$ErrorActionPreference = "Stop"

# Policy smokes: run the two smoke experiments through the REAL runner and
# prove end to end that recorded policy == executable policy == benchmark
# policy. Unlike test-benchmark-harness.ps1 this gate needs the built
# Release binary, KeePass, and live network; it is part of the launch gate,
# not the offline self-test suite.
#
# Fail-closed by construction: a non-empty smoke output root is stale
# evidence and aborts the gate (quarantine it first), and every assertion
# below names the exact defect that failed.

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$runnerPath = Join-Path $repoRoot "eng\benchmark-presets.ps1"
$prepPath = Join-Path $PSScriptRoot "prepare-review.ps1"
$selPath = Join-Path $PSScriptRoot "select-profiles.ps1"
Import-Module (Join-Path $PSScriptRoot "BenchmarkHarness.psm1") -Force

$currentHarnessFingerprint = Get-KeeFetchHarnessFingerprint -RepoRoot $repoRoot

$chainDisplayNames = @{
    "direct-site" = "Direct Site"
    "twenty-icons" = "Twenty Icons"
    "duckduckgo" = "DuckDuckGo"
    "google" = "Google"
    "yandex" = "Yandex"
    "favicone" = "Favicone"
    "icon-horse" = "Icon Horse"
}
$pseudoMetricProviders = @("Cache", "Coalesced", "Pipeline", "Google Play", "Harness")
$knownOutcomeVocab = @("empty", "timeout", "cancelled", "candidate", "error", "hit", "negative-hit", "shared", "skipped-budget-exhausted", "harness-error")

# Independent v2 mirror of FetchExecutionPolicy.CanonicalForm(). If this
# drifts from the C# the smoke fails, which is the point: the runner records
# the C# fingerprint, and this gate recomputes it from the experiment
# definition alone.
function Get-SmokeCandidateFingerprint {
    param([Parameter(Mandatory=$true)][object]$Def)
    $providerIds = @($Def.providerIds)
    $syn = "0"; if ([bool]$Def.allowSynthetic) { $syn = "1" }
    $stp = "0"; if ([bool]$Def.stopAfterStrongResolved) { $stp = "1" }
    $and = "0"; if ([bool]$Def.allowAndroidStoreLookup) { $and = "1" }
    $canonical = "v2|providers={0}|primaryMs={1}|fallbackMs={2}|cumulativeMs={3}|synthetic={4}|stopAfterStrongResolved={5}|androidStore={6}" -f `
        ($providerIds -join ","), [int]$Def.primaryTimeout, [int]$Def.fallbackTimeout, [int]$Def.cumulativeTimeout, $syn, $stp, $and
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
        return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace("-", "").ToLowerInvariant()
    } finally { $sha.Dispose() }
}

function Get-SmokeMetricEntries {
    param([Parameter(Mandatory=$true)][string]$ProviderMetricsJson, [Parameter(Mandatory=$true)][string]$Where)
    if ([string]::IsNullOrWhiteSpace($ProviderMetricsJson)) { return @() }
    $parsed = $null
    try { $parsed = ConvertFrom-Json -InputObject $ProviderMetricsJson }
    catch { throw "provider_metrics is not parseable JSON for ${Where}." }
    if ($null -ne $parsed -and -not ($parsed -is [System.Array])) {
        throw "provider_metrics for ${Where} is not a JSON array."
    }
    return @($parsed)
}

function Assert-SmokeCondition {
    param([Parameter(Mandatory=$true)][bool]$Condition, [Parameter(Mandatory=$true)][string]$Failure)
    if (-not $Condition) { throw $Failure }
}

# Runs one smoke experiment and verifies its evidence. $StopPairs maps a
# stop=true candidate id to its same-budget stop=false counterpart id.
function Invoke-SmokeExperiment {
    param(
        [Parameter(Mandatory=$true)][string]$ExperimentName,
        [hashtable]$StopPairs = @{}
    )

    $expPath = Join-Path $PSScriptRoot ("experiments\" + $ExperimentName + ".json")
    $exp = Get-Content -Raw -LiteralPath $expPath | ConvertFrom-Json
    $outputRoot = Join-Path $repoRoot $exp.output_root

    # Fail closed on stale roots: any existing run.json under the root is
    # pre-existing evidence that must be quarantined before a rerun.
    if (Test-Path -LiteralPath $outputRoot) {
        $staleRuns = @(Get-ChildItem -LiteralPath $outputRoot -Recurse -Filter "run.json" -ErrorAction SilentlyContinue)
        Assert-SmokeCondition ($staleRuns.Count -eq 0) `
            "Smoke output root $outputRoot already contains $($staleRuns.Count) run(s); quarantine it before rerunning the smoke."
    }

    Write-Host ("== smoke {0} ==" -f $ExperimentName)
    $runnerOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $runnerPath -Experiment $expPath
    $runnerOutput | Write-Host
    Assert-SmokeCondition ($LASTEXITCODE -eq 0) "Runner failed for smoke $ExperimentName (exit $LASTEXITCODE)."

    Assert-SmokeCondition (Test-Path -LiteralPath (Join-Path $outputRoot "schedule.json")) `
        "schedule.json missing from smoke root $outputRoot."

    $expectedFixtures = @($exp.fixture_ids)
    $fixtureSet = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
    foreach ($f in $expectedFixtures) { [void]$fixtureSet.Add([string]$f) }

    $defsByFingerprint = @{}
    foreach ($def in @($exp.candidates)) {
        $fp = Get-SmokeCandidateFingerprint -Def $def
        Assert-SmokeCondition (-not $defsByFingerprint.ContainsKey($fp)) `
            ("Candidates '{0}' and '{1}' share one policy fingerprint; the smoke cannot distinguish them." -f $defsByFingerprint[$fp], $def.id)
        $defsByFingerprint[$fp] = [string]$def.id
    }

    $runDirs = @()
    foreach ($dir in @(Get-ChildItem -LiteralPath $outputRoot -Directory)) {
        if (Test-Path -LiteralPath (Join-Path $dir.FullName "run.json")) { $runDirs += $dir.FullName }
    }
    Assert-SmokeCondition ($runDirs.Count -eq @($exp.candidates).Count) `
        ("Expected {0} measured runs under {1} but found {2}." -f @($exp.candidates).Count, $outputRoot, $runDirs.Count)

    $uniform = @{}
    $rowsByCandidate = @{}
    foreach ($rd in $runDirs) {
        $meta = Get-Content -Raw -LiteralPath (Join-Path $rd "run.json") | ConvertFrom-Json
        Assert-SmokeCondition ([string]$meta.status -eq "complete") "Smoke run $rd is not complete."
        Assert-SmokeCondition ([string]$meta.run_kind -eq "measured") "Smoke run $rd is not a measured run."
        Assert-SmokeCondition (@($exp.cache_modes) -contains [string]$meta.cache_mode) "Smoke run $rd has cache mode '$($meta.cache_mode)' outside the definition."
        foreach ($field in @("experiment_fingerprint", "corpus_fingerprint", "binary_hash", "execution_harness_fingerprint")) {
            Assert-SmokeCondition (-not [string]::IsNullOrWhiteSpace([string]$meta.$field)) "Smoke run $rd is missing $field."
            if ($uniform.ContainsKey($field)) {
                Assert-SmokeCondition ($uniform[$field] -eq [string]$meta.$field) "Smoke run $rd has a mixed $field."
            } else {
                $uniform[$field] = [string]$meta.$field
            }
        }
        Assert-SmokeCondition ($uniform["execution_harness_fingerprint"] -eq $currentHarnessFingerprint) `
            "Smoke evidence harness fingerprint does not match the current harness."

        $candId = [string]$meta.candidate_id
        $expectedFp = $null
        foreach ($def in @($exp.candidates)) { if ([string]$def.id -eq $candId) { $expectedFp = Get-SmokeCandidateFingerprint -Def $def } }
        Assert-SmokeCondition ($null -ne $expectedFp) "Smoke run $rd references candidate '$candId' not in the experiment definition."
        Assert-SmokeCondition ([string]$meta.policy_fingerprint -eq $expectedFp) `
            ("Recorded policy fingerprint for '{0}' does not equal the fingerprint recomputed from the experiment definition." -f $candId)

        $rows = @(Import-Csv -LiteralPath (Join-Path $rd "rows.csv"))
        Assert-SmokeCondition ($rows.Count -eq $expectedFixtures.Count) `
            ("Smoke run {0} has {1} rows, expected {2}." -f $rd, $rows.Count, $expectedFixtures.Count)
        $seenFixtures = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
        $rowsByCandidate[$candId] = $rows
        foreach ($row in $rows) {
            Assert-SmokeCondition ($fixtureSet.Contains([string]$row.fixture_id)) `
                ("Smoke run {0} contains unexpected fixture '{1}'." -f $rd, $row.fixture_id)
            [void]$seenFixtures.Add([string]$row.fixture_id)
        }
        Assert-SmokeCondition ($seenFixtures.Count -eq $expectedFixtures.Count) `
            ("Smoke run {0} covers {1} of {2} declared fixtures." -f $rd, $seenFixtures.Count, $expectedFixtures.Count)
    }

    # Policy semantics over the recorded rows.
    foreach ($candId in @($rowsByCandidate.Keys)) {
        $def = $null
        foreach ($d in @($exp.candidates)) { if ([string]$d.id -eq $candId) { $def = $d } }
        $stopEnabled = [bool]$def.stopAfterStrongResolved
        $chainProviders = @($def.providerIds | ForEach-Object { $chainDisplayNames[[string]$_] })
        $androidAllowed = [bool]$def.allowAndroidStoreLookup

        foreach ($row in @($rowsByCandidate[$candId])) {
            $where = "fixture '$($row.fixture_id)' (candidate '$candId')"
            $entries = @(Get-SmokeMetricEntries -ProviderMetricsJson ([string]$row.provider_metrics) -Where $where)
            $outcome = ([string]$row.machine_outcome).ToLowerInvariant()
            if ($outcome -eq "success") {
                Assert-SmokeCondition ($entries.Count -gt 0) "Successful fetch with zero provider activity for ${where}."
            }
            $providerCalls = 0
            foreach ($m in $entries) {
                $name = ([string]$m.provider).Trim()
                Assert-SmokeCondition ($chainDisplayNames.ContainsValue($name) -or ($pseudoMetricProviders -contains $name)) `
                    "provider_metrics for ${where} names unknown provider '$name'."
                Assert-SmokeCondition ($knownOutcomeVocab -contains ([string]$m.outcome).Trim().ToLowerInvariant()) `
                    "provider_metrics for ${where} records unknown outcome '$($m.outcome)'."
                Assert-SmokeCondition (([int]$m.calls) -ge 1) "provider_metrics for ${where} records calls < 1 for '$name'."
                if (-not ($pseudoMetricProviders -contains $name)) { $providerCalls += [int]$m.calls }
                if ($name -eq "Google Play") {
                    Assert-SmokeCondition $androidAllowed "Google Play lookup recorded for ${where} although allowAndroidStoreLookup=false."
                }
            }
            # stop=false queries every source in the chain before selecting.
            if ($outcome -eq "success" -and -not $stopEnabled) {
                foreach ($providerName in $chainProviders) {
                    $providerEntry = @($entries | Where-Object { (([string]$_.provider).Trim()) -eq $providerName })
                    Assert-SmokeCondition ($providerEntry.Count -ge 1) `
                        ("stop=false candidate '{0}' succeeded on fixture '{1}' without querying chain provider '{2}'." -f $candId, $row.fixture_id, $providerName)
                }
            }
            Add-Member -InputObject $row -NotePropertyName "_smokeProviderCalls" -NotePropertyValue $providerCalls -Force
        }
    }

    # stop=true never makes more provider calls than its same-budget
    # stop=false counterpart on the same fixture.
    foreach ($stopCand in @($StopPairs.Keys)) {
        $nostopCand = $StopPairs[$stopCand]
        foreach ($stopRow in @($rowsByCandidate[$stopCand])) {
            $nostopRow = @($rowsByCandidate[$nostopCand] | Where-Object { [string]$_.fixture_id -eq [string]$stopRow.fixture_id })[0]
            if ($null -eq $nostopRow) { continue }
            Assert-SmokeCondition ([int]$stopRow._smokeProviderCalls -le [int]$nostopRow._smokeProviderCalls) `
                ("stop=true candidate '{0}' made more provider calls ({1}) than stop=false '{2}' ({3}) on fixture '{4}'." -f `
                $stopCand, $stopRow._smokeProviderCalls, $nostopCand, $nostopRow._smokeProviderCalls, $stopRow.fixture_id)
        }
    }

    Write-Host ("smoke {0}: {1} runs verified (fingerprinted policies distinguishable)." -f $ExperimentName, $runDirs.Count)
    return $outputRoot
}

$policyRoot = Invoke-SmokeExperiment -ExperimentName "smoke-policy-check" -StopPairs @{
    "cand-smoke-fast-stop" = "cand-smoke-balanced-nostop"
    "cand-smoke-balanced-stop" = "cand-smoke-balanced-nostop"
}
[void](Invoke-SmokeExperiment -ExperimentName "smoke-distinguish-v13")

# End-to-end census + selection over REAL runner output. Labels here are
# automated placeholders for a mechanism smoke only (reviewer
# 'smoke-automated'); this is never study evidence and nothing is published.
$queuePath = Join-Path $policyRoot "review-queue.csv"
& $prepPath -RunDir $policyRoot -OutputPath $queuePath | Out-Null
$queueRows = @(Import-Csv -LiteralPath $queuePath)
Assert-SmokeCondition ($queueRows.Count -gt 0) "Smoke census queue is empty."
foreach ($q in $queueRows) {
    $q.review_label = "correct"
    $q.reviewer = "smoke-automated"
    $q.reviewed_at_utc = "2026-01-01T00:00:00Z"
}
$queueRows | Export-Csv -LiteralPath $queuePath -NoTypeInformation -Encoding UTF8
& $prepPath -RunDir $policyRoot -OutputPath $queuePath -Validate | Out-Null

$selectionOut = Join-Path $policyRoot "selection"
& $selPath -RunDir $policyRoot -ReviewQueue $queuePath -OutputDir $selectionOut | Out-Null
foreach ($artifact in @("selection-summary.json", "selection-report.md", "FetchProfileCatalog.Generated.cs", "v1.3-provider-study.md")) {
    Assert-SmokeCondition (Test-Path -LiteralPath (Join-Path $selectionOut $artifact)) "Smoke selection missing artifact $artifact."
}
$summary = Get-Content -Raw -LiteralPath (Join-Path $selectionOut "selection-summary.json") | ConvertFrom-Json
Assert-SmokeCondition ([string]$summary.execution_harness_fingerprint -eq $currentHarnessFingerprint) `
    "Smoke selection harness fingerprint mismatch."
Assert-SmokeCondition ([string]$summary.winners.privacy -eq "cand-smoke-direct-only") `
    ("Smoke privacy winner expected cand-smoke-direct-only, got '{0}'." -f $summary.winners.privacy)
foreach ($role in @("bulk-fast", "everyday", "privacy", "max-coverage")) {
    Assert-SmokeCondition (-not [string]::IsNullOrWhiteSpace([string]$summary.winners.$role)) "Smoke selection missing winner for role $role."
}

Write-Output ("smoke selection: census {0} units, 4 winners resolved on real runner output." -f $queueRows.Count)

if (-not $KeepOutput) {
    Remove-Item -LiteralPath $policyRoot -Recurse -Force
    $distinguishRoot = Join-Path $repoRoot "eng\benchmark-runs\smoke-distinguish-v13"
    if (Test-Path -LiteralPath $distinguishRoot) { Remove-Item -LiteralPath $distinguishRoot -Recurse -Force }
    Write-Output "Smoke output roots removed (rerun with -KeepOutput to inspect)."
}

Write-Output "Policy smokes passed."
