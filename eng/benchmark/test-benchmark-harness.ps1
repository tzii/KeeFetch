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

} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
Write-Output 'Benchmark harness self-tests passed.'
