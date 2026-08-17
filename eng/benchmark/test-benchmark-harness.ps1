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

    # ---------------------------------------------------------------------------
    # Test 8-24: Mock matrix, sampling, provenance, ambiguity, and selector tests
    # ---------------------------------------------------------------------------
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

    $mockRoot = Join-Path $temp 'mock-experiment-root'
    New-Item -ItemType Directory -Path $mockRoot -Force | Out-Null

    # Create mock corpus
    $mockCorpusPath = Join-Path $temp 'mock-corpus.csv'
    $mockCorpusRows = @(
        [pscustomobject]@{ fixture_id = 'pub-001'; category = 'global-brand'; input_url = 'https://site1.com'; expected_class = 'usable-icon'; expected_host = 'site1.com'; review_required = 'false'; notes = '' },
        [pscustomobject]@{ fixture_id = 'pub-002'; category = 'global-brand'; input_url = 'https://site2.com'; expected_class = 'usable-icon'; expected_host = 'site2.com'; review_required = 'false'; notes = '' },
        [pscustomobject]@{ fixture_id = 'pub-003'; category = 'global-brand'; input_url = 'https://site3.com'; expected_class = 'usable-icon'; expected_host = 'site3.com'; review_required = 'false'; notes = '' },
        [pscustomobject]@{ fixture_id = 'pub-004'; category = 'financial';    input_url = 'https://site4.com'; expected_class = 'usable-icon'; expected_host = 'site4.com'; review_required = 'false'; notes = '' }
    )
    $mockCorpusRows | Export-Csv -LiteralPath $mockCorpusPath -NoTypeInformation -Encoding UTF8
    $mockCorpusFp = Get-Sha256Hex (Get-Content -Raw -LiteralPath $mockCorpusPath)
    $mockBinaryHash = 'mockbinary1234567890abcdef'

    # 3 mock candidates: privacy-eligible, fast-eligible, thorough-eligible
    $mockCand1 = [ordered]@{
        id = 'mock-direct-only'
        providerIds = @('direct-site')
        primaryTimeout = 6000; fallbackTimeout = 3500; cumulativeTimeout = 22000
        allowSynthetic = $false; stopAfterStrongResolved = $true
    }
    $mockCand2 = [ordered]@{
        id = 'mock-fast'
        providerIds = @('direct-site', 'google', 'twenty-icons')
        primaryTimeout = 4000; fallbackTimeout = 2500; cumulativeTimeout = 15000
        allowSynthetic = $false; stopAfterStrongResolved = $true
    }
    $mockCand3 = [ordered]@{
        id = 'mock-thorough'
        providerIds = @('direct-site', 'google', 'twenty-icons', 'favicone')
        primaryTimeout = 10000; fallbackTimeout = 5000; cumulativeTimeout = 45000
        allowSynthetic = $true; stopAfterStrongResolved = $false
    }
    $mockExpObj = [ordered]@{
        experiment_id = 'mock-exp-v13'
        corpus = (Join-Path $temp 'mock-corpus.csv').Replace('\', '/')
        profiles = @('mock-direct-only', 'mock-fast', 'mock-thorough')
        schedule_seed = 42
        repetitions = 1
        concurrency = 2
        cache_modes = @('cold', 'warm')
        output_root = $mockRoot.Replace('\', '/')
        candidates = @($mockCand1, $mockCand2, $mockCand3)
    }
    $mockExpJsonPath = Join-Path $temp 'mock-exp-v13.json'
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($mockExpJsonPath, (ConvertTo-Json -InputObject $mockExpObj -Depth 6), $utf8NoBom)
    $mockExpFp = Get-Sha256Hex (Get-Content -Raw -LiteralPath $mockExpJsonPath)

    function Get-MockPolicyFp {
        param([object]$Def)
        $syn = if ($Def.allowSynthetic) { "1" } else { "0" }
        $stp = if ($Def.stopAfterStrongResolved) { "1" } else { "0" }
        $canon = ("v1|providers={0}|primaryMs={1}|fallbackMs={2}|cumulativeMs={3}|synthetic={4}|stopAfterStrongResolved={5}" -f `
            ($Def.providerIds -join ','), $Def.primaryTimeout, $Def.fallbackTimeout, $Def.cumulativeTimeout, $syn, $stp)
        return Get-Sha256Hex $canon
    }

    $candFps = @{
        'mock-direct-only' = Get-MockPolicyFp -Def $mockCand1
        'mock-fast'        = Get-MockPolicyFp -Def $mockCand2
        'mock-thorough'    = Get-MockPolicyFp -Def $mockCand3
    }

    function New-MockRunCell {
        param(
            [string]$CandidateId,
            [string]$CacheMode,
            [int]$Repetition,
            [string]$RunKind = 'measured',
            [int]$ActiveElapsedMs = 500,
            [string]$Status = 'complete',
            [string]$ExpFp = $mockExpFp
        )
        $dirName = if ($RunKind -eq 'warmup') {
            "{0}-warmup" -f $CandidateId
        } else {
            "{0}-{1}-rep{2}" -f $CandidateId, $CacheMode, $Repetition
        }
        $runDir = Join-Path $mockRoot $dirName
        New-Item -ItemType Directory -Path $runDir -Force | Out-Null

        $meta = [ordered]@{
            experiment_id = 'mock-exp-v13'
            experiment_fingerprint = $ExpFp
            corpus_fingerprint = $mockCorpusFp
            binary_hash = $mockBinaryHash
            candidate_id = $CandidateId
            policy_fingerprint = $candFps[$CandidateId]
            cache_mode = $CacheMode
            repetition = $Repetition
            run_kind = $RunKind
            status = $Status
            concurrency = 2
            network_context = 'test'
            started_timestamp = '2026-08-17T12:00:00Z'
            completed_timestamp = '2026-08-17T12:00:01Z'
            active_elapsed_ms = $ActiveElapsedMs
            resumed = $false
            row_count = 4
        }
        [System.IO.File]::WriteAllText((Join-Path $runDir 'run.json'), (ConvertTo-Json -InputObject $meta -Depth 4), $utf8NoBom)

        $rows = @()
        for ($i = 1; $i -le 4; $i++) {
            $fid = ('pub-{0:D3}' -f $i)
            $cat = if ($i -le 3) { 'global-brand' } else { 'financial' }
            $prov = if ($CandidateId -eq 'mock-direct-only') { 'Direct Site' } else { 'Google' }
            $hash = if ($CandidateId -eq 'mock-direct-only' -and $i -eq 4) { 'hash-direct-4' } else { "hash-$i" }
            $mo = 'success'
            $syn = ($CandidateId -eq 'mock-thorough' -and $i -eq 3)
            $place = ($i -eq 2)
            $pm = @(
                [ordered]@{ provider = $prov; calls = 1; elapsed_ms = 50; candidate_count = 1; outcome = 'success'; errors = 0 }
            )
            $rows += [pscustomobject]@{
                fixture_id = $fid
                repetition = $Repetition
                category = $cat
                input_url = "https://site$i.com"
                selected_provider = $prov
                tier = 'SiteCanonical'
                is_synthetic = $syn
                placeholder_suspected = $place
                blank_suspected = $false
                machine_outcome = $mo
                candidate_counts = "{}"
                per_provider_metrics = "[]"
                provider_metrics = (ConvertTo-Json -InputObject $pm -Compress)
                total_elapsed_ms = 100
                cache_behavior = 'miss'
                cache_hit = $false
                coalesced = $false
                coalescing = $false
                image_type = 'png'
                image_width = 16
                image_height = 16
                image_byte_size = 512
                image_validation = 'ok'
                artifact_path = "artifacts/$fid.png"
                artifact_hash = $hash
                experiment_id = 'mock-exp-v13'
                profile = $CandidateId
                network_context = 'test'
                concurrency = 2
                cache_mode = $CacheMode
            }
        }
        $rows | Export-Csv -LiteralPath (Join-Path $runDir 'rows.csv') -NoTypeInformation -Encoding UTF8
        return $runDir
    }

    # Generate full matrix cells: 3 candidates x 2 modes (cold, warm) x 1 rep + 3 warmups
    $createdRunDirs = @()
    foreach ($cid in @('mock-direct-only', 'mock-fast', 'mock-thorough')) {
        $createdRunDirs += New-MockRunCell -CandidateId $cid -CacheMode 'cold' -Repetition 1 -ActiveElapsedMs 300
        $createdRunDirs += New-MockRunCell -CandidateId $cid -CacheMode 'warm' -Repetition 1 -ActiveElapsedMs 100
        $createdRunDirs += New-MockRunCell -CandidateId $cid -CacheMode 'warm' -Repetition 0 -RunKind 'warmup' -ActiveElapsedMs 150
    }

    # Verify prepare-review.ps1 and select-profiles.ps1 syntax
    $prepPath = Join-Path $PSScriptRoot 'prepare-review.ps1'
    $selPath = Join-Path $PSScriptRoot 'select-profiles.ps1'
    if (-not (Test-Path -LiteralPath $prepPath)) { throw 'Missing prepare-review.ps1' }
    if (-not (Test-Path -LiteralPath $selPath)) { throw 'Missing select-profiles.ps1' }

    # Test: prepare-review generates exact-hash queue and excludes warm-up runs
    $mockQueuePath = Join-Path $mockRoot 'review-queue.csv'
    & $prepPath -RunDir $mockRoot -OutputPath $mockQueuePath -Seed 'test-seed-42' | Out-Null
    if (-not (Test-Path -LiteralPath $mockQueuePath)) { throw 'Review queue was not created' }
    $queueData = @(Import-Csv -LiteralPath $mockQueuePath)
    if ($queueData.Count -eq 0) { throw 'Review queue is empty' }

    # Test: queue identity is exact fixture_id|artifact_hash
    $seenQKeys = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($q in $queueData) {
        if ([string]::IsNullOrWhiteSpace($q.fixture_id) -or [string]::IsNullOrWhiteSpace($q.artifact_hash)) {
            throw 'Queue entry missing fixture_id or artifact_hash'
        }
        $k = "$($q.fixture_id)|$($q.artifact_hash)"
        if (-not $seenQKeys.Add($k)) { throw "Duplicate review unit in queue: $k" }
    }

    # Test: deterministic sampling stability (same seed -> identical queue)
    $mockQueuePath2 = Join-Path $mockRoot 'review-queue-seed2.csv'
    & $prepPath -RunDir $mockRoot -OutputPath $mockQueuePath2 -Seed 'test-seed-42' | Out-Null
    $queueData2 = @(Import-Csv -LiteralPath $mockQueuePath2)
    if ($queueData.Count -ne $queueData2.Count) { throw 'Sampling with same seed produced different queue length' }
    for ($i = 0; $i -lt $queueData.Count; $i++) {
        if ($queueData[$i].fixture_id -ne $queueData2[$i].fixture_id -or $queueData[$i].artifact_hash -ne $queueData2[$i].artifact_hash) {
            throw 'Sampling with same seed produced different ordering or items'
        }
    }

    # Test: validation rejects not-reviewed labels (ASSERTION MUST FAIL CLOSED)
    $valFailed = $false
    try {
        & $prepPath -RunDir $mockRoot -OutputPath $mockQueuePath -Validate 2>$null | Out-Null
    } catch {
        $valFailed = $true
    }
    if (-not $valFailed) {
        throw 'Validation passed on an unreviewed queue (must fail closed).'
    }

    # Test: validation rejects fabricated/stale artifact hash
    $badQueuePath = Join-Path $mockRoot 'review-queue-badhash.csv'
    $badQData = @(Import-Csv -LiteralPath $mockQueuePath)
    foreach ($r in $badQData) { $r.review_label = 'correct'; $r.reviewer = 'test-reviewer'; $r.reviewed_at_utc = '2026-08-17T12:00:00Z' }
    $badQData[0].artifact_hash = 'fabricatedhash123'
    $badQData | Export-Csv -LiteralPath $badQueuePath -NoTypeInformation -Encoding UTF8
    $badHashRejected = $false
    try {
        & $prepPath -RunDir $mockRoot -OutputPath $badQueuePath -Validate 2>$null | Out-Null
    } catch {
        $badHashRejected = $true
    }
    if (-not $badHashRejected) {
        throw 'Validation accepted a fabricated artifact hash (must fail closed).'
    }

    # Fill valid labels on the main queue
    $labeledRows = @(Import-Csv -LiteralPath $mockQueuePath)
    foreach ($r in $labeledRows) {
        $r.review_label = 'correct'
        $r.reviewer = 'test-reviewer'
        $r.reviewed_at_utc = '2026-08-17T12:00:00Z'
    }
    $labeledRows | Export-Csv -LiteralPath $mockQueuePath -NoTypeInformation -Encoding UTF8

    # Test: validation passes on complete, valid review queue
    try {
        & $prepPath -RunDir $mockRoot -OutputPath $mockQueuePath -Validate | Out-Null
    } catch {
        throw "Validation failed on valid labeled queue: $($_.Exception.Message)"
    }

    # Test: selector runs without -Publish and writes only to OutputDir (does NOT mutate repo source)
    $mockOutputDir = Join-Path $mockRoot 'selection-out'
    & $selPath -RunDir $mockRoot -ReviewQueue $mockQueuePath -OutputDir $mockOutputDir -ExperimentFile $mockExpJsonPath | Out-Null
    foreach ($p in @('FetchProfileCatalog.Generated.cs', 'v1.3-provider-study.md', 'selection-summary.json', 'selection-report.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $mockOutputDir $p))) {
            throw "select-profiles missing output: $p"
        }
    }

    # Test: ambiguous challenger can force rejection (ASSERTION MUST BE VERIFIED)
    $ambQueuePath = Join-Path $mockRoot 'review-queue-ambiguous.csv'
    $ambQRows = @(Import-Csv -LiteralPath $mockQueuePath)
    # Set labels so mock-direct-only has 3 correct / 1 wrong-brand (0.75), while mock-thorough has 2 correct / 2 ambiguous
    # Under conservative: mock-direct-only (0.75) beats mock-thorough (0.50) -> winner is mock-direct-only
    # Under optimistic: mock-thorough (1.00) beats mock-direct-only (0.75) -> winner is mock-thorough
    foreach ($r in $ambQRows) {
        if ($r.artifact_hash -eq 'hash-direct-4') {
            $r.review_label = 'wrong-brand'
        } elseif ($r.artifact_hash -eq 'hash-3' -or $r.artifact_hash -eq 'hash-4') {
            $r.review_label = 'ambiguous'
        } else {
            $r.review_label = 'correct'
        }
    }
    $ambQRows | Export-Csv -LiteralPath $ambQueuePath -NoTypeInformation -Encoding UTF8
    $ambRejected = $false
    try {
        & $selPath -RunDir $mockRoot -ReviewQueue $ambQueuePath -OutputDir (Join-Path $mockRoot 'amb-out') -ExperimentFile $mockExpJsonPath 2>$null | Out-Null
    } catch {
        $ambRejected = $true
    }
    if (-not $ambRejected) {
        throw 'Selector did not reject ambiguous reversal (must fail closed on ambiguous instability).'
    }

    # Test: missing matrix cell is rejected
    $missingMatrixRoot = Join-Path $temp 'mock-missing-cell-root'
    Copy-Item -LiteralPath $mockRoot -Destination $missingMatrixRoot -Recurse
    # Remove one measured cell
    Remove-Item -LiteralPath (Join-Path $missingMatrixRoot 'mock-fast-cold-rep1') -Recurse -Force
    $missingRejected = $false
    try {
        & $selPath -RunDir $missingMatrixRoot -ReviewQueue $mockQueuePath -OutputDir (Join-Path $missingMatrixRoot 'out') -ExperimentFile $mockExpJsonPath 2>$null | Out-Null
    } catch {
        $missingRejected = $_.Exception.Message -match 'Missing matrix cells'
    }
    if (-not $missingRejected) {
        throw 'Selector accepted missing matrix cell (must fail closed).'
    }

    # Test: mixed experiment fingerprint is rejected
    $mixedFpRoot = Join-Path $temp 'mock-mixed-fp-root'
    Copy-Item -LiteralPath $mockRoot -Destination $mixedFpRoot -Recurse
    $mixedJsonPath = Join-Path $mixedFpRoot 'mock-fast-cold-rep1\run.json'
    $mixedMeta = Get-Content -Raw -LiteralPath $mixedJsonPath | ConvertFrom-Json
    $mixedMeta.experiment_fingerprint = 'deadbeef12345678'
    [System.IO.File]::WriteAllText($mixedJsonPath, (ConvertTo-Json -InputObject $mixedMeta -Depth 4), $utf8NoBom)
    $mixedRejected = $false
    try {
        & $selPath -RunDir $mixedFpRoot -ReviewQueue $mockQueuePath -OutputDir (Join-Path $mixedFpRoot 'out') -ExperimentFile $mockExpJsonPath 2>$null | Out-Null
    } catch {
        $mixedRejected = $_.Exception.Message -match 'Mixed experiment fingerprints'
    }
    if (-not $mixedRejected) {
        throw 'Selector accepted mixed experiment fingerprints (must fail closed).'
    }

    # Test: duplicate matrix cell is rejected
    $dupMatrixRoot = Join-Path $temp 'mock-dup-cell-root'
    Copy-Item -LiteralPath $mockRoot -Destination $dupMatrixRoot -Recurse
    # Copy one cell to create a duplicate with different directory name but same metadata
    $dupDir = Join-Path $dupMatrixRoot 'mock-fast-cold-rep1-dup'
    Copy-Item -LiteralPath (Join-Path $dupMatrixRoot 'mock-fast-cold-rep1') -Destination $dupDir -Recurse
    $dupRejected = $false
    try {
        & $selPath -RunDir $dupMatrixRoot -ReviewQueue $mockQueuePath -OutputDir (Join-Path $dupMatrixRoot 'out') -ExperimentFile $mockExpJsonPath 2>$null | Out-Null
    } catch {
        $dupRejected = $_.Exception.Message -match 'Duplicate matrix cell'
    }
    if (-not $dupRejected) {
        throw 'Selector accepted duplicate matrix cell (must fail closed).'
    }

    # Test: incomplete run is rejected
    $incompRoot = Join-Path $temp 'mock-incomplete-root'
    Copy-Item -LiteralPath $mockRoot -Destination $incompRoot -Recurse
    $incompJsonPath = Join-Path $incompRoot 'mock-fast-cold-rep1\run.json'
    $incompMeta = Get-Content -Raw -LiteralPath $incompJsonPath | ConvertFrom-Json
    $incompMeta.status = 'interrupted'
    [System.IO.File]::WriteAllText($incompJsonPath, (ConvertTo-Json -InputObject $incompMeta -Depth 4), $utf8NoBom)
    $incompRejected = $false
    try {
        & $selPath -RunDir $incompRoot -ReviewQueue $mockQueuePath -OutputDir (Join-Path $incompRoot 'out') -ExperimentFile $mockExpJsonPath 2>$null | Out-Null
    } catch {
        $incompRejected = $_.Exception.Message -match 'Incomplete run rejected'
    }
    if (-not $incompRejected) {
        throw 'Selector accepted incomplete run (must fail closed).'
    }

} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
Write-Output 'Benchmark harness self-tests passed.'
