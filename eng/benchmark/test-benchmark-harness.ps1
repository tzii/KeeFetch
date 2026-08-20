$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BenchmarkHarness.psm1') -Force
$vocabPath = Join-Path $PSScriptRoot '..\..\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json'
$vocab = Get-Content -Raw -LiteralPath $vocabPath | ConvertFrom-Json
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('keefetch-benchmark-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    # Test 1: duplicate/private rejection (original)
    $csv = Join-Path $temp 'invalid.csv'
    @(
        [pscustomobject]@{ fixture_id='dup'; category='global-brand'; input_url='https://example.com'; expected_class='usable-icon'; expected_host='example.com'; review_required='false'; notes='' },
        [pscustomobject]@{ fixture_id='dup'; category='global-brand'; input_url='http://localhost'; expected_class='usable-icon'; expected_host='localhost'; review_required='false'; notes='' }
    ) | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
    $failed = $false
    try { Test-KeeFetchCorpus -CsvPath $csv -VocabularyPath $vocabPath }
    catch { $failed = $_.Exception.Message -match 'Duplicate fixture_id|private or loopback' }
    if (-not $failed) { throw 'Invalid corpus was accepted (duplicate/private check failed).' }

    # Helper: build a valid corpus matching quotas
    function New-ValidCorpusCsv {
        param([string]$Path)
        $rows = @()
        $id = 1
        foreach ($prop in $vocab.categories.PSObject.Properties) {
            $cat = $prop.Name
            $count = [int]$prop.Value
            for ($i = 0; $i -lt $count; $i++) {
                $fid = ('pub-{0:D3}' -f $id)
                $url = "https://example.com/$cat/$i"
                # pick a valid class: use first allowed class for simplicity
                $cls = $vocab.expected_classes[0]
                if ($cat -eq 'android-app') { $url = "androidapp://com.example.$i"; $cls = 'android-map' }
                elseif ($cat -eq 'missing-or-invalid') { $cls = 'graceful-not-found' }
                elseif ($cat -eq 'deduplication') { $cls = 'deduplicate' }
                $rows += [pscustomobject]@{
                    fixture_id = $fid; category = $cat; input_url = $url
                    expected_class = $cls; expected_host = 'example.com'
                    review_required = 'false'; notes = "test $cat $i"
                }
                $id++
            }
        }
        $rows | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
        return $rows
    }

    # Test 2: valid corpus passes quota enforcement
    $validCsv = Join-Path $temp 'valid.csv'
    New-ValidCorpusCsv -Path $validCsv | Out-Null
    try {
        $result = Test-KeeFetchCorpus -CsvPath $validCsv -VocabularyPath $vocabPath
        if ($result.Count -lt $vocab.minimum_total) { throw "Valid corpus returned fewer rows than minimum_total." }
    } catch {
        throw "Valid corpus was rejected: $($_.Exception.Message)"
    }

    # Test 3: wrong quotas must be rejected (remove one global-brand row -> 59 vs 60)
    $wrongCsv = Join-Path $temp 'wrong-quota.csv'
    $validRows = @(Import-Csv -LiteralPath $validCsv)
    # remove last global-brand row by filtering out one specific fixture_id
    $firstGlobal = ($validRows | Where-Object { $_.category -eq 'global-brand' } | Select-Object -First 1).fixture_id
    $wrongRows = @($validRows | Where-Object { $_.fixture_id -ne $firstGlobal })
    $wrongRows | Export-Csv -LiteralPath $wrongCsv -NoTypeInformation -Encoding UTF8
    $quotaRejected = $false
    try { Test-KeeFetchCorpus -CsvPath $wrongCsv -VocabularyPath $vocabPath | Out-Null }
    catch { $quotaRejected = $_.Exception.Message -match 'Quota mismatch|below minimum_total' }
    if (-not $quotaRejected) { throw 'Wrong-quota corpus was accepted (quota enforcement missing).' }

    # Test 4: total below minimum must also be rejected even if per-category would otherwise match relaxed check
    # Already covered by Test 3 (total 299 < 300). Explicitly verify minimum_total path with empty file
    $emptyCsv = Join-Path $temp 'empty.csv'
    @([pscustomobject]@{ fixture_id='pub-001'; category='global-brand'; input_url='https://example.com'; expected_class='usable-icon'; expected_host='example.com'; review_required='false'; notes='x' }) | Export-Csv -LiteralPath $emptyCsv -NoTypeInformation -Encoding UTF8
    $emptyRejected = $false
    try { Test-KeeFetchCorpus -CsvPath $emptyCsv -VocabularyPath $vocabPath | Out-Null }
    catch { $emptyRejected = $_.Exception.Message -match 'Quota mismatch|below minimum_total' }
    if (-not $emptyRejected) { throw 'Empty/small corpus was accepted (minimum_total enforcement missing).' }

    # Test 5: checkpointed row-level benchmark output (Task 4) â€” fake data, no network
    $benchRoot = Join-Path $temp 'bench-runs'
    New-Item -ItemType Directory -Path $benchRoot -Force | Out-Null
    $run = New-KeeFetchRun -OutputRoot $benchRoot -ExperimentId 'self-test' -CorpusVersion 'v1' -Concurrency 8 -CacheMode 'cold'

    function New-FakeResult {
        param([string]$FixtureId, [string]$Category, [string]$Url, [string]$Outcome = 'success', [int]$Repetition = 1)
        return [ordered]@{
            fixture_id = $FixtureId
            repetition = $Repetition
            category = $Category
            input_url = $Url
            selected_provider = 'Direct Site'
            tier = 'SiteCanonical'
            is_synthetic = $false
            placeholder_suspected = $false
            blank_suspected = $false
            machine_outcome = $Outcome
            candidate_counts = @{ 'Direct Site' = 2; 'Google' = 1 }
            per_provider_metrics = @(
                [ordered]@{ provider = 'Direct Site'; calls = 1; elapsed = 120; elapsed_ms = 120; candidate_count = 2; outcome = 'success'; errors = 0 },
                [ordered]@{ provider = 'Google'; calls = 1; elapsed = 90; elapsed_ms = 90; candidate_count = 1; outcome = 'success'; errors = 0 }
            )
            provider_metrics = @(
                [ordered]@{ provider = 'Direct Site'; calls = 1; elapsed = 120; elapsed_ms = 120; candidate_count = 2; outcome = 'success'; errors = 0 }
            )
            total_elapsed_ms = 210
            total_elapsed = 210
            cache_behavior = 'miss'
            cache_hit = $false
            coalesced = $false
            coalescing = $false
            image_type = 'png'
            image_width = 16
            image_height = 16
            image_byte_size = 1024
            image_validation = 'ok'
            artifact_path = 'artifacts/pub-001.png'
            artifact_hash = 'abc123'
            experiment_id = 'self-test'
            profile = 'Fast'
            network_context = 'default'
            concurrency = 8
            cache_mode = 'cold'
        }
    }

    $fake1 = New-FakeResult -FixtureId 'pub-001' -Category 'global-brand' -Url 'https://example.com/' -Outcome 'success' -Repetition 1
    $fake2 = New-FakeResult -FixtureId 'pub-002' -Category 'global-brand' -Url 'https://example.org/' -Outcome 'not-found' -Repetition 1

    Add-KeeFetchResult -RunDirectory $run.Directory -Result $fake1
    Add-KeeFetchResult -RunDirectory $run.Directory -Result $fake2

    Complete-KeeFetchRun -RunDirectory $run.Directory

    if (-not (Test-Path (Join-Path $run.Directory 'results.ndjson'))) { throw 'Missing NDJSON checkpoint.' }
    if (-not (Test-Path (Join-Path $run.Directory 'rows.csv'))) { throw 'Missing rows.csv.' }
    if (-not (Test-Path (Join-Path $run.Directory 'summary.csv'))) { throw 'Missing summary.csv.' }
    if (-not (Test-Path (Join-Path $run.Directory 'run.json'))) { throw 'Missing run.json.' }
    if ((Import-Csv (Join-Path $run.Directory 'rows.csv')).Count -ne 2) { throw 'Expected two finalized rows.' }

    # Resume test: second call with same fixture should not duplicate
    Add-KeeFetchResult -RunDirectory $run.Directory -Result $fake1
    Complete-KeeFetchRun -RunDirectory $run.Directory
    if ((Import-Csv (Join-Path $run.Directory 'rows.csv')).Count -ne 2) { throw 'Resume dedup failed: duplicate row was added.' }
    $ndjsonLines = @(Get-Content -LiteralPath (Join-Path $run.Directory 'results.ndjson') -Encoding UTF8 | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($ndjsonLines.Count -ne 2) { throw "NDJSON dedup failed: expected 2 lines but got $($ndjsonLines.Count)." }

    # Verify final status is complete and interrupted would stay incomplete is implicit: run.json now says complete
    $finalMeta = Get-Content -Raw -LiteralPath (Join-Path $run.Directory 'run.json') | ConvertFrom-Json
    if ($finalMeta.status -ne 'complete') { throw "Expected run.json status complete but got '$($finalMeta.status)'." }

    # Test 6: experiment-definition parsing (Task 5)
    $baselineExp = Join-Path $PSScriptRoot 'experiments/baseline-v12.json'
    $exp = Read-KeeFetchExperiment -ExperimentPath $baselineExp
    foreach ($field in @('experiment_id','corpus','profiles','repetitions','concurrency','cache_modes','output_root')) {
        if (-not ($exp.PSObject.Properties.Name -contains $field)) { throw "Experiment missing required property: $field" }
        $val = $exp.$field
        if ($null -eq $val) { throw "Experiment property $field is null." }
        if ($val -is [string] -and [string]::IsNullOrWhiteSpace($val)) { throw "Experiment property $field is empty." }
        if ($val -is [Array] -and $val.Count -eq 0) { throw "Experiment property $field is empty array." }
    }
    # Invalid cache mode must be rejected with message containing "Unknown cache mode"
    $badExp = Join-Path $temp 'bad-cache-mode.json'
    $badObj = [ordered]@{
        experiment_id = 'bad-cache'
        corpus = 'KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv'
        profiles = @('Balanced')
        repetitions = 1
        concurrency = 1
        cache_modes = @('hot')
        output_root = 'eng/benchmark-runs/bad-cache'
    }
    $badJson = ConvertTo-Json -InputObject $badObj -Depth 10
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($badExp, $badJson, $utf8NoBom)
    $badRejected = $false
    try { Read-KeeFetchExperiment -ExperimentPath $badExp | Out-Null }
    catch { $badRejected = $_.Exception.Message -match 'Unknown cache mode' }
    if (-not $badRejected) { throw 'Invalid cache mode was accepted or error did not contain Unknown cache mode.' }

    # Test 7: profile-candidates-v13 experiment + prepare-review + select-profiles (Task 5a)
    $candExp = Join-Path $PSScriptRoot 'experiments/profile-candidates-v13.json'
    if (-not (Test-Path -LiteralPath $candExp)) { throw "Missing profile-candidates-v13.json" }
    $cand = Get-Content -Raw -LiteralPath $candExp | ConvertFrom-Json
    if ($cand.experiment_id -ne "profile-candidates-v13") { throw "experiment_id mismatch: $($cand.experiment_id)" }
    if ($cand.corpus -ne "KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv") { throw "corpus mismatch" }
    if ($cand.repetitions -ne 3) { throw "repetitions should be 3" }
    if ($cand.concurrency -ne 8) { throw "concurrency should be 8" }
    if (@($cand.cache_modes).Count -ne 2 -or @($cand.cache_modes) -notcontains "cold" -or @($cand.cache_modes) -notcontains "warm") { throw "cache_modes should be cold,warm" }
    if ($cand.output_root -ne "eng/benchmark-runs/profile-candidates-v13") { throw "output_root mismatch" }
    $candidates = @($cand.candidates)
    if ($candidates.Count -lt 15 -or $candidates.Count -gt 20) { throw "candidates count must be 15-20 but got $($candidates.Count)" }
    # Check candidate authority: must have explicit providerIds + timeouts + synthetic, NOT Balanced mapping
    $idsSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($cc in $candidates) {
        if ([string]::IsNullOrWhiteSpace($cc.id)) { throw "candidate missing id" }
        if (-not $idsSet.Add([string]$cc.id)) { throw "duplicate candidate id: $($cc.id)" }
        if ($null -eq $cc.providerIds -or @($cc.providerIds).Count -eq 0) { throw "candidate $($cc.id) missing providerIds" }
        if ($null -eq $cc.primaryTimeout -or [int]$cc.primaryTimeout -le 0) { throw "candidate $($cc.id) missing primaryTimeout" }
        if ($null -eq $cc.fallbackTimeout -or [int]$cc.fallbackTimeout -le 0) { throw "candidate $($cc.id) missing fallbackTimeout" }
        if ($null -eq $cc.cumulativeTimeout -or [int]$cc.cumulativeTimeout -le 0) { throw "candidate $($cc.id) missing cumulativeTimeout" }
        if ($null -eq $cc.PSObject.Properties['allowSynthetic']) { throw "candidate $($cc.id) missing allowSynthetic" }
        # profiles list must contain candidate ids
        if (@($cand.profiles) -notcontains $cc.id) { throw "candidate $($cc.id) not in profiles list" }
    }
    # Verify required families exist
    $hasDirectOnly = $false
    $hasResolverPair = $false
    $hasResolverPlusFallback = $false
    $hasFullChain = $false
    $hasMinusOne = $false
    $hasFast = $false
    $hasBalanced = $false
    $hasThorough = $false
    foreach ($cc in $candidates) {
        $pids = @($cc.providerIds | ForEach-Object { ([string]$_).ToLowerInvariant() })
        if ($pids.Count -eq 1 -and $pids[0] -eq "direct-site") { $hasDirectOnly = $true }
        if ($pids.Count -eq 2 -and $pids[0] -eq "direct-site") { $hasResolverPair = $true }
        if ($pids.Count -eq 3 -and $pids[0] -eq "direct-site") { $hasResolverPlusFallback = $true }
        if ($pids.Count -eq 7) { $hasFullChain = $true }
        if ($pids.Count -eq 6) { $hasMinusOne = $true }
        if ([int]$cc.primaryTimeout -eq 4000 -and [int]$cc.fallbackTimeout -eq 2500 -and [int]$cc.cumulativeTimeout -eq 15000) { $hasFast = $true }
        if ([int]$cc.primaryTimeout -eq 6000 -and [int]$cc.fallbackTimeout -eq 3500 -and [int]$cc.cumulativeTimeout -eq 22000) { $hasBalanced = $true }
        if ([int]$cc.primaryTimeout -eq 10000 -and [int]$cc.fallbackTimeout -eq 5000 -and [int]$cc.cumulativeTimeout -eq 45000) { $hasThorough = $true }
    }
    if (-not $hasDirectOnly) { throw "candidates must include direct-site only" }
    if (-not $hasResolverPair) { throw "candidates must include direct-plus-one-resolver pairs" }
    if (-not $hasResolverPlusFallback) { throw "candidates must include direct-plus-one-resolver-plus-one-fallback combos" }
    if (-not $hasFullChain) { throw "candidates must include full-chain" }
    if (-not $hasMinusOne) { throw "candidates must include full-chain-minus-one" }
    if (-not $hasFast) { throw "candidates must include Fast budget (4000/2500/15000)" }
    if (-not $hasBalanced) { throw "candidates must include Balanced budget (6000/3500/22000)" }
    if (-not $hasThorough) { throw "candidates must include Thorough budget (10000/5000/45000)" }

    # Verify candidate authority doc in benchmark-presets
    $presetsPath = Join-Path $PSScriptRoot '..\benchmark-presets.ps1'
    if (-not (Test-Path -LiteralPath $presetsPath)) { $presetsPath = Join-Path $PSScriptRoot '..\..\benchmark-presets.ps1' }
    if (Test-Path -LiteralPath $presetsPath) {
        $presetsText = Get-Content -Raw -LiteralPath $presetsPath
        if ($presetsText.IndexOf("New-CustomConfigForCandidate") -lt 0) { throw "benchmark-presets.ps1 must document custom candidate path via New-CustomConfigForCandidate" }
        if ($presetsText.IndexOf("cand-") -lt 0) { throw "benchmark-presets.ps1 must handle cand- candidate authority" }
    }

    # ---- Fingerprinted mock experiment: prepare-review + select-profiles --
    function Get-TestSha256HexFile {
        param([string]$Path)
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $stream = [System.IO.File]::OpenRead($Path)
            try { $hash = $sha.ComputeHash($stream) } finally { $stream.Dispose() }
            return [BitConverter]::ToString($hash).Replace('-','').ToLowerInvariant()
        } finally { $sha.Dispose() }
    }

    function Get-TestCandidateFingerprint {
        param([object]$Def)
        $providerIds = @($Def.providerIds)
        $syn = '0'; if ([bool]$Def.allowSynthetic) { $syn = '1' }
        $stp = '0'; if ([bool]$Def.stopAfterStrongResolved) { $stp = '1' }
        $canonical = "v1|providers={0}|primaryMs={1}|fallbackMs={2}|cumulativeMs={3}|synthetic={4}|stopAfterStrongResolved={5}" -f `
            ($providerIds -join ','), [int]$Def.primaryTimeout, [int]$Def.fallbackTimeout, [int]$Def.cumulativeTimeout, $syn, $stp
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
            return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant()
        } finally { $sha.Dispose() }
    }

    $mockRoot = Join-Path $temp 'mock-select'
    New-Item -ItemType Directory -Path $mockRoot -Force | Out-Null

    # Mock corpus: 12 fixtures. The experiment records the absolute path so
    # the selector resolves the exact row count from it.
    $mockCorpus = Join-Path $mockRoot 'corpus.csv'
    $mockFixtureIds = @()
    for ($i = 1; $i -le 12; $i++) { $mockFixtureIds += ('mk-{0:D3}' -f $i) }
    $mockFixtureIds | ForEach-Object {
        [pscustomobject]@{
            fixture_id = $_; category = 'global-brand'; input_url = "https://example.com/$_"
            expected_class = 'usable-site-icon'; expected_host = 'example.com'
            review_required = 'false'; notes = 'mock'
        }
    } | Export-Csv -LiteralPath $mockCorpus -NoTypeInformation -Encoding UTF8

    $mockCandidateDefs = @(
        [ordered]@{ id = 'cand-direct-only-balanced'; providerIds = @('direct-site'); primaryTimeout = 6000; fallbackTimeout = 3500; cumulativeTimeout = 22000; allowSynthetic = $false; stopAfterStrongResolved = $true },
        [ordered]@{ id = 'cand-full-thorough-synth'; providerIds = @('direct-site','twenty-icons','duckduckgo','google','yandex','favicone','icon-horse'); primaryTimeout = 10000; fallbackTimeout = 5000; cumulativeTimeout = 45000; allowSynthetic = $true; stopAfterStrongResolved = $false },
        [ordered]@{ id = 'cand-direct-google-twenty-fast'; providerIds = @('direct-site','google','twenty-icons'); primaryTimeout = 4000; fallbackTimeout = 2500; cumulativeTimeout = 15000; allowSynthetic = $false; stopAfterStrongResolved = $true }
    )
    $mockCandidateIds = @($mockCandidateDefs | ForEach-Object { $_.id })
    $mockExpObj = [ordered]@{
        experiment_id = 'mock-select'
        corpus = $mockCorpus
        profiles = $mockCandidateIds
        schedule_seed = 42
        repetitions = 1
        concurrency = 2
        cache_modes = @('cold','warm')
        output_root = $mockRoot
        candidates = $mockCandidateDefs
    }
    $mockExpPath = Join-Path $mockRoot 'mock-select.json'
    $mockExpJson = ConvertTo-Json -InputObject $mockExpObj -Depth 10
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($mockExpPath, $mockExpJson, $utf8NoBom)

    $mockExpFp = Get-TestSha256HexFile -Path $mockExpPath
    $mockCorpusFp = Get-TestSha256HexFile -Path $mockCorpus
    $mockBinaryHash = 'deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef'
    # The selector verifies run provenance against the CURRENT execution
    # harness fingerprint; the mocks must carry the real one.
    $mockHarnessFp = Get-KeeFetchHarnessFingerprint -RepoRoot ((Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
    $mockPolicyFps = @{}
    foreach ($def in $mockCandidateDefs) { $mockPolicyFps[[string]$def.id] = Get-TestCandidateFingerprint -Def $def }

    function global:New-MockRow {
        param([string]$FixtureId, [string]$Profile, [string]$Outcome, [int]$Elapsed,
              [string]$Provider, [bool]$Synthetic = $false, [bool]$Placeholder = $false,
              [string]$CacheMode = 'cold', $Metrics = @())
        return [ordered]@{
            fixture_id = $FixtureId
            repetition = 1
            category = 'global-brand'
            input_url = "https://example.com/$FixtureId"
            selected_provider = $Provider
            tier = 'SiteCanonical'
            is_synthetic = $Synthetic
            placeholder_suspected = $Placeholder
            blank_suspected = $false
            machine_outcome = $Outcome
            candidate_counts = @{}
            per_provider_metrics = $Metrics
            provider_metrics = $Metrics
            total_elapsed_ms = $Elapsed
            total_elapsed = $Elapsed
            cache_behavior = 'miss'
            cache_hit = $false
            coalesced = $false
            coalescing = $false
            image_type = 'png'
            image_width = 16
            image_height = 16
            image_byte_size = 1024
            image_validation = 'ok'
            artifact_path = "artifacts/$FixtureId.png"
            artifact_hash = "hash-$FixtureId-$Profile"
            experiment_id = 'mock-select'
            profile = $Profile
            network_context = 'default'
            concurrency = 2
            cache_mode = $CacheMode
        }
    }

    # Outcomes per candidate (fixture index 0..11):
    #   fixtures 0..7  -> all three succeed via "Direct Site" (non-differing)
    #   fixture  8     -> direct-only "Direct Site", thorough "Google", fast "Direct Site" (differing)
    #   fixture  9     -> only thorough succeeds via "Google" (single resolved provider)
    #   fixtures 10..11-> all not-found
    # thorough additionally flags synthetic on fixture 5 and placeholder on 2.
    function New-MockRunDir {
        param([string]$CandId, [string]$Mode, [int]$Rep, [string]$Kind, [long]$ActiveMs)
        $def = $null
        foreach ($d in $mockCandidateDefs) { if ([string]$d.id -eq $CandId) { $def = $d } }
        $r = New-KeeFetchRun -OutputRoot $mockRoot -ExperimentId 'mock-select' -CorpusPath $mockCorpus `
            -CorpusVersion 'v1' -Concurrency 2 -CacheMode $Mode -Profiles @($CandId) -Repetitions 1 `
            -CacheModes @('cold','warm') -ExtraMetadata @{
                candidate_id = $CandId
                repetition = $Rep
                run_kind = $Kind
                policy_fingerprint = $mockPolicyFps[$CandId]
                experiment_fingerprint = $mockExpFp
                corpus_fingerprint = $mockCorpusFp
                binary_hash = $mockBinaryHash
                execution_harness_fingerprint = $mockHarnessFp
                schedule_seed = 42
            }
        for ($i = 0; $i -lt 12; $i++) {
            $fid = $mockFixtureIds[$i]
            $outcome = 'not-found'
            $provider = ''
            $synthetic = $false
            $placeholder = $false
            $elapsed = 100
            $metrics = @()
            if ($CandId -eq 'cand-direct-only-balanced') {
                if ($i -le 8) {
                    $outcome = 'success'; $provider = 'Direct Site'; $elapsed = 100
                    $metrics = @([ordered]@{ provider = 'Direct Site'; calls = 1; elapsed = 100; elapsed_ms = 100; candidate_count = 1; outcome = 'candidate'; errors = 0 })
                }
            } elseif ($CandId -eq 'cand-full-thorough-synth') {
                if ($i -le 9) {
                    $outcome = 'success'
                    $provider = 'Direct Site'
                    if ($i -ge 8) { $provider = 'Google' }
                    $elapsed = 300
                    if ($i -eq 5) { $synthetic = $true }
                    if ($i -eq 2) { $placeholder = $true }
                    $metrics = @([ordered]@{ provider = 'Google'; calls = 1; elapsed = 200; elapsed_ms = 200; candidate_count = 1; outcome = 'candidate'; errors = 0 })
                }
            } else {
                if ($i -le 8) {
                    $outcome = 'success'; $provider = 'Direct Site'; $elapsed = 40
                    $metrics = @([ordered]@{ provider = 'Twenty Icons'; calls = 1; elapsed = 30; elapsed_ms = 30; candidate_count = 1; outcome = 'candidate'; errors = 0 })
                }
            }
            $row = New-MockRow -FixtureId $fid -Profile $CandId -Outcome $outcome -Elapsed $elapsed `
                -Provider $provider -Synthetic $synthetic -Placeholder $placeholder -CacheMode $Mode -Metrics $metrics
            Add-KeeFetchResult -RunDirectory $r.Directory -Result $row
        }
        Complete-KeeFetchRun -RunDirectory $r.Directory -ActiveElapsedMs $ActiveMs -Resumed $false
        return $r
    }

    foreach ($cid in $mockCandidateIds) {
        [void](New-MockRunDir -CandId $cid -Mode 'cold' -Rep 1 -Kind 'measured' -ActiveMs 9000)
        # Warm-up rows carry absurd latencies: they must never leak into metrics.
        [void](New-MockRunDir -CandId $cid -Mode 'warm' -Rep 0 -Kind 'warmup' -ActiveMs 999999)
        [void](New-MockRunDir -CandId $cid -Mode 'warm' -Rep 1 -Kind 'measured' -ActiveMs 1500)
    }

    $prepPath = Join-Path $PSScriptRoot 'prepare-review.ps1'
    $selPath = Join-Path $PSScriptRoot 'select-profiles.ps1'
    foreach ($p in @($prepPath, $selPath)) {
        $errors = $null; $tokens = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($p, [ref]$tokens, [ref]$errors)
        if ($null -ne $errors -and $errors.Count -gt 0) { throw "$(Split-Path -Leaf $p) parse failed: $($errors[0].Message)" }
    }

    # Census: two generations must be byte-identical, and the queue must
    # contain exactly the unique cold (fixture, artifact hash) units - every
    # one of them, with no sampling anywhere.
    $queueA = Join-Path $mockRoot 'review-queue.csv'
    $queueB = Join-Path $mockRoot 'review-queue-b.csv'
    & $prepPath -RunDir $mockRoot -OutputPath $queueA | Out-Null
    & $prepPath -RunDir $mockRoot -OutputPath $queueB | Out-Null
    $bytesA = [System.IO.File]::ReadAllBytes($queueA)
    $bytesB = [System.IO.File]::ReadAllBytes($queueB)
    if (-not ($bytesA.Length -eq $bytesB.Length)) { throw 'prepare-review census is not deterministic (different sizes)' }
    for ($bi = 0; $bi -lt $bytesA.Length; $bi++) {
        if ($bytesA[$bi] -ne $bytesB[$bi]) { throw 'prepare-review census is not deterministic (content differs)' }
    }
    Remove-Item -LiteralPath $queueB -Force
    $rq = @(Import-Csv -LiteralPath $queueA)
    if ($rq.Count -eq 0) { throw 'review queue empty' }
    $unitKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($q in $rq) { [void]$unitKeys.Add("$($q.fixture_id)|$($q.artifact_hash)") }
    if ($unitKeys.Count -ne $rq.Count) { throw "review queue rows ($($rq.Count)) are not unique units ($($unitKeys.Count))" }
    $expectedCensus = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($d in @(Get-ChildItem -LiteralPath $mockRoot -Directory)) {
        $rj = Join-Path $d.FullName 'run.json'
        if (-not (Test-Path -LiteralPath $rj)) { continue }
        $rm = Get-Content -Raw -LiteralPath $rj | ConvertFrom-Json
        if ([string]$rm.run_kind -eq 'warmup') { continue }
        if ([string]$rm.cache_mode -ne 'cold') { continue }
        foreach ($cr in @(Import-Csv -LiteralPath (Join-Path $d.FullName 'rows.csv'))) {
            if (-not [string]::IsNullOrWhiteSpace([string]$cr.artifact_hash)) {
                [void]$expectedCensus.Add("$($cr.fixture_id)|$($cr.artifact_hash)")
            }
        }
    }
    if ($rq.Count -ne $expectedCensus.Count) {
        throw "census queue has $($rq.Count) rows but the cold evidence has $($expectedCensus.Count) unique units"
    }
    foreach ($k in @($expectedCensus)) {
        if (-not $unitKeys.Contains($k)) { throw "cold census unit missing from queue: $k" }
    }
    $mustRows = @($rq | Where-Object { $_.notes -match 'synthetic|placeholder|profile-differing' })
    if ($mustRows.Count -lt 3) { throw "expected flagged rows (synthetic/placeholder/profile-differing), got $($mustRows.Count)" }
    $sampledRows = @($rq | Where-Object { $_.notes -match 'sampled' })
    if ($sampledRows.Count -ne 0) { throw 'census queue must not contain any sampled units' }
    foreach ($col in @('fixture_id','artifact_hash','profiles','categories','occurrences','review_label','reviewer','reviewed_at_utc','notes')) {
        if ($rq[0].PSObject.Properties.Name -notcontains $col) { throw "review-queue missing column $col" }
    }

    # Validation must fail closed while rows remain not-reviewed. The
    # exception is asserted directly - no self-set success flags.
    $validateThrewOnUnreviewed = $false
    try {
        & $prepPath -RunDir $mockRoot -OutputPath $queueA -Validate 2>$null | Out-Null
    } catch { $validateThrewOnUnreviewed = $true }
    if (-not $validateThrewOnUnreviewed) { throw 'validate accepted a queue with not-reviewed rows' }

    # A fabricated artifact hash must be rejected with an exact-hash error.
    $badHashRows = @(Import-Csv -LiteralPath $queueA)
    $badHashRows[0].artifact_hash = 'fabricatedhash000000000000000000000000000000000000000000000000000000'
    $badHashRows[0].review_label = 'correct'
    $badHashRows[0].reviewer = 't'
    $badHashRows[0].reviewed_at_utc = '2026-01-01T00:00:00Z'
    $badHashPath = Join-Path $mockRoot 'bad-hash-queue.csv'
    $badHashRows | Export-Csv -LiteralPath $badHashPath -NoTypeInformation -Encoding UTF8
    $fabricatedRejected = $false
    try {
        & $prepPath -RunDir $mockRoot -OutputPath $badHashPath -Validate 2>$null | Out-Null
    } catch { $fabricatedRejected = $_.Exception.Message -match 'not a cold census unit|Required review unit missing' }
    if (-not $fabricatedRejected) { throw 'validate accepted a fabricated artifact hash' }

    # Happy labels: everything correct.
    $labeled = @(Import-Csv -LiteralPath $queueA)
    foreach ($l in $labeled) {
        $l.review_label = 'correct'
        $l.reviewer = 'test-reviewer'
        $l.reviewed_at_utc = '2026-01-01T00:00:00Z'
    }
    $labeled | Export-Csv -LiteralPath $queueA -NoTypeInformation -Encoding UTF8
    try {
        & $prepPath -RunDir $mockRoot -OutputPath $queueA -Validate | Out-Null
    } catch {
        throw "validate failed on a fully labeled queue: $($_.Exception.Message)"
    }

    # Selector: happy path. It must not mutate the repository without -Publish.
    $repoGeneratedBefore = Get-TestSha256HexFile -Path (Join-Path $PSScriptRoot '..\..\FetchProfiles\FetchProfileCatalog.Generated.cs')
    $selOut = Join-Path $mockRoot 'selection-out'
    & $selPath -RunDir $mockRoot -ReviewQueue $queueA -OutputDir $selOut -ExperimentFile $mockExpPath | Out-Null
    $repoGeneratedAfter = Get-TestSha256HexFile -Path (Join-Path $PSScriptRoot '..\..\FetchProfiles\FetchProfileCatalog.Generated.cs')
    if ($repoGeneratedBefore -ne $repoGeneratedAfter) { throw 'selector mutated FetchProfileCatalog.Generated.cs without -Publish' }

    foreach ($p in @('selection-summary.json','selection-report.md','FetchProfileCatalog.Generated.cs','v1.3-provider-study.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $selOut $p))) { throw "select-profiles missing output $p" }
    }
    $summary = Get-Content -Raw -LiteralPath (Join-Path $selOut 'selection-summary.json') | ConvertFrom-Json
    if ($summary.winners.privacy -ne 'cand-direct-only-balanced') { throw "privacy winner expected cand-direct-only-balanced, got $($summary.winners.privacy)" }
    # All-correct labels tie every candidate's estimated usable rate at 1.0,
    # so max-coverage resolves through the documented cold-p95 tie-break (the
    # fast chain has the lowest latency) and bulk-fast picks the same
    # eligible candidate by its smallest active batch.
    if ($summary.winners.'max-coverage' -ne 'cand-direct-google-twenty-fast') { throw "max-coverage winner expected cand-direct-google-twenty-fast, got $($summary.winners.'max-coverage')" }
    if ($summary.winners.'bulk-fast' -ne 'cand-direct-google-twenty-fast') { throw "bulk-fast winner expected cand-direct-google-twenty-fast, got $($summary.winners.'bulk-fast')" }
    if ([string]::IsNullOrWhiteSpace([string]$summary.winners.everyday)) { throw 'everyday winner missing' }

    # Machine availability and human usability stay separate: with every
    # reviewed label correct, usable is 100% while coverage equals machine
    # availability, and warm-up latencies (999999ms) never leak into metrics.
    $thoroughStats = @($summary.candidates | Where-Object { $_.profile_id -eq 'cand-full-thorough-synth' })[0]
    if ([Math]::Abs([double]$thoroughStats.machine_availability - 10.0/12.0) -gt 0.001) { throw "thorough machine availability expected 10/12, got $($thoroughStats.machine_availability)" }
    if ([Math]::Abs([double]$thoroughStats.estimated_usable_rate - 1.0) -gt 0.001) { throw "thorough usable expected 1.0, got $($thoroughStats.estimated_usable_rate)" }
    if ([Math]::Abs([double]$thoroughStats.coverage - 10.0/12.0) -gt 0.001) { throw "coverage must equal availability x usability" }
    if ([int]$thoroughStats.cold_median_ms -ne 300) { throw "cold median leaked warm-up rows or wrong rows: $($thoroughStats.cold_median_ms)" }
    if ([int]$thoroughStats.active_batch_cold_ms -ne 9000) { throw "active cold batch expected 9000, got $($thoroughStats.active_batch_cold_ms)" }
    if ([double]$thoroughStats.third_party_disclosure_rate -le 0) { throw 'thorough disclosure rate must reflect observed Google calls' }

    # Commit-4 contract: census counts, no sampling fields, no Wilson output,
    # harness provenance recorded, cold-only denominators.
    if ([int]$thoroughStats.cold_rows -ne 12) { throw "scoring must be cold-only (expected 12 cold rows, got $($thoroughStats.cold_rows))" }
    if ([int]$thoroughStats.reviewed_units -ne 10) { throw "thorough reviewed census units expected 10, got $($thoroughStats.reviewed_units)" }
    if ([int]$thoroughStats.usable_units -ne 10) { throw "thorough usable units expected 10, got $($thoroughStats.usable_units)" }
    foreach ($removedField in @('sampled_covered','targeted_covered','weighted_reviewed_total')) {
        if ($thoroughStats.PSObject.Properties.Name -contains $removedField) { throw "summary still carries sampling field $removedField" }
    }
    if ([string]$summary.execution_harness_fingerprint -ne $mockHarnessFp) { throw 'summary must record the execution harness fingerprint of the evidence' }
    if ([int]$summary.census_units -ne $rq.Count) { throw "summary census_units ($($summary.census_units)) must equal the queue size ($($rq.Count))" }
    $evidenceText = Get-Content -Raw -LiteralPath (Join-Path $selOut 'v1.3-provider-study.md')
    if ($evidenceText -match 'Wilson') { throw 'evidence report still mentions Wilson intervals' }
    if ($evidenceText -notmatch 'CENSUS') { throw 'evidence report must document the census methodology' }
    if ($evidenceText -notmatch 'cold-only') { throw 'evidence report must document cold-only scoring' }

    # Generated descriptions must state policy facts for the actual winner.
    $csText = Get-Content -Raw -LiteralPath (Join-Path $selOut 'FetchProfileCatalog.Generated.cs')
    foreach ($mid in @('bulk-fast','everyday','privacy','max-coverage')) {
        if ($csText.IndexOf('"' + $mid + '"') -lt 0) { throw "generated CS missing role $mid" }
    }

    # Adversarial: wrong-brand labels on the fast chain's units must sink its
    # estimated usable rate without touching machine availability, flipping
    # max-coverage away from it. Under the census every unit is reviewed, so
    # no success is ever implicitly correct.
    $advRows = @(Import-Csv -LiteralPath $queueA)
    $fastUnits = @($advRows | Where-Object { $_.profiles -match 'cand-direct-google-twenty-fast' })
    if ($fastUnits.Count -eq 0) { throw ('expected fast-chain census units for the adversarial case; queue rows: ' + (($advRows | ForEach-Object { "$($_.fixture_id)|$($_.artifact_hash)|$($_.profiles)|$($_.notes)" }) -join ' ;; ')) }
    foreach ($s in $fastUnits) { $s.review_label = 'wrong-brand' }
    $advPath = Join-Path $mockRoot 'adv-queue.csv'
    $advRows | Export-Csv -LiteralPath $advPath -NoTypeInformation -Encoding UTF8
    $advOut = Join-Path $mockRoot 'adv-out'
    & $selPath -RunDir $mockRoot -ReviewQueue $advPath -OutputDir $advOut -ExperimentFile $mockExpPath | Out-Null
    $advSummary = Get-Content -Raw -LiteralPath (Join-Path $advOut 'selection-summary.json') | ConvertFrom-Json
    $advFast = @($advSummary.candidates | Where-Object { $_.profile_id -eq 'cand-direct-google-twenty-fast' })[0]
    $advThorough = @($advSummary.candidates | Where-Object { $_.profile_id -eq 'cand-full-thorough-synth' })[0]
    if ([Math]::Abs([double]$advThorough.machine_availability - 10.0/12.0) -gt 0.001) { throw 'adversarial case must not change machine availability' }
    if ([double]$advFast.estimated_usable_rate -ge 0.5) { throw "wrong-brand sample failed to sink the estimated usable rate: $($advFast.estimated_usable_rate)" }
    if ($advSummary.winners.'max-coverage' -eq 'cand-direct-google-twenty-fast') { throw 'unsampled successes were implicitly counted as correct (winner unchanged)' }

    # Ambiguity sensitivity: ambiguous mass on the current winner can flip
    # the max-coverage ranking between ambiguity-as-failure and
    # ambiguity-as-usable, which must reject the whole selection.
    $ambRows = @(Import-Csv -LiteralPath $queueA)
    foreach ($a in @($ambRows | Where-Object { $_.profiles -match 'cand-direct-google-twenty-fast' } | Select-Object -First 4)) {
        $a.review_label = 'ambiguous'
    }
    $ambPath = Join-Path $mockRoot 'amb-queue.csv'
    $ambRows | Export-Csv -LiteralPath $ambPath -NoTypeInformation -Encoding UTF8
    $ambThrew = $false
    $ambMessage = ''
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $ambPath -OutputDir (Join-Path $mockRoot 'amb-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
    } catch {
        $ambThrew = $true
        $ambMessage = $_.Exception.Message
    }
    if (-not $ambThrew) { throw 'selector accepted a selection that ambiguity can reverse' }
    if ($ambMessage -notmatch 'Ambiguity sensitivity') { throw "ambiguity rejection message unexpected: $ambMessage" }

    # Duplicate matrix cell must be rejected.
    $measuredDirs = @(Get-ChildItem -LiteralPath $mockRoot -Directory | Where-Object {
        (Test-Path (Join-Path $_.FullName 'run.json')) -and
        ((Get-Content -Raw (Join-Path $_.FullName 'run.json') | ConvertFrom-Json).run_kind -eq 'measured')
    })
    $dupSource = $measuredDirs[0]
    $dupTarget = Join-Path $mockRoot 'run_duplicate_cell'
    Copy-Item -LiteralPath $dupSource.FullName -Destination $dupTarget -Recurse
    $dupThrew = $false
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $queueA -OutputDir (Join-Path $mockRoot 'dup-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
    } catch { $dupThrew = $_.Exception.Message -match 'Duplicate matrix cell' }
    if (-not $dupThrew) { throw 'duplicate matrix cell was accepted' }
    Remove-Item -LiteralPath $dupTarget -Recurse -Force

    # Missing matrix cell must be rejected (the run must leave the run root,
    # otherwise discovery still finds it).
    $missingSource = $measuredDirs[1]
    $missingBackup = Join-Path $temp 'missing-backup'
    Move-Item -LiteralPath $missingSource.FullName -Destination $missingBackup
    $missingThrew = $false
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $queueA -OutputDir (Join-Path $mockRoot 'missing-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
    } catch { $missingThrew = $_.Exception.Message -match 'Missing matrix cells' }
    if (-not $missingThrew) { throw 'missing matrix cell was accepted' }
    Move-Item -LiteralPath $missingBackup -Destination $missingSource.FullName

    # A modified experiment file (fingerprint mismatch) must be rejected.
    $experimentBackup = Join-Path $mockRoot 'experiment-backup.json'
    Copy-Item -LiteralPath $mockExpPath -Destination $experimentBackup
    [System.IO.File]::WriteAllText($mockExpPath, $mockExpJson + [Environment]::NewLine, $utf8NoBom)
    $mismatchThrew = $false
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $queueA -OutputDir (Join-Path $mockRoot 'mismatch-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
    } catch { $mismatchThrew = $_.Exception.Message -match 'does not match the fingerprint' }
    if (-not $mismatchThrew) { throw 'experiment fingerprint mismatch was accepted' }
    Copy-Item -LiteralPath $experimentBackup -Destination $mockExpPath -Force

    # A review queue with a hole (a removed census unit) must fail closed.
    $holeRows = @(Import-Csv -LiteralPath $queueA)
    $holeRows = @($holeRows[0..($holeRows.Count - 2)])
    $holePath = Join-Path $mockRoot 'hole-queue.csv'
    $holeRows | Export-Csv -LiteralPath $holePath -NoTypeInformation -Encoding UTF8
    $holeThrew = $false
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $holePath -OutputDir (Join-Path $mockRoot 'hole-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
    } catch { $holeThrew = $_.Exception.Message -match 'Census unit missing from the review queue' }
    if (-not $holeThrew) { throw 'selector accepted a census with a hole (missing review unit)' }

    # A fabricated review key (not a cold census unit) must fail closed.
    $fabRows = @(Import-Csv -LiteralPath $queueA)
    $fabRows[0].artifact_hash = 'fabricated-selector-hash-0000000000000000000000000'
    $fabPath = Join-Path $mockRoot 'fab-queue.csv'
    $fabRows | Export-Csv -LiteralPath $fabPath -NoTypeInformation -Encoding UTF8
    $fabThrew = $false
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $fabPath -OutputDir (Join-Path $mockRoot 'fab-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
    } catch { $fabThrew = $_.Exception.Message -match 'not cold census units' }
    if (-not $fabThrew) { throw 'selector accepted a fabricated review key' }

    # Strict provider_metrics: each violation must reject the selection.
    $coldThoroughDir = @($measuredDirs | Where-Object {
        $m = Get-Content -Raw (Join-Path $_.FullName 'run.json') | ConvertFrom-Json
        return (([string]$m.candidate_id -eq 'cand-full-thorough-synth') -and ([string]$m.cache_mode -eq 'cold'))
    })[0]
    if ($null -eq $coldThoroughDir) { throw 'could not locate the thorough cold run for strict-parser tests' }
    $thoroughRowsCsv = Join-Path $coldThoroughDir.FullName 'rows.csv'
    $thoroughRowsBackup = [System.IO.File]::ReadAllBytes($thoroughRowsCsv)
    $strictCases = @(
        @{ Match = 'not parseable JSON'; Json = '{not json' },
        @{ Match = 'unknown provider';     Json = '[{"provider":"MysteryCorp","calls":1,"elapsed_ms":5,"candidate_count":1,"outcome":"candidate","errors":0}]' },
        @{ Match = 'unknown outcome';      Json = '[{"provider":"Google","calls":1,"elapsed_ms":5,"candidate_count":1,"outcome":"mystery","errors":0}]' },
        @{ Match = 'zero provider activity'; Json = '[]' }
    )
    try {
        foreach ($case in $strictCases) {
            [System.IO.File]::WriteAllBytes($thoroughRowsCsv, $thoroughRowsBackup)
            # Row 0 is fixture mk-001, a successful thorough fetch: every case
            # must trip the strict parser on it.
            $rows = @(Import-Csv -LiteralPath $thoroughRowsCsv)
            $rows[0].provider_metrics = $case.Json
            $rows | Export-Csv -LiteralPath $thoroughRowsCsv -NoTypeInformation -Encoding UTF8
            $strictThrew = $false
            try {
                & $selPath -RunDir $mockRoot -ReviewQueue $queueA -OutputDir (Join-Path $mockRoot ('strict-out-' + [Guid]::NewGuid().ToString('N').Substring(0,8))) -ExperimentFile $mockExpPath 2>$null | Out-Null
            } catch { $strictThrew = $_.Exception.Message -match $case.Match }
            if (-not $strictThrew) { throw "strict provider_metrics parser accepted a row violating: $($case.Match)" }
        }
    } finally {
        [System.IO.File]::WriteAllBytes($thoroughRowsCsv, $thoroughRowsBackup)
    }

    # Uniform but stale execution-harness fingerprints must fail closed: the
    # evidence must come from the harness that is selecting it.
    $allRunJsonPaths = @(Get-ChildItem -LiteralPath $mockRoot -Directory | ForEach-Object {
        $p = Join-Path $_.FullName 'run.json'
        if (Test-Path -LiteralPath $p) { $p }
    })
    $runJsonBackups = @{}
    foreach ($p in $allRunJsonPaths) { $runJsonBackups[$p] = [System.IO.File]::ReadAllBytes($p) }
    try {
        foreach ($p in $allRunJsonPaths) {
            $rm = Get-Content -Raw -LiteralPath $p | ConvertFrom-Json
            $rm.execution_harness_fingerprint = '0' * 64
            [System.IO.File]::WriteAllText($p, ($rm | ConvertTo-Json -Depth 20), (New-Object System.Text.UTF8Encoding $false))
        }
        $fpThrew = $false
        try {
            & $selPath -RunDir $mockRoot -ReviewQueue $queueA -OutputDir (Join-Path $mockRoot 'fp-out') -ExperimentFile $mockExpPath 2>$null | Out-Null
        } catch { $fpThrew = $_.Exception.Message -match 'different execution harness' }
        if (-not $fpThrew) { throw 'selector accepted evidence produced by a different execution harness' }
    } finally {
        foreach ($p in $allRunJsonPaths) { [System.IO.File]::WriteAllBytes($p, $runJsonBackups[$p]) }
    }

    # Shared JSON escaping: quotes, backslashes, and control characters must
    # round-trip through ConvertTo-KeeFetchJsonString.
    $escInput = "quote`"back\\slash`ttab`nline`rcr" + [string][char]1 + [string][char]31
    $escaped = ConvertTo-KeeFetchJsonString -Value $escInput
    if ($escaped -notmatch '^".*"$') { throw "escaped value must be quoted: $escaped" }
    if ($escaped.IndexOf('"""') -ge 0) { throw 'unescaped quote in JSON string' }
    foreach ($seq in @('\"', '\\', '\t', '\n', '\r', '\u0001', '\u001f')) {
        if ($escaped.IndexOf($seq) -lt 0) { throw "expected escape sequence $seq missing from: $escaped" }
    }
    $reparsed = (ConvertFrom-Json ('{"v": ' + $escaped + '}')).v
    if ($reparsed -ne $escInput) { throw "escape round-trip failed: [$reparsed] vs [$escInput]" }

    # ---- Test 8: seeded schedule ordering (deterministic, seeded) ----------
    $schedItems = @('a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r')
    $orderA = Get-SeededScheduleOrder -Items $schedItems -Seed 20260817 -Phase 'cold' -Repetition 1
    $orderB = Get-SeededScheduleOrder -Items $schedItems -Seed 20260817 -Phase 'cold' -Repetition 1
    if (($orderA -join '|') -ne ($orderB -join '|')) { throw 'Get-SeededScheduleOrder is not deterministic for identical inputs.' }
    if ((@($orderA | Sort-Object) -join '|') -ne (@($schedItems | Sort-Object) -join '|')) {
        throw 'Seeded schedule order is not a permutation of the input items.'
    }
    $orderRep2 = Get-SeededScheduleOrder -Items $schedItems -Seed 20260817 -Phase 'cold' -Repetition 2
    $orderWarm = Get-SeededScheduleOrder -Items $schedItems -Seed 20260817 -Phase 'warm' -Repetition 0
    $orderSeed2 = Get-SeededScheduleOrder -Items $schedItems -Seed 999 -Phase 'cold' -Repetition 1
    if (($orderA -join '|') -eq ($orderRep2 -join '|')) { throw 'Repetition must influence the seeded schedule.' }
    if (($orderA -join '|') -eq ($orderWarm -join '|')) { throw 'Phase must influence the seeded schedule.' }
    if (($orderA -join '|') -eq ($orderSeed2 -join '|')) { throw 'Seed must influence the seeded schedule.' }
    $single = @(Get-SeededScheduleOrder -Items @('only') -Seed 1 -Phase 'cold' -Repetition 1)
    if ($single.Count -ne 1 -or [string]$single[0] -ne 'only') { throw 'Single-item schedule must return exactly that item.' }

    # ---- Test 9: atomic warm-block validation -------------------------------
    function New-WarmRunState {
        param([string]$Status, [int]$Repetition)
        return [PSCustomObject]@{ Status = $Status; Repetition = $Repetition }
    }
    function Assert-WarmBlockInvalid {
        param([array]$Warmups, [array]$Measured, [int]$Reps, [string]$Pattern, [string]$Scenario)
        $reason = $null
        try {
            $reason = Get-WarmBlockInvalidReason -Warmups $Warmups -Measured $Measured -Repetitions $Reps
        } catch {
            throw "Get-WarmBlockInvalidReason threw for scenario '$Scenario': $($_.Exception.Message)"
        }
        if ([string]::IsNullOrWhiteSpace([string]$reason)) { throw "Expected invalid warm block ('$Scenario') but validation returned no reason." }
        if ($reason -notmatch $Pattern) { throw "Warm-block reason for '$Scenario' was '$reason'; expected pattern '$Pattern'." }
    }
    $okWarm = @(New-WarmRunState -Status 'complete' -Repetition 0)
    $okMeasured = @(1..3 | ForEach-Object { New-WarmRunState -Status 'complete' -Repetition $_ })
    $validReason = Get-WarmBlockInvalidReason -Warmups $okWarm -Measured $okMeasured -Repetitions 3
    if ($null -ne $validReason) { throw "Valid complete warm block was rejected: $validReason" }
    Assert-WarmBlockInvalid -Warmups @() -Measured $okMeasured -Reps 3 -Pattern 'missing warm-up' -Scenario 'missing warm-up'
    Assert-WarmBlockInvalid -Warmups @((New-WarmRunState 'complete' 0), (New-WarmRunState 'complete' 0)) -Measured $okMeasured -Reps 3 -Pattern 'duplicate warm-up' -Scenario 'duplicate warm-up'
    Assert-WarmBlockInvalid -Warmups @(New-WarmRunState 'incomplete' 0) -Measured $okMeasured -Reps 3 -Pattern 'incomplete warm-up' -Scenario 'incomplete warm-up'
    Assert-WarmBlockInvalid -Warmups $okWarm -Measured @((New-WarmRunState 'complete' 1), (New-WarmRunState 'complete' 3)) -Reps 3 -Pattern 'missing warm measured cell for repetition 2' -Scenario 'missing repetition'
    Assert-WarmBlockInvalid -Warmups $okWarm -Measured @((New-WarmRunState 'complete' 1), (New-WarmRunState 'complete' 2), (New-WarmRunState 'complete' 2), (New-WarmRunState 'complete' 3)) -Reps 3 -Pattern 'duplicate warm measured cell for repetition 2' -Scenario 'duplicate repetition'
    Assert-WarmBlockInvalid -Warmups $okWarm -Measured @((New-WarmRunState 'incomplete' 1), (New-WarmRunState 'complete' 2), (New-WarmRunState 'complete' 3)) -Reps 3 -Pattern 'incomplete warm measured cell for repetition 1' -Scenario 'incomplete measured repetition'
    Assert-WarmBlockInvalid -Warmups $okWarm -Measured @((New-WarmRunState 'complete' 1), (New-WarmRunState 'complete' 2), (New-WarmRunState 'complete' 4)) -Reps 3 -Pattern 'outside 1\.\.3' -Scenario 'repetition out of range'

    # ---- Test 10: execution-harness fingerprint -----------------------------
    $fpRepo = Join-Path $temp 'fp-repo'
    New-Item -ItemType Directory -Path (Join-Path $fpRepo 'eng\benchmark') -Force | Out-Null
    $realRunner = Join-Path $PSScriptRoot '..\benchmark-presets.ps1'
    if (-not (Test-Path -LiteralPath $realRunner)) { $realRunner = Join-Path $PSScriptRoot '..\..\benchmark-presets.ps1' }
    $realModule = Join-Path $PSScriptRoot 'BenchmarkHarness.psm1'
    $fpRunnerCopy = Join-Path $fpRepo 'eng\benchmark-presets.ps1'
    $fpModuleCopy = Join-Path $fpRepo 'eng\benchmark\BenchmarkHarness.psm1'
    Copy-Item -LiteralPath $realRunner -Destination $fpRunnerCopy
    Copy-Item -LiteralPath $realModule -Destination $fpModuleCopy
    $fpA = Get-KeeFetchHarnessFingerprint -RepoRoot $fpRepo
    $fpB = Get-KeeFetchHarnessFingerprint -RepoRoot $fpRepo
    if ([string]::IsNullOrWhiteSpace($fpA) -or $fpA -notmatch '^[0-9a-f]{64}$') { throw "Harness fingerprint must be 64 hex chars: $fpA" }
    if ($fpA -ne $fpB) { throw 'Harness fingerprint is not deterministic for identical inputs.' }
    $liveRepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
    $fpLive = Get-KeeFetchHarnessFingerprint -RepoRoot $liveRepoRoot
    if ($fpLive -ne $fpA) { throw 'Fingerprint of identical working-tree copies must equal the live harness fingerprint.' }
    # Line-ending normalization: identical bytes modulo CRLF must not change identity.
    $lfOnly = ([System.IO.File]::ReadAllText($fpRunnerCopy)) -replace "`r`n", "`n" -replace "`r", "`n"
    [System.IO.File]::WriteAllText($fpRunnerCopy, ($lfOnly -replace "`n", "`r`n"), (New-Object System.Text.UTF8Encoding $false))
    $fpCrlf = Get-KeeFetchHarnessFingerprint -RepoRoot $fpRepo
    if ($fpCrlf -ne $fpA) { throw 'Harness fingerprint must be invariant under CRLF/LF normalization.' }
    # Any content change in a harness component must change the identity.
    [System.IO.File]::AppendAllText($fpModuleCopy, "# fingerprint-sentinel$([Environment]::NewLine)", (New-Object System.Text.UTF8Encoding $false))
    $fpChanged = Get-KeeFetchHarnessFingerprint -RepoRoot $fpRepo
    if ($fpChanged -eq $fpA) { throw 'Harness fingerprint did not change after a harness component was modified.' }
    $missingRepo = Join-Path $temp 'fp-empty-repo'
    New-Item -ItemType Directory -Path $missingRepo -Force | Out-Null
    $fpThrew = $false
    try { Get-KeeFetchHarnessFingerprint -RepoRoot $missingRepo | Out-Null }
    catch { $fpThrew = $true }
    if (-not $fpThrew) { throw 'Harness fingerprint must fail closed when a component file is missing.' }

    # ---- Test 11: exact matrix validation (Assert-KeeFetchRunRows) ----------
    $rowsRoot = Join-Path $temp 'rows-assert'
    New-Item -ItemType Directory -Path $rowsRoot -Force | Out-Null
    function New-AssertRun {
        param([string[]]$FixtureIds, [string]$Outcome = 'success', [string]$Profile = 'cand-x', [switch]$WithArtifacts)
        $r = New-KeeFetchRun -OutputRoot $rowsRoot -ExperimentId 'rows-assert' -CorpusPath 'corpus.csv' -CorpusVersion 'v1' -Concurrency 4 -CacheMode 'cold' -Profiles @($Profile) -Repetitions 1 -ExtraMetadata @{ corpus_fingerprint = 'c_fp_1' }
        foreach ($fid in $FixtureIds) {
            $row = [ordered]@{
                fixture_id = $fid; repetition = 1; category = 'global-brand'; input_url = "https://$fid.example/"
                selected_provider = 'Direct Site'; tier = 'SiteCanonical'; is_synthetic = $false; placeholder_suspected = $false; blank_suspected = $false
                machine_outcome = $Outcome; candidate_counts = @{}; per_provider_metrics = @(); provider_metrics = @()
                total_elapsed_ms = 10; total_elapsed = 10; cache_behavior = 'miss'; cache_hit = $false; coalesced = $false; coalescing = $false
                image_type = 'png'; image_width = 16; image_height = 16; image_byte_size = 64; image_validation = 'ok'
                artifact_path = ''; artifact_hash = ''
                experiment_id = 'rows-assert'; profile = $Profile; network_context = 'default'; concurrency = 4; cache_mode = 'cold'
                run_id = [string]$r.RunId
            }
            if ($WithArtifacts.IsPresent -and $Outcome -eq 'success') {
                $artDir = Join-Path $r.Directory 'artifacts'
                if (-not (Test-Path -LiteralPath $artDir)) { New-Item -ItemType Directory -Path $artDir -Force | Out-Null }
                [System.IO.File]::WriteAllBytes((Join-Path $artDir ($fid + '.png')), ([System.Text.Encoding]::ASCII.GetBytes('artifact-' + $fid)))
                $row['artifact_path'] = 'artifacts/' + $fid + '.png'
                $row['artifact_hash'] = 'hash-' + $fid
            }
            Add-KeeFetchResult -RunDirectory $r.Directory -Result $row
        }
        Complete-KeeFetchRun -RunDirectory $r.Directory
        return $r
    }
    function Assert-RunRowsThrows {
        param([string]$Pattern, [scriptblock]$Action, [string]$Scenario)
        $threw = $false
        $msg = ''
        try { & $Action | Out-Null } catch { $threw = $true; $msg = $_.Exception.Message }
        if (-not $threw) { throw "Assert-KeeFetchRunRows accepted invalid evidence ('$Scenario')." }
        if ($msg -notmatch $Pattern) { throw "Assert-KeeFetchRunRows('$Scenario') message '$msg' does not match '$Pattern'." }
    }

    # Valid matrix passes, with and without artifact requirements.
    $goodRun = New-AssertRun -FixtureIds @('f1','f2','f3')
    Assert-KeeFetchRunRows -RunDirectory $goodRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$goodRun.RunId) -ExpectedConcurrency 4
    $artRun = New-AssertRun -FixtureIds @('f1','f2','f3') -WithArtifacts
    Assert-KeeFetchRunRows -RunDirectory $artRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$artRun.RunId) -ExpectedConcurrency 4 -RequireSuccessArtifacts

    # Successful cold rows must carry artifacts when required.
    Assert-RunRowsThrows -Scenario 'success row without artifact' -Pattern 'no artifact hash' -Action {
        Assert-KeeFetchRunRows -RunDirectory $goodRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$goodRun.RunId) -ExpectedConcurrency 4 -RequireSuccessArtifacts
    }
    # Artifact recorded but missing on disk is invalid evidence.
    $missingArtRun = New-AssertRun -FixtureIds @('f1') -WithArtifacts
    Remove-Item -LiteralPath (Join-Path $missingArtRun.Directory 'artifacts\f1.png') -Force
    Assert-RunRowsThrows -Scenario 'artifact missing on disk' -Pattern 'missing on disk' -Action {
        Assert-KeeFetchRunRows -RunDirectory $missingArtRun.Directory -ExpectedFixtureIds @('f1') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$missingArtRun.RunId) -ExpectedConcurrency 4 -RequireSuccessArtifacts
    }
    # Corpus fingerprint mismatch invalidates the run.
    Assert-RunRowsThrows -Scenario 'corpus fingerprint mismatch' -Pattern 'corpus fingerprint mismatch' -Action {
        Assert-KeeFetchRunRows -RunDirectory $goodRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'changed' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$goodRun.RunId) -ExpectedConcurrency 4
    }
    # Missing fixture: fewer rows than the corpus set.
    $shortRun = New-AssertRun -FixtureIds @('f1','f2')
    Assert-RunRowsThrows -Scenario 'missing fixture (row count)' -Pattern 'expected 3 rows but recorded 2' -Action {
        Assert-KeeFetchRunRows -RunDirectory $shortRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$shortRun.RunId) -ExpectedConcurrency 4
    }
    # Unexpected fixture from outside the corpus.
    $alienRun = New-AssertRun -FixtureIds @('f1','f2','g1')
    Assert-RunRowsThrows -Scenario 'unexpected fixture' -Pattern "unexpected fixture 'g1'" -Action {
        Assert-KeeFetchRunRows -RunDirectory $alienRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$alienRun.RunId) -ExpectedConcurrency 4
    }
    # Duplicate fixture within the cell (same fixture under two repetitions).
    $dupRun = New-AssertRun -FixtureIds @('f1','f2')
    Add-KeeFetchResult -RunDirectory $dupRun.Directory -Result ([ordered]@{
        fixture_id = 'f1'; repetition = 2; machine_outcome = 'not-found'; artifact_path = ''; artifact_hash = ''
        experiment_id = 'rows-assert'; profile = 'cand-x'; cache_mode = 'cold'; run_id = [string]$dupRun.RunId; concurrency = 4
    })
    Complete-KeeFetchRun -RunDirectory $dupRun.Directory
    Assert-RunRowsThrows -Scenario 'duplicate fixture within cell' -Pattern "duplicate fixture 'f1'" -Action {
        Assert-KeeFetchRunRows -RunDirectory $dupRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$dupRun.RunId) -ExpectedConcurrency 4
    }
    # Per-row metadata must match run metadata exactly.
    Assert-RunRowsThrows -Scenario 'profile metadata mismatch' -Pattern "profile 'cand-x' does not match run metadata 'cand-y'" -Action {
        Assert-KeeFetchRunRows -RunDirectory $goodRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-y' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId ([string]$goodRun.RunId) -ExpectedConcurrency 4
    }
    Assert-RunRowsThrows -Scenario 'run id metadata mismatch' -Pattern 'run_id .* does not match run metadata' -Action {
        Assert-KeeFetchRunRows -RunDirectory $goodRun.Directory -ExpectedFixtureIds @('f1','f2','f3') -CurrentCorpusFingerprint 'c_fp_1' -ExpectedExperimentId 'rows-assert' -ExpectedProfile 'cand-x' -ExpectedCacheMode 'cold' -ExpectedRepetition 1 -ExpectedRunId 'run_other' -ExpectedConcurrency 4
    }

} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
Write-Output 'Benchmark harness self-tests passed.'
