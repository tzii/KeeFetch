param(
    [string]$RunDir,
    [string]$ReviewQueue,
    [string]$OutputDir,
    [string]$ExperimentFile,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

# Selects the v1.3 managed profiles from a completed benchmark experiment.
#
# Fail-closed provenance: the exact experiment definition is recovered from
# the experiment fingerprint recorded by every run; candidate configuration
# comes only from that definition (no heuristic reconstruction), and the
# complete expected matrix - every candidate, cache mode, and repetition,
# with exact row counts and matching policy fingerprints - must be present
# exactly once.
#
# Human review and machine outcomes are separate evidence: unreviewed machine
# successes are never counted as correct. Label estimates use the
# targeted+stratified-sample design (targeted units weigh 1; sampled units
# weigh stratum population / sample) with Wilson confidence intervals, and
# every winner rule is checked for ambiguity sensitivity before selection.
#
# Without -Publish nothing outside OutputDir is written. With -Publish the
# generated catalog and the canonical evidence report are written into the
# repository after all gates pass.

function FindRepoRoot {
    $candidate = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($candidate)) { $candidate = (Get-Location).Path }
    $root = Split-Path -Parent (Split-Path -Parent $candidate)
    if (Test-Path -LiteralPath (Join-Path $root "KeeFetch.sln")) { return (Resolve-Path -LiteralPath $root).Path }
    $cur = (Get-Location).Path
    for ($i = 0; $i -lt 5; $i++) {
        if (Test-Path -LiteralPath (Join-Path $cur "KeeFetch.sln")) { return (Resolve-Path -LiteralPath $cur).Path }
        $cur = Split-Path -Parent $cur
        if ([string]::IsNullOrWhiteSpace($cur)) { break }
    }
    return (Resolve-Path -LiteralPath $root).Path
}

function ParseBoolValue {
    param([object]$Value)
    if ($Value -is [bool]) { return [bool]$Value }
    if ($null -eq $Value) { return $false }
    $s = [string]$Value
    if ([string]::IsNullOrWhiteSpace($s)) { return $false }
    $t = $s.Trim().ToLowerInvariant()
    if ($t -eq "true" -or $t -eq "1" -or $t -eq "yes") { return $true }
    return $false
}

function Get-Sha256HexFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $hash = $sha.ComputeHash($stream)
        } finally {
            $stream.Dispose()
        }
        return [BitConverter]::ToString($hash).Replace("-","").ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-PercentileFromSorted {
    param([long[]]$SortedValues, [double]$Percent)
    if ($SortedValues.Count -eq 0) { return 0 }
    $index = [Math]::Ceiling($Percent * $SortedValues.Count) - 1
    if ($index -lt 0) { $index = 0 }
    if ($index -ge $SortedValues.Count) { $index = $SortedValues.Count - 1 }
    return $SortedValues[$index]
}

function Get-WilsonInterval {
    param([double]$Successes, [double]$Total, [double]$Z = 1.96)
    if ($Total -le 0) { return @{ Lower = 0.0; Upper = 0.0 } }
    $p = $Successes / $Total
    $z2 = $Z * $Z
    $denom = 1.0 + $z2 / $Total
    $center = ($p + $z2 / (2.0 * $Total)) / $denom
    $margin = ($Z * [Math]::Sqrt(($p * (1.0 - $p) + $z2 / (4.0 * $Total)) / $Total)) / $denom
    $lower = $center - $margin
    $upper = $center + $margin
    if ($lower -lt 0) { $lower = 0.0 }
    if ($upper -gt 1) { $upper = 1.0 }
    return @{ Lower = $lower; Upper = $upper }
}

$repoRoot = FindRepoRoot

if ([string]::IsNullOrWhiteSpace($RunDir)) {
    $defaultRunRoot = Join-Path $repoRoot "eng/benchmark-runs/profile-candidates-v13"
    if (Test-Path -LiteralPath $defaultRunRoot) { $RunDir = $defaultRunRoot } else { $RunDir = $repoRoot }
}
if ([string]::IsNullOrWhiteSpace($ReviewQueue)) {
    $ReviewQueue = Join-Path $RunDir "review-queue.csv"
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $RunDir "selection"
}

if (-not (Test-Path -LiteralPath $RunDir)) { throw "RunDir not found: $RunDir" }
if (-not (Test-Path -LiteralPath $ReviewQueue)) { throw "ReviewQueue not found: $ReviewQueue" }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$runDirResolved = (Resolve-Path -LiteralPath $RunDir).Path
$reviewQueueResolved = (Resolve-Path -LiteralPath $ReviewQueue).Path
$outputDirResolved = (Resolve-Path -LiteralPath $OutputDir).Path

# ---------------------------------------------------------------------------
# 1. Discover runs and enforce provenance + the exact expected matrix
# ---------------------------------------------------------------------------

$runDirs = @()
$directRunJson = Join-Path $runDirResolved "run.json"
if (Test-Path -LiteralPath $directRunJson) { $runDirs += $runDirResolved }
foreach ($dir in @(Get-ChildItem -LiteralPath $runDirResolved -Directory -ErrorAction SilentlyContinue)) {
    if (Test-Path -LiteralPath (Join-Path $dir.FullName "run.json")) { $runDirs += $dir.FullName }
}
if ($runDirs.Count -eq 0) { throw "No run.json found under RunDir: $runDirResolved" }

$runs = @()
foreach ($rd in $runDirs) {
    $meta = Get-Content -Raw -LiteralPath (Join-Path $rd "run.json") | ConvertFrom-Json
    $runs += [PSCustomObject]@{ Directory = $rd; Meta = $meta }
}

# One experiment, one corpus, one binary.
$experimentFingerprint = $null
$corpusFingerprint = $null
$binaryHash = $null
$experimentId = $null
$scheduleSeed = 0
foreach ($r in $runs) {
    foreach ($field in @('experiment_fingerprint','corpus_fingerprint','binary_hash')) {
        $value = ""
        if ($r.Meta.PSObject.Properties.Name -contains $field) { $value = [string]$r.Meta.$field }
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "Run $($r.Directory) is missing $field; pre-fingerprint evidence cannot be selected from."
        }
    }
    $ef = [string]$r.Meta.experiment_fingerprint
    $cf = [string]$r.Meta.corpus_fingerprint
    $bh = [string]$r.Meta.binary_hash
    if ($null -eq $experimentFingerprint) {
        $experimentFingerprint = $ef
        $corpusFingerprint = $cf
        $binaryHash = $bh
        if ($r.Meta.PSObject.Properties.Name -contains 'experiment_id') { $experimentId = [string]$r.Meta.experiment_id }
        if ($r.Meta.PSObject.Properties.Name -contains 'schedule_seed') { try { $scheduleSeed = [int]$r.Meta.schedule_seed } catch {} }
    } else {
        if ($ef -ne $experimentFingerprint) { throw "Mixed experiment fingerprints across runs ($ef vs $experimentFingerprint)." }
        if ($cf -ne $corpusFingerprint) { throw "Mixed corpus fingerprints across runs." }
        if ($bh -ne $binaryHash) { throw "Mixed binary hashes across runs." }
    }
}

# Recover the exact experiment definition by fingerprint. -ExperimentFile
# overrides the default repository location (used by self-tests); the
# fingerprint check against the runs is identical either way.
$experimentFile = $ExperimentFile
if ([string]::IsNullOrWhiteSpace($experimentFile)) {
    $experimentFile = Join-Path $repoRoot ("eng/benchmark/experiments/" + $experimentId + ".json")
}
if (-not (Test-Path -LiteralPath $experimentFile)) {
    throw "Experiment definition not found for id '$experimentId' (expected $experimentFile)."
}
$experimentFileFingerprint = Get-Sha256HexFile -Path $experimentFile
if ($experimentFileFingerprint -ne $experimentFingerprint) {
    throw "Experiment definition on disk ($experimentFileFingerprint) does not match the fingerprint recorded by the runs ($experimentFingerprint)."
}
$experimentJson = Get-Content -Raw -LiteralPath $experimentFile | ConvertFrom-Json

$candidatesDef = @{}
$candidateIds = @()
foreach ($c in @($experimentJson.candidates)) {
    $cid = [string]$c.id
    if ([string]::IsNullOrWhiteSpace($cid)) { throw "Experiment definition contains a candidate without an id." }
    if (-not $candidatesDef.ContainsKey($cid)) {
        $candidatesDef[$cid] = $c
        $candidateIds += $cid
    }
}
$expectedCacheModes = @($experimentJson.cache_modes)
$expectedRepetitions = [int]$experimentJson.repetitions
$expectedCorpus = [string]$experimentJson.corpus
$expectedCorpusPath = $expectedCorpus
if (-not [System.IO.Path]::IsPathRooted($expectedCorpusPath)) {
    $expectedCorpusPath = Join-Path $repoRoot $expectedCorpusPath
}
$expectedRowCount = @(@(Import-Csv -LiteralPath $expectedCorpusPath)).Count

$thirdPartyIds = @{}
foreach ($def in @('twenty-icons','duckduckgo','google','yandex','favicone','icon-horse')) { $thirdPartyIds[$def] = $true }

function Get-CandidateCanonicalForm {
    param([Parameter(Mandatory=$true)][object]$Def)
    $providerIds = @()
    if ($Def.PSObject.Properties.Name -contains 'providerIds') { $providerIds = @($Def.providerIds) }
    elseif ($Def.PSObject.Properties.Name -contains 'provider_ids') { $providerIds = @($Def.provider_ids) }
    $primary = 0; $fallback = 0; $cumulative = 0
    try { $primary = [int]$Def.primaryTimeout } catch {}
    try { $fallback = [int]$Def.fallbackTimeout } catch {}
    try { $cumulative = [int]$Def.cumulativeTimeout } catch {}
    $synthetic = $false
    try { $synthetic = [bool]$Def.allowSynthetic } catch {}
    $stop = $false
    try { $stop = [bool]$Def.stopAfterStrongResolved } catch {}
    $syn = if ($synthetic) { "1" } else { "0" }
    $stp = if ($stop) { "1" } else { "0" }
    return ("v1|providers={0}|primaryMs={1}|fallbackMs={2}|cumulativeMs={3}|synthetic={4}|stopAfterStrongResolved={5}" -f `
        ($providerIds -join ','), $primary, $fallback, $cumulative, $syn, $stp)
}

function Get-CandidateFingerprint {
    param([Parameter(Mandatory=$true)][object]$Def)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes((Get-CandidateCanonicalForm -Def $Def))
        $hash = $sha.ComputeHash($bytes)
        return [BitConverter]::ToString($hash).Replace("-","").ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

foreach ($cid in $candidateIds) {
    $defFp = Get-CandidateFingerprint -Def $candidatesDef[$cid]
    $candidatesDef[$cid] | Add-Member -NotePropertyName "_policy_fingerprint" -NotePropertyValue $defFp -Force
}

# Validate the exact matrix.
$measuredByCell = @{}
$warmupsByCandidate = @{}
$cellKeys = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($r in $runs) {
    $m = $r.Meta
    $status = ""
    if ($m.PSObject.Properties.Name -contains 'status') { $status = [string]$m.status }
    if ($status -ne "complete") {
        throw "Incomplete run rejected: $($r.Directory) (status '$status')."
    }
    $kind = "measured"
    if ($m.PSObject.Properties.Name -contains 'run_kind') { $kind = [string]$m.run_kind }
    $cand = ""
    if ($m.PSObject.Properties.Name -contains 'candidate_id') { $cand = [string]$m.candidate_id }
    if (-not $candidatesDef.ContainsKey($cand)) {
        throw "Run $($r.Directory) references candidate '$cand' that is not in the experiment definition."
    }
    $policyFp = ""
    if ($m.PSObject.Properties.Name -contains 'policy_fingerprint') { $policyFp = [string]$m.policy_fingerprint }
    if ($policyFp -ne [string]$candidatesDef[$cand]._policy_fingerprint) {
        throw "Run $($r.Directory) policy fingerprint mismatch for candidate '$cand'."
    }

    $rowsCsv = Join-Path $r.Directory "rows.csv"
    if (-not (Test-Path -LiteralPath $rowsCsv)) {
        throw "Run $($r.Directory) is missing rows.csv."
    }
    $rowCount = @(@(Import-Csv -LiteralPath $rowsCsv)).Count

    if ($kind -eq "warmup") {
        if ($warmupsByCandidate.ContainsKey($cand)) { throw "Duplicate warmup for candidate '$cand'." }
        $warmupsByCandidate[$cand] = $r
        continue
    }

    $cacheMode = ""
    if ($m.PSObject.Properties.Name -contains 'cache_mode') { $cacheMode = [string]$m.cache_mode }
    $repetition = 0
    if ($m.PSObject.Properties.Name -contains 'repetition') { try { $repetition = [int]$m.repetition } catch {} }

    if ($expectedCacheModes -notcontains $cacheMode) {
        throw "Run $($r.Directory) has cache mode '$cacheMode' outside the experiment definition."
    }
    if ($repetition -lt 1 -or $repetition -gt $expectedRepetitions) {
        throw "Run $($r.Directory) has repetition $repetition outside 1..$expectedRepetitions."
    }
    if ($rowCount -ne $expectedRowCount) {
        throw "Run $($r.Directory) has $rowCount rows, expected $expectedRowCount."
    }

    $cellKey = "$cand|$cacheMode|$repetition"
    if ($cellKeys.Contains($cellKey)) {
        throw "Duplicate matrix cell $cellKey."
    }
    [void]$cellKeys.Add($cellKey)
    $measuredByCell[$cellKey] = $r
}

$expectedCells = @()
foreach ($cid in $candidateIds) {
    foreach ($mode in $expectedCacheModes) {
        for ($rep = 1; $rep -le $expectedRepetitions; $rep++) {
            $expectedCells += "$cid|$mode|$rep"
        }
    }
}
$missingCells = @($expectedCells | Where-Object { -not $cellKeys.Contains($_) })
if ($missingCells.Count -gt 0) {
    throw ("Missing matrix cells: " + ($missingCells -join ", "))
}
$extraCells = @($cellKeys | Where-Object { $expectedCells -notcontains $_ })
if ($extraCells.Count -gt 0) {
    throw ("Unexpected extra matrix cells: " + ($extraCells -join ", "))
}
if ($expectedCacheModes -contains "warm") {
    foreach ($cid in $candidateIds) {
        if (-not $warmupsByCandidate.ContainsKey($cid)) {
            throw "Missing warm-up run for candidate '$cid'."
        }
    }
}

Write-Output ("Matrix validated: {0} candidates x {1} cache modes x {2} repetitions ({3} measured cells)." -f `
    $candidateIds.Count, $expectedCacheModes.Count, $expectedRepetitions, $measuredByCell.Count)

# ---------------------------------------------------------------------------
# 2. Load measured rows (warm-up runs are never evidence)
# ---------------------------------------------------------------------------

$allRows = @()
foreach ($cellKey in @($measuredByCell.Keys | Sort-Object)) {
    $r = $measuredByCell[$cellKey]
    $rowsCsv = Join-Path $r.Directory "rows.csv"
    foreach ($row in @(Import-Csv -LiteralPath $rowsCsv)) {
        $row | Add-Member -NotePropertyName "_cell" -NotePropertyValue $cellKey -Force
        $row | Add-Member -NotePropertyName "_run_directory" -NotePropertyValue $r.Directory -Force
        $allRows += $row
    }
}

# ---------------------------------------------------------------------------
# 3. Review queue: exact-hash identity, full coverage, no fallback keys
# ---------------------------------------------------------------------------

$allowedLabels = @("correct","acceptable-synthetic","generic","wrong-brand","blank","unusable","ambiguous")
$queueRows = @(Import-Csv -LiteralPath $reviewQueueResolved)
if ($queueRows.Count -eq 0) { throw "Review queue empty: $reviewQueueResolved" }

$reviewMap = @{}
$queueKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($qr in $queueRows) {
    $fid = ""
    if ($qr.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$qr.fixture_id }
    $hash = ""
    if ($qr.PSObject.Properties.Name -contains "artifact_hash") { $hash = [string]$qr.artifact_hash }
    if ([string]::IsNullOrWhiteSpace($fid) -or [string]::IsNullOrWhiteSpace($hash)) {
        throw "Review queue row without exact fixture_id|artifact_hash identity."
    }
    $key = "$fid|$hash"
    if ($queueKeys.Contains($key)) { throw "Duplicate review key in queue: $key" }
    [void]$queueKeys.Add($key)

    $label = ""
    if ($qr.PSObject.Properties.Name -contains "review_label") { $label = ([string]$qr.review_label).Trim().ToLowerInvariant() }
    if ($label -eq "not-reviewed" -or [string]::IsNullOrWhiteSpace($label)) {
        throw "Unreviewed rows remain in the queue ($key). Complete the human review first."
    }
    if ($allowedLabels -notcontains $label) { throw "Invalid review label '$label' for $key" }
    $reviewer = ""
    if ($qr.PSObject.Properties.Name -contains "reviewer") { $reviewer = [string]$qr.reviewer }
    $reviewedAt = ""
    if ($qr.PSObject.Properties.Name -contains "reviewed_at_utc") { $reviewedAt = [string]$qr.reviewed_at_utc }
    if ([string]::IsNullOrWhiteSpace($reviewer) -or [string]::IsNullOrWhiteSpace($reviewedAt)) {
        throw "Reviewed row missing reviewer/timestamp: $key"
    }
    $dt = [DateTime]::MinValue
    if (-not [DateTime]::TryParse($reviewedAt, [ref]$dt)) {
        throw "Unparseable reviewed_at_utc for ${key}: '$reviewedAt'"
    }
    $reviewMap[$key] = $label
}

# Every queue entry must refer to an artifact that exists in the evidence;
# every flagged (must-review) result must be present in the queue.
$resultUnitToProfiles = @{}
$resultUnitsRequiringReview = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$fixtureProviders = @{}
foreach ($row in $allRows) {
    $fid = [string]$row.fixture_id
    $profile = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $profile = [string]$row.profile }
    $hash = [string]$row.artifact_hash
    $prov = ""
    if ($row.PSObject.Properties.Name -contains "selected_provider") { $prov = [string]$row.selected_provider }
    if (-not [string]::IsNullOrWhiteSpace($prov)) {
        if (-not $fixtureProviders.ContainsKey($fid)) {
            $fixtureProviders[$fid] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        }
        [void]$fixtureProviders[$fid].Add($prov)
    }
    if ([string]::IsNullOrWhiteSpace($hash)) { continue }
    $key = "$fid|$hash"
    if (-not $resultUnitToProfiles.ContainsKey($key)) { $resultUnitToProfiles[$key] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase) }
    [void]$resultUnitToProfiles[$key].Add($profile)
    $isSyntheticRow = ParseBoolValue -Value $row.is_synthetic
    $isPlaceholderRow = ParseBoolValue -Value $row.placeholder_suspected
    $isBlankRow = ParseBoolValue -Value $row.blank_suspected
    if ($isSyntheticRow -or $isPlaceholderRow -or $isBlankRow) {
        [void]$resultUnitsRequiringReview.Add($key)
    }
}
foreach ($fid in @($fixtureProviders.Keys)) {
    if ($fixtureProviders[$fid].Count -gt 1) {
        foreach ($key in @($resultUnitToProfiles.Keys)) {
            if ($key.StartsWith("$fid|")) { [void]$resultUnitsRequiringReview.Add($key) }
        }
    }
}

foreach ($qk in @($queueKeys)) {
    if (-not $resultUnitToProfiles.ContainsKey($qk)) {
        throw "Queue entry $qk refers to a nonexistent result artifact (stale queue?)."
    }
}
$missingRequired = @($resultUnitsRequiringReview | Where-Object { -not $queueKeys.Contains($_) })
if ($missingRequired.Count -gt 0) {
    throw ("Flagged results missing from the review queue: " + (($missingRequired | Select-Object -First 5) -join ", ") + " (and possibly more). Regenerate the queue and review the new rows.")
}

# ---------------------------------------------------------------------------
# 4. Weighted review statistics per candidate
# ---------------------------------------------------------------------------

# Stratum weights for sampled units: weight = stratum population / sample
# count, strata defined by category|first-profile over unique non-targeted
# success units (mirrors prepare-review.ps1).
$unitInfo = @{}
foreach ($key in @($resultUnitToProfiles.Keys)) {
    $unitInfo[$key] = [PSCustomObject]@{
        Weight = 1.0
        Targeted = $resultUnitsRequiringReview.Contains($key)
        Category = $null
        Success = $false
    }
}
$unitIndex = @{}
foreach ($row in $allRows) {
    $fid = [string]$row.fixture_id
    $hash = [string]$row.artifact_hash
    if ([string]::IsNullOrWhiteSpace($hash)) { continue }
    $key = "$fid|$hash"
    if (-not $unitInfo.ContainsKey($key)) { continue }
    $info = $unitInfo[$key]
    if ($null -eq $info.Category -and -not [string]::IsNullOrWhiteSpace([string]$row.category)) {
        $info.Category = [string]$row.category
    }
    if ((([string]$row.machine_outcome).ToLowerInvariant()) -eq "success") { $info.Success = $true }
    if (-not $unitIndex.ContainsKey($key)) { $unitIndex[$key] = $true }
}

$populationByStratum = @{}
$sampledByStratum = @{}
foreach ($key in @($unitInfo.Keys)) {
    $info = $unitInfo[$key]
    if ($info.Targeted -or -not $info.Success) { continue }
    $profiles = @($resultUnitToProfiles[$key] | Sort-Object)
    $category = $info.Category
    if ([string]::IsNullOrWhiteSpace($category)) { $category = "unknown" }
    $stratumKey = "$category|$($profiles[0])"
    if (-not $populationByStratum.ContainsKey($stratumKey)) {
        $populationByStratum[$stratumKey] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    }
    [void]$populationByStratum[$stratumKey].Add($key)
    if ($queueKeys.Contains($key)) {
        if (-not $sampledByStratum.ContainsKey($stratumKey)) {
            $sampledByStratum[$stratumKey] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        }
        [void]$sampledByStratum[$stratumKey].Add($key)
    }
}
foreach ($stratumKey in @($sampledByStratum.Keys)) {
    $sampleCount = $sampledByStratum[$stratumKey].Count
    $populationCount = 1
    if ($populationByStratum.ContainsKey($stratumKey)) { $populationCount = $populationByStratum[$stratumKey].Count }
    $weight = [double]$populationCount / [double]$sampleCount
    foreach ($key in @($sampledByStratum[$stratumKey])) {
        $unitInfo[$key].Weight = $weight
    }
}

# Per-candidate stats.
$statsList = @()
foreach ($cid in $candidateIds) {
    $rowsForCid = @($allRows | Where-Object {
        $p = ""
        if ($_.PSObject.Properties.Name -contains "profile") { $p = [string]$_.profile }
        return $p -eq $cid
    })
    $total = $rowsForCid.Count
    if ($total -eq 0) { throw "No rows found for candidate '$cid'." }

    $successMachine = 0
    $timeoutCount = 0
    $providerErrorCount = 0
    $harnessErrorCount = 0
    $invalidImageCount = 0
    $syntheticRows = 0
    $providerCallCount = 0
    $thirdPartyCallCount = 0
    $elapsedCold = @()
    $elapsedWarm = @()
    $activeBatchCold = @()
    $activeBatchWarm = @()
    $resumedAny = $false

    foreach ($r in $rowsForCid) {
        $mo = ""
        if ($r.PSObject.Properties.Name -contains "machine_outcome") { $mo = ([string]$r.machine_outcome).ToLowerInvariant() }
        if ($mo -eq "success") { $successMachine++ }
        elseif ($mo -eq "timeout") { $timeoutCount++ }
        elseif ($mo -eq "provider-error") { $providerErrorCount++ }
        elseif ($mo -eq "harness-error") { $harnessErrorCount++ }
        elseif ($mo -eq "invalid-image") { $invalidImageCount++ }
        if (ParseBoolValue -Value $r.is_synthetic) { $syntheticRows++ }

        $cacheMode = ""
        if ($r.PSObject.Properties.Name -contains "cache_mode") { $cacheMode = [string]$r.cache_mode }
        $elapsed = 0
        if ($r.PSObject.Properties.Name -contains "total_elapsed_ms") {
            try { $elapsed = [long]$r.total_elapsed_ms } catch { $elapsed = 0 }
        }
        if ($cacheMode -eq "warm") { $elapsedWarm += $elapsed } else { $elapsedCold += $elapsed }

        $pmJson = ""
        if ($r.PSObject.Properties.Name -contains "provider_metrics") { $pmJson = [string]$r.provider_metrics }
        if (-not [string]::IsNullOrWhiteSpace($pmJson)) {
            try {
                $metrics = ConvertFrom-Json -InputObject $pmJson
                foreach ($m in @($metrics)) {
                    if ($null -eq $m) { continue }
                    $name = ([string]$m.provider)
                    if ($name -eq "Cache" -or $name -eq "Coalesced" -or $name -eq "Harness" -or $name -eq "Pipeline" -or $name -eq "Google Play") { continue }
                    $providerCallCount++
                    $canonical = $name.ToLowerInvariant()
                    if ($canonical -eq "twenty icons") { $canonical = "twenty-icons" }
                    elseif ($canonical -eq "icon horse") { $canonical = "icon-horse" }
                    if ($thirdPartyIds.ContainsKey($canonical)) { $thirdPartyCallCount++ }
                }
            } catch {}
        }
    }

    # Active wall-clock batch durations per cell; fixtures disclosed to any
    # third party come from observed provider calls, not the configured chain.
    $disclosedAnyThird = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in $rowsForCid) {
        $pmJson = ""
        if ($r.PSObject.Properties.Name -contains "provider_metrics") { $pmJson = [string]$r.provider_metrics }
        if ([string]::IsNullOrWhiteSpace($pmJson)) { continue }
        try {
            foreach ($m in @(ConvertFrom-Json -InputObject $pmJson)) {
                if ($null -eq $m) { continue }
                $canonical = ([string]$m.provider).ToLowerInvariant()
                if ($canonical -eq "twenty icons") { $canonical = "twenty-icons" }
                elseif ($canonical -eq "icon horse") { $canonical = "icon-horse" }
                if ($thirdPartyIds.ContainsKey($canonical)) {
                    [void]$disclosedAnyThird.Add([string]$r.fixture_id)
                    break
                }
            }
        } catch {}
    }

    foreach ($cellKey in @($measuredByCell.Keys)) {
        if (-not $cellKey.StartsWith("$cid|")) { continue }
        $meta = $measuredByCell[$cellKey].Meta
        if ($meta.PSObject.Properties.Name -contains 'resumed') {
            try { if ([bool]$meta.resumed) { $resumedAny = $true } } catch {}
        }
        $activeMs = -1
        if ($meta.PSObject.Properties.Name -contains 'active_elapsed_ms') {
            try { $activeMs = [long]$meta.active_elapsed_ms } catch {}
        }
        $mode = ""
        if ($meta.PSObject.Properties.Name -contains 'cache_mode') { $mode = [string]$meta.cache_mode }
        if ($activeMs -ge 0) {
            if ($mode -eq "warm") { $activeBatchWarm += $activeMs } else { $activeBatchCold += $activeMs }
        }
    }

    # Weighted label estimates over the candidate's success units. Machine
    # availability and human usability are separate evidence; an unsampled
    # success is NEVER assumed correct - it is represented only through the
    # stratified weights of the sampled units.
    $weightedUsable = 0.0
    $weightedWrongBrand = 0.0
    $weightedGeneric = 0.0
    $weightedBlankUnusable = 0.0
    $weightedAmbiguous = 0.0
    $weightedReviewedTotal = 0.0
    $targetedCovered = 0
    $sampledCovered = 0

    $seenUnitsForCid = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in $rowsForCid) {
        $mo = ""
        if ($r.PSObject.Properties.Name -contains "machine_outcome") { $mo = ([string]$r.machine_outcome).ToLowerInvariant() }
        if ($mo -ne "success") { continue }
        $hash = [string]$r.artifact_hash
        if ([string]::IsNullOrWhiteSpace($hash)) { continue }
        $key = "$([string]$r.fixture_id)|$hash"
        if (-not $seenUnitsForCid.Add($key)) { continue }

        if ($queueKeys.Contains($key)) {
            $info = $unitInfo[$key]
            if ($info.Targeted) { $targetedCovered++ } else { $sampledCovered++ }
            $label = $null
            if ($reviewMap.ContainsKey($key)) { $label = $reviewMap[$key] }
            if ($null -eq $label) { continue }
            $weightedReviewedTotal += $info.Weight
            switch ($label) {
                "correct" { $weightedUsable += $info.Weight }
                "acceptable-synthetic" { $weightedUsable += $info.Weight }
                "generic" { $weightedGeneric += $info.Weight }
                "wrong-brand" { $weightedWrongBrand += $info.Weight }
                "blank" { $weightedBlankUnusable += $info.Weight }
                "unusable" { $weightedBlankUnusable += $info.Weight }
                "ambiguous" { $weightedAmbiguous += $info.Weight }
                default { }
            }
        }
    }

    # Correctness denominator excludes ambiguous (v1.3 design).
    $correctnessDenominator = $weightedReviewedTotal - $weightedAmbiguous
    $estimatedUsableRate = 0.0
    $estimatedWrongBrand = 0.0
    if ($correctnessDenominator -gt 0) {
        $estimatedUsableRate = $weightedUsable / $correctnessDenominator
        $estimatedWrongBrand = $weightedWrongBrand / $correctnessDenominator
    }
    $estimatedCorrectness = $estimatedUsableRate

    $machineAvailability = [double]$successMachine / [double]$total
    # Documented coverage formula: machine availability x reviewed usability.
    $coverage = $machineAvailability * $estimatedUsableRate

    $failures = $timeoutCount + $providerErrorCount + $harnessErrorCount
    $reliability = 1.0 - ([double]$failures / [double]$total)
    if ($reliability -lt 0) { $reliability = 0 }
    if ($reliability -gt 1) { $reliability = 1 }

    $sortedCold = @($elapsedCold | Sort-Object)
    $sortedWarm = @($elapsedWarm | Sort-Object)
    $coldMedian = Get-PercentileFromSorted -SortedValues ([long[]]$sortedCold) -Percent 0.50
    $coldP95 = Get-PercentileFromSorted -SortedValues ([long[]]$sortedCold) -Percent 0.95
    $coldMax = 0
    if ($sortedCold.Count -gt 0) { $coldMax = ($sortedCold | Measure-Object -Maximum).Maximum }
    $warmMedian = Get-PercentileFromSorted -SortedValues ([long[]]$sortedWarm) -Percent 0.50
    $warmP95 = Get-PercentileFromSorted -SortedValues ([long[]]$sortedWarm) -Percent 0.95
    $warmMax = 0
    if ($sortedWarm.Count -gt 0) { $warmMax = ($sortedWarm | Measure-Object -Maximum).Maximum }
    $avgBatchCold = ($activeBatchCold | Measure-Object -Average).Average
    $avgBatchWarm = ($activeBatchWarm | Measure-Object -Average).Average
    if ($null -eq $avgBatchCold) { $avgBatchCold = 0 }
    if ($null -eq $avgBatchWarm) { $avgBatchWarm = 0 }
    $throughput = 0.0
    if ($avgBatchCold -gt 0) {
        $throughput = ([double]$total / [double]$expectedCacheModes.Count) / ($avgBatchCold / 1000.0)
    }

    $def = $candidatesDef[$cid]
    $statsList += [PSCustomObject]@{
        profile_id = $cid
        definition = $def
        total = $total
        machine_success = $successMachine
        machine_availability = $machineAvailability
        timeout = $timeoutCount
        provider_error = $providerErrorCount
        harness_error = $harnessErrorCount
        invalid_image = $invalidImageCount
        synthetic_rows = $syntheticRows
        targeted_covered = $targetedCovered
        sampled_covered = $sampledCovered
        weighted_reviewed_total = $weightedReviewedTotal
        estimated_usable_rate = $estimatedUsableRate
        estimated_correctness = $estimatedCorrectness
        estimated_wrong_brand = $estimatedWrongBrand
        weighted_ambiguous = $weightedAmbiguous
        coverage = $coverage
        reliability = $reliability
        cold_median_ms = $coldMedian
        cold_p95_ms = $coldP95
        cold_max_ms = $coldMax
        warm_median_ms = $warmMedian
        warm_p95_ms = $warmP95
        warm_max_ms = $warmMax
        active_batch_cold_ms = [long]$avgBatchCold
        active_batch_warm_ms = [long]$avgBatchWarm
        cold_throughput_per_sec = $throughput
        resumed_any = $resumedAny
        provider_call_count = $providerCallCount
        provider_calls_per_input = [double]$providerCallCount / [double]$total
        provider_calls_per_success = if ($successMachine -gt 0) { [double]$providerCallCount / [double]$successMachine } else { 0.0 }
        wasted_fallback_calls = [long]($providerCallCount - $successMachine)
        third_party_call_count = $thirdPartyCallCount
        third_party_calls_per_input = [double]$thirdPartyCallCount / [double]$total
        third_party_disclosure_rate = [double]$disclosedAnyThird.Count / [double]$total
    }
}

# ---------------------------------------------------------------------------
# 5. Winner rules + ambiguity sensitivity
# ---------------------------------------------------------------------------

# Conservative (ambiguity counted as failure, in the denominator) and
# optimistic (ambiguity counted as usable) usable-rate variants.
foreach ($s in $statsList) {
    $denomAll = $s.weighted_reviewed_total
    $usableNumerator = $s.estimated_usable_rate * ($denomAll - $s.weighted_ambiguous)
    $usableConservative = 0.0
    if ($denomAll -gt 0) { $usableConservative = $usableNumerator / $denomAll }
    $usableOptimistic = 0.0
    $denomOpt = $denomAll - $s.weighted_ambiguous + $s.weighted_ambiguous
    if ($denomOpt -gt 0) { $usableOptimistic = ($usableNumerator + $s.weighted_ambiguous) / $denomOpt }
    $s | Add-Member -NotePropertyName "_usable_conservative" -NotePropertyValue $usableConservative -Force
    $s | Add-Member -NotePropertyName "_usable_optimistic" -NotePropertyValue $usableOptimistic -Force
}

$ambiguityNotes = @()
$ambiguityRejections = @()

function Test-AmbiguityStability {
    param(
        [string]$Role,
        [object[]]$OrderedConservative,
        [object[]]$OrderedOptimistic
    )
    $winnerC = $OrderedConservative[0].profile_id
    $winnerO = $OrderedOptimistic[0].profile_id
    if ($winnerC -ne $winnerO) {
        $script:ambiguityRejections += ("{0}: winner flips between '{1}' (ambiguity-as-failure) and '{2}' (ambiguity-as-usable)" -f $Role, $winnerC, $winnerO)
    } else {
        $script:ambiguityNotes += ("{0}: stable under ambiguity ({1})" -f $Role, $winnerC)
    }
    return $winnerC
}

# 1. Privacy winner: no third-party provider in the chain AND zero observed
# third-party disclosures; best estimated usable rate.
$privacyCandidates = @($statsList | Where-Object {
    $chain = @()
    if ($_.definition.PSObject.Properties.Name -contains 'providerIds') { $chain = @($_.definition.providerIds) }
    $hasThird = $false
    foreach ($p in $chain) { if ($thirdPartyIds.ContainsKey(([string]$p).ToLowerInvariant())) { $hasThird = $true; break } }
    return ((-not $hasThird) -and ($_.third_party_disclosure_rate -eq 0.0))
})
if ($privacyCandidates.Count -eq 0) { throw "No privacy-eligible candidate (direct-site-only with zero observed third-party disclosures)." }
$privacyWinner = @($privacyCandidates | Sort-Object -Property @{ Expression = { -$_.estimated_usable_rate } }, @{ Expression = { -$_.estimated_correctness } }, @{ Expression = { $_.cold_p95_ms } }, @{ Expression = { $_.profile_id } })[0]
[void](Test-AmbiguityStability -Role "privacy" `
    -OrderedConservative @($privacyCandidates | Sort-Object -Property @{ Expression = { -$_._usable_conservative } }, @{ Expression = { $_.profile_id } }) `
    -OrderedOptimistic @($privacyCandidates | Sort-Object -Property @{ Expression = { -$_._usable_optimistic } }, @{ Expression = { $_.profile_id } }))

# 2. Max-coverage winner: highest estimated usable rate.
$maxCoverageWinner = @($statsList | Sort-Object -Property @{ Expression = { -$_.estimated_usable_rate } }, @{ Expression = { -$_.estimated_correctness } }, @{ Expression = { $_.cold_p95_ms } }, @{ Expression = { $_.profile_id } })[0]
[void](Test-AmbiguityStability -Role "max-coverage" `
    -OrderedConservative @($statsList | Sort-Object -Property @{ Expression = { -$_._usable_conservative } }, @{ Expression = { $_.profile_id } }) `
    -OrderedOptimistic @($statsList | Sort-Object -Property @{ Expression = { -$_._usable_optimistic } }, @{ Expression = { $_.profile_id } }))

# 3. Bulk-fast: lowest ACTIVE wall-clock cold batch duration among candidates
# with usable >= 90% of the max-coverage winner and wrong-brand <= 1%.
$bulkThresholdMain = $maxCoverageWinner.estimated_usable_rate * 0.90
$bulkThresholdC = $maxCoverageWinner._usable_conservative * 0.90
$bulkThresholdO = $maxCoverageWinner._usable_optimistic * 0.90
$bulkEligibleMain = @($statsList | Where-Object { $_.estimated_usable_rate -ge $bulkThresholdMain -and $_.estimated_wrong_brand -le 0.01 })
if ($bulkEligibleMain.Count -eq 0) {
    throw ("No bulk-fast eligible candidate: need estimated usable >= 90% of max-coverage ({0:P2}) and wrong-brand <= 1%" -f $bulkThresholdMain)
}
$bulkWinner = @($bulkEligibleMain | Sort-Object -Property @{ Expression = { $_.active_batch_cold_ms } }, @{ Expression = { $_.cold_median_ms } }, @{ Expression = { $_.profile_id } })[0]
$bulkEligibleC = @($statsList | Where-Object { $_._usable_conservative -ge $bulkThresholdC -and $_.estimated_wrong_brand -le 0.01 })
$bulkEligibleO = @($statsList | Where-Object { $_._usable_optimistic -ge $bulkThresholdO -and $_.estimated_wrong_brand -le 0.01 })
if ($bulkEligibleC.Count -eq 0 -or $bulkEligibleO.Count -eq 0) {
    $ambiguityRejections += "bulk-fast: ambiguity can empty the eligible set"
} else {
    [void](Test-AmbiguityStability -Role "bulk-fast" `
        -OrderedConservative @($bulkEligibleC | Sort-Object -Property @{ Expression = { $_.active_batch_cold_ms } }, @{ Expression = { $_.profile_id } }) `
        -OrderedOptimistic @($bulkEligibleO | Sort-Object -Property @{ Expression = { $_.active_batch_cold_ms } }, @{ Expression = { $_.profile_id } }))
}

# 4. Everyday: documented weighted score. Privacy score uses OBSERVED
# third-party disclosure rates, not configured provider counts.
function Get-MinMax {
    param([object[]]$Values)
    $min = $null
    $max = $null
    foreach ($v in $Values) {
        if ($null -eq $min -or $v -lt $min) { $min = $v }
        if ($null -eq $max -or $v -gt $max) { $max = $v }
    }
    return @{ min = $min; max = $max }
}
function NormalizeValue {
    param([double]$Value, [double]$Min, [double]$Max)
    if ($Max -eq $Min) { return 1.0 }
    return ($Value - $Min) / ($Max - $Min)
}

$cm = Get-MinMax -Values @($statsList | ForEach-Object { $_.estimated_correctness })
$covM = Get-MinMax -Values @($statsList | ForEach-Object { $_.coverage })
$relM = Get-MinMax -Values @($statsList | ForEach-Object { $_.reliability })
$latM = Get-MinMax -Values @($statsList | ForEach-Object { [double]$_.cold_median_ms })
$privM = Get-MinMax -Values @($statsList | ForEach-Object { [double]$_.third_party_disclosure_rate })

function Get-EverydayScore {
    param([Parameter(Mandatory=$true)][object]$S, [double]$UsableOverride = -1.0)
    $usable = $S.estimated_usable_rate
    if ($UsableOverride -ge 0) { $usable = $UsableOverride }
    $correctness = $usable
    $cn = NormalizeValue -Value ([double]$correctness) -Min $cm.min -Max $cm.max
    $covn = NormalizeValue -Value ([double]($S.machine_availability * $usable)) -Min $covM.min -Max $covM.max
    $reln = NormalizeValue -Value ([double]$S.reliability) -Min $relM.min -Max $relM.max
    $latRaw = NormalizeValue -Value ([double]$S.cold_median_ms) -Min $latM.min -Max $latM.max
    $latScore = 1.0 - $latRaw
    $privRaw = NormalizeValue -Value ([double]$S.third_party_disclosure_rate) -Min $privM.min -Max $privM.max
    $privScore = 1.0 - $privRaw
    return (0.40 * $cn + 0.25 * $covn + 0.15 * $latScore + 0.10 * $reln + 0.10 * $privScore)
}

foreach ($s in $statsList) {
    $s | Add-Member -NotePropertyName "_everyday_score" -NotePropertyValue (Get-EverydayScore -S $s) -Force
}
$everydayWinner = @($statsList | Sort-Object -Property @{ Expression = { -$_._everyday_score } }, @{ Expression = { $_.profile_id } })[0]
[void](Test-AmbiguityStability -Role "everyday" `
    -OrderedConservative @($statsList | Sort-Object -Property @{ Expression = { -(Get-EverydayScore -S $_ -UsableOverride $_._usable_conservative) } }, @{ Expression = { $_.profile_id } }) `
    -OrderedOptimistic @($statsList | Sort-Object -Property @{ Expression = { -(Get-EverydayScore -S $_ -UsableOverride $_._usable_optimistic) } }, @{ Expression = { $_.profile_id } }))

if ($ambiguityRejections.Count -gt 0) {
    throw ("Ambiguity sensitivity rejected the selection:`n - " + ($ambiguityRejections -join "`n - ") + "`nExpand the human review to resolve ambiguous labels, or conservatively count ambiguity as failure and rerun.")
}

# ---------------------------------------------------------------------------
# 6. Generated artifacts (facts only; descriptions from policy, not names)
# ---------------------------------------------------------------------------

function Get-ProviderDisplayName {
    param([string]$Id)
    switch ($Id) {
        "direct-site" { return "the site itself" }
        "twenty-icons" { return "Twenty Icons" }
        "duckduckgo" { return "DuckDuckGo" }
        "google" { return "Google" }
        "yandex" { return "Yandex" }
        "favicone" { return "Favicone" }
        "icon-horse" { return "Icon Horse" }
        default { return $Id }
    }
}

function Build-Description {
    param([Parameter(Mandatory=$true)][object]$Def)
    $chain = @()
    if ($Def.PSObject.Properties.Name -contains 'providerIds') { $chain = @($Def.providerIds) }
    $names = @()
    foreach ($p in $chain) { $names += (Get-ProviderDisplayName -Id ([string]$p)) }
    $cumulativeS = [int]([Math]::Ceiling([int]$Def.cumulativeTimeout / 1000.0))
    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add(("tries {0} icon source(s) in order ({1}) within a {2}s total budget" -f $names.Count, ($names -join ", "), $cumulativeS))
    if ([bool]$Def.stopAfterStrongResolved) {
        $parts.Add("stops as soon as a strong resolver returns a high-confidence icon")
    } else {
        $parts.Add("queries every source in the chain before selecting")
    }
    if ([bool]$Def.allowSynthetic) {
        $parts.Add("allows a generated fallback icon when no real icon is found")
    }
    return ($parts -join "; ") + "."
}

function Convert-ToCsStringLiteral {
    param([string]$Value)
    return '"' + $Value.Replace("\", "\\").Replace('"', '\"') + '"'
}

$roles = @(
    @{ Id = "bulk-fast";      DisplayName = "Fast";      IntendedUse = "Large batch fetching with reduced latency";                 Winner = $bulkWinner },
    @{ Id = "everyday";       DisplayName = "Balanced";  IntendedUse = "Default everyday use balancing coverage and speed";        Winner = $everydayWinner },
    @{ Id = "privacy";        DisplayName = "Privacy";   IntendedUse = "Privacy-sensitive fetching without third-party providers"; Winner = $privacyWinner },
    @{ Id = "max-coverage";   DisplayName = "Thorough";  IntendedUse = "Maximum coverage with the full resolver chain";            Winner = $maxCoverageWinner }
)

foreach ($role in $roles) {
    Write-Host ("{0,-14} -> {1}" -f $role.Id, $role.Winner.profile_id)
}

$generatedLines = New-Object System.Collections.Generic.List[string]
$generatedLines.Add("using System;")
$generatedLines.Add("using System.Collections.Generic;")
$generatedLines.Add("")
$generatedLines.Add("namespace KeeFetch.FetchProfiles")
$generatedLines.Add("{")
$generatedLines.Add("    internal static partial class FetchProfileCatalog")
$generatedLines.Add("    {")
$generatedLines.Add("        private static List<FetchProfileDefinition> CreateManagedProfiles()")
$generatedLines.Add("        {")
$generatedLines.Add("            List<FetchProfileDefinition> profiles = new List<FetchProfileDefinition>();")
$generatedLines.Add("")
foreach ($role in $roles) {
    $def = $role.Winner.definition
    $chain = @()
    if ($def.PSObject.Properties.Name -contains 'providerIds') { $chain = @($def.providerIds) }
    $chainLiteral = "new string[] { " + (($chain | ForEach-Object { Convert-ToCsStringLiteral -Value ([string]$_) }) -join ", ") + " }"
    $synthetic = if ([bool]$def.allowSynthetic) { "true" } else { "false" }
    $stop = if ([bool]$def.stopAfterStrongResolved) { "true" } else { "false" }
    $generatedLines.Add("            profiles.Add(new FetchProfileDefinition(")
    $generatedLines.Add("                " + (Convert-ToCsStringLiteral -Value $role.Id) + ",")
    $generatedLines.Add("                " + (Convert-ToCsStringLiteral -Value $role.DisplayName) + ",")
    $generatedLines.Add("                " + (Convert-ToCsStringLiteral -Value (Build-Description -Def $def)) + ",")
    $generatedLines.Add("                " + (Convert-ToCsStringLiteral -Value $role.IntendedUse) + ",")
    $generatedLines.Add("                " + $chainLiteral + ",")
    $generatedLines.Add("                " + ([int]$def.primaryTimeout) + ",")
    $generatedLines.Add("                " + ([int]$def.fallbackTimeout) + ",")
    $generatedLines.Add("                " + ([int]$def.cumulativeTimeout) + ",")
    $generatedLines.Add("                " + $synthetic + ",")
    $generatedLines.Add("                " + $stop + ",")
    $generatedLines.Add("                true,")
    $generatedLines.Add("                " + (Convert-ToCsStringLiteral -Value "docs/benchmarks/v1.3-provider-study.md") + "));")
    $generatedLines.Add("")
}
$generatedLines.Add("            return profiles;")
$generatedLines.Add("        }")
$generatedLines.Add("    }")
$generatedLines.Add("}")
$generatedCs = ($generatedLines -join "`n") + "`n"

# ---------------------------------------------------------------------------
# 7. Reports
# ---------------------------------------------------------------------------

$evidenceLines = New-Object System.Collections.Generic.List[string]
$evidenceLines.Add("# v1.3 Provider Study Evidence Report")
$evidenceLines.Add("")
$evidenceLines.Add("Generated: $((Get-Date).ToUniversalTime().ToString('o'))")
$evidenceLines.Add("")
$evidenceLines.Add("## Identity")
$evidenceLines.Add("")
$evidenceLines.Add("- Experiment: ``$experimentId`` (fingerprint ``$experimentFingerprint``)")
$evidenceLines.Add("- Corpus fingerprint: ``$corpusFingerprint`` ($expectedRowCount fixtures)")
$evidenceLines.Add("- KeeFetch binary hash: ``$binaryHash``")
$evidenceLines.Add("- Schedule seed: ``$scheduleSeed`` (cold cells interleaved per repetition with rotation; warm blocks contiguous per candidate after clear-once warm-up)")
$evidenceLines.Add("- Matrix: $($candidateIds.Count) candidates x $($expectedCacheModes -join '+') x $expectedRepetitions repetitions")
$evidenceLines.Add("")
$evidenceLines.Add("## Methodology")
$evidenceLines.Add("")
$evidenceLines.Add("- Execution policies: every candidate executed its recorded provider chain, per-provider and cumulative budgets, synthetic flag, and early-stop flag; run.json policy fingerprints were verified against the experiment definition.")
$evidenceLines.Add("- Cold/warm: cold runs clear all caches before every measured run. Warm blocks clear once, perform an unmeasured warm-up over the full corpus, then run measured warm repetitions without clearing. Warm-up runs are marked and excluded from all metrics.")
$evidenceLines.Add("- Review: must-review units are all synthetic, placeholder-suspected, blank-suspected, and profile-differing results; remaining successes are sampled at 10% per category|profile stratum with deterministic SHA-256 ranking. Review identity is the exact (fixture, artifact hash) pair.")
$evidenceLines.Add("- Statistics: machine availability is reported separately from human labels. Label rates use design weights (targeted units weigh 1; sampled units weigh stratum population / sample). Correctness excludes ambiguous labels from numerator and denominator. Coverage = machine availability x estimated usability. Wilson 95% intervals are reported for estimated rates.")
$evidenceLines.Add("- Batch speed uses active wall-clock duration per cell from run.json (resumed runs accumulate active time only; wall-clock across interruptions is never used).")
$evidenceLines.Add("- Ambiguity sensitivity: every winner rule was recomputed with ambiguous labels counted as failure and as usable; the selection is reported only when stable.")
$evidenceLines.Add("")
$evidenceLines.Add("## Candidate comparison")
$evidenceLines.Add("")
$evidenceLines.Add("| candidate | machine avail. | est. usable (Wilson 95%) | est. wrong-brand | coverage | reliability | cold median/p95 (ms) | warm median (ms) | active cold batch (ms) | 3p disclosure | timeout | provider err |")
$evidenceLines.Add("|---|---:|---|---:|---:|---:|---|---:|---:|---:|---:|---:|")
foreach ($s in ($statsList | Sort-Object -Property profile_id)) {
    $denomForWilson = $s.weighted_reviewed_total - $s.weighted_ambiguous
    $wilson = Get-WilsonInterval -Successes ($s.estimated_usable_rate * $denomForWilson) -Total $denomForWilson
    $evidenceLines.Add(("| {0} | {1:P1} | {2:P1} [{3:P0}-{4:P0}] | {5:P2} | {6:P1} | {7:P1} | {8}/{9} | {10} | {11} | {12:P1} | {13} | {14} |" -f `
        $s.profile_id, $s.machine_availability, $s.estimated_usable_rate, $wilson.Lower, $wilson.Upper, `
        $s.estimated_wrong_brand, $s.coverage, $s.reliability, $s.cold_median_ms, $s.cold_p95_ms, $s.warm_median_ms, `
        $s.active_batch_cold_ms, $s.third_party_disclosure_rate, $s.timeout, $s.provider_error))
}
$evidenceLines.Add("")
$evidenceLines.Add("## Winners")
$evidenceLines.Add("")
foreach ($role in $roles) {
    $evidenceLines.Add("- **$($role.Id)** ($($role.DisplayName)): ``$($role.Winner.profile_id)`` - $(Build-Description -Def $role.Winner.definition)")
}
$evidenceLines.Add("")
$evidenceLines.Add("## Limitations")
$evidenceLines.Add("")
$evidenceLines.Add("- Live-network measurements reflect the environment and time of the run; absolute latencies vary, rankings are the decision evidence.")
$evidenceLines.Add("- Estimated label rates carry sampling uncertainty; Wilson intervals are shown. Winner rules were additionally protected by the ambiguity sensitivity check.")
$evidenceLines.Add("- Privacy is measured by observed third-party call evidence, not by the configured chain alone.")
$evidenceLines.Add("")
$evidenceLines.Add("## Reproduction")
$evidenceLines.Add("")
$evidenceLines.Add("``````powershell")
$evidenceLines.Add("dotnet build KeeFetch.csproj --configuration Release -p:KeePassPath=<KeePass dir>")
$evidenceLines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark-presets.ps1 -Experiment eng/benchmark/experiments/$experimentId.json")
$evidenceLines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/prepare-review.ps1 -RunDir <output root>")
$evidenceLines.Add("# human review of the queue, then:")
$evidenceLines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/select-profiles.ps1 -RunDir <output root>")
$evidenceLines.Add("``````")
$evidenceLines.Add("")
$evidence = $evidenceLines -join "`n"

$summary = [PSCustomObject]@{
    experiment_id = $experimentId
    experiment_fingerprint = $experimentFingerprint
    corpus_fingerprint = $corpusFingerprint
    binary_hash = $binaryHash
    schedule_seed = $scheduleSeed
    matrix = "{0}x{1}x{2}" -f $candidateIds.Count, ($expectedCacheModes -join "+"), $expectedRepetitions
    winners = @{
        "bulk-fast" = $bulkWinner.profile_id
        "everyday" = $everydayWinner.profile_id
        "privacy" = $privacyWinner.profile_id
        "max-coverage" = $maxCoverageWinner.profile_id
    }
    ambiguity_notes = $ambiguityNotes
    candidates = @($statsList | ForEach-Object {
        [PSCustomObject]@{
            profile_id = $_.profile_id
            machine_availability = $_.machine_availability
            estimated_usable_rate = $_.estimated_usable_rate
            estimated_wrong_brand = $_.estimated_wrong_brand
            coverage = $_.coverage
            reliability = $_.reliability
            cold_median_ms = $_.cold_median_ms
            cold_p95_ms = $_.cold_p95_ms
            warm_median_ms = $_.warm_median_ms
            active_batch_cold_ms = $_.active_batch_cold_ms
            third_party_disclosure_rate = $_.third_party_disclosure_rate
            targeted_covered = $_.targeted_covered
            sampled_covered = $_.sampled_covered
            resumed_any = $_.resumed_any
        }
    })
}
$summaryJson = ConvertTo-Json -InputObject $summary -Depth 6

# ---------------------------------------------------------------------------
# 8. Output (default: OutputDir only; -Publish: repository mutations)
# ---------------------------------------------------------------------------

$generatedCsPath = Join-Path $outputDirResolved "FetchProfileCatalog.Generated.cs"
$evidencePath = Join-Path $outputDirResolved "v1.3-provider-study.md"
$summaryPath = Join-Path $outputDirResolved "selection-summary.json"
$reportPath = Join-Path $outputDirResolved "selection-report.md"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($generatedCsPath, $generatedCs.Replace("`r`n", "`n"), $utf8NoBom)
[System.IO.File]::WriteAllText($evidencePath, $evidence.Replace("`r`n", "`n"), $utf8NoBom)
[System.IO.File]::WriteAllText($summaryPath, $summaryJson.Replace("`r`n", "`n"), $utf8NoBom)
$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add("# Selection report")
$reportLines.Add("")
foreach ($role in $roles) {
    $reportLines.Add(("- **{0}** -> ``{1}``" -f $role.Id, $role.Winner.profile_id))
}
$reportLines.Add("")
$reportLines.Add("Ambiguity checks:")
foreach ($note in $ambiguityNotes) { $reportLines.Add("- $note") }
$reportLines.Add("")
$reportLines.Add("Full methodology and candidate table: v1.3-provider-study.md (also written to this directory).")
[System.IO.File]::WriteAllText($reportPath, ($reportLines -join "`n") + "`n", $utf8NoBom)

Write-Output ""
Write-Output "Selection outputs written to: $outputDirResolved"
Write-Output "  - FetchProfileCatalog.Generated.cs (candidate; review before publishing)"
Write-Output "  - v1.3-provider-study.md (evidence report)"
Write-Output "  - selection-summary.json"
Write-Output "  - selection-report.md"

if ($Publish) {
    $repoGeneratedPath = Join-Path $repoRoot "FetchProfiles\FetchProfileCatalog.Generated.cs"
    $repoEvidencePath = Join-Path $repoRoot "docs\benchmarks\v1.3-provider-study.md"

    [System.IO.File]::WriteAllText($repoGeneratedPath, $generatedCs.Replace("`r`n", "`n"), $utf8NoBom)
    $evidenceDir = Split-Path -Parent $repoEvidencePath
    if (-not (Test-Path -LiteralPath $evidenceDir)) {
        New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($repoEvidencePath, $evidence.Replace("`r`n", "`n"), $utf8NoBom)

    if (-not (Test-Path -LiteralPath $repoEvidencePath)) {
        throw "Published evidence report missing: $repoEvidencePath"
    }

    Write-Output ""
    Write-Output "Published:"
    Write-Output "  - $repoGeneratedPath"
    Write-Output "  - $repoEvidencePath"
    Write-Output "Next: rebuild, run the full test suite, and regenerate site/data/profiles.json with eng/export-profile-data.ps1."
}
