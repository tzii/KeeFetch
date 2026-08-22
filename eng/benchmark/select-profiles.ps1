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
# with exact row counts, matching policy fingerprints, and a uniform
# execution-harness fingerprint that matches this repository's harness -
# must be present exactly once.
#
# Scoring is COLD-ONLY: every scored quantity (machine availability, label
# rates, provider calls, disclosure, latency, batch speed) is computed over
# measured cold cells. Warm rows are latency evidence only and appear as
# informational percentiles, never in a winner rule.
#
# Human review is a CENSUS, not a sample: the review queue must contain
# exactly the unique cold (fixture_id, artifact_hash) units - no hole, no
# extra key - and each human label propagates to every occurrence of that
# exact artifact. There are no design weights and no interval estimates
# anywhere downstream; rates are exact proportions of the reviewed census
# population. Unreviewed machine successes are never counted as correct.
#
# provider_metrics is parsed STRICTLY: malformed JSON, unknown provider
# names, unknown outcomes, non-integral counts, or a successful fetch with
# zero recorded provider activity fail the whole selection.
#
# One shared winner function decides every role; before any winner is
# accepted the whole rule is replayed with ambiguous labels counted as
# failure (pessimistic) and as usable (optimistic), and a winner that is
# not stable across both replays rejects the selection.
#
# Without -Publish nothing outside OutputDir is written. With -Publish the
# generated catalog and the canonical evidence report are written into the
# repository only after all gates pass, and each write is read back and
# byte-compared before publication is reported.

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

# Canonical third-party identity of a provider_metrics entry. Metrics carry
# provider display names; third-party membership is decided on catalog ids.
function Get-CanonicalMetricProvider {
    param([string]$Name)
    $c = ([string]$Name).Trim().ToLowerInvariant()
    if ($c -eq "twenty icons") { return "twenty-icons" }
    if ($c -eq "icon horse") { return "icon-horse" }
    return $c
}

# Strict provider_metrics vocabulary: exactly the provider display names the
# downloader/catalog can emit plus the instrumentation pseudo-providers, and
# exactly the outcomes ProviderAttemptMetric sites can produce (including the
# runner's harness-error overlay). Anything else is malformed or fabricated.
$knownMetricProviders = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($n in @('Direct Site','Twenty Icons','DuckDuckGo','Google','Yandex','Favicone','Icon Horse','Cache','Coalesced','Pipeline','Google Play','Harness')) {
    [void]$knownMetricProviders.Add($n)
}
$knownMetricOutcomes = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($o in @('empty','timeout','cancelled','candidate','error','hit','negative-hit','shared','skipped-budget-exhausted','harness-error')) {
    [void]$knownMetricOutcomes.Add($o)
}

# Parses and validates one row's provider_metrics column. Returns the parsed
# metric objects (possibly an empty array for non-success rows); any schema
# violation throws with full row identity.
function Get-StrictProviderMetrics {
    param([Parameter(Mandatory=$true)][object]$Row)

    $where = "fixture '$([string]$Row.fixture_id)' (profile '$([string]$Row.profile)', cell '$([string]$Row._cell)')"
    $pmJson = ""
    if ($Row.PSObject.Properties.Name -contains "provider_metrics") { $pmJson = [string]$Row.provider_metrics }
    if ([string]::IsNullOrWhiteSpace($pmJson)) {
        $parsed = @()
    } else {
        $parsedRaw = $null
        try {
            $parsedRaw = ConvertFrom-Json -InputObject $pmJson
        } catch {
            throw "provider_metrics is not parseable JSON for ${where}: $($_.Exception.Message)"
        }
        # Windows PowerShell's ConvertFrom-Json returns a top-level JSON array
        # as one unenumerated object[]; enumerate it explicitly. A non-array
        # top level is malformed - the runner always writes an array.
        $parsed = @()
        if ($null -ne $parsedRaw) {
            if (-not ($parsedRaw -is [System.Array])) {
                throw "provider_metrics for ${where} is not a JSON array."
            }
            foreach ($element in $parsedRaw) { $parsed += $element }
        }
    }

    foreach ($m in $parsed) {
        if ($null -eq $m) { throw "provider_metrics contains a null element for ${where}." }
        foreach ($field in @('provider','calls','elapsed_ms','candidate_count','outcome','errors')) {
            if ($m.PSObject.Properties.Name -notcontains $field) {
                throw "provider_metrics element for ${where} is missing the '$field' field."
            }
        }
        $name = ([string]$m.provider).Trim()
        if ([string]::IsNullOrWhiteSpace($name)) { throw "provider_metrics element for ${where} has an empty provider name." }
        if (-not $knownMetricProviders.Contains($name)) {
            throw "provider_metrics for ${where} names unknown provider '$name' (not in the strict provider vocabulary)."
        }
        foreach ($numField in @('calls','elapsed_ms','candidate_count','errors')) {
            $v = $m.$numField
            if (-not ($v -is [int] -or $v -is [long])) {
                throw "provider_metrics field '$numField' for ${where} is not an integer: $v"
            }
            if ($v -lt 0) { throw "provider_metrics field '$numField' for ${where} is negative: $v." }
        }
        if ([int]$m.calls -lt 1) {
            throw "provider_metrics for ${where} records provider '$name' with calls < 1."
        }
        $outcome = ([string]$m.outcome).Trim()
        if ([string]::IsNullOrWhiteSpace($outcome)) { throw "provider_metrics element for ${where} has an empty outcome." }
        if (-not $knownMetricOutcomes.Contains($outcome)) {
            throw "provider_metrics for ${where} records unknown outcome '$outcome' for provider '$name' (not in the strict outcome vocabulary)."
        }
    }

    $mo = ""
    if ($Row.PSObject.Properties.Name -contains "machine_outcome") { $mo = ([string]$Row.machine_outcome).ToLowerInvariant() }
    if ($mo -eq "success" -and $parsed.Count -eq 0) {
        throw "Successful fetch for ${where} recorded zero provider activity; a success must carry at least one provider_metrics entry."
    }
    return $parsed
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

# One experiment, one corpus, one binary, one execution harness.
Import-Module (Join-Path $PSScriptRoot "BenchmarkHarness.psm1") -Force
$currentHarnessFingerprint = Get-KeeFetchHarnessFingerprint -RepoRoot $repoRoot

$experimentFingerprint = $null
$corpusFingerprint = $null
$binaryHash = $null
$harnessFingerprint = $null
$experimentId = $null
$scheduleSeed = 0
foreach ($r in $runs) {
    foreach ($field in @('experiment_fingerprint','corpus_fingerprint','binary_hash','execution_harness_fingerprint')) {
        $value = ""
        if ($r.Meta.PSObject.Properties.Name -contains $field) { $value = [string]$r.Meta.$field }
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "Run $($r.Directory) is missing $field; pre-fingerprint evidence cannot be selected from."
        }
    }
    $ef = [string]$r.Meta.experiment_fingerprint
    $cf = [string]$r.Meta.corpus_fingerprint
    $bh = [string]$r.Meta.binary_hash
    $hf = [string]$r.Meta.execution_harness_fingerprint
    if ($null -eq $experimentFingerprint) {
        $experimentFingerprint = $ef
        $corpusFingerprint = $cf
        $binaryHash = $bh
        $harnessFingerprint = $hf
        if ($r.Meta.PSObject.Properties.Name -contains 'experiment_id') { $experimentId = [string]$r.Meta.experiment_id }
        if ($r.Meta.PSObject.Properties.Name -contains 'schedule_seed') { try { $scheduleSeed = [int]$r.Meta.schedule_seed } catch {} }
    } else {
        if ($ef -ne $experimentFingerprint) { throw "Mixed experiment fingerprints across runs ($ef vs $experimentFingerprint)." }
        if ($cf -ne $corpusFingerprint) { throw "Mixed corpus fingerprints across runs." }
        if ($bh -ne $binaryHash) { throw "Mixed binary hashes across runs." }
        if ($hf -ne $harnessFingerprint) { throw "Mixed execution-harness fingerprints across runs ($hf vs $harnessFingerprint)." }
    }
}
if ($harnessFingerprint -ne $currentHarnessFingerprint) {
    throw ("Run evidence was produced by a different execution harness (found $harnessFingerprint, current $currentHarnessFingerprint). Any runner/harness change requires clean evidence; select at the harness revision recorded by the runs.")
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
$expectedCorpusRows = @(Import-Csv -LiteralPath $expectedCorpusPath)
# Smoke and scaled experiments may restrict the corpus with an explicit
# fixture_ids filter; the runner executes exactly those rows, so the
# expected matrix is computed over the same filtered set.
if ($experimentJson.PSObject.Properties.Name -contains 'fixture_ids' -and $null -ne $experimentJson.fixture_ids -and @($experimentJson.fixture_ids).Count -gt 0) {
    $filterSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($fid in @($experimentJson.fixture_ids)) { [void]$filterSet.Add([string]$fid) }
    $expectedCorpusRows = @($expectedCorpusRows | Where-Object { $filterSet.Contains([string]$_.fixture_id) })
    if ($expectedCorpusRows.Count -ne $filterSet.Count) {
        throw "Experiment fixture_ids filter matched $($expectedCorpusRows.Count) corpus rows but declared $($filterSet.Count) fixtures."
    }
}
$expectedRowCount = $expectedCorpusRows.Count
$expectedFixtureIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($fr in $expectedCorpusRows) { [void]$expectedFixtureIds.Add([string]$fr.fixture_id) }

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
    # The runner resolves every candidate through the real downloader and
    # records its C# FetchExecutionPolicy.Fingerprint() - the v2 canonical
    # form, which includes the behavior-affecting AllowAndroidStoreLookup
    # flag. The field is mandatory there, so it is mandatory here too:
    # behavior is never inferred from an absent field.
    if (-not ($Def.PSObject.Properties.Name -contains 'allowAndroidStoreLookup')) {
        throw "Candidate definition '$($Def.id)' must declare allowAndroidStoreLookup explicitly; the recorded policy fingerprint covers it."
    }
    $android = ParseBoolValue -Value $Def.allowAndroidStoreLookup
    $syn = if ($synthetic) { "1" } else { "0" }
    $stp = if ($stop) { "1" } else { "0" }
    $and = if ($android) { "1" } else { "0" }
    return ("v2|providers={0}|primaryMs={1}|fallbackMs={2}|cumulativeMs={3}|synthetic={4}|stopAfterStrongResolved={5}|androidStore={6}" -f `
        ($providerIds -join ','), $primary, $fallback, $cumulative, $syn, $stp, $and)
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
    $runRows = @(Import-Csv -LiteralPath $rowsCsv)
    $rowCount = $runRows.Count

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
    # Exact fixture set per cell (count equality alone misses a duplicated
    # fixture displacing a missing one).
    $rowFixtureIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($rr in $runRows) { [void]$rowFixtureIds.Add([string]$rr.fixture_id) }
    $missingFixtures = @($expectedFixtureIds | Where-Object { -not $rowFixtureIds.Contains($_) })
    if ($missingFixtures.Count -gt 0) {
        throw "Run $($r.Directory) is missing expected fixtures: $($missingFixtures -join ', ')."
    }
    $unexpectedFixtures = @($rowFixtureIds | Where-Object { -not $expectedFixtureIds.Contains($_) })
    if ($unexpectedFixtures.Count -gt 0) {
        throw "Run $($r.Directory) contains unexpected fixtures: $($unexpectedFixtures -join ', ')."
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
# 2. Load measured rows with strict provider_metrics parsing (warm-up runs
#    are never evidence)
# ---------------------------------------------------------------------------

$allRows = @()
foreach ($cellKey in @($measuredByCell.Keys | Sort-Object)) {
    $r = $measuredByCell[$cellKey]
    $rowsCsv = Join-Path $r.Directory "rows.csv"
    foreach ($row in @(Import-Csv -LiteralPath $rowsCsv)) {
        $row | Add-Member -NotePropertyName "_cell" -NotePropertyValue $cellKey -Force
        $row | Add-Member -NotePropertyName "_run_directory" -NotePropertyValue $r.Directory -Force
        $isCold = $true
        $rowCacheMode = ""
        if ($row.PSObject.Properties.Name -contains "cache_mode") { $rowCacheMode = [string]$row.cache_mode }
        if ($rowCacheMode -eq "warm") { $isCold = $false }
        $row | Add-Member -NotePropertyName "_cold" -NotePropertyValue $isCold -Force
        $row | Add-Member -NotePropertyName "_metrics" -NotePropertyValue (Get-StrictProviderMetrics -Row $row) -Force
        $allRows += $row
    }
}
$coldRows = @($allRows | Where-Object { $_._cold })
if ($coldRows.Count -eq 0) { throw "No measured cold rows found; scoring is cold-only and requires cold evidence." }

# ---------------------------------------------------------------------------
# 3. Review queue: the queue must be EXACTLY the cold census - every unique
#    cold (fixture_id, artifact_hash) unit, no hole and no extra key
# ---------------------------------------------------------------------------

$allowedLabels = @("correct","acceptable-synthetic","generic","wrong-brand","blank","unusable","ambiguous")
$queueRows = @(Import-Csv -LiteralPath $reviewQueueResolved)
if ($queueRows.Count -eq 0) { throw "Review queue empty: $reviewQueueResolved" }

$reviewMap = @{}
$queueKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$queueReviewers = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
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
        throw "Unreviewed rows remain in the queue ($key). Complete the review (human, or owner-approved machine review with machine: reviewer provenance) first."
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
    $styles = [System.Globalization.DateTimeStyles]::AllowLeadingWhite -bor [System.Globalization.DateTimeStyles]::AllowTrailingWhite
    if (-not [DateTime]::TryParse($reviewedAt, [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$dt)) {
        throw "Unparseable reviewed_at_utc for ${key}: '$reviewedAt'"
    }
    if (-not [string]::IsNullOrWhiteSpace($reviewer)) { [void]$queueReviewers.Add($reviewer.Trim()) }
    $reviewMap[$key] = $label
}

# Review provenance is derived from the queue itself, never asserted: a
# machine reviewer (machine: prefix) must be disclosed as machine review in
# the evidence report; mixed provenance is listed verbatim.
$machineReviewers = @($queueReviewers | Where-Object { $_ -match '^machine:' } | Sort-Object)
$humanReviewers = @($queueReviewers | Where-Object { $_ -notmatch '^machine:' } | Sort-Object)
$isMachineReview = ($machineReviewers.Count -gt 0 -and $humanReviewers.Count -eq 0)
$reviewerList = (@($queueReviewers | Sort-Object) -join ', ')
$reviewKindPhrase = "human-reviewed"
$reviewDisclosureLines = @()
if ($isMachineReview) {
    $reviewKindPhrase = "machine-reviewed"
    $reviewDisclosureLines = @(
        "- Review provenance: every census label was produced by MACHINE REVIEW, not a human reviewer. Reviewer of record: ``$reviewerList``. The owner approved this methodology amendment on 2026-08-22; the labeling pipeline, dual-lane pilot with pixel arbitration, prompts, batch outputs, and the owner's stratified spot-check are recorded under ``machine-review/`` in the selecting repository. These labels must never be presented as human review."
    )
} elseif ($machineReviewers.Count -gt 0) {
    $reviewKindPhrase = "reviewed (mixed human and machine provenance)"
    $reviewDisclosureLines = @(
        "- Review provenance is MIXED - human reviewers and machine reviewers both appear in the queue. Reviewers of record: ``$reviewerList``. Machine labels (machine: prefix) and their pipeline evidence are recorded under ``machine-review/``; they must never be presented as human review."
    )
}

# Regenerate the cold census independently from the evidence (same definition
# as prepare-review.ps1: every cold row with a non-empty artifact hash,
# regardless of machine outcome). The queue must equal it exactly: a census
# with holes is a sample with unknown selection bias, and a queue key that is
# not a census unit is stale or fabricated.
$coldCensusKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($row in $coldRows) {
    $fid = [string]$row.fixture_id
    $hash = [string]$row.artifact_hash
    if ([string]::IsNullOrWhiteSpace($fid) -or [string]::IsNullOrWhiteSpace($hash)) { continue }
    [void]$coldCensusKeys.Add("$fid|$hash")
}
if ($coldCensusKeys.Count -eq 0) {
    throw "Cold evidence contains no artifacts; the census has no units and nothing can be scored."
}
$extraQueueKeys = @($queueKeys | Where-Object { -not $coldCensusKeys.Contains($_) })
if ($extraQueueKeys.Count -gt 0) {
    throw ("Review queue contains keys that are not cold census units (stale or fabricated): " + (($extraQueueKeys | Select-Object -First 5) -join ", ") + " (and possibly more). Regenerate the queue from the current evidence.")
}
$missingCensusKeys = @($coldCensusKeys | Where-Object { -not $queueKeys.Contains($_) })
if ($missingCensusKeys.Count -gt 0) {
    throw ("Census unit missing from the review queue: " + (($missingCensusKeys | Select-Object -First 5) -join ", ") + " (and possibly more). The queue must be a complete census; regenerate and review every cold unit.")
}

# ---------------------------------------------------------------------------
# 4. Cold-only scoring per candidate (census counts, no weights)
# ---------------------------------------------------------------------------

$statsList = @()
foreach ($cid in $candidateIds) {
    $rowsForCid = @($allRows | Where-Object {
        $p = ""
        if ($_.PSObject.Properties.Name -contains "profile") { $p = [string]$_.profile }
        return $p -eq $cid
    })
    $rowsCold = @($rowsForCid | Where-Object { $_._cold })
    $rowsWarm = @($rowsForCid | Where-Object { -not $_._cold })
    $total = $rowsCold.Count
    if ($total -eq 0) { throw "No cold rows found for candidate '$cid'." }

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
    $resumedAny = $false

    # Scored quantities: COLD rows only.
    foreach ($r in $rowsCold) {
        $mo = ""
        if ($r.PSObject.Properties.Name -contains "machine_outcome") { $mo = ([string]$r.machine_outcome).ToLowerInvariant() }
        if ($mo -eq "success") { $successMachine++ }
        elseif ($mo -eq "timeout") { $timeoutCount++ }
        elseif ($mo -eq "provider-error") { $providerErrorCount++ }
        elseif ($mo -eq "harness-error") { $harnessErrorCount++ }
        elseif ($mo -eq "invalid-image") { $invalidImageCount++ }
        if (ParseBoolValue -Value $r.is_synthetic) { $syntheticRows++ }

        $elapsed = 0
        if ($r.PSObject.Properties.Name -contains "total_elapsed_ms") {
            try { $elapsed = [long]$r.total_elapsed_ms } catch { $elapsed = 0 }
        }
        $elapsedCold += $elapsed

        foreach ($m in @($r._metrics)) {
            if ($null -eq $m) { continue }
            $name = ([string]$m.provider)
            if ($name -eq "Cache" -or $name -eq "Coalesced" -or $name -eq "Harness" -or $name -eq "Pipeline" -or $name -eq "Google Play") { continue }
            $providerCallCount++
            $canonical = Get-CanonicalMetricProvider -Name $name
            if ($thirdPartyIds.ContainsKey($canonical)) { $thirdPartyCallCount++ }
        }
    }

    # Fixtures disclosed to any third party come from observed provider calls
    # in COLD rows, not from the configured chain.
    $disclosedAnyThird = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in $rowsCold) {
        foreach ($m in @($r._metrics)) {
            if ($null -eq $m) { continue }
            $canonical = Get-CanonicalMetricProvider -Name ([string]$m.provider)
            if ($thirdPartyIds.ContainsKey($canonical)) {
                [void]$disclosedAnyThird.Add([string]$r.fixture_id)
                break
            }
        }
    }

    # Warm rows are latency evidence only.
    foreach ($r in $rowsWarm) {
        $elapsed = 0
        if ($r.PSObject.Properties.Name -contains "total_elapsed_ms") {
            try { $elapsed = [long]$r.total_elapsed_ms } catch { $elapsed = 0 }
        }
        $elapsedWarm += $elapsed
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
        if ($activeMs -ge 0 -and $mode -ne "warm") { $activeBatchCold += $activeMs }
    }

    # Census label counts over the candidate's distinct COLD success units.
    # Machine availability and human usability remain separate evidence: an
    # unreviewed success is never counted as correct - under the census every
    # success unit is reviewed, and the counts below are exact.
    $usableCount = 0
    $genericCount = 0
    $wrongBrandCount = 0
    $blankUnusableCount = 0
    $ambiguousCount = 0
    $reviewedTotal = 0
    $successWithoutArtifact = 0
    $seenUnitsForCid = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in $rowsCold) {
        $mo = ""
        if ($r.PSObject.Properties.Name -contains "machine_outcome") { $mo = ([string]$r.machine_outcome).ToLowerInvariant() }
        if ($mo -ne "success") { continue }
        $hash = [string]$r.artifact_hash
        if ([string]::IsNullOrWhiteSpace($hash)) { $successWithoutArtifact++; continue }
        $key = "$([string]$r.fixture_id)|$hash"
        if (-not $seenUnitsForCid.Add($key)) { continue }
        $label = $null
        if ($reviewMap.ContainsKey($key)) { $label = $reviewMap[$key] }
        if ($null -eq $label) {
            throw "Census unit $key (candidate '$cid') has no review label even though the queue passed census equality; refusing to score."
        }
        $reviewedTotal++
        switch ($label) {
            "correct" { $usableCount++ }
            "acceptable-synthetic" { $usableCount++ }
            "generic" { $genericCount++ }
            "wrong-brand" { $wrongBrandCount++ }
            "blank" { $blankUnusableCount++ }
            "unusable" { $blankUnusableCount++ }
            "ambiguous" { $ambiguousCount++ }
            default { }
        }
    }

    # Census proportions. Main rate excludes ambiguous labels from numerator
    # and denominator (v1.3 design); the pessimistic/optimistic variants are
    # used by the ambiguity replay in section 5.
    $correctnessDenominator = $reviewedTotal - $ambiguousCount
    $usableMain = 0.0
    $usablePessimistic = 0.0
    $usableOptimistic = 0.0
    $estimatedWrongBrand = 0.0
    if ($correctnessDenominator -gt 0) {
        $usableMain = [double]$usableCount / [double]$correctnessDenominator
        $estimatedWrongBrand = [double]$wrongBrandCount / [double]$correctnessDenominator
    }
    if ($reviewedTotal -gt 0) {
        $usablePessimistic = [double]$usableCount / [double]$reviewedTotal
        $usableOptimistic = [double]($usableCount + $ambiguousCount) / [double]$reviewedTotal
    }

    $machineAvailability = [double]$successMachine / [double]$total
    # Documented coverage formula: cold machine availability x reviewed usability.
    $coverage = $machineAvailability * $usableMain

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
    $avgBatchCold = ($activeBatchCold | Measure-Object -Average).Average
    if ($null -eq $avgBatchCold) { $avgBatchCold = 0 }
    $throughput = 0.0
    $coldCellCount = 0
    foreach ($mode in $expectedCacheModes) { if ($mode -ne "warm") { $coldCellCount++ } }
    if ($coldCellCount -eq 0) { $coldCellCount = 1 }
    if ($avgBatchCold -gt 0) {
        $throughput = ([double]$total / [double]$coldCellCount) / ($avgBatchCold / 1000.0)
    }

    $def = $candidatesDef[$cid]
    $stats = [PSCustomObject]@{
        profile_id = $cid
        definition = $def
        cold_rows = $total
        warm_rows = $rowsWarm.Count
        machine_success = $successMachine
        machine_availability = $machineAvailability
        timeout = $timeoutCount
        provider_error = $providerErrorCount
        harness_error = $harnessErrorCount
        invalid_image = $invalidImageCount
        synthetic_rows = $syntheticRows
        reviewed_units = $reviewedTotal
        usable_units = $usableCount
        generic_units = $genericCount
        wrong_brand_units = $wrongBrandCount
        blank_unusable_units = $blankUnusableCount
        ambiguous_units = $ambiguousCount
        success_without_artifact = $successWithoutArtifact
        estimated_usable_rate = $usableMain
        estimated_wrong_brand = $estimatedWrongBrand
        coverage = $coverage
        reliability = $reliability
        cold_median_ms = $coldMedian
        cold_p95_ms = $coldP95
        cold_max_ms = $coldMax
        warm_median_ms = $warmMedian
        warm_p95_ms = $warmP95
        active_batch_cold_ms = [long]$avgBatchCold
        cold_throughput_per_sec = $throughput
        resumed_any = $resumedAny
        provider_call_count = $providerCallCount
        provider_calls_per_input = [double]$providerCallCount / [double]$total
        third_party_call_count = $thirdPartyCallCount
        third_party_calls_per_input = [double]$thirdPartyCallCount / [double]$total
        third_party_disclosure_rate = [double]$disclosedAnyThird.Count / [double]$total
    }
    $stats | Add-Member -NotePropertyName "_usable_main" -NotePropertyValue $usableMain -Force
    $stats | Add-Member -NotePropertyName "_usable_pessimistic" -NotePropertyValue $usablePessimistic -Force
    $stats | Add-Member -NotePropertyName "_usable_optimistic" -NotePropertyValue $usableOptimistic -Force
    $statsList += $stats
}

# ---------------------------------------------------------------------------
# 5. Winner rules + ambiguity replay (one shared winner function)
# ---------------------------------------------------------------------------

# Scenario usable-rate accessor: 'main' excludes ambiguous labels from the
# denominator, 'pessimistic' counts them as failure, 'optimistic' counts
# them as usable.
function Get-ScenarioUsable {
    param([Parameter(Mandatory=$true)][object]$Stat, [Parameter(Mandatory=$true)][string]$Scenario)
    switch ($Scenario) {
        "main" { return [double]$Stat._usable_main }
        "pessimistic" { return [double]$Stat._usable_pessimistic }
        "optimistic" { return [double]$Stat._usable_optimistic }
        default { throw "Unknown ambiguity scenario '$Scenario'." }
    }
}

$ambiguityNotes = @()
$ambiguityRejections = @()

# The single winner function used by every role. The Ranking scriptblock
# receives the scenario name and returns the full ordered candidate list
# (best first) under that scenario's ambiguity treatment. All three
# replayed rankings must be non-empty and name the same winner, or the
# whole selection is rejected.
function Invoke-WinnerRule {
    param(
        [Parameter(Mandatory=$true)][string]$Role,
        [Parameter(Mandatory=$true)][scriptblock]$Ranking
    )
    $ranked = @{}
    foreach ($scenario in @('main','pessimistic','optimistic')) {
        $ordered = @(& $Ranking $scenario)
        if ($ordered.Count -eq 0) {
            $script:ambiguityRejections += ("{0}: the eligible set is empty under the '{1}' ambiguity replay" -f $Role, $scenario)
            return $null
        }
        $ranked[$scenario] = $ordered
    }
    $winnerMain = $ranked['main'][0].profile_id
    $winnerPess = $ranked['pessimistic'][0].profile_id
    $winnerOpti = $ranked['optimistic'][0].profile_id
    if ($winnerMain -ne $winnerPess -or $winnerMain -ne $winnerOpti) {
        $script:ambiguityRejections += ("{0}: winner is not stable under ambiguity replay (as-reviewed '{1}', ambiguity-as-failure '{2}', ambiguity-as-usable '{3}')" -f $Role, $winnerMain, $winnerPess, $winnerOpti)
        return $null
    }
    $script:ambiguityNotes += ("{0}: stable under ambiguity replay ({1})" -f $Role, $winnerMain)
    return $ranked['main'][0]
}

# 1. Privacy winner: no third-party provider in the chain AND zero observed
# cold third-party disclosures; best estimated usable rate.
$privacyCandidates = @($statsList | Where-Object {
    $chain = @()
    if ($_.definition.PSObject.Properties.Name -contains 'providerIds') { $chain = @($_.definition.providerIds) }
    $hasThird = $false
    foreach ($p in $chain) { if ($thirdPartyIds.ContainsKey(([string]$p).ToLowerInvariant())) { $hasThird = $true; break } }
    return ((-not $hasThird) -and ($_.third_party_disclosure_rate -eq 0.0))
})
if ($privacyCandidates.Count -eq 0) { throw "No privacy-eligible candidate (direct-site-only with zero observed cold third-party disclosures)." }
$privacyWinner = Invoke-WinnerRule -Role "privacy" -Ranking {
    param([string]$scenario)
    @($privacyCandidates | Sort-Object -Property `
        @{ Expression = { -(Get-ScenarioUsable -Stat $_ -Scenario $scenario) } }, `
        @{ Expression = { $_.cold_p95_ms } }, `
        @{ Expression = { $_.profile_id } })
}

# 2. Max-coverage winner: highest estimated usable rate.
$maxCoverageWinner = Invoke-WinnerRule -Role "max-coverage" -Ranking {
    param([string]$scenario)
    @($statsList | Sort-Object -Property `
        @{ Expression = { -(Get-ScenarioUsable -Stat $_ -Scenario $scenario) } }, `
        @{ Expression = { $_.cold_p95_ms } }, `
        @{ Expression = { $_.profile_id } })
}

# 3. Bulk-fast: lowest ACTIVE wall-clock cold batch duration among
# candidates with usable >= 90% of the scenario's best usable rate and
# wrong-brand <= 1%. The threshold is scenario-scoped: the replay compares
# like with like instead of mixing scenarios.
$bulkWinner = Invoke-WinnerRule -Role "bulk-fast" -Ranking {
    param([string]$scenario)
    $bestUsable = 0.0
    foreach ($s in $statsList) {
        $u = Get-ScenarioUsable -Stat $s -Scenario $scenario
        if ($u -gt $bestUsable) { $bestUsable = $u }
    }
    $threshold = $bestUsable * 0.90
    $eligible = @($statsList | Where-Object {
        return ((Get-ScenarioUsable -Stat $_ -Scenario $scenario) -ge $threshold -and $_.estimated_wrong_brand -le 0.01)
    })
    return @($eligible | Sort-Object -Property `
        @{ Expression = { $_.active_batch_cold_ms } }, `
        @{ Expression = { $_.cold_median_ms } }, `
        @{ Expression = { $_.profile_id } })
}

# 4. Everyday: documented weighted score, with every normalization base
# recomputed under the replayed scenario. Privacy score uses OBSERVED cold
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

$everydayWinner = Invoke-WinnerRule -Role "everyday" -Ranking {
    param([string]$scenario)
    $cm = Get-MinMax -Values @($statsList | ForEach-Object { Get-ScenarioUsable -Stat $_ -Scenario $scenario })
    $covM = Get-MinMax -Values @($statsList | ForEach-Object { $_.machine_availability * (Get-ScenarioUsable -Stat $_ -Scenario $scenario) })
    $relM = Get-MinMax -Values @($statsList | ForEach-Object { [double]$_.reliability })
    $latM = Get-MinMax -Values @($statsList | ForEach-Object { [double]$_.cold_median_ms })
    $privM = Get-MinMax -Values @($statsList | ForEach-Object { [double]$_.third_party_disclosure_rate })
    $scored = @($statsList | ForEach-Object {
        $s = $_
        $usable = Get-ScenarioUsable -Stat $s -Scenario $scenario
        $cn = NormalizeValue -Value ([double]$usable) -Min $cm.min -Max $cm.max
        $covn = NormalizeValue -Value ([double]($s.machine_availability * $usable)) -Min $covM.min -Max $covM.max
        $reln = NormalizeValue -Value ([double]$s.reliability) -Min $relM.min -Max $relM.max
        $latRaw = NormalizeValue -Value ([double]$s.cold_median_ms) -Min $latM.min -Max $latM.max
        $latScore = 1.0 - $latRaw
        $privRaw = NormalizeValue -Value ([double]$s.third_party_disclosure_rate) -Min $privM.min -Max $privM.max
        $privScore = 1.0 - $privRaw
        $score = 0.40 * $cn + 0.25 * $covn + 0.15 * $latScore + 0.10 * $reln + 0.10 * $privScore
        $s | Add-Member -NotePropertyName "_everyday_score_scenario" -NotePropertyValue $score -Force
        return $s
    })
    return @($scored | Sort-Object -Property @{ Expression = { -$_._everyday_score_scenario } }, @{ Expression = { $_.profile_id } })
}

if ($ambiguityRejections.Count -gt 0) {
    throw ("Ambiguity sensitivity rejected the selection:`n - " + ($ambiguityRejections -join "`n - ") + "`nExpand the human review to resolve ambiguous labels, or conservatively count ambiguity as failure and rerun.")
}
foreach ($roleWinner in @($privacyWinner, $maxCoverageWinner, $bulkWinner, $everydayWinner)) {
    if ($null -eq $roleWinner) { throw "A winner rule returned no winner; the selection is incomplete and cannot proceed." }
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
    @{ Id = "max-coverage";   DisplayName = "Thorough";  IntendedUse = "Maximum coverage with the study-selected resolver chain";        Winner = $maxCoverageWinner }
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
    $androidStore = if ($def.PSObject.Properties.Name -contains 'allowAndroidStoreLookup' -and [bool]$def.allowAndroidStoreLookup) { "true" } else { "false" }
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
    $generatedLines.Add("                " + $androidStore + ",")
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
$evidenceLines.Add("- Execution harness fingerprint: ``$harnessFingerprint`` (matches the selecting repository's harness)")
$evidenceLines.Add("- Schedule seed: $scheduleSeed (SHA-256 seeded schedule; cold cells interleaved per repetition, warm blocks contiguous per candidate after a clear-once warm-up)")
$evidenceLines.Add("- Matrix: $($candidateIds.Count) candidates x $($expectedCacheModes -join '+') x $expectedRepetitions repetitions")
$evidenceLines.Add("")
$evidenceLines.Add("## Methodology")
$evidenceLines.Add("")
$evidenceLines.Add("- Execution policies: every candidate executed its recorded provider chain, per-provider and cumulative budgets, synthetic flag, and early-stop flag; run.json policy fingerprints were verified against the experiment definition, and the execution-harness fingerprint was verified uniform and current.")
$evidenceLines.Add("- Cold/warm: cold runs clear all caches before every measured run. Warm blocks clear once, perform an unmeasured warm-up over the full corpus, then run measured warm repetitions without clearing. Warm-up runs are marked and excluded from all metrics.")
$evidenceLines.Add("- Scoring is cold-only: machine availability, label rates, provider calls, third-party disclosure, and latency statistics are computed over measured cold cells; warm rows appear only as informational latency percentiles and never enter a winner rule.")
$evidenceLines.Add("- Review is a CENSUS: every unique cold (fixture_id, artifact_hash) unit is $reviewKindPhrase exactly once and the label propagates to every occurrence of that exact artifact across repetitions and candidates. There is no sampling, no stratification, and no design weighting; the selection verifies that the review queue equals the cold census exactly (no missing unit, no extra key).")
$evidenceLines.Add("- Statistics: machine availability and census labels are separate evidence; an unreviewed success is never counted as correct. Reported label rates are exact proportions over the reviewed census population - with a complete census there is no sampling error to estimate, so no interval estimates are reported anywhere.")
foreach ($disclosureLine in $reviewDisclosureLines) { $evidenceLines.Add($disclosureLine) }
$evidenceLines.Add("- Correctness excludes ambiguous labels from numerator and denominator. Rows without an artifact hash cannot carry exact identity and remain machine evidence only.")
$evidenceLines.Add("- provider_metrics is parsed strictly: every entry must name a known provider and outcome with integral counts, and every successful fetch must record at least one provider activity; violations reject the selection.")
$evidenceLines.Add("- Batch speed uses active wall-clock duration per cell from run.json (resumed runs accumulate active time only; wall-clock across interruptions is never used).")
$evidenceLines.Add("- Ambiguity replay: every winner rule was recomputed with ambiguous labels counted as failure and as usable; the selection is reported only when every role's winner is stable across both replays.")
$evidenceLines.Add("")
$evidenceLines.Add("## Candidate comparison (cold cells)")
$evidenceLines.Add("")
$evidenceLines.Add("| candidate | cold rows | machine avail. | reviewed units | usable rate | wrong-brand | coverage | reliability | cold median/p95 (ms) | warm median (ms) | active cold batch (ms) | 3p disclosure | timeout | provider err |")
$evidenceLines.Add("|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|")
foreach ($s in ($statsList | Sort-Object -Property profile_id)) {
    $evidenceLines.Add(("| {0} | {1} | {2:P1} | {3} | {4:P1} | {5:P2} | {6:P1} | {7:P1} | {8}/{9} | {10} | {11} | {12:P1} | {13} | {14} |" -f `
        $s.profile_id, $s.cold_rows, $s.machine_availability, $s.reviewed_units, $s.estimated_usable_rate, `
        $s.estimated_wrong_brand, $s.coverage, $s.reliability, $s.cold_median_ms, $s.cold_p95_ms, $s.warm_median_ms, `
        $s.active_batch_cold_ms, $s.third_party_disclosure_rate, $s.timeout, $s.provider_error))
}
$evidenceLines.Add("")
$evidenceLines.Add("Warm median is informational latency evidence from the warm blocks; it is not scored.")
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
$evidenceLines.Add("- The census removes sampling error from label rates, not reviewer judgment error: each label is one $($reviewKindPhrase.Replace('-reviewed','').Replace('reviewed (','').Replace(')','')) decision per unique artifact" + $(if ($isMachineReview) { " (machine judgment error characteristics differ from human judgment error)" } else { "" }) + ".")
$evidenceLines.Add("- Privacy is measured by observed third-party call evidence, not by the configured chain alone.")
$evidenceLines.Add("")
$evidenceLines.Add("## Reproduction")
$evidenceLines.Add("")
$evidenceLines.Add("``````powershell")
$evidenceLines.Add("dotnet build KeeFetch.csproj --configuration Release -p:KeePassPath=<KeePass dir>")
$evidenceLines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark-presets.ps1 -Experiment eng/benchmark/experiments/$experimentId.json")
$evidenceLines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/prepare-review.ps1 -RunDir <output root>")
$evidenceLines.Add("# review of the complete cold census (this report's provenance: $reviewKindPhrase), then:")
$evidenceLines.Add("powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/select-profiles.ps1 -RunDir <output root>")
$evidenceLines.Add("``````")
$evidenceLines.Add("")
$evidence = $evidenceLines -join "`n"

$summary = [PSCustomObject]@{
    experiment_id = $experimentId
    experiment_fingerprint = $experimentFingerprint
    corpus_fingerprint = $corpusFingerprint
    binary_hash = $binaryHash
    execution_harness_fingerprint = $harnessFingerprint
    schedule_seed = $scheduleSeed
    matrix = "{0}x{1}x{2}" -f $candidateIds.Count, ($expectedCacheModes -join "+"), $expectedRepetitions
    review_queue = $reviewQueueResolved
    census_units = $coldCensusKeys.Count
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
            cold_rows = $_.cold_rows
            warm_rows = $_.warm_rows
            machine_availability = $_.machine_availability
            estimated_usable_rate = $_.estimated_usable_rate
            estimated_wrong_brand = $_.estimated_wrong_brand
            reviewed_units = $_.reviewed_units
            usable_units = $_.usable_units
            generic_units = $_.generic_units
            wrong_brand_units = $_.wrong_brand_units
            blank_unusable_units = $_.blank_unusable_units
            ambiguous_units = $_.ambiguous_units
            success_without_artifact = $_.success_without_artifact
            coverage = $_.coverage
            reliability = $_.reliability
            cold_median_ms = $_.cold_median_ms
            cold_p95_ms = $_.cold_p95_ms
            warm_median_ms = $_.warm_median_ms
            provider_call_count = $_.provider_call_count
            active_batch_cold_ms = $_.active_batch_cold_ms
            third_party_disclosure_rate = $_.third_party_disclosure_rate
            resumed_any = $_.resumed_any
        }
    })
}
$summaryJson = ConvertTo-Json -InputObject $summary -Depth 6

# ---------------------------------------------------------------------------
# 8. Output (default: OutputDir only; -Publish: verified repository writes)
# ---------------------------------------------------------------------------

$generatedCsPath = Join-Path $outputDirResolved "FetchProfileCatalog.Generated.cs"
$evidencePath = Join-Path $outputDirResolved "v1.3-provider-study.md"
$summaryPath = Join-Path $outputDirResolved "selection-summary.json"
$reportPath = Join-Path $outputDirResolved "selection-report.md"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$generatedCsNormalized = $generatedCs.Replace("`r`n", "`n")
$evidenceNormalized = $evidence.Replace("`r`n", "`n")
$summaryNormalized = $summaryJson.Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($generatedCsPath, $generatedCsNormalized, $utf8NoBom)
[System.IO.File]::WriteAllText($evidencePath, $evidenceNormalized, $utf8NoBom)
[System.IO.File]::WriteAllText($summaryPath, $summaryNormalized, $utf8NoBom)
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
$reportNormalized = (($reportLines -join "`n") + "`n").Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($reportPath, $reportNormalized, $utf8NoBom)

Write-Output ""
Write-Output "Selection outputs written to: $outputDirResolved"
Write-Output "  - FetchProfileCatalog.Generated.cs (candidate; review before publishing)"
Write-Output "  - v1.3-provider-study.md (evidence report)"
Write-Output "  - selection-summary.json"
Write-Output "  - selection-report.md"

if ($Publish) {
    # Fail-closed publication: every OutputDir artifact must exist, and each
    # repository write is read back and byte-compared before reporting
    # success. Any mismatch leaves nothing silently half-published.
    foreach ($artifact in @($generatedCsPath, $evidencePath, $summaryPath, $reportPath)) {
        if (-not (Test-Path -LiteralPath $artifact)) {
            throw "Published-source artifact missing before publication: $artifact"
        }
    }
    $repoGeneratedPath = Join-Path $repoRoot "FetchProfiles\FetchProfileCatalog.Generated.cs"
    $repoEvidencePath = Join-Path $repoRoot "docs\benchmarks\v1.3-provider-study.md"

    [System.IO.File]::WriteAllText($repoGeneratedPath, $generatedCsNormalized, $utf8NoBom)
    $evidenceDir = Split-Path -Parent $repoEvidencePath
    if (-not (Test-Path -LiteralPath $evidenceDir)) {
        New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($repoEvidencePath, $evidenceNormalized, $utf8NoBom)

    $readBackGenerated = [System.IO.File]::ReadAllText($repoGeneratedPath)
    if (-not $readBackGenerated.Equals($generatedCsNormalized, [System.StringComparison]::Ordinal)) {
        throw "Published catalog write-back verification failed (content mismatch): $repoGeneratedPath"
    }
    $readBackEvidence = [System.IO.File]::ReadAllText($repoEvidencePath)
    if (-not $readBackEvidence.Equals($evidenceNormalized, [System.StringComparison]::Ordinal)) {
        throw "Published evidence report write-back verification failed (content mismatch): $repoEvidencePath"
    }

    Write-Output ""
    Write-Output "Published (write-back verified):"
    Write-Output "  - $repoGeneratedPath"
    Write-Output "  - $repoEvidencePath"
    Write-Output "Next: rebuild, run the full test suite, and regenerate site/data/profiles.json with eng/export-profile-data.ps1."
}
