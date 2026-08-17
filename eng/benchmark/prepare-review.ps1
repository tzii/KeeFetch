param(
    [string]$RunDir,
    [switch]$Validate,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

# Builds (and validates) the human review queue for a benchmark experiment.
#
# The queue is a CENSUS, not a sample: every unique cold (fixture_id,
# artifact_hash) unit is human-reviewed exactly once, and the recorded label
# propagates to every occurrence of that exact artifact - all repetitions and
# candidates that produced it. There is no sampling, no strata, no design
# weights, and no seed; coverage is complete by construction, so no interval
# estimate of label rates is needed anywhere downstream.
#
# Review identity is the exact (fixture_id, artifact_hash) pair; fixture|profile
# is never accepted as a review key. Units without an artifact hash cannot
# carry exact identity and remain machine evidence only.
#
# notes annotate why a unit drew attention (synthetic, placeholder_suspected,
# blank_suspected, profile-differing); annotation is informational and never
# affects inclusion - the census contains every cold unit regardless.

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

function Get-RunCacheMode {
    param([object]$Metadata)
    $mode = "cold"
    if ($Metadata.PSObject.Properties.Name -contains 'cache_mode') {
        $mode = [string]$Metadata.cache_mode
    }
    return $mode
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

# Loads every row of the measured COLD cells. Cold artifacts are the scoring
# evidence, so the census is defined over them; warm rows are latency
# evidence only and never create review units.
function Get-ColdMeasuredRows {
    param([Parameter(Mandatory=$true)][object[]]$MeasuredRuns)

    $coldRows = @()
    $coldCellCount = 0
    foreach ($mr in $MeasuredRuns) {
        if ((Get-RunCacheMode -Metadata $mr.Metadata) -ne "cold") { continue }
        $coldCellCount++
        $rowsCsv = Join-Path $mr.Directory "rows.csv"
        if (-not (Test-Path -LiteralPath $rowsCsv)) {
            throw "Measured cold run missing rows.csv: $($mr.Directory)"
        }
        foreach ($r in @(Import-Csv -LiteralPath $rowsCsv)) {
            $r | Add-Member -NotePropertyName "_run_directory" -NotePropertyValue $mr.Directory -Force
            $coldRows += $r
        }
    }
    if ($coldCellCount -eq 0) {
        throw "No measured cold cells found; the review census is defined over cold artifacts and requires cold evidence."
    }
    if ($coldRows.Count -eq 0) {
        throw "Measured cold runs contain no rows."
    }
    return $coldRows
}

# Builds the census units: one unit per unique cold (fixture_id,
# artifact_hash) with exact-hash identity. Rows without an artifact hash are
# machine evidence only and never become review units.
function Get-CensusUnits {
    param([Parameter(Mandatory=$true)][object[]]$ColdRows)

    $fixtureProviders = @{}
    foreach ($row in $ColdRows) {
        $fid = [string]$row.fixture_id
        if ([string]::IsNullOrWhiteSpace($fid)) { continue }
        $prov = ""
        if ($row.PSObject.Properties.Name -contains "selected_provider") { $prov = [string]$row.selected_provider }
        if (-not $fixtureProviders.ContainsKey($fid)) {
            $fixtureProviders[$fid] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        }
        if (-not [string]::IsNullOrWhiteSpace($prov)) {
            [void]$fixtureProviders[$fid].Add($prov)
        }
    }

    $units = @{}
    $flaggedNoHash = 0
    foreach ($row in $ColdRows) {
        $fid = [string]$row.fixture_id
        $hash = [string]$row.artifact_hash
        if ([string]::IsNullOrWhiteSpace($fid)) { continue }
        if ([string]::IsNullOrWhiteSpace($hash)) {
            # No exact identity possible; count flagged rows so the summary can
            # surface that they remained machine evidence.
            if ((ParseBoolValue -Value $row.is_synthetic) -or
                (ParseBoolValue -Value $row.placeholder_suspected) -or
                (ParseBoolValue -Value $row.blank_suspected)) {
                $flaggedNoHash++
            }
            continue
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
            }
        }
        $unit = $units[$key]
        $unit.Occurrences++
        $category = ""
        if ($row.PSObject.Properties.Name -contains "category") { $category = [string]$row.category }
        $profile = ""
        if ($row.PSObject.Properties.Name -contains "profile") { $profile = [string]$row.profile }
        if (-not [string]::IsNullOrWhiteSpace($category)) { [void]$unit.Categories.Add($category) }
        if (-not [string]::IsNullOrWhiteSpace($profile)) { [void]$unit.Profiles.Add($profile) }

        if (ParseBoolValue -Value $row.is_synthetic) { [void]$unit.Reasons.Add("synthetic") }
        if (ParseBoolValue -Value $row.placeholder_suspected) { [void]$unit.Reasons.Add("placeholder_suspected") }
        if (ParseBoolValue -Value $row.blank_suspected) { [void]$unit.Reasons.Add("blank_suspected") }
    }

    # Profile-differing annotation: a fixture resolved to different providers
    # across profiles (cold rows). Informational only - every cold unit is in
    # the census regardless.
    foreach ($fid in @($fixtureProviders.Keys)) {
        if ($fixtureProviders[$fid].Count -gt 1) {
            foreach ($key in @($units.Keys)) {
                if ($units[$key].FixtureId -eq $fid) {
                    [void]$units[$key].Reasons.Add("profile-differing")
                }
            }
        }
    }

    return [PSCustomObject]@{
        Units = $units
        FlaggedNoHash = $flaggedNoHash
    }
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

    # Cold census inventory from the measured runs for existence/staleness
    # checks. A queue key that is not a cold census unit is stale or
    # fabricated - there is no smaller valid queue than the census.
    $censusUnits = $null
    if (-not [string]::IsNullOrWhiteSpace($RunDir) -and (Test-Path -LiteralPath $RunDir)) {
        $measuredRuns = Get-MeasuredRunDirectories -BaseDir $RunDir
        $coldRows = Get-ColdMeasuredRows -MeasuredRuns $measuredRuns
        $censusUnits = (Get-CensusUnits -ColdRows $coldRows).Units
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

        if ($null -ne $censusUnits -and -not $censusUnits.ContainsKey($key)) {
            $errors += "Line ${lineNo}: review key $key is not a cold census unit (stale or fabricated)"
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

    # Completeness: the census is regenerated from the same cold evidence and
    # every census unit must be present in the queue under review. A census
    # with holes is a sample with unknown selection bias.
    if ($null -ne $censusUnits) {
        foreach ($reqKey in @($censusUnits.Keys | Sort-Object)) {
            if (-not $seenKeys.ContainsKey($reqKey)) {
                $errors += "Required review unit missing from queue: $reqKey (the queue is a census; regenerate and review every cold unit)"
            }
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
$coldRows = Get-ColdMeasuredRows -MeasuredRuns $measuredRuns
$census = Get-CensusUnits -ColdRows $coldRows
$units = $census.Units
if ($units.Count -eq 0) {
    throw "No cold artifacts found under $baseDir - nothing to review. The census requires cold successes with recorded artifact hashes."
}

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
$flaggedUnits = 0
foreach ($unit in ($units.Values | Sort-Object -Property FixtureId, ArtifactHash)) {
    $key = "$($unit.FixtureId)|$($unit.ArtifactHash)"
    $reason = (@($unit.Reasons) | Sort-Object) -join ";"
    if ($reason -ne "") { $flaggedUnits++ }
    $notes = if ($reason -ne "") { $reason } else { "census" }

    $reviewLabel = "not-reviewed"
    $reviewer = ""
    $reviewedAt = ""
    if ($existingMap.ContainsKey($key)) {
        $ex = $existingMap[$key]
        $exLabel = ""
        if ($ex.PSObject.Properties.Name -contains "review_label") { $exLabel = [string]$ex.review_label }
        $exReviewer = ""
        if ($ex.PSObject.Properties.Name -contains "reviewer") { $exReviewer = [string]$ex.reviewer }
        $exAt = ""
        if ($ex.PSObject.Properties.Name -contains "reviewed_at_utc") { $exAt = [string]$ex.reviewed_at_utc }
        if (-not [string]::IsNullOrWhiteSpace($exLabel) -and $exLabel.Trim().ToLowerInvariant() -ne "not-reviewed") {
            $reviewLabel = $exLabel
            $reviewer = $exReviewer
            $reviewedAt = $exAt
        }
        # Human labels are preserved verbatim; only the informational notes are
        # refreshed from the current census.
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

Write-Output ("review-queue written: {0} with {1} census units from {2} cold result rows ({3} flagged for attention, {4} flagged rows without artifact hash stayed machine evidence)" -f `
    $queuePathOut, $queueOut.Count, $coldRows.Count, $flaggedUnits, $census.FlaggedNoHash)
