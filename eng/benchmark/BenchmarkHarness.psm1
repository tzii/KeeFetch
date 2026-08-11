function Test-KeeFetchCorpus {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$CsvPath,
          [Parameter(Mandatory=$true)][string]$VocabularyPath)

    $vocabulary = Get-Content -Raw -LiteralPath $VocabularyPath | ConvertFrom-Json
    $allowedClasses = @($vocabulary.expected_classes)
    $allowedCategories = @($vocabulary.categories.PSObject.Properties.Name)
    $rows = @(Import-Csv -LiteralPath $CsvPath)
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

    foreach ($row in $rows) {
        if ([string]::IsNullOrWhiteSpace($row.fixture_id)) { throw 'Missing fixture_id.' }
        if (-not $seen.Add($row.fixture_id)) { throw "Duplicate fixture_id: $($row.fixture_id)" }
        if ($allowedCategories -notcontains $row.category) { throw "Unknown category: $($row.category)" }
        if ($allowedClasses -notcontains $row.expected_class) { throw "Unknown expected_class: $($row.expected_class)" }
        $uri = $null
        if (-not [Uri]::TryCreate($row.input_url, [UriKind]::Absolute, [ref]$uri)) { throw "Invalid absolute URL: $($row.input_url)" }
        if ($uri.IsLoopback -or $uri.HostNameType -eq [UriHostNameType]::IPv4 -or $uri.HostNameType -eq [UriHostNameType]::IPv6) {
            throw "Fixture targets a private or loopback host: $($row.fixture_id)"
        }
        if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) { throw "Fixture contains credentials: $($row.fixture_id)" }
    }

    # Quota enforcement: every actual count must equal vocabulary quota and total >= minimum_total
    $counts = @{}
    foreach ($cat in $allowedCategories) { $counts[$cat] = 0 }
    foreach ($row in $rows) { $counts[$row.category]++ }
    foreach ($cat in $allowedCategories) {
        $expected = [int]$vocabulary.categories.$cat
        $actual = [int]$counts[$cat]
        if ($actual -ne $expected) {
            throw "Quota mismatch for category '$cat': expected $expected but got $actual."
        }
    }
    $minimumTotal = [int]$vocabulary.minimum_total
    if ($rows.Count -lt $minimumTotal) {
        throw "Total corpus size $($rows.Count) is below minimum_total $minimumTotal."
    }

    return $rows
}

function Get-KeeFetchCommitInternal {
    param([string]$ExplicitCommit)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitCommit)) {
        return $ExplicitCommit
    }
    try {
        $output = & git rev-parse HEAD 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($output)) {
            $trimmed = $output.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                return $trimmed
            }
        }
    } catch {
    }
    return "unknown"
}

function Write-JsonFileUtf8NoBom {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][object]$Object,
        [int]$Depth = 20
    )
    $json = ConvertTo-Json -InputObject $Object -Depth $Depth
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function New-KeeFetchRun {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$OutputRoot,
        [Parameter(Mandatory=$false)][string]$ExperimentId = "benchmark",
        [Parameter(Mandatory=$false)][string]$CorpusPath = "",
        [Parameter(Mandatory=$false)][string]$CorpusVersion = "v1",
        [Parameter(Mandatory=$false)][string]$NetworkContext = "default",
        [Parameter(Mandatory=$false)][int]$Concurrency = 8,
        [Parameter(Mandatory=$false)][string]$CacheMode = "cold",
        [Parameter(Mandatory=$false)][string[]]$Profiles = @(),
        [Parameter(Mandatory=$false)][int]$Repetitions = 1,
        [Parameter(Mandatory=$false)][string]$Commit = "",
        [Parameter(Mandatory=$false)][string[]]$CacheModes = @(),
        [Parameter(Mandatory=$false)][hashtable]$ExtraMetadata = $null
    )

    if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
        throw "OutputRoot must not be empty."
    }

    # Ensure output root exists
    if (-not (Test-Path -LiteralPath $OutputRoot)) {
        New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
    }

    $runId = "run_" + (Get-Date -Format "yyyyMMdd_HHmmss") + "_" + ([Guid]::NewGuid().ToString("N").Substring(0,8))
    $timestamp = (Get-Date).ToUniversalTime().ToString("o")
    $commitValue = Get-KeeFetchCommitInternal -ExplicitCommit $Commit

    $runDir = Join-Path $OutputRoot $runId
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null

    $metadata = [ordered]@{
        run_id = $runId
        timestamp = $timestamp
        commit = $commitValue
        corpus_version = $CorpusVersion
        corpus_path = $CorpusPath
        network_context = $NetworkContext
        concurrency = $Concurrency
        cache_mode = $CacheMode
        cache_modes = $CacheModes
        experiment_id = $ExperimentId
        profiles = $Profiles
        repetitions = $Repetitions
        status = "incomplete"
        output_root = $OutputRoot
        directory = $runDir
    }

    if ($null -ne $ExtraMetadata) {
        foreach ($k in $ExtraMetadata.Keys) {
            $metadata[$k] = $ExtraMetadata[$k]
        }
    }

    $runJsonPath = Join-Path $runDir "run.json"
    $resultsPath = Join-Path $runDir "results.ndjson"

    # Write run.json with status incomplete
    Write-JsonFileUtf8NoBom -Path $runJsonPath -Object $metadata -Depth 20

    # Init empty results.ndjson (UTF-8 without BOM, no truncation of existing if any)
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    if (-not (Test-Path -LiteralPath $resultsPath)) {
        [System.IO.File]::WriteAllText($resultsPath, "", $utf8NoBom)
    } else {
        # ensure existence but keep content (resume scenario not here)
        if ((Get-Item -LiteralPath $resultsPath).Length -eq 0) {
            [System.IO.File]::WriteAllText($resultsPath, "", $utf8NoBom)
        }
    }

    $result = [PSCustomObject]@{
        RunId = $runId
        Directory = $runDir
        RunJsonPath = $runJsonPath
        ResultsPath = $resultsPath
        Metadata = $metadata
    }
    $result | Add-Member -NotePropertyName "run_id" -NotePropertyValue $runId -Force
    $result | Add-Member -NotePropertyName "directory" -NotePropertyValue $runDir -Force

    # Add note property for compatibility
    $result | Add-Member -NotePropertyName "OutputRoot" -NotePropertyValue $OutputRoot -ErrorAction SilentlyContinue
    return $result
}

function Add-KeeFetchResult {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$RunDirectory,
        [Parameter(Mandatory=$false)][object]$Result,
        [Parameter(Mandatory=$false)][string]$FixtureId,
        [Parameter(Mandatory=$false)][string]$Category,
        [Parameter(Mandatory=$false)][string]$InputUrl,
        [Parameter(Mandatory=$false)][string]$SelectedProvider,
        [Parameter(Mandatory=$false)][string]$Tier,
        [Parameter(Mandatory=$false)][object]$IsSynthetic,
        [Parameter(Mandatory=$false)][object]$PlaceholderSuspected,
        [Parameter(Mandatory=$false)][object]$BlankSuspected,
        [Parameter(Mandatory=$false)][string]$MachineOutcome,
        [Parameter(Mandatory=$false)][object]$CandidateCounts,
        [Parameter(Mandatory=$false)][object]$PerProviderMetrics,
        [Parameter(Mandatory=$false)][object]$ProviderMetrics,
        [Parameter(Mandatory=$false)][int]$TotalElapsedMs = -1,
        [Parameter(Mandatory=$false)][int]$TotalElapsed = -1,
        [Parameter(Mandatory=$false)][string]$CacheBehavior,
        [Parameter(Mandatory=$false)][object]$CacheHit,
        [Parameter(Mandatory=$false)][object]$Coalesced,
        [Parameter(Mandatory=$false)][object]$Coalescing,
        [Parameter(Mandatory=$false)][string]$ImageType,
        [Parameter(Mandatory=$false)][int]$ImageWidth = -1,
        [Parameter(Mandatory=$false)][int]$ImageHeight = -1,
        [Parameter(Mandatory=$false)][int]$ImageByteSize = -1,
        [Parameter(Mandatory=$false)][string]$ImageValidation,
        [Parameter(Mandatory=$false)][string]$ArtifactPath,
        [Parameter(Mandatory=$false)][string]$ArtifactHash,
        [Parameter(Mandatory=$false)][string]$ExperimentId,
        [Parameter(Mandatory=$false)][string]$Profile,
        [Parameter(Mandatory=$false)][int]$Repetition = -1,
        [Parameter(Mandatory=$false)][string]$RunId,
        [Parameter(Mandatory=$false)][string]$Timestamp,
        [Parameter(Mandatory=$false)][string]$Commit,
        [Parameter(Mandatory=$false)][string]$CorpusVersion,
        [Parameter(Mandatory=$false)][object]$CandidateCount,
        [Parameter(Mandatory=$false)][string]$NetworkContext,
        [Parameter(Mandatory=$false)][int]$Concurrency = -1,
        [Parameter(Mandatory=$false)][string]$CacheMode
    )

    if (-not (Test-Path -LiteralPath $RunDirectory)) {
        throw "RunDirectory does not exist: $RunDirectory"
    }

    $resultsPath = Join-Path $RunDirectory "results.ndjson"
    $runJsonPath = Join-Path $RunDirectory "run.json"

    # Load run metadata for defaults
    $runMeta = $null
    if (Test-Path -LiteralPath $runJsonPath) {
        try {
            $runMeta = Get-Content -Raw -LiteralPath $runJsonPath | ConvertFrom-Json
        } catch {
            $runMeta = $null
        }
    }

    # Build record from Result object if provided
    $record = [ordered]@{}
    if ($null -ne $Result) {
        if ($Result -is [System.Collections.IDictionary]) {
            foreach ($k in $Result.Keys) {
                $record[$k] = $Result[$k]
            }
        } elseif ($Result -is [PSCustomObject] -or $Result -is [PSObject]) {
            foreach ($prop in $Result.PSObject.Properties) {
                $record[$prop.Name] = $prop.Value
            }
        } else {
            # fallback: try to enumerate properties
            foreach ($prop in $Result.PSObject.Properties) {
                $record[$prop.Name] = $prop.Value
            }
        }
    }

    # Overlay explicitly bound parameters (if Result already had them, explicit wins)
    if ($PSBoundParameters.ContainsKey('RunId') -and -not [string]::IsNullOrWhiteSpace($RunId)) { $record['run_id'] = $RunId }
    if ($PSBoundParameters.ContainsKey('Timestamp') -and -not [string]::IsNullOrWhiteSpace($Timestamp)) { $record['timestamp'] = $Timestamp }
    if ($PSBoundParameters.ContainsKey('Commit') -and -not [string]::IsNullOrWhiteSpace($Commit)) { $record['commit'] = $Commit }
    if ($PSBoundParameters.ContainsKey('CorpusVersion') -and -not [string]::IsNullOrWhiteSpace($CorpusVersion)) { $record['corpus_version'] = $CorpusVersion }
    if ($PSBoundParameters.ContainsKey('FixtureId') -and -not [string]::IsNullOrWhiteSpace($FixtureId)) { $record['fixture_id'] = $FixtureId }
    if ($PSBoundParameters.ContainsKey('Category') -and -not [string]::IsNullOrWhiteSpace($Category)) { $record['category'] = $Category }
    if ($PSBoundParameters.ContainsKey('InputUrl') -and -not [string]::IsNullOrWhiteSpace($InputUrl)) { $record['input_url'] = $InputUrl }
    if ($PSBoundParameters.ContainsKey('SelectedProvider')) { $record['selected_provider'] = $SelectedProvider }
    if ($PSBoundParameters.ContainsKey('Tier') -and -not [string]::IsNullOrWhiteSpace($Tier)) { $record['tier'] = $Tier }
    if ($PSBoundParameters.ContainsKey('IsSynthetic')) { $record['is_synthetic'] = [bool]$IsSynthetic }
    if ($PSBoundParameters.ContainsKey('PlaceholderSuspected')) { $record['placeholder_suspected'] = [bool]$PlaceholderSuspected }
    if ($PSBoundParameters.ContainsKey('BlankSuspected')) { $record['blank_suspected'] = [bool]$BlankSuspected }
    if ($PSBoundParameters.ContainsKey('MachineOutcome') -and -not [string]::IsNullOrWhiteSpace($MachineOutcome)) { $record['machine_outcome'] = $MachineOutcome }
    if ($PSBoundParameters.ContainsKey('CandidateCounts') -and $null -ne $CandidateCounts) { $record['candidate_counts'] = $CandidateCounts }
    if ($PSBoundParameters.ContainsKey('CandidateCount') -and $null -ne $CandidateCount) {
        if (-not $record.Contains('candidate_counts')) { $record['candidate_counts'] = $CandidateCount }
    }
    if ($PSBoundParameters.ContainsKey('PerProviderMetrics') -and $null -ne $PerProviderMetrics) { $record['per_provider_metrics'] = $PerProviderMetrics }
    if ($PSBoundParameters.ContainsKey('ProviderMetrics') -and $null -ne $ProviderMetrics) {
        if ($record.Contains('per_provider_metrics')) {
            # keep both
            $record['provider_metrics'] = $ProviderMetrics
        } else {
            $record['per_provider_metrics'] = $ProviderMetrics
            $record['provider_metrics'] = $ProviderMetrics
        }
    }
    if ($PSBoundParameters.ContainsKey('TotalElapsedMs') -and $TotalElapsedMs -ge 0) { $record['total_elapsed_ms'] = $TotalElapsedMs; $record['total_elapsed'] = $TotalElapsedMs }
    if ($PSBoundParameters.ContainsKey('TotalElapsed') -and $TotalElapsed -ge 0) { $record['total_elapsed'] = $TotalElapsed; if (-not $record.Contains('total_elapsed_ms')) { $record['total_elapsed_ms'] = $TotalElapsed } }
    if ($PSBoundParameters.ContainsKey('CacheBehavior') -and -not [string]::IsNullOrWhiteSpace($CacheBehavior)) { $record['cache_behavior'] = $CacheBehavior }
    if ($PSBoundParameters.ContainsKey('CacheHit')) { $record['cache_hit'] = [bool]$CacheHit; if (-not $record.Contains('cache_behavior')) { if ([bool]$CacheHit) { $record['cache_behavior'] = "hit" } else { $record['cache_behavior'] = "miss" } } }
    if ($PSBoundParameters.ContainsKey('Coalesced')) { $record['coalesced'] = [bool]$Coalesced; $record['coalescing'] = [bool]$Coalesced }
    if ($PSBoundParameters.ContainsKey('Coalescing')) { $record['coalescing'] = [bool]$Coalescing; if (-not $record.Contains('coalesced')) { $record['coalesced'] = [bool]$Coalescing } }
    if ($PSBoundParameters.ContainsKey('ImageType')) { $record['image_type'] = $ImageType }
    if ($PSBoundParameters.ContainsKey('ImageWidth') -and $ImageWidth -ge 0) { $record['image_width'] = $ImageWidth }
    if ($PSBoundParameters.ContainsKey('ImageHeight') -and $ImageHeight -ge 0) { $record['image_height'] = $ImageHeight }
    if ($PSBoundParameters.ContainsKey('ImageByteSize') -and $ImageByteSize -ge 0) { $record['image_byte_size'] = $ImageByteSize }
    if ($PSBoundParameters.ContainsKey('ImageValidation')) { $record['image_validation'] = $ImageValidation }
    if ($PSBoundParameters.ContainsKey('ArtifactPath')) { $record['artifact_path'] = $ArtifactPath }
    if ($PSBoundParameters.ContainsKey('ArtifactHash')) { $record['artifact_hash'] = $ArtifactHash }
    if ($PSBoundParameters.ContainsKey('ExperimentId') -and -not [string]::IsNullOrWhiteSpace($ExperimentId)) { $record['experiment_id'] = $ExperimentId }
    if ($PSBoundParameters.ContainsKey('Profile') -and -not [string]::IsNullOrWhiteSpace($Profile)) { $record['profile'] = $Profile }
    if ($PSBoundParameters.ContainsKey('Repetition') -and $Repetition -ge 0) { $record['repetition'] = $Repetition }
    if ($PSBoundParameters.ContainsKey('NetworkContext') -and -not [string]::IsNullOrWhiteSpace($NetworkContext)) { $record['network_context'] = $NetworkContext }
    if ($PSBoundParameters.ContainsKey('Concurrency') -and $Concurrency -ge 0) { $record['concurrency'] = $Concurrency }
    if ($PSBoundParameters.ContainsKey('CacheMode') -and -not [string]::IsNullOrWhiteSpace($CacheMode)) { $record['cache_mode'] = $CacheMode }

    # Fill defaults for ALL spec §6.2 fields if missing
    if (-not $record.Contains('run_id')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'run_id') { $record['run_id'] = [string]$runMeta.run_id }
        elseif ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'runId') { $record['run_id'] = [string]$runMeta.runId }
        else { $record['run_id'] = [System.IO.Path]::GetFileName($RunDirectory) }
    }
    if (-not $record.Contains('timestamp')) { $record['timestamp'] = (Get-Date).ToUniversalTime().ToString("o") }
    if (-not $record.Contains('commit')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'commit') { $record['commit'] = [string]$runMeta.commit } else { $record['commit'] = "unknown" }
    }
    if (-not $record.Contains('corpus_version')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'corpus_version') { $record['corpus_version'] = [string]$runMeta.corpus_version } else { $record['corpus_version'] = "v1" }
    }
    if (-not $record.Contains('fixture_id')) { throw "Add-KeeFetchResult requires fixture_id." }
    if (-not $record.Contains('repetition')) { $record['repetition'] = 1 }
    if (-not $record.Contains('category')) { $record['category'] = "" }
    if (-not $record.Contains('input_url')) { $record['input_url'] = "" }
    if (-not $record.Contains('selected_provider')) { $record['selected_provider'] = "" }
    if (-not $record.Contains('tier')) { $record['tier'] = "Rejected" }
    if (-not $record.Contains('is_synthetic')) { $record['is_synthetic'] = $false }
    if (-not $record.Contains('placeholder_suspected')) { $record['placeholder_suspected'] = $false }
    if (-not $record.Contains('blank_suspected')) { $record['blank_suspected'] = $false }
    if (-not $record.Contains('machine_outcome')) { $record['machine_outcome'] = "harness-error" }
    if (-not $record.Contains('candidate_counts')) { $record['candidate_counts'] = @{} }
    if (-not $record.Contains('per_provider_metrics')) {
        if ($record.Contains('provider_metrics')) { $record['per_provider_metrics'] = $record['provider_metrics'] } else { $record['per_provider_metrics'] = @() }
    }
    if (-not $record.Contains('provider_metrics')) { $record['provider_metrics'] = $record['per_provider_metrics'] }
    if (-not $record.Contains('total_elapsed_ms')) {
        if ($record.Contains('total_elapsed')) { $record['total_elapsed_ms'] = [int]$record['total_elapsed'] } else { $record['total_elapsed_ms'] = 0 }
    }
    if (-not $record.Contains('total_elapsed')) { $record['total_elapsed'] = $record['total_elapsed_ms'] }
    if (-not $record.Contains('cache_behavior')) {
        if ($record.Contains('cache_hit')) {
            if ([bool]$record['cache_hit']) { $record['cache_behavior'] = "hit" } else { $record['cache_behavior'] = "miss" }
        } else { $record['cache_behavior'] = "miss" }
    }
    if (-not $record.Contains('cache_hit')) {
        if ($record['cache_behavior'] -eq "hit") { $record['cache_hit'] = $true } else { $record['cache_hit'] = $false }
    }
    if (-not $record.Contains('coalesced')) { $record['coalesced'] = $false }
    if (-not $record.Contains('coalescing')) { $record['coalescing'] = $record['coalesced'] }
    if (-not $record.Contains('image_type')) { $record['image_type'] = "" }
    if (-not $record.Contains('image_width')) { $record['image_width'] = 0 }
    if (-not $record.Contains('image_height')) { $record['image_height'] = 0 }
    if (-not $record.Contains('image_byte_size')) { $record['image_byte_size'] = 0 }
    if (-not $record.Contains('image_validation')) { $record['image_validation'] = "" }
    if (-not $record.Contains('artifact_path')) { $record['artifact_path'] = "" }
    if (-not $record.Contains('artifact_hash')) { $record['artifact_hash'] = "" }
    if (-not $record.Contains('experiment_id')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'experiment_id') { $record['experiment_id'] = [string]$runMeta.experiment_id } else { $record['experiment_id'] = "" }
    }
    if (-not $record.Contains('profile')) { $record['profile'] = "" }
    if (-not $record.Contains('network_context')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'network_context') { $record['network_context'] = [string]$runMeta.network_context } else { $record['network_context'] = "default" }
    }
    if (-not $record.Contains('concurrency')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'concurrency') { $record['concurrency'] = [int]$runMeta.concurrency } else { $record['concurrency'] = 8 }
    }
    if (-not $record.Contains('cache_mode')) {
        if ($null -ne $runMeta -and $runMeta.PSObject.Properties.Name -contains 'cache_mode') { $record['cache_mode'] = [string]$runMeta.cache_mode } else { $record['cache_mode'] = "cold" }
    }

    # Normalize candidate_counts to be an object/hashtable serializable
    # Ensure per_provider_metrics entries have required subfields
    $normalizedMetrics = @()
    $metricsArray = $record['per_provider_metrics']
    if ($metricsArray -is [System.Collections.IEnumerable] -and -not ($metricsArray -is [string])) {
        foreach ($m in $metricsArray) {
            $entry = [ordered]@{}
            if ($m -is [System.Collections.IDictionary]) {
                foreach ($k in $m.Keys) { $entry[$k] = $m[$k] }
            } elseif ($m -is [PSCustomObject] -or $m -is [PSObject]) {
                foreach ($p in $m.PSObject.Properties) { $entry[$p.Name] = $p.Value }
            } else {
                $entry['value'] = $m
            }
            if (-not $entry.Contains('provider') -and $entry.Contains('provider_name')) { $entry['provider'] = $entry['provider_name'] }
            if (-not $entry.Contains('calls')) { $entry['calls'] = 0 }
            if (-not $entry.Contains('elapsed')) {
                if ($entry.Contains('elapsed_ms')) { $entry['elapsed'] = $entry['elapsed_ms'] } else { $entry['elapsed'] = 0 }
            }
            if (-not $entry.Contains('elapsed_ms')) { $entry['elapsed_ms'] = $entry['elapsed'] }
            if (-not $entry.Contains('candidate_count')) { $entry['candidate_count'] = 0 }
            if (-not $entry.Contains('outcome')) { $entry['outcome'] = "" }
            if (-not $entry.Contains('errors')) { $entry['errors'] = 0 }
            $normalizedMetrics += [PSCustomObject]$entry
        }
    }
    $record['per_provider_metrics'] = $normalizedMetrics
    $record['provider_metrics'] = $normalizedMetrics

    # Resume deduplication: load completed (fixture_id, repetition) keys and skip
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    if (Test-Path -LiteralPath $resultsPath) {
        $existingLines = @()
        try {
            $existingLines = Get-Content -LiteralPath $resultsPath -Encoding UTF8 -ErrorAction SilentlyContinue
        } catch {
            $existingLines = @()
        }
        foreach ($line in $existingLines) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $obj = ConvertFrom-Json -InputObject $line -ErrorAction Stop
                $fid = ""
                if ($obj.PSObject.Properties.Name -contains 'fixture_id') { $fid = [string]$obj.fixture_id }
                $rep = "1"
                if ($obj.PSObject.Properties.Name -contains 'repetition') { $rep = [string]$obj.repetition }
                $key = "$fid|$rep"
                [void]$seen.Add($key)
            } catch {
                continue
            }
        }
    }

    $incomingFid = [string]$record['fixture_id']
    $incomingRep = [string]$record['repetition']
    $incomingKey = "$incomingFid|$incomingRep"
    if ($seen.Contains($incomingKey)) {
        return
    }

    # Append one compact JSON object per completed fixture to results.ndjson using UTF-8 without truncation
    $jsonLine = ConvertTo-Json -InputObject $record -Depth 20 -Compress
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::AppendAllText($resultsPath, $jsonLine + [Environment]::NewLine, $utf8NoBom)
}

function Complete-KeeFetchRun {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$RunDirectory
    )

    if (-not (Test-Path -LiteralPath $RunDirectory)) {
        throw "RunDirectory does not exist: $RunDirectory"
    }

    $resultsPath = Join-Path $RunDirectory "results.ndjson"
    $rowsCsvPath = Join-Path $RunDirectory "rows.csv"
    $summaryCsvPath = Join-Path $RunDirectory "summary.csv"
    $runJsonPath = Join-Path $RunDirectory "run.json"

    $records = @()
    if (Test-Path -LiteralPath $resultsPath) {
        $lines = @()
        try {
            $lines = Get-Content -LiteralPath $resultsPath -Encoding UTF8 -ErrorAction SilentlyContinue
        } catch {
            $lines = @()
        }
        $dedup = @{}
        foreach ($line in $lines) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $obj = ConvertFrom-Json -InputObject $line -ErrorAction Stop
                $fid = ""
                if ($obj.PSObject.Properties.Name -contains 'fixture_id') { $fid = [string]$obj.fixture_id }
                $rep = "1"
                if ($obj.PSObject.Properties.Name -contains 'repetition') { $rep = [string]$obj.repetition }
                $key = "$fid|$rep"
                if (-not $dedup.ContainsKey($key)) {
                    $dedup[$key] = $obj
                }
            } catch {
                continue
            }
        }
        $records = @($dedup.Values)
    }

    # Sort by fixture_id/repetition
    $sorted = @($records | Sort-Object -Property @{ Expression = { if ($_.PSObject.Properties.Name -contains 'fixture_id') { [string]$_.fixture_id } else { "" } }; Ascending = $true }, @{ Expression = { if ($_.PSObject.Properties.Name -contains 'repetition') { try { [int]$_.repetition } catch { 0 } } else { 1 } }; Ascending = $true })

    # Export rows.csv (all fields as columns)
    $preferredOrder = @('run_id','timestamp','commit','corpus_version','experiment_id','profile','fixture_id','repetition','category','input_url','selected_provider','tier','is_synthetic','placeholder_suspected','blank_suspected','machine_outcome','candidate_counts','per_provider_metrics','provider_metrics','total_elapsed_ms','total_elapsed','cache_behavior','cache_hit','coalesced','coalescing','image_type','image_width','image_height','image_byte_size','image_validation','artifact_path','artifact_hash','concurrency','cache_mode','network_context')

    if ($sorted.Count -eq 0) {
        # Create empty CSV with preferred headers
        $headerObj = [PSCustomObject]@{}
        foreach ($k in $preferredOrder) { $headerObj | Add-Member -NotePropertyName $k -NotePropertyValue "" }
        @($headerObj) | Export-Csv -LiteralPath $rowsCsvPath -NoTypeInformation -Encoding UTF8
        # Remove the dummy row, keep only header
        $content = Get-Content -LiteralPath $rowsCsvPath -Encoding UTF8
        if ($content.Count -gt 1) {
            $content | Select-Object -First 1 | Set-Content -LiteralPath $rowsCsvPath -Encoding UTF8
        }
    } else {
        $flattened = @()
        foreach ($rec in $sorted) {
            $flat = [ordered]@{}
            foreach ($prop in $rec.PSObject.Properties) {
                $name = $prop.Name
                $value = $prop.Value
                if ($null -eq $value) {
                    $flat[$name] = ""
                } elseif ($value -is [string]) {
                    $flat[$name] = $value
                } elseif ($value -is [bool]) {
                    $flat[$name] = $value
                } elseif ($value -is [ValueType]) {
                    $flat[$name] = $value
                } else {
                    try {
                        $flat[$name] = ConvertTo-Json -InputObject $value -Compress -Depth 10
                    } catch {
                        $flat[$name] = [string]$value
                    }
                }
            }
            $flattened += [PSCustomObject]$flat
        }

        $allKeysHash = @{}
        foreach ($f in $flattened) {
            foreach ($p in $f.PSObject.Properties.Name) {
                $allKeysHash[$p] = $true
            }
        }
        $orderedKeys = @()
        foreach ($k in $preferredOrder) {
            if ($allKeysHash.ContainsKey($k)) { $orderedKeys += $k }
        }
        $remaining = @($allKeysHash.Keys | Where-Object { $preferredOrder -notcontains $_ } | Sort-Object)
        $orderedKeys += $remaining

        $normalized = @()
        foreach ($f in $flattened) {
            $obj = [ordered]@{}
            foreach ($k in $orderedKeys) {
                if ($f.PSObject.Properties.Name -contains $k) { $obj[$k] = $f.$k } else { $obj[$k] = "" }
            }
            $normalized += [PSCustomObject]$obj
        }

        $normalized | Export-Csv -LiteralPath $rowsCsvPath -NoTypeInformation -Encoding UTF8
    }

    # Aggregate by experiment/profile into summary.csv
    $groups = @{}
    foreach ($rec in $sorted) {
        $exp = ""
        if ($rec.PSObject.Properties.Name -contains 'experiment_id') { $exp = [string]$rec.experiment_id }
        $prof = ""
        if ($rec.PSObject.Properties.Name -contains 'profile') { $prof = [string]$rec.profile }
        # fallback to experiment field
        if ([string]::IsNullOrWhiteSpace($exp) -and $rec.PSObject.Properties.Name -contains 'experiment') { $exp = [string]$rec.experiment }
        $key = "$exp|$prof"
        if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
        $groups[$key] += $rec
    }

    $summaryRows = @()
    if ($groups.Count -eq 0) {
        $summaryRows += [PSCustomObject]@{
            experiment_id = ""
            profile = ""
            total = 0
            success = 0
            not_found = 0
            invalid_image = 0
            timeout = 0
            provider_error = 0
            harness_error = 0
            synthetic = 0
            avg_elapsed_ms = 0
        }
    } else {
        foreach ($key in ($groups.Keys | Sort-Object)) {
            $groupRecords = $groups[$key]
            $parts = $key.Split('|')
            $exp = $parts[0]
            $prof = $parts[1]
            $total = $groupRecords.Count
            $success = 0
            $notFound = 0
            $invalidImage = 0
            $timeout = 0
            $providerError = 0
            $harnessError = 0
            $synthetic = 0
            $elapsedSum = 0
            $elapsedCount = 0
            foreach ($r in $groupRecords) {
                $outcome = ""
                if ($r.PSObject.Properties.Name -contains 'machine_outcome') { $outcome = [string]$r.machine_outcome }
                if ($outcome -eq "success") { $success++ }
                elseif ($outcome -eq "not-found") { $notFound++ }
                elseif ($outcome -eq "invalid-image") { $invalidImage++ }
                elseif ($outcome -eq "timeout") { $timeout++ }
                elseif ($outcome -eq "provider-error") { $providerError++ }
                elseif ($outcome -eq "harness-error") { $harnessError++ }

                $isSyn = $false
                if ($r.PSObject.Properties.Name -contains 'is_synthetic') {
                    try { $isSyn = [bool]$r.is_synthetic } catch { $isSyn = $false }
                }
                if ($isSyn) { $synthetic++ }

                $elapsed = 0
                $foundElapsed = $false
                if ($r.PSObject.Properties.Name -contains 'total_elapsed_ms') {
                    try { $elapsed = [long]$r.total_elapsed_ms; $foundElapsed = $true } catch { }
                }
                if (-not $foundElapsed -and $r.PSObject.Properties.Name -contains 'total_elapsed') {
                    try { $elapsed = [long]$r.total_elapsed; $foundElapsed = $true } catch { }
                }
                if ($foundElapsed) { $elapsedSum += $elapsed; $elapsedCount++ }
            }
            $avgElapsed = 0
            if ($elapsedCount -gt 0) { $avgElapsed = [int]($elapsedSum / $elapsedCount) }
            $summaryRows += [PSCustomObject]@{
                experiment_id = $exp
                profile = $prof
                total = $total
                success = $success
                not_found = $notFound
                invalid_image = $invalidImage
                timeout = $timeout
                provider_error = $providerError
                harness_error = $harnessError
                synthetic = $synthetic
                avg_elapsed_ms = $avgElapsed
            }
        }
    }

    $summaryRows | Export-Csv -LiteralPath $summaryCsvPath -NoTypeInformation -Encoding UTF8

    # Write final run.json with status: complete
    $runMeta = $null
    if (Test-Path -LiteralPath $runJsonPath) {
        try { $runMeta = Get-Content -Raw -LiteralPath $runJsonPath | ConvertFrom-Json } catch { $runMeta = $null }
    }
    $finalMeta = [ordered]@{}
    if ($null -ne $runMeta) {
        foreach ($p in $runMeta.PSObject.Properties) { $finalMeta[$p.Name] = $p.Value }
    } else {
        $finalMeta['run_id'] = [System.IO.Path]::GetFileName($RunDirectory)
        $finalMeta['timestamp'] = (Get-Date).ToUniversalTime().ToString("o")
        $finalMeta['commit'] = "unknown"
        $finalMeta['corpus_version'] = "v1"
        $finalMeta['status'] = "incomplete"
    }
    $finalMeta['status'] = "complete"
    $finalMeta['completed_at'] = (Get-Date).ToUniversalTime().ToString("o")
    $finalMeta['total_records'] = $sorted.Count
    # Ensure directory field present
    $finalMeta['directory'] = $RunDirectory

    Write-JsonFileUtf8NoBom -Path $runJsonPath -Object $finalMeta -Depth 20
}

Export-ModuleMember -Function Test-KeeFetchCorpus, New-KeeFetchRun, Add-KeeFetchResult, Complete-KeeFetchRun
