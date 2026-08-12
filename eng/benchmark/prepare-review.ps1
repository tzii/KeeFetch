param(
    [string]$RunDir,
    [switch]$Validate,
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

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

function GetRowsCsvFiles {
    param([string]$BaseDir)
    $files = @()
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
    $direct = Join-Path $BaseDir "rows.csv"
    if (Test-Path -LiteralPath $direct) {
        $files += (Resolve-Path -LiteralPath $direct).Path
    }
    $subDirs = @(Get-ChildItem -LiteralPath $BaseDir -Directory -ErrorAction SilentlyContinue)
    foreach ($dir in $subDirs) {
        $candidate = Join-Path $dir.FullName "rows.csv"
        if (Test-Path -LiteralPath $candidate) {
            $files += (Resolve-Path -LiteralPath $candidate).Path
        }
        # Also look one level deeper for benchmark-runs/<run>/rows.csv when BaseDir is output_root
        $nested = @(Get-ChildItem -LiteralPath $dir.FullName -Directory -ErrorAction SilentlyContinue)
        foreach ($nd in $nested) {
            $nestedCandidate = Join-Path $nd.FullName "rows.csv"
            if (Test-Path -LiteralPath $nestedCandidate) {
                $files += (Resolve-Path -LiteralPath $nestedCandidate).Path
            }
        }
    }
    if ($files.Count -eq 0) {
        # Try recursive search as fallback
        $found = @(Get-ChildItem -Path $BaseDir -Filter "rows.csv" -Recurse -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
        foreach ($f in $found) {
            $already = $false
            foreach ($existing in $files) { if ($existing -eq $f) { $already = $true; break } }
            if (-not $already) { $files += $f }
        }
    }
    return $files
}

function GetReviewQueuePath {
    param([string]$BaseDir, [string]$OutPath)
    if (-not [string]::IsNullOrWhiteSpace($OutPath)) {
        return $OutPath
    }
    if ([string]::IsNullOrWhiteSpace($BaseDir)) {
        $BaseDir = (Get-Location).Path
    }
    # If BaseDir is a file, use its directory
    if (Test-Path -LiteralPath $BaseDir) {
        $it = Get-Item -LiteralPath $BaseDir -ErrorAction SilentlyContinue
        if ($null -ne $it -and -not $it.PSIsContainer) {
            $BaseDir = Split-Path -Parent $BaseDir
        }
    }
    return (Join-Path $BaseDir "review-queue.csv")
}

if ($Validate) {
    $queuePath = GetReviewQueuePath -BaseDir $RunDir -OutPath $OutputPath
    # If RunDir itself points to review-queue.csv, handle
    if (-not [string]::IsNullOrWhiteSpace($RunDir) -and (Test-Path -LiteralPath $RunDir)) {
        $ri = Get-Item -LiteralPath $RunDir -ErrorAction SilentlyContinue
        if ($null -ne $ri -and -not $ri.PSIsContainer -and $ri.Name.ToLowerInvariant().EndsWith(".csv")) {
            $queuePath = (Resolve-Path -LiteralPath $RunDir).Path
        }
    }
    if (-not (Test-Path -LiteralPath $queuePath)) {
        # Try OutputPath as direct file
        if (-not [string]::IsNullOrWhiteSpace($OutputPath) -and (Test-Path -LiteralPath $OutputPath)) {
            $queuePath = (Resolve-Path -LiteralPath $OutputPath).Path
        } else {
            throw "review-queue.csv not found for validation: $queuePath"
        }
    } else {
        $queuePath = (Resolve-Path -LiteralPath $queuePath).Path
    }

    $rows = @(Import-Csv -LiteralPath $queuePath)

    # Build map of artifact hashes from rows.csv for hash validation if RunDir available
    $artifactMap = @{}
    $rowsCsvFilesForValidation = @()
    if (-not [string]::IsNullOrWhiteSpace($RunDir) -and (Test-Path -LiteralPath $RunDir)) {
        $candidateBase = $RunDir
        $runItemCheck = Get-Item -LiteralPath $candidateBase -ErrorAction SilentlyContinue
        if ($null -ne $runItemCheck -and -not $runItemCheck.PSIsContainer -and $runItemCheck.Name.ToLowerInvariant().EndsWith(".csv")) {
            $candidateBase = Split-Path -Parent $candidateBase
        }
        try {
            $rowsCsvFilesForValidation = GetRowsCsvFiles -BaseDir $candidateBase
        } catch {
            $rowsCsvFilesForValidation = @()
        }
        foreach ($rf in $rowsCsvFilesForValidation) {
            try {
                $runRows = @(Import-Csv -LiteralPath $rf)
                foreach ($rr in $runRows) {
                    $fid = ""
                    if ($rr.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$rr.fixture_id }
                    $prof = ""
                    if ($rr.PSObject.Properties.Name -contains "profile") { $prof = [string]$rr.profile }
                    if ([string]::IsNullOrWhiteSpace($prof) -and $rr.PSObject.Properties.Name -contains "profile_id") { $prof = [string]$rr.profile_id }
                    $ah = ""
                    if ($rr.PSObject.Properties.Name -contains "artifact_hash") { $ah = [string]$rr.artifact_hash }
                    $ap = ""
                    if ($rr.PSObject.Properties.Name -contains "artifact_path") { $ap = [string]$rr.artifact_path }
                    $key = "$fid|$prof|$ah"
                    $key2 = "$fid|$prof"
                    if (-not $artifactMap.ContainsKey($key)) { $artifactMap[$key] = $ah }
                    if (-not $artifactMap.ContainsKey($key2)) { $artifactMap[$key2] = $ah }
                    # Store path for file hash check
                    $pathKey = "$fid|$prof|path"
                    if (-not [string]::IsNullOrWhiteSpace($ap)) {
                        $artifactMap[$pathKey] = $ap
                    }
                }
            } catch {
            }
        }
    }

    $errors = @()
    $lineNo = 1
    foreach ($row in $rows) {
        $lineNo = $lineNo + 1
        $label = ""
        if ($row.PSObject.Properties.Name -contains "review_label") { $label = [string]$row.review_label }
        $labelTrim = $label.Trim().ToLowerInvariant()
        if ($allowedLabels -notcontains $labelTrim) {
            $errors += "Line ${lineNo}: label outside allowed set: '$label'"
            continue
        }
        if ($labelTrim -ne "not-reviewed") {
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
                $parsed = [DateTime]::TryParse($reviewedAt, [ref]$dt)
                if (-not $parsed) {
                    $errors += "Line ${lineNo}: reviewed_at_utc not parseable: '$reviewedAt'"
                }
            }
        }
        # Artifact hash validation if map available
        if ($artifactMap.Count -gt 0) {
            $fid2 = ""
            if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid2 = [string]$row.fixture_id }
            $prof2 = ""
            if ($row.PSObject.Properties.Name -contains "profile_id") { $prof2 = [string]$row.profile_id }
            if ([string]::IsNullOrWhiteSpace($prof2) -and $row.PSObject.Properties.Name -contains "profile") { $prof2 = [string]$row.profile }
            $ah2 = ""
            if ($row.PSObject.Properties.Name -contains "artifact_hash") { $ah2 = [string]$row.artifact_hash }
            if (-not [string]::IsNullOrWhiteSpace($ah2)) {
                $lookupKey = "$fid2|$prof2|$ah2"
                $lookupKey2 = "$fid2|$prof2"
                $expected = $null
                if ($artifactMap.ContainsKey($lookupKey)) {
                    $expected = $artifactMap[$lookupKey]
                } elseif ($artifactMap.ContainsKey($lookupKey2)) {
                    $expected = $artifactMap[$lookupKey2]
                    # If review hash doesn't match expected, flag
                    if ($ah2 -ne $expected -and -not [string]::IsNullOrWhiteSpace($expected)) {
                        $errors += "Line ${lineNo}: artifact hash mismatch for $fid2/$prof2 expected '$expected' got '$ah2'"
                    }
                } else {
                    # No matching rows.csv entry - might be stale, but not error unless we require coverage
                }
                # Check file hash if artifact file exists
                $pathKey2 = "$fid2|$prof2|path"
                if ($artifactMap.ContainsKey($pathKey2)) {
                    $artPath = [string]$artifactMap[$pathKey2]
                    if (-not [string]::IsNullOrWhiteSpace($artPath)) {
                        $baseForArt = $RunDir
                        if ([string]::IsNullOrWhiteSpace($baseForArt)) { $baseForArt = (Get-Location).Path }
                        if (Test-Path -LiteralPath $baseForArt) {
                            $bi = Get-Item -LiteralPath $baseForArt -ErrorAction SilentlyContinue
                            if ($null -ne $bi -and -not $bi.PSIsContainer) { $baseForArt = Split-Path -Parent $baseForArt }
                        }
                        # Search for artifact file
                        $fullArtPath = $null
                        $candidateArt = Join-Path $baseForArt $artPath
                        if (Test-Path -LiteralPath $candidateArt) {
                            $fullArtPath = $candidateArt
                        } else {
                            # Try under each rows.csv directory
                            foreach ($rf2 in $rowsCsvFilesForValidation) {
                                $runDirForArt = Split-Path -Parent $rf2
                                $candidate2 = Join-Path $runDirForArt $artPath
                                if (Test-Path -LiteralPath $candidate2) { $fullArtPath = $candidate2; break }
                                # Also try artifacts folder directly
                                $candidate3 = Join-Path $runDirForArt "artifacts" | Join-Path -ChildPath ([System.IO.Path]::GetFileName($artPath))
                                if (Test-Path -LiteralPath $candidate3) { $fullArtPath = $candidate3; break }
                            }
                        }
                        if ($null -ne $fullArtPath -and (Test-Path -LiteralPath $fullArtPath)) {
                            try {
                                $bytes = [System.IO.File]::ReadAllBytes($fullArtPath)
                                $sha = [System.Security.Cryptography.SHA256]::Create()
                                $hashBytes = $null
                                try {
                                    $hashBytes = $sha.ComputeHash($bytes)
                                } finally {
                                    if ($null -ne $sha) { $sha.Dispose() }
                                }
                                $computed = [BitConverter]::ToString($hashBytes).Replace("-","").ToLowerInvariant()
                                if ($computed -ne $ah2.ToLowerInvariant()) {
                                    $errors += "Line ${lineNo}: artifact file hash mismatch for $fid2/$prof2 file '$fullArtPath' expected '$ah2' computed '$computed'"
                                }
                            } catch {
                            }
                        }
                    }
                }
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

# Generation path

$baseDir = $RunDir
if ([string]::IsNullOrWhiteSpace($baseDir)) {
    $baseDir = (Get-Location).Path
}

# Resolve baseDir
if (-not (Test-Path -LiteralPath $baseDir)) {
    throw "RunDir not found: $baseDir"
}

$rowsFiles = GetRowsCsvFiles -BaseDir $baseDir
if ($rowsFiles.Count -eq 0) {
    throw "No rows.csv found under RunDir: $baseDir"
}

$allRows = @()
foreach ($rf in $rowsFiles) {
    try {
        $imported = @(Import-Csv -LiteralPath $rf)
        foreach ($r in $imported) {
            # Tag with source file for artifact resolution
            $r | Add-Member -NotePropertyName "_source_rows_file" -NotePropertyValue $rf -Force -ErrorAction SilentlyContinue
            $allRows += $r
        }
    } catch {
        throw "Failed to read rows.csv '$rf': $($_.Exception.Message)"
    }
}

if ($allRows.Count -eq 0) {
    throw "No rows found in rows.csv files under $baseDir"
}

# Determine output path
$queuePathOut = GetReviewQueuePath -BaseDir $baseDir -OutPath $OutputPath
# If OutputPath was provided and is a directory, join file name
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    if (Test-Path -LiteralPath $OutputPath) {
        $opItem = Get-Item -LiteralPath $OutputPath -ErrorAction SilentlyContinue
        if ($null -ne $opItem -and $opItem.PSIsContainer) {
            $queuePathOut = Join-Path $OutputPath "review-queue.csv"
        }
    } else {
        # If OutputPath has no extension or is existing dir path, handle
        if ($OutputPath.EndsWith("\") -or $OutputPath.EndsWith("/")) {
            $queuePathOut = Join-Path $OutputPath "review-queue.csv"
        }
    }
}

# Build fixture->providers map for profile-differing detection
$fixtureToProviders = @{}
foreach ($row in $allRows) {
    $fid = ""
    if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$row.fixture_id }
    if ([string]::IsNullOrWhiteSpace($fid)) { continue }
    $prov = ""
    if ($row.PSObject.Properties.Name -contains "selected_provider") { $prov = [string]$row.selected_provider }
    if (-not $fixtureToProviders.ContainsKey($fid)) {
        $fixtureToProviders[$fid] = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    }
    if (-not [string]::IsNullOrWhiteSpace($prov)) {
        [void]$fixtureToProviders[$fid].Add($prov)
    }
}

# Identify must-review rows
$mustKeys = New-Object 'System.Collections.Generic.HashSet[string]'
$mustRows = @()
# Map from row key to row and reason
$rowKeyToReason = @{}

for ($idx = 0; $idx -lt $allRows.Count; $idx++) {
    $row = $allRows[$idx]
    $fid = ""
    if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$row.fixture_id }
    $prof = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $prof = [string]$row.profile }
    if ([string]::IsNullOrWhiteSpace($prof) -and $row.PSObject.Properties.Name -contains "profile_id") { $prof = [string]$row.profile_id }
    $ah = ""
    if ($row.PSObject.Properties.Name -contains "artifact_hash") { $ah = [string]$row.artifact_hash }
    $runId = ""
    if ($row.PSObject.Properties.Name -contains "run_id") { $runId = [string]$row.run_id }
    $cat = ""
    if ($row.PSObject.Properties.Name -contains "category") { $cat = [string]$row.category }
    $isSyn = ParseBoolValue -Value $row.is_synthetic
    # Some CSV may have is_synthetic as string "TRUE"/"FALSE"
    if (-not $isSyn -and $row.PSObject.Properties.Name -contains "is_synthetic") {
        $rawSyn = [string]$row.is_synthetic
        if ($rawSyn.ToLowerInvariant() -eq "true") { $isSyn = $true }
    }
    $placeholder = ParseBoolValue -Value $row.placeholder_suspected
    $blank = ParseBoolValue -Value $row.blank_suspected
    # Also check alternative column names
    if ($row.PSObject.Properties.Name -contains "placeholder_suspected") {
        $placeholder = ParseBoolValue -Value $row.placeholder_suspected
    }
    if ($row.PSObject.Properties.Name -contains "blank_suspected") {
        $blank = ParseBoolValue -Value $row.blank_suspected
    }

    $reasons = @()
    if ($isSyn) { $reasons += "synthetic" }
    if ($placeholder) { $reasons += "placeholder_suspected" }
    if ($blank) { $reasons += "blank_suspected" }
    if ($fixtureToProviders.ContainsKey($fid)) {
        $provSet = $fixtureToProviders[$fid]
        if ($provSet.Count -gt 1) { $reasons += "profile-differing" }
    }

    if ($reasons.Count -gt 0) {
        $key = "$fid|$prof|$ah"
        # Use composite key with index to handle duplicate fixture/profile with different repetition? But spec says key is fixture,profile,hash
        # If duplicate key already added, skip duplicate row (keep first)
        if (-not $mustKeys.Contains($key)) {
            [void]$mustKeys.Add($key)
            $mustRows += $row
            $rowKeyToReason[$key] = ($reasons -join ";")
        } else {
            # Already have this key, but merge reasons if needed
            $existingReason = $rowKeyToReason[$key]
            foreach ($r in $reasons) {
                if ($existingReason.IndexOf($r) -lt 0) {
                    $existingReason = $existingReason + ";" + $r
                }
            }
            $rowKeyToReason[$key] = $existingReason
        }
    }
}

# Remaining successes for stratified sampling
$remainingSuccesses = @()
foreach ($row in $allRows) {
    $fid = ""
    if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$row.fixture_id }
    $prof = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $prof = [string]$row.profile }
    if ([string]::IsNullOrWhiteSpace($prof) -and $row.PSObject.Properties.Name -contains "profile_id") { $prof = [string]$row.profile_id }
    $ah = ""
    if ($row.PSObject.Properties.Name -contains "artifact_hash") { $ah = [string]$row.artifact_hash }
    $key = "$fid|$prof|$ah"
    if ($mustKeys.Contains($key)) { continue }
    $outcome = ""
    if ($row.PSObject.Properties.Name -contains "machine_outcome") { $outcome = [string]$row.machine_outcome }
    if ($outcome.ToLowerInvariant() -ne "success") { continue }
    $remainingSuccesses += $row
}

# Group remaining by category|profile
$grouped = @{}
foreach ($row in $remainingSuccesses) {
    $cat = ""
    if ($row.PSObject.Properties.Name -contains "category") { $cat = [string]$row.category }
    if ([string]::IsNullOrWhiteSpace($cat)) { $cat = "unknown" }
    $prof = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $prof = [string]$row.profile }
    if ([string]::IsNullOrWhiteSpace($prof) -and $row.PSObject.Properties.Name -contains "profile_id") { $prof = [string]$row.profile_id }
    if ([string]::IsNullOrWhiteSpace($prof)) { $prof = "unknown" }
    $gk = "$cat|$prof"
    if (-not $grouped.ContainsKey($gk)) { $grouped[$gk] = @() }
    $grouped[$gk] += $row
}

foreach ($gk in $grouped.Keys) {
    $groupRows = @($grouped[$gk] | Sort-Object -Property fixture_id)
    $cnt = $groupRows.Count
    if ($cnt -eq 0) { continue }
    $sampleCount = [Math]::Ceiling($cnt * 0.10)
    if ($sampleCount -lt 1) { $sampleCount = 1 }
    if ($sampleCount -gt $cnt) { $sampleCount = $cnt }
    for ($i = 0; $i -lt $sampleCount; $i++) {
        $row = $groupRows[$i]
        $fid = ""
        if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$row.fixture_id }
        $prof = ""
        if ($row.PSObject.Properties.Name -contains "profile") { $prof = [string]$row.profile }
        if ([string]::IsNullOrWhiteSpace($prof) -and $row.PSObject.Properties.Name -contains "profile_id") { $prof = [string]$row.profile_id }
        $ah = ""
        if ($row.PSObject.Properties.Name -contains "artifact_hash") { $ah = [string]$row.artifact_hash }
        $key = "$fid|$prof|$ah"
        if (-not $mustKeys.Contains($key)) {
            [void]$mustKeys.Add($key)
            $mustRows += $row
            $rowKeyToReason[$key] = "sampled-10pct"
        }
    }
}

# Sort mustRows deterministically by fixture_id, profile
$sortedMust = @($mustRows | Sort-Object -Property @{ Expression = { [string]$_.fixture_id }; Ascending = $true }, @{ Expression = { if ($_.PSObject.Properties.Name -contains "profile") { [string]$_.profile } elseif ($_.PSObject.Properties.Name -contains "profile_id") { [string]$_.profile_id } else { "" } }; Ascending = $true })

# Load existing queue for preservation
$existingMap = @{}
if (Test-Path -LiteralPath $queuePathOut) {
    try {
        $existingRows = @(Import-Csv -LiteralPath $queuePathOut)
        foreach ($er in $existingRows) {
            $fidE = ""
            if ($er.PSObject.Properties.Name -contains "fixture_id") { $fidE = [string]$er.fixture_id }
            $profE = ""
            if ($er.PSObject.Properties.Name -contains "profile_id") { $profE = [string]$er.profile_id }
            if ([string]::IsNullOrWhiteSpace($profE) -and $er.PSObject.Properties.Name -contains "profile") { $profE = [string]$er.profile }
            $ahE = ""
            if ($er.PSObject.Properties.Name -contains "artifact_hash") { $ahE = [string]$er.artifact_hash }
            $keyE = "$fidE|$profE|$ahE"
            $existingMap[$keyE] = $er
        }
    } catch {
    }
}

# Build queue rows to write
$queueOut = @()
foreach ($row in $sortedMust) {
    $fid = ""
    if ($row.PSObject.Properties.Name -contains "fixture_id") { $fid = [string]$row.fixture_id }
    $prof = ""
    if ($row.PSObject.Properties.Name -contains "profile") { $prof = [string]$row.profile }
    if ([string]::IsNullOrWhiteSpace($prof) -and $row.PSObject.Properties.Name -contains "profile_id") { $prof = [string]$row.profile_id }
    $ah = ""
    if ($row.PSObject.Properties.Name -contains "artifact_hash") { $ah = [string]$row.artifact_hash }
    $runId = ""
    if ($row.PSObject.Properties.Name -contains "run_id") { $runId = [string]$row.run_id }
    $key = "$fid|$prof|$ah"
    $reason = $rowKeyToReason[$key]
    if ($null -eq $reason) { $reason = "" }

    $reviewLabel = "not-reviewed"
    $reviewer = ""
    $reviewedAt = ""
    $notes = $reason
    if ($existingMap.ContainsKey($key)) {
        $ex = $existingMap[$key]
        # Preserve only if hash unchanged (key includes hash, so unchanged)
        $exLabel = ""
        if ($ex.PSObject.Properties.Name -contains "review_label") { $exLabel = [string]$ex.review_label }
        $exReviewer = ""
        if ($ex.PSObject.Properties.Name -contains "reviewer") { $exReviewer = [string]$ex.reviewer }
        $exAt = ""
        if ($ex.PSObject.Properties.Name -contains "reviewed_at_utc") { $exAt = [string]$ex.reviewed_at_utc }
        $exNotes = ""
        if ($ex.PSObject.Properties.Name -contains "notes") { $exNotes = [string]$ex.notes }
        # Only preserve if existing label not empty
        if (-not [string]::IsNullOrWhiteSpace($exLabel)) {
            $reviewLabel = $exLabel
        }
        $reviewer = $exReviewer
        $reviewedAt = $exAt
        # Preserve notes if existing notes non-empty, otherwise use reason
        if (-not [string]::IsNullOrWhiteSpace($exNotes)) {
            $notes = $exNotes
        }
    }

    $obj = [PSCustomObject]@{
        run_id = $runId
        fixture_id = $fid
        profile_id = $prof
        artifact_hash = $ah
        review_label = $reviewLabel
        reviewer = $reviewer
        reviewed_at_utc = $reviewedAt
        notes = $notes
    }
    $queueOut += $obj
}

# Ensure output directory exists
$outDir = Split-Path -Parent $queuePathOut
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# Write CSV UTF8
$queueOut | Export-Csv -LiteralPath $queuePathOut -NoTypeInformation -Encoding UTF8

Write-Output "review-queue written: $queuePathOut with $($queueOut.Count) rows from $($allRows.Count) total"

