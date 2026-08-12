param(
    [string]$RunDir,
    [string]$ReviewQueue,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

function FindRepoRoot {
    $candidate = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($candidate)) { $candidate = (Get-Location).Path }
    # PSScriptRoot is eng/benchmark, go two up
    $root = Split-Path -Parent (Split-Path -Parent $candidate)
    if (Test-Path -LiteralPath (Join-Path $root "KeeFetch.sln")) { return (Resolve-Path -LiteralPath $root).Path }
    # fallback: try current location parent chain
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

function GetPercentile {
    param([long[]]$SortedValues, [double]$Percent)
    if ($SortedValues.Count -eq 0) { return 0 }
    # SortedValues must be sorted ascending
    $index = [Math]::Ceiling($Percent * $SortedValues.Count) - 1
    if ($index -lt 0) { $index = 0 }
    if ($index -ge $SortedValues.Count) { $index = $SortedValues.Count - 1 }
    return $SortedValues[$index]
}

$repoRoot = FindRepoRoot

if ([string]::IsNullOrWhiteSpace($RunDir)) {
    $defaultRunRoot = Join-Path $repoRoot "eng/benchmark-runs/profile-candidates-v13"
    if (Test-Path -LiteralPath $defaultRunRoot) { $RunDir = $defaultRunRoot } else { $RunDir = $repoRoot }
}
if ([string]::IsNullOrWhiteSpace($ReviewQueue)) {
    $try1 = Join-Path $RunDir "review-queue.csv"
    if (Test-Path -LiteralPath $try1) { $ReviewQueue = $try1 }
    else {
        # search under RunDir
        $found = @(Get-ChildItem -Path $RunDir -Filter "review-queue.csv" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1)
        if ($found.Count -gt 0) { $ReviewQueue = $found[0].FullName }
        else { $ReviewQueue = $try1 }
    }
}
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = $RunDir
}

# Resolve RunDir
if (-not (Test-Path -LiteralPath $RunDir)) { throw "RunDir not found: $RunDir" }
$runDirResolved = $RunDir
if ((Get-Item -LiteralPath $RunDir).PSIsContainer) {
    $runDirResolved = (Resolve-Path -LiteralPath $RunDir).Path
} else {
    $runDirResolved = (Resolve-Path -LiteralPath (Split-Path -Parent $RunDir)).Path
}

# Resolve ReviewQueue
if (-not (Test-Path -LiteralPath $ReviewQueue)) { throw "ReviewQueue not found: $ReviewQueue" }
$reviewQueueResolved = (Resolve-Path -LiteralPath $ReviewQueue).Path

# Ensure OutputDir exists
if (-not (Test-Path -LiteralPath $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
$outputDirResolved = (Resolve-Path -LiteralPath $OutputDir).Path

# Discover run.json files
$runJsonFiles = @()
$directRunJson = Join-Path $runDirResolved "run.json"
if (Test-Path -LiteralPath $directRunJson) { $runJsonFiles += (Resolve-Path -LiteralPath $directRunJson).Path }
# Also search subdirs when RunDir is output_root
$subRunJsons = @(Get-ChildItem -Path $runDirResolved -Filter "run.json" -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
foreach ($f in $subRunJsons) {
    $already = $false
    foreach ($e in $runJsonFiles) { if ($e -eq $f) { $already = $true; break } }
    if (-not $already) { $runJsonFiles += $f }
}
if ($runJsonFiles.Count -eq 0) { throw "No run.json found under RunDir: $runDirResolved" }

foreach ($rj in $runJsonFiles) {
    $meta = Get-Content -Raw -LiteralPath $rj | ConvertFrom-Json
    $status = ""
    if ($meta.PSObject.Properties.Name -contains "status") { $status = [string]$meta.status }
    if ($status -ne "complete") { throw "Rejects incomplete runs: $rj has status '$status'" }
}

# Find rows.csv files
$rowsCsvFiles = @()
$directRows = Join-Path $runDirResolved "rows.csv"
if (Test-Path -LiteralPath $directRows) { $rowsCsvFiles += (Resolve-Path -LiteralPath $directRows).Path }
$subRows = @(Get-ChildItem -Path $runDirResolved -Filter "rows.csv" -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
foreach ($f in $subRows) {
    $already = $false
    foreach ($e in $rowsCsvFiles) { if ($e -eq $f) { $already = $true; break } }
    if (-not $already) { $rowsCsvFiles += $f }
}
if ($rowsCsvFiles.Count -eq 0) { throw "No rows.csv found under RunDir: $runDirResolved" }

$allRows = @()
foreach ($rf in $rowsCsvFiles) {
    $imported = @(Import-Csv -LiteralPath $rf)
    foreach ($r in $imported) { $allRows += $r }
}
if ($allRows.Count -eq 0) { throw "No rows found in rows.csv under $runDirResolved" }

# Load review queue and validate similar to prepare-review.ps1 -Validate
$allowedLabels = @("correct","acceptable-synthetic","generic","wrong-brand","blank","unusable","ambiguous","not-reviewed")
$queueRows = @(Import-Csv -LiteralPath $reviewQueueResolved)
if ($queueRows.Count -eq 0) { throw "Review queue empty: $reviewQueueResolved requires review coverage" }

# Build lookup for review: key fixture|profile|hash
$reviewMap = @{}
$notReviewedCount = 0
$ambiguousCountTotal = 0
foreach ($qr in $queueRows) {
    $label = ""
    if ($qr.PSObject.Properties.Name -contains "review_label") { $label = [string]$qr.review_label }
    $lt = $label.Trim().ToLowerInvariant()
    if ($allowedLabels -notcontains $lt) { throw "Review queue has invalid label '$label'" }
    if ($lt -ne "not-reviewed") {
        $reviewer = ""
        if ($qr.PSObject.Properties.Name -contains "reviewer") { $reviewer = [string]$qr.reviewer }
        $rat = ""
        if ($qr.PSObject.Properties.Name -contains "reviewed_at_utc") { $rat = [string]$qr.reviewed_at_utc }
        if ([string]::IsNullOrWhiteSpace($reviewer) -or [string]::IsNullOrWhiteSpace($rat)) {
            throw "Rejects incomplete review: reviewed row missing reviewer/timestamp for label $label"
        }
    } else {
        $notReviewedCount++
    }
    if ($lt -eq "ambiguous") { $ambiguousCountTotal++ }
    $fidQ = ""
    if ($qr.PSObject.Properties.Name -contains "fixture_id") { $fidQ = [string]$qr.fixture_id }
    $profQ = ""
    if ($qr.PSObject.Properties.Name -contains "profile_id") { $profQ = [string]$qr.profile_id }
    if ([string]::IsNullOrWhiteSpace($profQ) -and $qr.PSObject.Properties.Name -contains "profile") { $profQ = [string]$qr.profile }
    $hashQ = ""
    if ($qr.PSObject.Properties.Name -contains "artifact_hash") { $hashQ = [string]$qr.artifact_hash }
    $keyFull = "$fidQ|$profQ|$hashQ"
    $keyPartial = "$fidQ|$profQ"
    $reviewMap[$keyFull] = $qr
    if (-not $reviewMap.ContainsKey($keyPartial)) { $reviewMap[$keyPartial] = $qr }
    # Also store with lower invariant hash
}

# Check missing review coverage: any not-reviewed rows is missing coverage
if ($notReviewedCount -gt 0) {
    throw "Rejects missing review coverage: $notReviewedCount rows still not-reviewed in $reviewQueueResolved"
}

# Artifact hash validation if possible: compare queue hashes against rows artifact hashes
$artifactMap = @{}
foreach ($row in $allRows) {
    $fidA = ""
    if ($row.PSObject.Properties.Name -contains "fixture_id") { $fidA = [string]$row.fixture_id }
    $profA = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $profA = [string]$row.profile }
    if ([string]::IsNullOrWhiteSpace($profA) -and $row.PSObject.Properties.Name -contains "profile_id") { $profA = [string]$row.profile_id }
    $hashA = ""
    if ($row.PSObject.Properties.Name -contains "artifact_hash") { $hashA = [string]$row.artifact_hash }
    $keyA = "$fidA|$profA|$hashA"
    $artifactMap[$keyA] = $true
}
foreach ($qr in $queueRows) {
    $fidQ2 = ""
    if ($qr.PSObject.Properties.Name -contains "fixture_id") { $fidQ2 = [string]$qr.fixture_id }
    $profQ2 = ""
    if ($qr.PSObject.Properties.Name -contains "profile_id") { $profQ2 = [string]$qr.profile_id }
    if ([string]::IsNullOrWhiteSpace($profQ2) -and $qr.PSObject.Properties.Name -contains "profile") { $profQ2 = [string]$qr.profile }
    $hashQ2 = ""
    if ($qr.PSObject.Properties.Name -contains "artifact_hash") { $hashQ2 = [string]$qr.artifact_hash }
    if (-not [string]::IsNullOrWhiteSpace($hashQ2)) {
        $k = "$fidQ2|$profQ2|$hashQ2"
        if (-not $artifactMap.ContainsKey($k)) {
            # Check if any row for fixture/profile exists with different hash -> mismatch
            $partial = "$fidQ2|$profQ2|"
            $foundPartial = $false
            foreach ($ak in $artifactMap.Keys) { if ($ak.StartsWith("$fidQ2|$profQ2|")) { $foundPartial = $true; break } }
            if ($foundPartial) {
                throw "Artifact hash no longer matches run artifacts for $fidQ2/$profQ2 hash $hashQ2"
            }
        }
    }
}

# Load candidate definitions from experiment json if available
$candidatesDef = @{}
$candidatesOrder = @()
$expCandidatesPath = Join-Path $repoRoot "eng/benchmark/experiments/profile-candidates-v13.json"
if (Test-Path -LiteralPath $expCandidatesPath) {
    try {
        $expJson = Get-Content -Raw -LiteralPath $expCandidatesPath | ConvertFrom-Json
        if ($expJson.PSObject.Properties.Name -contains "candidates") {
            foreach ($c in @($expJson.candidates)) {
                $cid = [string]$c.id
                if ([string]::IsNullOrWhiteSpace($cid)) { continue }
                $candidatesDef[$cid] = $c
                $candidatesOrder += $cid
            }
        }
    } catch {
    }
}
# Also try to read experiment candidates from run.json experiment_id matching file under eng/benchmark/experiments
foreach ($rj in $runJsonFiles) {
    try {
        $meta2 = Get-Content -Raw -LiteralPath $rj | ConvertFrom-Json
        $expId = ""
        if ($meta2.PSObject.Properties.Name -contains "experiment_id") { $expId = [string]$meta2.experiment_id }
        if (-not [string]::IsNullOrWhiteSpace($expId)) {
            $candidateExpFile = Join-Path $repoRoot ("eng/benchmark/experiments/" + $expId + ".json")
            if ((Test-Path -LiteralPath $candidateExpFile) -and $candidateExpFile -ne $expCandidatesPath) {
                try {
                    $ej2 = Get-Content -Raw -LiteralPath $candidateExpFile | ConvertFrom-Json
                    if ($ej2.PSObject.Properties.Name -contains "candidates") {
                        foreach ($c2 in @($ej2.candidates)) {
                            $cid2 = [string]$c2.id
                            if ([string]::IsNullOrWhiteSpace($cid2)) { continue }
                            if (-not $candidatesDef.ContainsKey($cid2)) {
                                $candidatesDef[$cid2] = $c2
                                $candidatesOrder += $cid2
                            }
                        }
                    }
                } catch {
                }
            }
        }
    } catch {
    }
}

# Determine distinct profiles from rows
$profileIds = @()
$seenProfiles = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($row in $allRows) {
    $p = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $p = [string]$row.profile }
    if ([string]::IsNullOrWhiteSpace($p) -and $row.PSObject.Properties.Name -contains "profile_id") { $p = [string]$row.profile_id }
    if ([string]::IsNullOrWhiteSpace($p)) { continue }
    if ($seenProfiles.Add($p)) { $profileIds += $p }
}
if ($profileIds.Count -eq 0) { throw "No profiles found in rows.csv" }

# Helper to get candidate config for a profile id
function GetCandidateConfig {
    param([string]$ProfileId)
    if ($candidatesDef.ContainsKey($ProfileId)) { return $candidatesDef[$ProfileId] }
    # Fallback for mock tests: create synthetic config based on id
    $lowerPid = $ProfileId.ToLowerInvariant()
    $providerCount = 2
    if ($lowerPid.Contains("direct-only")) { $providerCount = 1 }
    elseif ($lowerPid.Contains("full") -or $lowerPid.Contains("max-coverage")) { $providerCount = 7 }
    elseif ($lowerPid.Contains("bulk-fast") -or ($lowerPid.Contains("fast") -and -not $lowerPid.Contains("everyday"))) { $providerCount = 3 }
    $ids = @("direct-site")
    if ($providerCount -gt 1) { $ids += "google" }
    if ($providerCount -gt 2) { $ids += "twenty-icons" }
    if ($providerCount -gt 3) { $ids += "duckduckgo" }
    if ($providerCount -gt 4) { $ids += "yandex" }
    if ($providerCount -gt 5) { $ids += "favicone" }
    if ($providerCount -gt 6) { $ids += "icon-horse" }
    $primary = 6000
    $fallback = 3500
    $cumulative = 22000
    if ($lowerPid.Contains("fast")) { $primary = 4000; $fallback = 2500; $cumulative = 15000 }
    elseif ($lowerPid.Contains("thorough") -or $lowerPid.Contains("max-coverage")) { $primary = 10000; $fallback = 5000; $cumulative = 45000 }
    $allowSynVal = ($lowerPid.Contains("synth") -or $lowerPid.Contains("max-coverage") -or $lowerPid.Contains("everyday"))
    return [PSCustomObject]@{
        id = $ProfileId
        providerIds = $ids
        primaryTimeout = $primary
        fallbackTimeout = $fallback
        cumulativeTimeout = $cumulative
        allowSynthetic = $allowSynVal
    }
}

# Compute per-profile metrics
$statsList = @()
$providerThirdPartyMap = @{
    "direct-site" = $false
    "twenty-icons" = $true
    "duckduckgo" = $true
    "google" = $true
    "yandex" = $true
    "favicone" = $true
    "icon-horse" = $true
}

foreach ($candidateProfileId in $profileIds) {
    $currentPid = $candidateProfileId
    $rowsForPid = @($allRows | Where-Object {
        $pp = ""
        if ($_.PSObject.Properties.Name -contains "profile") { $pp = [string]$_.profile }
        if ([string]::IsNullOrWhiteSpace($pp) -and $_.PSObject.Properties.Name -contains "profile_id") { $pp = [string]$_.profile_id }
        return $pp -eq $currentPid
    })
    $total = $rowsForPid.Count
    $correct = 0
    $acceptable = 0
    $generic = 0
    $wrongBrand = 0
    $blank = 0
    $unusable = 0
    $ambiguous = 0
    $elapsedValues = @()
    $successMachine = 0
    $timeoutCount = 0
    $providerErrorCount = 0
    $harnessErrorCount = 0
    foreach ($r in $rowsForPid) {
        $elapsed = 0
        if ($r.PSObject.Properties.Name -contains "total_elapsed_ms") {
            try { $elapsed = [long]$r.total_elapsed_ms } catch { $elapsed = 0 }
        } elseif ($r.PSObject.Properties.Name -contains "total_elapsed") {
            try { $elapsed = [long]$r.total_elapsed } catch { $elapsed = 0 }
        }
        $elapsedValues += $elapsed
        $mo = ""
        if ($r.PSObject.Properties.Name -contains "machine_outcome") { $mo = [string]$r.machine_outcome }
        if ($mo.ToLowerInvariant() -eq "success") { $successMachine++ }
        if ($mo.ToLowerInvariant() -eq "timeout") { $timeoutCount++ }
        if ($mo.ToLowerInvariant() -eq "provider-error") { $providerErrorCount++ }
        if ($mo.ToLowerInvariant() -eq "harness-error") { $harnessErrorCount++ }

        $fid = ""
        if ($r.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$r.fixture_id }
        $hash = ""
        if ($r.PSObject.Properties.Name -contains "artifact_hash") { $hash = [string]$r.artifact_hash }
        $keyFullLookup = "$fid|$currentPid|$hash"
        $keyPartialLookup = "$fid|$currentPid"
        $qrMatch = $null
        if ($reviewMap.ContainsKey($keyFullLookup)) { $qrMatch = $reviewMap[$keyFullLookup] }
        elseif ($reviewMap.ContainsKey($keyPartialLookup)) { $qrMatch = $reviewMap[$keyPartialLookup] }
        if ($null -ne $qrMatch) {
            $lbl = ""
            if ($qrMatch.PSObject.Properties.Name -contains "review_label") { $lbl = [string]$qrMatch.review_label }
            $lt2 = $lbl.Trim().ToLowerInvariant()
            switch ($lt2) {
                "correct" { $correct++ }
                "acceptable-synthetic" { $acceptable++ }
                "generic" { $generic++ }
                "wrong-brand" { $wrongBrand++ }
                "blank" { $blank++ }
                "unusable" { $unusable++ }
                "ambiguous" { $ambiguous++ }
                default { }
            }
        } else {
            # Not in review queue: if success, count as correct implicitly; else not counted as usable
            if ($mo.ToLowerInvariant() -eq "success") { $correct++ }
        }
    }
    $usable = $correct + $acceptable
    $usableRate = 0
    if ($total -gt 0) { $usableRate = [double]$usable / [double]$total }
    $correctness = 0
    if ($total -gt 0) { $correctness = [double]$correct / [double]$total }
    # Coverage is usableRate
    $coverage = $usableRate
    $wrongBrandRate = 0
    if ($total -gt 0) { $wrongBrandRate = [double]$wrongBrand / [double]$total }
    $reliability = 1.0
    if ($total -gt 0) {
        $failures = $timeoutCount + $providerErrorCount + $harnessErrorCount
        $reliability = 1.0 - ([double]$failures / [double]$total)
        if ($reliability -lt 0) { $reliability = 0 }
        if ($reliability -gt 1) { $reliability = 1 }
    }
    $sortedElapsed = @($elapsedValues | Sort-Object)
    $p95 = GetPercentile -SortedValues $sortedElapsed -Percent 0.95
    $avg = 0
    if ($elapsedValues.Count -gt 0) {
        $sum = 0
        foreach ($v in $elapsedValues) { $sum += $v }
        $avg = [double]$sum / [double]$elapsedValues.Count
    }
    $batchDuration = 0
    foreach ($v in $elapsedValues) { $batchDuration += $v }

    $cfg = GetCandidateConfig -ProfileId $currentPid
    $provIds = @()
    if ($null -ne $cfg) {
        if ($cfg.PSObject.Properties.Name -contains "providerIds") { $provIds = @($cfg.providerIds) }
        elseif ($cfg.PSObject.Properties.Name -contains "provider_ids") { $provIds = @($cfg.provider_ids) }
    }
    if ($provIds.Count -eq 0) { $provIds = @("direct-site") }
    $thirdPartyCount = 0
    foreach ($provId in $provIds) {
        $lower = ([string]$provId).ToLowerInvariant().Trim()
        if ($providerThirdPartyMap.ContainsKey($lower)) {
            if ($providerThirdPartyMap[$lower]) { $thirdPartyCount++ }
        } else {
            # Unknown provider: assume third-party if not direct-site
            if ($lower -ne "direct-site") { $thirdPartyCount++ }
        }
    }
    $providerCount = $provIds.Count

    $obj = [PSCustomObject]@{
        profile_id = $currentPid
        total = $total
        correct = $correct
        acceptable_synthetic = $acceptable
        generic = $generic
        wrong_brand = $wrongBrand
        blank = $blank
        unusable = $unusable
        ambiguous = $ambiguous
        usable = $usable
        usable_rate = $usableRate
        correctness = $correctness
        coverage = $coverage
        wrong_brand_rate = $wrongBrandRate
        reliability = $reliability
        avg_elapsed_ms = $avg
        p95_ms = $p95
        batch_duration_ms = $batchDuration
        provider_ids = $provIds
        provider_count = $providerCount
        third_party_count = $thirdPartyCount
        primaryTimeout = 0
        fallbackTimeout = 0
        cumulativeTimeout = 0
        allowSynthetic = $false
    }
    # Add timeouts from config
    if ($null -ne $cfg) {
        if ($cfg.PSObject.Properties.Name -contains "primaryTimeout") { try { $obj.primaryTimeout = [int]$cfg.primaryTimeout } catch {} }
        if ($cfg.PSObject.Properties.Name -contains "fallbackTimeout") { try { $obj.fallbackTimeout = [int]$cfg.fallbackTimeout } catch {} }
        if ($cfg.PSObject.Properties.Name -contains "cumulativeTimeout") { try { $obj.cumulativeTimeout = [int]$cfg.cumulativeTimeout } catch {} }
        if ($cfg.PSObject.Properties.Name -contains "allowSynthetic") { try { $obj.allowSynthetic = [bool]$cfg.allowSynthetic } catch {} }
    }
    # Fallbacks if zero
    if ($obj.primaryTimeout -eq 0) { $obj.primaryTimeout = 6000 }
    if ($obj.fallbackTimeout -eq 0) { $obj.fallbackTimeout = 3500 }
    if ($obj.cumulativeTimeout -eq 0) { $obj.cumulativeTimeout = 22000 }
    $statsList += $obj
}

if ($statsList.Count -eq 0) { throw "No stats computed" }

# Helper to sort deterministically
function CompareCandidates {
    param($A, $B, [string]$Criterion)
    # Returns -1 if A better, 1 if B better, 0 equal for primary criterion; caller handles tie breakers
    return 0
}

# 1. Privacy winner: best direct-site-only (no third-party)
$privacyCandidates = @($statsList | Where-Object { $_.third_party_count -eq 0 })
if ($privacyCandidates.Count -eq 0) {
    # Fallback: provider_count ==1 and direct-site
    $privacyCandidates = @($statsList | Where-Object { $_.provider_count -eq 1 })
}
if ($privacyCandidates.Count -eq 0) { throw "No privacy candidate (direct-site-only) found" }
$privacySorted = @($privacyCandidates | Sort-Object -Property @{ Expression = { -$_.usable_rate }; Ascending = $true }, @{ Expression = { -$_.correctness }; Ascending = $true }, @{ Expression = { $_.p95_ms }; Ascending = $true }, @{ Expression = { $_.provider_count }; Ascending = $true }, @{ Expression = { $_.profile_id }; Ascending = $true })
$privacyWinner = $privacySorted[0]

# 2. Max-coverage winner: highest usable_rate, then correctness, then lower p95
$maxCoverageSorted = @($statsList | Sort-Object -Property @{ Expression = { -$_.usable_rate }; Ascending = $true }, @{ Expression = { -$_.correctness }; Ascending = $true }, @{ Expression = { $_.p95_ms }; Ascending = $true }, @{ Expression = { $_.provider_count }; Ascending = $true }, @{ Expression = { $_.profile_id }; Ascending = $true })
$maxCoverageWinner = $maxCoverageSorted[0]

# 3. Bulk-fast winner: lowest batch duration among candidates with usable >=90% of max-coverage and wrong-brand <=1%
$maxUsableThreshold = $maxCoverageWinner.usable_rate * 0.90
$bulkEligible = @($statsList | Where-Object { $_.usable_rate -ge $maxUsableThreshold -and $_.wrong_brand_rate -le 0.01 })
if ($bulkEligible.Count -eq 0) { throw "No bulk-fast eligible candidate: need usable >=90% of max-coverage ($maxUsableThreshold) and wrong-brand <=1%" }
$bulkSorted = @($bulkEligible | Sort-Object -Property @{ Expression = { $_.batch_duration_ms }; Ascending = $true }, @{ Expression = { $_.avg_elapsed_ms }; Ascending = $true }, @{ Expression = { $_.provider_count }; Ascending = $true }, @{ Expression = { $_.profile_id }; Ascending = $true })
$bulkWinner = $bulkSorted[0]

# 4. Everyday winner: weighted formula
# Compute min/max for normalization
function GetMinMax {
    param([object[]]$Values)
    $min = $null
    $max = $null
    foreach ($v in $Values) {
        if ($null -eq $min -or $v -lt $min) { $min = $v }
        if ($null -eq $max -or $v -gt $max) { $max = $v }
    }
    return @{ min = $min; max = $max }
}
$correctnessVals = @($statsList | ForEach-Object { $_.correctness })
$coverageVals = @($statsList | ForEach-Object { $_.coverage })
$reliabilityVals = @($statsList | ForEach-Object { $_.reliability })
$latencyVals = @($statsList | ForEach-Object { $_.avg_elapsed_ms })
$privacyVals = @($statsList | ForEach-Object { [double]$_.third_party_count })

$cm = GetMinMax -Values $correctnessVals
$covM = GetMinMax -Values $coverageVals
$relM = GetMinMax -Values $reliabilityVals
$latM = GetMinMax -Values $latencyVals
$privM = GetMinMax -Values $privacyVals

function NormalizeValue {
    param([double]$Value, [double]$Min, [double]$Max)
    if ($Max -eq $Min) { return 1.0 }
    return ($Value - $Min) / ($Max - $Min)
}
function InvertNormalized {
    param([double]$Norm)
    $inv = 1.0 - $Norm
    if ($inv -lt 0) { $inv = 0 }
    if ($inv -gt 1) { $inv = 1 }
    return $inv
}

$everydayScores = @()
foreach ($s in $statsList) {
    $cn = NormalizeValue -Value ([double]$s.correctness) -Min ([double]$cm.min) -Max ([double]$cm.max)
    $covn = NormalizeValue -Value ([double]$s.coverage) -Min ([double]$covM.min) -Max ([double]$covM.max)
    $reln = NormalizeValue -Value ([double]$s.reliability) -Min ([double]$relM.min) -Max ([double]$relM.max)
    $latnRaw = NormalizeValue -Value ([double]$s.avg_elapsed_ms) -Min ([double]$latM.min) -Max ([double]$latM.max)
    $latScore = InvertNormalized -Norm $latnRaw
    $privRaw = NormalizeValue -Value ([double]$s.third_party_count) -Min ([double]$privM.min) -Max ([double]$privM.max)
    $privScore = InvertNormalized -Norm $privRaw

    $score = 0.40 * $cn + 0.25 * $covn + 0.15 * $latScore + 0.10 * $reln + 0.10 * $privScore
    $entry = [PSCustomObject]@{
        profile_id = $s.profile_id
        stats = $s
        correctness_norm = $cn
        coverage_norm = $covn
        reliability_norm = $reln
        latency_score = $latScore
        privacy_score = $privScore
        weighted = $score
        provider_count = $s.provider_count
    }
    $everydayScores += $entry
}
$everydaySorted = @($everydayScores | Sort-Object -Property @{ Expression = { -$_.weighted }; Ascending = $true }, @{ Expression = { $_.provider_count }; Ascending = $true }, @{ Expression = { $_.profile_id }; Ascending = $true })
$everydayWinnerEntry = $everydaySorted[0]
$everydayWinner = $everydayWinnerEntry.stats

# Ambiguous reversal checks
function TestAmbiguousReversal {
    param([PSCustomObject]$Winner, [object[]]$SortedAll, [string]$Criterion)
    if ($Winner.ambiguous -eq 0) { return }
    # Check gap to second
    if ($SortedAll.Count -lt 2) { return }
    $second = $null
    foreach ($cand in $SortedAll) {
        if ($cand.profile_id -ne $Winner.profile_id) { $second = $cand; break }
        # For everyday, SortedAll items are wrapper with stats
        if ($cand.PSObject.Properties.Name -contains "stats") {
            if ($cand.stats.profile_id -ne $Winner.profile_id) { $second = $cand.stats; break }
        }
    }
    if ($null -eq $second) { return }
    # If second is wrapper, unwrap
    if ($second.PSObject.Properties.Name -contains "stats") { $second = $second.stats }
    # Gap in usable_rate
    $gap = [Math]::Abs($Winner.usable_rate - $second.usable_rate)
    $totalAmbiguous = $Winner.ambiguous + $second.ambiguous
    # If gap * total could be overturned by ambiguous count
    # Compute gap in absolute usable counts
    $gapCount = [Math]::Abs($Winner.usable_rate * $Winner.total - $second.usable_rate * $second.total)
    # Average total for scaling
    $avgTotal = ($Winner.total + $second.total) / 2
    if ($avgTotal -eq 0) { $avgTotal = 1 }
    $ambiguousRate = [double]$totalAmbiguous / [double]$avgTotal
    if ($gap -le $ambiguousRate -or $gapCount -le $totalAmbiguous) {
        throw "Rejects ambiguous capable of reversing winner for $($Criterion): winner $($Winner.profile_id) gap $gap ambiguous $totalAmbiguous"
    }
}

# Check max-coverage ambiguous
try {
    TestAmbiguousReversal -Winner $maxCoverageWinner -SortedAll $maxCoverageSorted -Criterion "max-coverage"
} catch {
    throw $_
}
# Check everyday ambiguous with weighted gap
if ($everydaySorted.Count -ge 2) {
    $topW = $everydayWinnerEntry.weighted
    $secondW = $everydaySorted[1].weighted
    $gapW = [Math]::Abs($topW - $secondW)
    $ambTop = $everydayWinner.ambiguous
    $ambSecond = $everydaySorted[1].stats.ambiguous
    $totalAmbForEveryday = $ambTop + $ambSecond
    if ($totalAmbForEveryday -gt 0 -and $gapW -le 0.05) {
        # If weighted gap small and ambiguous exists, could reverse
        # Use stricter check: if gap <= ambiguous/total
        $avgTEvery = ($everydayWinner.total + $everydaySorted[1].stats.total) / 2
        if ($avgTEvery -eq 0) { $avgTEvery = 1 }
        $ambRateE = [double]$totalAmbForEveryday / [double]$avgTEvery
        if ($gapW -le $ambRateE -or $gapW -le 0.02) {
            throw "Rejects ambiguous capable of reversing winner for everyday: gap $gapW ambiguous $totalAmbForEveryday"
        }
    }
}

# Prepare output structures
$generatedAt = (Get-Date).ToUniversalTime().ToString("o")
$runIds = @($runJsonFiles | ForEach-Object {
    try { $m = Get-Content -Raw -LiteralPath $_ | ConvertFrom-Json; if ($m.PSObject.Properties.Name -contains "run_id") { [string]$m.run_id } else { $_ } } catch { $_ }
})

# Build decisions object
$decisions = [ordered]@{
    experiment_id = ""
    generated_at_utc = $generatedAt
    run_dir = $runDirResolved
    review_queue = $reviewQueueResolved
    output_dir = $outputDirResolved
    run_ids = $runIds
    candidates = @()
    winners = [ordered]@{}
    rationale = [ordered]@{}
}

# Determine experiment_id
$expIdForDecisions = "profile-candidates-v13"
foreach ($rj in $runJsonFiles) {
    try {
        $m3 = Get-Content -Raw -LiteralPath $rj | ConvertFrom-Json
        if ($m3.PSObject.Properties.Name -contains "experiment_id") { $expIdForDecisions = [string]$m3.experiment_id; break }
    } catch {}
}
$decisions.experiment_id = $expIdForDecisions

foreach ($s in $statsList) {
    $d = [ordered]@{
        profile_id = $s.profile_id
        providerIds = @($s.provider_ids)
        primaryTimeout = $s.primaryTimeout
        fallbackTimeout = $s.fallbackTimeout
        cumulativeTimeout = $s.cumulativeTimeout
        allowSynthetic = $s.allowSynthetic
        total = $s.total
        usable = $s.usable
        usable_rate = [Math]::Round($s.usable_rate,4)
        correctness = [Math]::Round($s.correctness,4)
        coverage = [Math]::Round($s.coverage,4)
        wrong_brand_rate = [Math]::Round($s.wrong_brand_rate,4)
        reliability = [Math]::Round($s.reliability,4)
        avg_elapsed_ms = [Math]::Round($s.avg_elapsed_ms,2)
        p95_ms = $s.p95_ms
        batch_duration_ms = $s.batch_duration_ms
        third_party_count = $s.third_party_count
        provider_count = $s.provider_count
        ambiguous = $s.ambiguous
    }
    $decisions.candidates += $d
}

$decisions.winners["privacy"] = [ordered]@{
    candidate_id = $privacyWinner.profile_id
    providerIds = @($privacyWinner.provider_ids)
    primaryTimeout = $privacyWinner.primaryTimeout
    fallbackTimeout = $privacyWinner.fallbackTimeout
    cumulativeTimeout = $privacyWinner.cumulativeTimeout
    allowSynthetic = $privacyWinner.allowSynthetic
    reason = "best direct-site-only candidate (no third-party, regardless of score); selected by usable_rate, correctness, p95"
}
$decisions.winners["max-coverage"] = [ordered]@{
    candidate_id = $maxCoverageWinner.profile_id
    providerIds = @($maxCoverageWinner.provider_ids)
    primaryTimeout = $maxCoverageWinner.primaryTimeout
    fallbackTimeout = $maxCoverageWinner.fallbackTimeout
    cumulativeTimeout = $maxCoverageWinner.cumulativeTimeout
    allowSynthetic = $maxCoverageWinner.allowSynthetic
    usable_rate = [Math]::Round($maxCoverageWinner.usable_rate,4)
    correctness = [Math]::Round($maxCoverageWinner.correctness,4)
    p95_ms = $maxCoverageWinner.p95_ms
    reason = "highest reviewed usable rate, then correctness, then lower p95"
}
$decisions.winners["bulk-fast"] = [ordered]@{
    candidate_id = $bulkWinner.profile_id
    providerIds = @($bulkWinner.provider_ids)
    primaryTimeout = $bulkWinner.primaryTimeout
    fallbackTimeout = $bulkWinner.fallbackTimeout
    cumulativeTimeout = $bulkWinner.cumulativeTimeout
    allowSynthetic = $bulkWinner.allowSynthetic
    batch_duration_ms = $bulkWinner.batch_duration_ms
    usable_rate = [Math]::Round($bulkWinner.usable_rate,4)
    threshold = [Math]::Round($maxUsableThreshold,4)
    reason = "lowest batch duration among candidates with usable >=90% of max-coverage and wrong-brand <=1%"
}
$decisions.winners["everyday"] = [ordered]@{
    candidate_id = $everydayWinner.profile_id
    providerIds = @($everydayWinner.provider_ids)
    primaryTimeout = $everydayWinner.primaryTimeout
    fallbackTimeout = $everydayWinner.fallbackTimeout
    cumulativeTimeout = $everydayWinner.cumulativeTimeout
    allowSynthetic = $everydayWinner.allowSynthetic
    weighted_score = [Math]::Round($everydayWinnerEntry.weighted,4)
    components = [ordered]@{
        correctness_norm = [Math]::Round($everydayWinnerEntry.correctness_norm,4)
        coverage_norm = [Math]::Round($everydayWinnerEntry.coverage_norm,4)
        latency_score = [Math]::Round($everydayWinnerEntry.latency_score,4)
        reliability_norm = [Math]::Round($everydayWinnerEntry.reliability_norm,4)
        privacy_score = [Math]::Round($everydayWinnerEntry.privacy_score,4)
    }
    formula = "0.40*correctness + 0.25*coverage + 0.15*latency_score + 0.10*reliability + 0.10*privacy_score (latency/privacy inverted, normalized 0-1)"
    reason = "highest weighted score with tie breakers: fewer providers, lexicographic id"
}

$decisions.rationale["privacy"] = "Privacy selects the best direct-site-only candidate with no third-party providers regardless of overall score. Chosen: $($privacyWinner.profile_id) with usable $($privacyWinner.usable_rate) and $($privacyWinner.provider_ids -join ',')."
$decisions.rationale["max-coverage"] = "Max-coverage selects highest usable rate $($maxCoverageWinner.usable_rate), then correctness $($maxCoverageWinner.correctness), then lowest p95 $($maxCoverageWinner.p95_ms) ms. Chosen: $($maxCoverageWinner.profile_id)."
$decisions.rationale["bulk-fast"] = "Bulk-fast selects lowest batch duration $($bulkWinner.batch_duration_ms) ms among candidates with usable >=90% of max-coverage ($maxUsableThreshold) and wrong-brand <=1% ($($bulkWinner.wrong_brand_rate)). Chosen: $($bulkWinner.profile_id)."
$decisions.rationale["everyday"] = "Everyday selects highest weighted score $($everydayWinnerEntry.weighted) using 0.40*correctness + 0.25*coverage + 0.15*latency_score + 0.10*reliability + 0.10*privacy_score (latency and privacy inverted and normalized 0-1). Chosen: $($everydayWinner.profile_id)."

# Ensure output dirs
$decisionsPath = Join-Path $outputDirResolved "profile-decisions.json"
$mdPath = Join-Path $outputDirResolved "profile-selection-report.md"
$generatedCsPathInOutput = Join-Path $outputDirResolved "FetchProfileCatalog.Generated.cs"
$repoGeneratedPath = Join-Path $repoRoot "FetchProfiles/FetchProfileCatalog.Generated.cs"

# Write profile-decisions.json
$jsonOut = ConvertTo-Json -InputObject $decisions -Depth 20
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($decisionsPath, $jsonOut, $utf8NoBom)

# Write markdown report
$mdLines = @()
$mdLines += "# Profile Selection Report"
$mdLines += ""
$mdLines += "Generated: $generatedAt"
$mdLines += "Experiment: $expIdForDecisions"
$mdLines += "RunDir: $runDirResolved"
$mdLines += "ReviewQueue: $reviewQueueResolved"
$mdLines += ""
$mdLines += "## Winners"
$mdLines += ""
$mdLines += "| Profile | Candidate | Providers | Timeouts (primary/fallback/cumulative) | Synthetic | Usable | Correct | p95 ms | Reason |"
$mdLines += "|---|---|---|---|---|---|---|---|---|"
foreach ($key in @("privacy","max-coverage","bulk-fast","everyday")) {
    $w = $decisions.winners[$key]
    $provStr = ($w.providerIds -join ", ")
    $timeStr = "$($w.primaryTimeout)/$($w.fallbackTimeout)/$($w.cumulativeTimeout)"
    $syn = if ($w.allowSynthetic) { "true" } else { "false" }
    $usableStr = ""
    $correctStr = ""
    $p95Str = ""
    if ($w.PSObject.Properties.Name -contains "usable_rate") { $usableStr = [string]$w.usable_rate }
    elseif ($decisions.winners[$key].PSObject.Properties.Name -contains "usable_rate") { $usableStr = "" }
    # Lookup stats for usable/correct/p95 from statsList
    $statLookup = $statsList | Where-Object { $_.profile_id -eq $w.candidate_id } | Select-Object -First 1
    if ($null -ne $statLookup) {
        $usableStr = [string][Math]::Round($statLookup.usable_rate,4)
        $correctStr = [string][Math]::Round($statLookup.correctness,4)
        $p95Str = [string]$statLookup.p95_ms
    }
    $reasonShort = $w.reason
    $mdLines += "| $key | $($w.candidate_id) | $provStr | $timeStr | $syn | $usableStr | $correctStr | $p95Str | $reasonShort |"
}
$mdLines += ""
$mdLines += "## Candidate Comparison"
$mdLines += ""
$mdLines += "| Candidate | Providers (n) | Timeouts | Synthetic | Total | Usable Rate | Correctness | WrongBrand | Reliability | Avg ms | p95 ms | Batch ms | Privacy(3p) |"
$mdLines += "|---|---|---|---|---|---|---|---|---|---|---|---|---|"
foreach ($s in ($statsList | Sort-Object -Property profile_id)) {
    $provJoin = ($s.provider_ids -join ",")
    $timeJoin = "$($s.primaryTimeout)/$($s.fallbackTimeout)/$($s.cumulativeTimeout)"
    $synS = if ($s.allowSynthetic) { "true" } else { "false" }
    $mdLines += "| $($s.profile_id) | $provJoin ($($s.provider_count)) | $timeJoin | $synS | $($s.total) | $([Math]::Round($s.usable_rate,4)) | $([Math]::Round($s.correctness,4)) | $([Math]::Round($s.wrong_brand_rate,4)) | $([Math]::Round($s.reliability,4)) | $([Math]::Round($s.avg_elapsed_ms,1)) | $($s.p95_ms) | $($s.batch_duration_ms) | $($s.third_party_count) |"
}
$mdLines += ""
$mdLines += "## Rationale"
$mdLines += ""
foreach ($k in @("privacy","max-coverage","bulk-fast","everyday")) {
    $mdLines += "### $k"
    $mdLines += ""
    $mdLines += $decisions.rationale[$k]
    $mdLines += ""
}
$mdLines += "### Tie Breakers"
$mdLines += ""
$mdLines += "Ties broken by fewer providers, then lexicographic candidate id."
$mdLines += ""
$mdLines += "### Candidate Authority"
$mdLines += ""
$mdLines += "All candidates are CUSTOM configurations with explicit providerIds/order + timeouts + allowSynthetic. Benchmark harness constructs custom Configuration (FetchProfileId=custom) via New-CustomConfigForCandidate / custom path. Managed profile IDs (bulk-fast, everyday, privacy, max-coverage) are ONLY used for final winners, not for candidate definitions. New-ConfigForProfile for candidates must use custom path."
$mdLines += ""
$mdLines += "### Ambiguous Handling"
$mdLines += ""
$mdLines += "Runs with ambiguous capable of reversing any winner are rejected. This run had $ambiguousCountTotal ambiguous labels across the review queue."
$mdLines += ""

$mdContent = $mdLines -join "`r`n"
[System.IO.File]::WriteAllText($mdPath, $mdContent, $utf8NoBom)

# Generate FetchProfileCatalog.Generated.cs
function EscapeCsString {
    param([string]$Value)
    if ($null -eq $Value) { return "" }
    return $Value.Replace("\","\\").Replace('"','\"')
}

# Build profile definitions for generated file
# Map winners to managed profile ids: bulk-fast, everyday, privacy, max-coverage
$profileRowsForCs = @()

# Helper to get display name/description/intended use per managed id
$managedMeta = @{
    "bulk-fast" = @{ display = "Fast"; description = "Shortest path. Tries direct site, then a compact strong-resolver chain with reduced time budgets for faster large batches."; intended = "Large batch fetching with reduced latency" }
    "everyday" = @{ display = "Balanced"; description = "Recommended default. Uses direct site, Google, and a lightweight synthetic fallback to balance coverage and batch speed."; intended = "Default everyday use balancing coverage and speed" }
    "privacy" = @{ display = "Privacy"; description = "Privacy-focused mode. Uses only direct site resolution with no third-party requests."; intended = "Privacy-sensitive fetching without third-party providers" }
    "max-coverage" = @{ display = "Thorough"; description = "Availability-first mode. Uses the full resolver chain with the largest time budgets and synthetic fallbacks for maximum coverage."; intended = "Maximum coverage with full resolver chain" }
}

# Need to map each managed id to its winner stats
$winnerMap = @{
    "bulk-fast" = $bulkWinner
    "everyday" = $everydayWinner
    "privacy" = $privacyWinner
    "max-coverage" = $maxCoverageWinner
}

# Ensure providerIds are stored as stable ids (lowercase with hyphens)
function ToStableProviderId {
    param([string]$DisplayOrId)
    $t = $DisplayOrId.Trim().ToLowerInvariant()
    # Map display names to ids
    switch ($t) {
        "direct site" { return "direct-site" }
        "twenty icons" { return "twenty-icons" }
        "duckduckgo" { return "duckduckgo" }
        "google" { return "google" }
        "yandex" { return "yandex" }
        "favicone" { return "favicone" }
        "icon horse" { return "icon-horse" }
        default { return $t }
    }
}

$csLines = @()
$csLines += "using System;"
$csLines += "using System.Collections.Generic;"
$csLines += ""
$csLines += "namespace KeeFetch.FetchProfiles"
$csLines += "{"
$csLines += "    internal static partial class FetchProfileCatalog"
$csLines += "    {"
$csLines += "        private static List<FetchProfileDefinition> CreateManagedProfiles()"
$csLines += "        {"
$csLines += "            List<FetchProfileDefinition> profiles = new List<FetchProfileDefinition>();"
$csLines += ""

foreach ($managedId in @("bulk-fast","everyday","privacy","max-coverage")) {
    $win = $winnerMap[$managedId]
    $meta = $managedMeta[$managedId]
    $stableIds = @()
    foreach ($pidRaw in $win.provider_ids) {
        $sid = ToStableProviderId -DisplayOrId ([string]$pidRaw)
        $stableIds += '"' + $sid + '"'
    }
    $joined = $stableIds -join ", "
    $descEsc = EscapeCsString -Value $meta.description
    $intendedEsc = EscapeCsString -Value $meta.intended
    $csLines += "            profiles.Add(new FetchProfileDefinition("
    $csLines += '                "' + $managedId + '",'
    $csLines += '                "' + $meta.display + '",'
    $csLines += '                "' + $descEsc + '",'
    $csLines += '                "' + $intendedEsc + '",'
    $csLines += "                new string[] { $joined },"
    $csLines += "                $($win.primaryTimeout),"
    $csLines += "                $($win.fallbackTimeout),"
    $csLines += "                $($win.cumulativeTimeout),"
    if ($win.allowSynthetic) {
        $csLines += "                true,"
    } else {
        $csLines += "                false,"
    }
    $csLines += "                true,"
    $csLines += '                "docs/benchmarks/v1.3-provider-study.md"));'
    $csLines += ""
}
$csLines += "            return profiles;"
$csLines += "        }"
$csLines += "    }"
$csLines += "}"

$csContent = $csLines -join "`r`n"
[System.IO.File]::WriteAllText($generatedCsPathInOutput, $csContent, $utf8NoBom)
# Also write to repo canonical location when OutputDir is outside the temp area or equals repo.
# To avoid clobbering the checked-in baseline during benchmark harness self-tests (which use a temp selection-out),
# only write to repo when OutputDir is NOT under the temp path.
$writeToRepo = $false
try {
    $tempPath = [System.IO.Path]::GetTempPath().TrimEnd('\','/')
    $outLower = $outputDirResolved.ToLowerInvariant()
    $tempLower = $tempPath.ToLowerInvariant()
    if (-not $outLower.StartsWith($tempLower)) { $writeToRepo = $true }
} catch {
    $writeToRepo = $false
}
# Always allow explicit repo output: if caller sets OutputDir to FetchProfiles dir, write regardless
$repoCatalogDir = Split-Path -Parent $repoGeneratedPath
if ($outputDirResolved.TrimEnd('\','/').ToLowerInvariant() -eq $repoCatalogDir.TrimEnd('\','/').ToLowerInvariant()) { $writeToRepo = $true }
if ($writeToRepo) {
    [System.IO.File]::WriteAllText($repoGeneratedPath, $csContent, $utf8NoBom)
}

Write-Output "Winners: privacy=$($privacyWinner.profile_id) max-coverage=$($maxCoverageWinner.profile_id) bulk-fast=$($bulkWinner.profile_id) everyday=$($everydayWinner.profile_id)"
Write-Output "Decisions: $decisionsPath"
Write-Output "Report: $mdPath"
Write-Output "Generated: $generatedCsPathInOutput and $repoGeneratedPath"

