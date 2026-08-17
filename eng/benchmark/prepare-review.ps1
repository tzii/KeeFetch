param(
    [string]$RunDir,
    [switch]$Validate,
    [string]$OutputPath,
    [string]$Seed = ''
)

$ErrorActionPreference = "Stop"

# Builds (and validates) the human review queue for a benchmark experiment.
#
# Review identity is the exact (fixture_id, artifact_hash) pair: the same
# visual artifact produced by any repetition or candidate is one review unit,
# with occurrence/profile mappings retained for statistics. fixture|profile
# is never accepted as a review key.
#
# Must-review coverage: every synthetic result, every placeholder- or
# blank-suspected result, and every fixture that different profiles resolved
# differently. The remaining machine successes are sampled per
# category|profile stratum using a deterministic SHA-256 ranking over
# seed|fixture|profile|hash, never a positional first-N take.

$allowedLabels = @("correct","acceptable-synthetic","generic","wrong-brand","blank","unusable","ambiguous","not-reviewed")

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

function Get-Sha256Hex {
    param([Parameter(Mandatory=$true)][string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $sha.ComputeHash($bytes)
        return [BitConverter]::ToString($hash).Replace("-","").ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Get-MeasuredRunDirectories {
    param([string]$BaseDir)

    if ([string]::IsNullOrWhiteSpace($BaseDir)) {
        $BaseDir = (Get-Location).Path
    }
    if (-not (Test-Path -LiteralPath $BaseDir)) {
        throw "RunDir not found: $BaseDir"
    }
    $item = Get-Item -LiteralPath $BaseDir -ErrorAction SilentlyContinue
    if ($null -ne $item -and -not $item.PSIsContainer) {
        $BaseDir = Split-Path -Parent $BaseDir
    }

    $runs = @()
    $runJson = Join-Path $BaseDir "run.json"
    if (Test-Path -LiteralPath $runJson) { $runs += $BaseDir }
    foreach ($dir in @(Get-ChildItem -LiteralPath $BaseDir -Directory -ErrorAction SilentlyContinue)) {
        $candidate = Join-Path $dir.FullName "run.json"
        if (Test-Path -LiteralPath $candidate) { $runs += $dir.FullName }
        foreach ($nested in @(Get-ChildItem -LiteralPath $dir.FullName -Directory -ErrorAction SilentlyContinue)) {
            $nestedJson = Join-Path $nested.FullName "run.json"
            if (Test-Path -LiteralPath $nestedJson) { $runs += $nested.FullName }
        }
    }
    if ($runs.Count -eq 0) {
        throw "No run.json found under RunDir: $BaseDir"
    }

    $measured = @()
    foreach ($run in $runs) {
        $meta = Get-Content -Raw -LiteralPath (Join-Path $run "run.json") | ConvertFrom-Json
        $status = ""
        if ($meta.PSObject.Properties.Name -contains 'status') { $status = [string]$meta.status }
        if ($status -ne "complete") {
            throw "Incomplete run rejected: $run"
        }
        $kind = "measured"
        if ($meta.PSObject.Properties.Name -contains 'run_kind') { $kind = [string]$meta.run_kind }
        if ($kind -eq "warmup") { continue }
        $measured += [PSCustomObject]@{
            Directory = $run
            Metadata = $meta
        }
    }
    if ($measured.Count -eq 0) {
        throw "No complete measured runs found under RunDir: $BaseDir (warm-up runs never count as evidence)."
    }
    return $measured
}

function Get-ReviewQueuePath {
    param([string]$BaseDir, [string]$OutPath)
    if (-not [string]::IsNullOrWhiteSpace($OutPath)) {
        return $OutPath
    }
    if ([string]::IsNullOrWhiteSpace($BaseDir)) {
        $BaseDir = (Get-Location).Path
    }
    return (Join-Path $BaseDir "review-queue.csv")
}

# ---------------------------------------------------------------------------
# Validation path
# ---------------------------------------------------------------------------

if ($Validate) {
    $queuePath = $null
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $queuePath = $OutputPath
    } elseif (-not [string]::IsNullOrWhiteSpace($RunDir)) {
        $base = $RunDir
        if (Test-Path -LiteralPath $base) {
            $it = Get-Item -LiteralPath $base -ErrorAction SilentlyContinue
            if ($null -ne $it -and -not $it.PSIsContainer -and $it.Name.ToLowerInvariant().EndsWith(".csv")) {
                $base = Split-Path -Parent $base
            }
        }
        $queuePath = Join-Path $base "review-queue.csv"
    }
    if ([string]::IsNullOrWhiteSpace($queuePath)) {
        $queuePath = Join-Path (Get-Location).Path "review-queue.csv"
    }
    if (-not (Test-Path -LiteralPath $queuePath)) {
        throw "review-queue.csv not found for validation: $queuePath"
    }
    $queuePath = (Resolve-Path -LiteralPath $queuePath).Path

    $rows = @(Import-Csv -LiteralPath $queuePath)
    if ($rows.Count -eq 0) { throw "Review queue is empty: $queuePath" }

    # Artifact inventory from the measured runs for existence/staleness checks.
    $artifactUnits = @{}
    $fixtureHashes = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $mustReviewUnits = $null
    if (-not [string]::IsNullOrWhiteSpace($RunDir) -and (Test-Path -LiteralPath $RunDir)) {
        $measuredRuns = Get-MeasuredRunDirectories -BaseDir $RunDir
        foreach ($mr in $measuredRuns) {
            $rowsCsv = Join-Path $mr.Directory "rows.csv"
            if (-not (Test-Path -LiteralPath $rowsCsv)) { continue }
            foreach ($r in @(Import-Csv -LiteralPath $rowsCsv)) {
                $fid = [string]$r.fixture_id
                $hash = [string]$r.artifact_hash
                if (-not [string]::IsNullOrWhiteSpace($hash)) {
                    [void]$fixtureHashes.Add("$fid|$hash")
                    if (-not $artifactUnits.ContainsKey("$fid|$hash")) {
                        $artifactUnits["$fid|$hash"] = $true
                    }
                }
                $fixturesByHash = "${fid}|${hash}"
            }
        }
    }

    $errors = @()
    $seenKeys = @{}
    $lineNo = 1
    foreach ($row in $rows) {
        $lineNo = $lineNo + 1

        $fid = ""
        if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$row.fixture_id }
        $hash = ""
        if ($row.PSObject.Properties.Name -contains "artifact_hash") { $hash = [string]$row.artifact_hash }
        $key = "$fid|$hash"

        if ([string]::IsNullOrWhiteSpace($fid)) {
            $errors += "Line ${lineNo}: review row missing fixture_id"
            continue
        }
        if ([string]::IsNullOrWhiteSpace($hash)) {
            $errors += "Line ${lineNo}: review row missing artifact_hash (exact-hash identity is mandatory)"
            continue
        }
        if ($seenKeys.ContainsKey($key)) {
            $errors += "Line ${lineNo}: duplicate review key $key"
            continue
        }
        $seenKeys[$key] = $row

        if ($artifactUnits.Count -gt 0 -and -not $artifactUnits.ContainsKey($key)) {
            $errors += "Line ${lineNo}: review key $key does not exist in the run artifacts (stale or fabricated)"
        }

        $label = ""
        if ($row.PSObject.Properties.Name -contains "review_label") { $label = [string]$row.review_label }
        $labelTrim = $label.Trim().ToLowerInvariant()
        if ($allowedLabels -notcontains $labelTrim) {
            $errors += "Line ${lineNo}: label outside allowed set: '$label'"
            continue
        }
        if ($labelTrim -eq "not-reviewed") {
            $errors += "Line ${lineNo}: queue still contains not-reviewed rows; complete the review before validating"
            continue
        }

        $reviewer = ""
        if ($row.PSObject.Properties.Name -contains "reviewer") { $reviewer = [string]$row.reviewer }
        $reviewedAt = ""
        if ($row.PSObject.Properties.Name -contains "reviewed_at_utc") { $reviewedAt = [string]$row.reviewed_at_utc }
        if ([string]::IsNullOrWhiteSpace($reviewer)) {
            $errors += "Line ${lineNo}: reviewed row missing reviewer"
        }
        if ([string]::IsNullOrWhiteSpace($reviewedAt)) {
            $errors += "Line ${lineNo}: reviewed row missing reviewed_at_utc"
        } else {
            $dt = [DateTime]::MinValue
            $styles = [System.Globalization.DateTimeStyles]::AllowLeadingWhite -bor [System.Globalization.DateTimeStyles]::AllowTrailingWhite
            if (-not [DateTime]::TryParse($reviewedAt, [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$dt)) {
                $errors += "Line ${lineNo}: reviewed_at_utc not parseable: '$reviewedAt'"
            }
        }
    }

    # Completeness: every unit that requires review (as regenerated from the
    # same deterministic inputs) must be present in the queue under review.
    if (-not [string]::IsNullOrWhiteSpace($RunDir) -and (Test-Path -LiteralPath $RunDir)) {
        $regenBase = $RunDir
        $regenItem = Get-Item -LiteralPath $regenBase -ErrorAction SilentlyContinue
        if ($null -ne $regenItem -and -not $regenItem.PSIsContainer) {
            $regenBase = Split-Path -Parent $regenBase
        }
        $tempQueue = [System.IO.Path]::GetTempFileName()
        try {
            & $PSCommandPath -RunDir $regenBase -OutputPath $tempQueue | Out-Null
            foreach ($req in @(Import-Csv -LiteralPath $tempQueue)) {
                $reqKey = "$([string]$req.fixture_id)|$([string]$req.artifact_hash)"
                if (-not $seenKeys.ContainsKey($reqKey)) {
                    $errors += "Required review unit missing from queue: $reqKey (regenerate and review the new rows)"
                }
            }
        } finally {
            Remove-Item -LiteralPath $tempQueue -Force -ErrorAction SilentlyContinue
        }
    }

    if ($errors.Count -gt 0) {
        $msg = "review-queue validation failed:`n" + ($errors -join "`n")
        throw $msg
    }

    Write-Output "review-queue validated: $($rows.Count) rows"
    return
}

# ---------------------------------------------------------------------------
# Generation path
# ---------------------------------------------------------------------------

$baseDir = $RunDir
if ([string]::IsNullOrWhiteSpace($baseDir)) {
    $baseDir = (Get-Location).Path
}
if (-not (Test-Path -LiteralPath $baseDir)) {
    throw "RunDir not found: $baseDir"
}

$measuredRuns = Get-MeasuredRunDirectories -BaseDir $baseDir

# Deterministic seed: explicit -Seed wins; otherwise the experiment
# fingerprint shared by the runs; otherwise a fixed constant.
$reviewSeed = $Seed
if ([string]::IsNullOrWhiteSpace($reviewSeed)) {
    $experimentFingerprint = ""
    foreach ($mr in $measuredRuns) {
        if ($mr.Metadata.PSObject.Properties.Name -contains 'experiment_fingerprint') {
            $experimentFingerprint = [string]$mr.Metadata.experiment_fingerprint
            break
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($experimentFingerprint)) {
        $reviewSeed = $experimentFingerprint
    } else {
        $reviewSeed = "keefetch-review"
    }
}

$allRows = @()
foreach ($mr in $measuredRuns) {
    $rowsCsv = Join-Path $mr.Directory "rows.csv"
    if (-not (Test-Path -LiteralPath $rowsCsv)) {
        throw "Measured run missing rows.csv: $($mr.Directory)"
    }
    foreach ($r in @(Import-Csv -LiteralPath $rowsCsv)) {
        $r | Add-Member -NotePropertyName "_run_directory" -NotePropertyValue $mr.Directory -Force
        $allRows += $r
    }
}
if ($allRows.Count -eq 0) {
    throw "No rows found in measured runs under $baseDir"
}

# fixture -> set of distinct selected providers across all profiles/reps
$fixtureProviders = @{}
foreach ($row in $allRows) {
    $fid = [string]$row.fixture_id
    if ([string]::IsNullOrWhiteSpace($fid)) { continue }
    $prov = [string]$row.selected_provider
    if (-not $fixtureProviders.ContainsKey($fid)) {
        $fixtureProviders[$fid] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    }
    if (-not [string]::IsNullOrWhiteSpace($prov)) {
        [void]$fixtureProviders[$fid].Add($prov)
    }
}

# A review unit is the exact (fixture_id, artifact_hash) pair. Occurrences
# track which profiles and repetitions produced the artifact.
$units = @{}
foreach ($row in $allRows) {
    $fid = [string]$row.fixture_id
    $hash = [string]$row.artifact_hash
    if ([string]::IsNullOrWhiteSpace($fid)) { continue }

    $isSyn = ParseBoolValue -Value $row.is_synthetic
    $placeholder = ParseBoolValue -Value $row.placeholder_suspected
    $blank = ParseBoolValue -Value $row.blank_suspected
    $outcome = ""
    if ($row.PSObject.Properties.Name -contains "machine_outcome") { $outcome = [string]$row.machine_outcome }
    $profile = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $profile = [string]$row.profile }
    $category = ""
    if ($row.PSObject.Properties.Name -contains "category") { $category = [string]$row.category }

    if ([string]::IsNullOrWhiteSpace($hash)) {
        # Failures without artifacts can only be must-review when they carry a
        # review trigger flag; otherwise they are machine-evidence only.
        if (-not ($isSyn -or $placeholder -or $blank)) { continue }
        $hash = ""
    }

    $key = "$fid|$hash"
    if (-not $units.ContainsKey($key)) {
        $units[$key] = [PSCustomObject]@{
            FixtureId = $fid
            ArtifactHash = $hash
            Categories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
            Profiles = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
            Reasons = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
            Occurrences = 0
            MachineOutcome = $outcome
            SampleStratumProfile = $null
        }
    }
    $unit = $units[$key]
    $unit.Occurrences++
    if (-not [string]::IsNullOrWhiteSpace($category)) { [void]$unit.Categories.Add($category) }
    if (-not [string]::IsNullOrWhiteSpace($profile)) { [void]$unit.Profiles.Add($profile) }

    if ($isSyn) { [void]$unit.Reasons.Add("synthetic") }
    if ($placeholder) { [void]$unit.Reasons.Add("placeholder_suspected") }
    if ($blank) { [void]$unit.Reasons.Add("blank_suspected") }
}

# Profile-differing trigger: a fixture resolved to different providers across
# profiles. Every unit of such a fixture is required review.
foreach ($fid in @($fixtureProviders.Keys)) {
    if ($fixtureProviders[$fid].Count -gt 1) {
        foreach ($key in @($units.Keys)) {
            if ($units[$key].FixtureId -eq $fid) {
                [void]$units[$key].Reasons.Add("profile-differing")
            }
        }
    }
}

$mustUnits = @()
$remainingSuccessUnits = @()
foreach ($key in @($units.Keys)) {
    $unit = $units[$key]
    if ($unit.Reasons.Count -gt 0) {
        $mustUnits += $unit
    } elseif ($unit.MachineOutcome -eq "success" -and -not [string]::IsNullOrWhiteSpace($unit.ArtifactHash)) {
        $remainingSuccessUnits += $unit
    }
}

# Deterministic stratified sample: rank unique units within each
# category|profile stratum by SHA-256(seed|fixture|profile|hash) ascending.
# A unit's stratum profile is its lexicographically first producing profile.
$strata = @{}
foreach ($unit in $remainingSuccessUnits) {
    $category = (@($unit.Categories) | Sort-Object | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($category)) { $category = "unknown" }
    $profile = (@($unit.Profiles) | Sort-Object | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($profile)) { $profile = "unknown" }
    $stratumKey = "$category|$profile"
    if (-not $strata.ContainsKey($stratumKey)) { $strata[$stratumKey] = @() }
    $strata[$stratumKey] += $unit
}

$sampledUnits = @()
foreach ($stratumKey in @($strata.Keys | Sort-Object)) {
    $ranked = @($strata[$stratumKey] | ForEach-Object {
        $profile = (@($_.Profiles) | Sort-Object | Select-Object -First 1)
        $rank = Get-Sha256Hex -Text ("{0}|{1}|{2}|{3}" -f $reviewSeed, $_.FixtureId, $profile, $_.ArtifactHash)
        [PSCustomObject]@{ Unit = $_; Rank = $rank }
    } | Sort-Object -Property Rank)

    $population = $ranked.Count
    $sampleCount = [Math]::Ceiling($population * 0.10)
    if ($sampleCount -lt 1) { $sampleCount = 1 }
    if ($sampleCount -gt $population) { $sampleCount = $population }
    for ($i = 0; $i -lt $sampleCount; $i++) {
        $sampledUnits += $ranked[$i].Unit
    }
}

$queueUnits = @($mustUnits) + @($sampledUnits)

# Determine output path
$queuePathOut = Get-ReviewQueuePath -BaseDir $baseDir -OutPath $OutputPath

# Preserve existing human labels by exact review key.
$existingMap = @{}
if (Test-Path -LiteralPath $queuePathOut) {
    try {
        foreach ($er in @(Import-Csv -LiteralPath $queuePathOut)) {
            $fidE = [string]$er.fixture_id
            $hashE = [string]$er.artifact_hash
            $keyE = "$fidE|$hashE"
            if (-not $existingMap.ContainsKey($keyE)) {
                $existingMap[$keyE] = $er
            }
        }
    } catch {
    }
}

$queueOut = @()
foreach ($unit in ($queueUnits | Sort-Object -Property FixtureId, ArtifactHash)) {
    $key = "$($unit.FixtureId)|$($unit.ArtifactHash)"
    $reason = (@($unit.Reasons) | Sort-Object) -join ";"
    if ($reason -eq "") { $reason = "sampled-10pct" }

    $reviewLabel = "not-reviewed"
    $reviewer = ""
    $reviewedAt = ""
    $notes = $reason
    if ($existingMap.ContainsKey($key)) {
        $ex = $existingMap[$key]
        $exLabel = ""
        if ($ex.PSObject.Properties.Name -contains "review_label") { $exLabel = [string]$ex.review_label }
        $exReviewer = ""
        if ($ex.PSObject.Properties.Name -contains "reviewer") { $exReviewer = [string]$ex.reviewer }
        $exAt = ""
        if ($ex.PSObject.Properties.Name -contains "reviewed_at_utc") { $exAt = [string]$ex.reviewed_at_utc }
        $exNotes = ""
        if ($ex.PSObject.Properties.Name -contains "notes") { $exNotes = [string]$ex.notes }
        if (-not [string]::IsNullOrWhiteSpace($exLabel) -and $exLabel.Trim().ToLowerInvariant() -ne "not-reviewed") {
            $reviewLabel = $exLabel
            $reviewer = $exReviewer
            $reviewedAt = $exAt
        }
        if (-not [string]::IsNullOrWhiteSpace($exNotes) -and $exNotes -ne $reason) {
            $notes = "$reason; $exNotes"
        }
    }

    $obj = [PSCustomObject]@{
        fixture_id = $unit.FixtureId
        artifact_hash = $unit.ArtifactHash
        profiles = (@($unit.Profiles) | Sort-Object) -join ";"
        categories = (@($unit.Categories) | Sort-Object) -join ";"
        occurrences = $unit.Occurrences
        review_label = $reviewLabel
        reviewer = $reviewer
        reviewed_at_utc = $reviewedAt
        notes = $notes
    }
    $queueOut += $obj
}

$outDir = Split-Path -Parent $queuePathOut
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$queueOut | Export-Csv -LiteralPath $queuePathOut -NoTypeInformation -Encoding UTF8

Write-Output ("review-queue written: {0} with {1} review units ({2} must-review, {3} sampled) from {4} result rows; seed={5}" -f `
    $queuePathOut, $queueOut.Count, $mustUnits.Count, $sampledUnits.Count, $allRows.Count, $reviewSeed)
