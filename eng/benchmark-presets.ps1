param(
    [Parameter(Mandatory=$true)][string]$Experiment,
    [string]$ResumeRun = ''
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$keepassPath = "C:\Program Files\KeePass Password Safe 2\KeePass.exe"
$assemblyPath = Join-Path $repoRoot "bin\Release\net48\KeeFetch.dll"
$providerNames = @(
    "Direct Site",
    "Twenty Icons",
    "DuckDuckGo",
    "Google",
    "Yandex",
    "Favicone",
    "Icon Horse"
)

$harnessPath = Join-Path $PSScriptRoot "benchmark\BenchmarkHarness.psm1"
if (-not (Test-Path -LiteralPath $harnessPath)) {
    $harnessPath = Join-Path $repoRoot "eng\benchmark\BenchmarkHarness.psm1"
}
Import-Module $harnessPath -Force

# Resolve experiment path
$experimentPath = $Experiment
if (-not [System.IO.Path]::IsPathRooted($experimentPath)) {
    $candidate = Join-Path $repoRoot $experimentPath
    if (Test-Path -LiteralPath $candidate) {
        $experimentPath = (Resolve-Path -LiteralPath $candidate).Path
    } elseif (Test-Path -LiteralPath $experimentPath) {
        $experimentPath = (Resolve-Path -LiteralPath $experimentPath).Path
    } else {
        $candidate2 = Join-Path (Get-Location).Path $Experiment
        if (Test-Path -LiteralPath $candidate2) {
            $experimentPath = (Resolve-Path -LiteralPath $candidate2).Path
        }
    }
} else {
    if (Test-Path -LiteralPath $experimentPath) {
        $experimentPath = (Resolve-Path -LiteralPath $experimentPath).Path
    }
}

$experiment = Read-KeeFetchExperiment -ExperimentPath $experimentPath

# Resolve corpus and output root
$corpusPath = $experiment.corpus_path
if ([string]::IsNullOrWhiteSpace($corpusPath)) {
    $corpusPath = $experiment.corpus
    if (-not [System.IO.Path]::IsPathRooted($corpusPath)) {
        $corpusPath = Join-Path $repoRoot $corpusPath
    }
}
$outputRoot = $experiment.output_root
if (-not [System.IO.Path]::IsPathRooted($outputRoot)) {
    $outputRoot = Join-Path $repoRoot $outputRoot
}

# Validate corpus
$vocabPath = Join-Path $repoRoot "KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json"
$hasFixtureFilter = $false
if ($experiment.PSObject.Properties.Name -contains 'fixture_ids' -and $null -ne $experiment.fixture_ids -and $experiment.fixture_ids.Count -gt 0) {
    $hasFixtureFilter = $true
}

$filteredRows = @()
if ($hasFixtureFilter) {
    $allRows = @(Import-Csv -LiteralPath $corpusPath)
    $filterSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($fid in $experiment.fixture_ids) { [void]$filterSet.Add([string]$fid) }
    $filteredRows = @($allRows | Where-Object { $filterSet.Contains([string]$_.fixture_id) })
    if ($filteredRows.Count -eq 0) {
        throw "Filtered corpus is empty. No rows matched fixture_ids."
    }
    # Lenient validation for filtered corpus: check required fields and URL validity
    foreach ($row in $filteredRows) {
        if ([string]::IsNullOrWhiteSpace($row.fixture_id)) { throw "Missing fixture_id in filtered corpus." }
        if ([string]::IsNullOrWhiteSpace($row.category)) { throw "Missing category for fixture $($row.fixture_id)" }
        if ([string]::IsNullOrWhiteSpace($row.input_url)) { throw "Missing input_url for fixture $($row.fixture_id)" }
        $uri = $null
        if (-not [Uri]::TryCreate($row.input_url, [UriKind]::Absolute, [ref]$uri)) { throw "Invalid absolute URL: $($row.input_url)" }
        if ($uri.IsLoopback -or $uri.HostNameType -eq [UriHostNameType]::IPv4 -or $uri.HostNameType -eq [UriHostNameType]::IPv6) {
            throw "Fixture targets a private or loopback host: $($row.fixture_id)"
        }
        if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) { throw "Fixture contains credentials: $($row.fixture_id)" }
    }
} else {
    # Full corpus validation with quotas
    if (Test-Path -LiteralPath $vocabPath) {
        Test-KeeFetchCorpus -CsvPath $corpusPath -VocabularyPath $vocabPath | Out-Null
    }
    $filteredRows = @(Import-Csv -LiteralPath $corpusPath)
}

# Load resume keys if ResumeRun provided
$resumeKeys = New-Object 'System.Collections.Generic.HashSet[string]'
if (-not [string]::IsNullOrWhiteSpace($ResumeRun)) {
    $resumePath = $ResumeRun
    if (-not [System.IO.Path]::IsPathRooted($resumePath)) {
        $candidateResume = Join-Path $repoRoot $resumePath
        if (Test-Path -LiteralPath $candidateResume) {
            $resumePath = (Resolve-Path -LiteralPath $candidateResume).Path
        } elseif (Test-Path -LiteralPath $resumePath) {
            $resumePath = (Resolve-Path -LiteralPath $resumePath).Path
        }
    } else {
        if (Test-Path -LiteralPath $resumePath) {
            $resumePath = (Resolve-Path -LiteralPath $resumePath).Path
        }
    }
    # If resumePath is a directory, look for results.ndjson inside
    $resumeNdjson = $null
    if (Test-Path -LiteralPath $resumePath) {
        $item = Get-Item -LiteralPath $resumePath
        if ($item.PSIsContainer) {
            $candidateNdjson = Join-Path $resumePath "results.ndjson"
            if (Test-Path -LiteralPath $candidateNdjson) { $resumeNdjson = $candidateNdjson }
        } else {
            # if it's a file, assume it's ndjson
            $resumeNdjson = $resumePath
        }
    }
    if ($null -ne $resumeNdjson -and (Test-Path -LiteralPath $resumeNdjson)) {
        $lines = @()
        try { $lines = Get-Content -LiteralPath $resumeNdjson -Encoding UTF8 -ErrorAction SilentlyContinue } catch { $lines = @() }
        foreach ($line in $lines) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $obj = ConvertFrom-Json -InputObject $line -ErrorAction Stop
                $fid = ""
                if ($obj.PSObject.Properties.Name -contains 'fixture_id') { $fid = [string]$obj.fixture_id }
                $rep = "1"
                if ($obj.PSObject.Properties.Name -contains 'repetition') { $rep = [string]$obj.repetition }
                $prof = ""
                if ($obj.PSObject.Properties.Name -contains 'profile') { $prof = [string]$obj.profile }
                $cm = ""
                if ($obj.PSObject.Properties.Name -contains 'cache_mode') { $cm = [string]$obj.cache_mode }
                # Key includes profile+cache_mode+fixture+repetition for resume
                $key = "$prof|$cm|$fid|$rep"
                [void]$resumeKeys.Add($key)
                # Also add simple key for backward compat
                $simpleKey = "$fid|$rep"
                [void]$resumeKeys.Add($simpleKey)
            } catch { continue }
        }
    }
}

if (-not (Test-Path -LiteralPath $keepassPath)) {
    throw "KeePass.exe not found at $keepassPath"
}

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build KeeFetch first. Missing $assemblyPath"
}

[Reflection.Assembly]::LoadFrom($keepassPath) | Out-Null
$keefetchAssembly = [Reflection.Assembly]::LoadFrom($assemblyPath)

$configType = $keefetchAssembly.GetType("KeeFetch.Configuration", $true)
$downloaderType = $keefetchAssembly.GetType("KeeFetch.FaviconDownloader", $true)
$downloaderCtor = $downloaderType.GetConstructor(
    [Reflection.BindingFlags] "Instance, Public, NonPublic",
    $null,
    [Type[]] @($configType),
    $null)

$downloadMethod = $downloaderType.GetMethod("DownloadAsync", [Type[]] @([string], [Threading.CancellationToken]))
$clearCacheMethod = $downloaderType.GetMethod("ClearCache",
    [Reflection.BindingFlags] "Static, Public, NonPublic")

function Get-ProfileDefinition {
    param([string]$ProfileName)

    switch ($ProfileName.ToLowerInvariant()) {
        "fast" {
            return @{
                Name = "Fast"
                BaseMode = "Fast"
            }
        }
        "balanced" {
            return @{
                Name = "Balanced"
                BaseMode = "Balanced"
            }
        }
        "thorough" {
            return @{
                Name = "Thorough"
                BaseMode = "Thorough"
            }
        }
        "balanced-nosynth" {
            return @{
                Name = "Balanced-NoSynth"
                BaseMode = "Balanced"
                AllowSyntheticFallbacks = $false
                EnabledProviders = @("Direct Site", "Google", "Twenty Icons", "DuckDuckGo", "Yandex")
                ProviderOrder = @("Direct Site", "Google", "Twenty Icons", "DuckDuckGo", "Yandex")
            }
        }
        "balanced-noduckduckgo" {
            return @{
                Name = "Balanced-NoDuckDuckGo"
                BaseMode = "Balanced"
                EnabledProviders = @("Direct Site", "Google", "Twenty Icons", "Yandex", "Favicone")
                ProviderOrder = @("Direct Site", "Google", "Twenty Icons", "Yandex", "Favicone")
            }
        }
        "balanced-notwenty" {
            return @{
                Name = "Balanced-NoTwentyIcons"
                BaseMode = "Balanced"
                EnabledProviders = @("Direct Site", "Google", "DuckDuckGo", "Yandex", "Favicone")
                ProviderOrder = @("Direct Site", "Google", "DuckDuckGo", "Yandex", "Favicone")
            }
        }
        "balanced-googlefavicone" {
            return @{
                Name = "Balanced-GoogleFavicone"
                BaseMode = "Balanced"
                EnabledProviders = @("Direct Site", "Google", "Favicone")
                ProviderOrder = @("Direct Site", "Google", "Favicone")
            }
        }
        "thorough-noiconhorse" {
            return @{
                Name = "Thorough-NoIconHorse"
                BaseMode = "Thorough"
                EnabledProviders = @("Direct Site", "Twenty Icons", "DuckDuckGo", "Google", "Yandex", "Favicone")
                ProviderOrder = @("Direct Site", "Twenty Icons", "DuckDuckGo", "Google", "Yandex", "Favicone")
            }
        }
        default {
            throw "Unknown profile '$ProfileName'"
        }
    }
}

function New-ConfigForProfile {
    param([string]$ProfileName)

    $definition = Get-ProfileDefinition -ProfileName $ProfileName

    $ace = New-Object KeePass.App.Configuration.AceCustomConfig
    $config = New-Object KeeFetch.Configuration -ArgumentList $ace
    $presetValue = [Enum]::Parse([KeeFetch.FetchPresetMode], $definition.BaseMode, $true)

    $config.FetchPresetMode = $presetValue
    $config.Timeout = [KeeFetch.Configuration]::GetPresetTimeout($presetValue)
    $config.UseThirdPartyFallbacks = [KeeFetch.Configuration]::GetPresetUseThirdPartyFallbacks($presetValue)
    $config.AllowSyntheticFallbacks = [KeeFetch.Configuration]::GetPresetAllowSyntheticFallbacks($presetValue)

    foreach ($providerName in $providerNames) {
        $enabled = [KeeFetch.Configuration]::IsProviderEnabledByPreset($presetValue, $providerName)
        $config.SetProviderEnabled($providerName, $enabled)
    }

    $providerOrder = [KeeFetch.Configuration]::GetPresetProviderOrderList($presetValue)

    if ($definition.ContainsKey("UseThirdPartyFallbacks")) {
        $config.UseThirdPartyFallbacks = [bool]$definition.UseThirdPartyFallbacks
    }

    if ($definition.ContainsKey("AllowSyntheticFallbacks")) {
        $config.AllowSyntheticFallbacks = [bool]$definition.AllowSyntheticFallbacks
    }

    if ($definition.ContainsKey("EnabledProviders")) {
        foreach ($providerName in $providerNames) {
            $enabled = $definition.EnabledProviders -contains $providerName
            $config.SetProviderEnabled($providerName, $enabled)
        }
    }

    if ($definition.ContainsKey("ProviderOrder")) {
        $providerOrder = $definition.ProviderOrder
    }

    $config.ProviderOrder = [string]::Join(",", $providerOrder)

    return [PSCustomObject]@{
        Name = $definition.Name
        Config = $config
        BaseMode = $definition.BaseMode
    }
}

function Invoke-Download {
    param(
        [object]$Config,
        [string]$Url
    )

    $downloader = $downloaderCtor.Invoke([object[]] @($Config.PSObject.BaseObject))
    $task = $downloadMethod.Invoke($downloader, @($Url, [Threading.CancellationToken]::None))
    return $task.GetAwaiter().GetResult()
}

# Helper to map FaviconResult to benchmark record
function Get-MachineOutcome {
    param([object]$FaviconResult)

    if ($null -eq $FaviconResult) { return "harness-error" }
    $statusName = $FaviconResult.Status.ToString()
    if ($statusName -eq "Success") {
        return "success"
    }
    # Check provider metrics for timeout/error
    $hasTimeout = $false
    $hasError = $false
    $hasCancel = $false
    try {
        foreach ($m in $FaviconResult.ProviderMetrics) {
            if ($null -eq $m) { continue }
            $outcome = [string]$m.Outcome
            if ($outcome -match "timeout") { $hasTimeout = $true }
            elseif ($outcome -match "cancel") { $hasCancel = $true }
            elseif ($outcome -match "error") { $hasError = $true }
        }
    } catch {}
    if ($hasTimeout) { return "timeout" }
    if ($hasCancel) { return "harness-error" }
    if ($hasError) { return "provider-error" }
    # Check diagnostics for invalid image
    try {
        $diag = [string]$FaviconResult.DiagnosticsSummary
        if ($diag -match "invalid") { return "invalid-image" }
    } catch {}
    return "not-found"
}

foreach ($profileName in $experiment.profiles) {
    $profile = New-ConfigForProfile -ProfileName $profileName
    $config = $profile.Config

    foreach ($cacheMode in $experiment.cache_modes) {
        for ($repetition = 1; $repetition -le $experiment.repetitions; $repetition++) {

            # Cache handling
            if ($cacheMode -eq "cold") {
                $clearCacheMethod.Invoke($null, @()) | Out-Null
            } elseif ($repetition -eq 1) {
                # For warm, clear once at start of warm sequence for this profile
                # Keep warm cache between repetitions, so only clear before first warm repetition
                # But if previous cache_mode was cold, warm should start with cold-cleared? We clear for first warm.
                $clearCacheMethod.Invoke($null, @()) | Out-Null
            }

            # Create run
            $run = New-KeeFetchRun -OutputRoot $outputRoot -ExperimentId $experiment.experiment_id -CorpusPath $corpusPath -CorpusVersion "v1" -Concurrency $experiment.concurrency -CacheMode $cacheMode -Profiles @($profileName) -Repetitions $experiment.repetitions -CacheModes $experiment.cache_modes
            $runDir = $run.Directory
            $runId = $run.RunId
            Write-Host ""
            Write-Host "=== Experiment $($experiment.experiment_id) | Profile $profileName | Cache $cacheMode | Repetition $repetition | Run $runId ==="

            # If ResumeRun provided and this is a warm repetition that should resume, we already loaded resumeKeys
            # But also need to handle per-run dedup via Add-KeeFetchResult itself
            # For sequential execution, process each fixture
            $completedCount = 0
            foreach ($row in $filteredRows) {
                $fixtureId = [string]$row.fixture_id
                $category = [string]$row.category
                $inputUrl = [string]$row.input_url

                # Resume skip check
                $resumeKey = "$profileName|$cacheMode|$fixtureId|$repetition"
                $simpleKey = "$fixtureId|$repetition"
                if ($resumeKeys.Contains($resumeKey) -or $resumeKeys.Contains($simpleKey)) {
                    Write-Host "Skipping $fixtureId (resume)"
                    continue
                }

                # Also check if this run already has this fixture (Add-KeeFetchResult dedup will handle, but we can pre-check)
                # Invoke download
                try {
                    $result = Invoke-Download -Config $config -Url $inputUrl
                } catch {
                    $result = [PSCustomObject]@{
                        Status = "NotFound"
                        Provider = ""
                        SelectedTier = "Rejected"
                        WasSyntheticFallback = $false
                        DiagnosticsSummary = "harness-exception: $($_.Exception.Message)"
                        ProviderMetrics = @()
                        ElapsedMilliseconds = 0
                        IconData = $null
                        Selection = $null
                    }
                }

                # Normalize result access: handle both real .NET object and synthetic fallback
                $statusName = ""
                try { $statusName = $result.Status.ToString() } catch { $statusName = [string]$result.Status }
                if ([string]::IsNullOrWhiteSpace($statusName) -and $result.PSObject.Properties.Name -contains 'Status') {
                    $statusName = [string]$result.Status
                    if ($statusName -match "NotFound") { $statusName = "NotFound" } elseif ($statusName -match "Success") { $statusName = "Success" }
                }

                $selectedProvider = ""
                try { $selectedProvider = [string]$result.Provider } catch {}
                if ([string]::IsNullOrWhiteSpace($selectedProvider)) { $selectedProvider = "" }

                $tierName = "Rejected"
                try {
                    $tierName = $result.SelectedTier.ToString()
                    if ([string]::IsNullOrWhiteSpace($tierName)) { $tierName = "Rejected" }
                } catch {
                    try { $tierName = [string]$result.SelectedTier } catch { $tierName = "Rejected" }
                }

                $isSynthetic = $false
                try { $isSynthetic = [bool]$result.WasSyntheticFallback } catch { $isSynthetic = $false }

                $placeholderSuspected = $false
                $blankSuspected = $false
                try {
                    if ($null -ne $result.Selection -and $null -ne $result.Selection.SelectedCandidate) {
                        $cand = $result.Selection.SelectedCandidate
                        $placeholderSuspected = [bool]$cand.IsPlaceholderSuspected
                        $blankSuspected = [bool]$cand.IsBlankSuspected
                        if ($cand.IsSynthetic) { $isSynthetic = $true }
                    }
                } catch {}

                $machineOutcome = Get-MachineOutcome -FaviconResult $result

                # per_provider_metrics
                $perProviderMetrics = @()
                try {
                    foreach ($m in $result.ProviderMetrics) {
                        if ($null -eq $m) { continue }
                        $entry = [ordered]@{
                            provider = [string]$m.ProviderName
                            calls = 1
                            elapsed = [int]$m.ElapsedMilliseconds
                            elapsed_ms = [int]$m.ElapsedMilliseconds
                            candidate_count = [int]$m.CandidateCount
                            outcome = [string]$m.Outcome
                            errors = 0
                        }
                        if ([string]$m.Outcome -match "error") { $entry.errors = 1 }
                        $perProviderMetrics += [PSCustomObject]$entry
                    }
                } catch {}

                # candidate_counts
                $candidateCounts = @{}
                try {
                    foreach ($m in $result.ProviderMetrics) {
                        if ($null -eq $m) { continue }
                        $candidateCounts[[string]$m.ProviderName] = [int]$m.CandidateCount
                    }
                } catch {}

                $totalElapsed = 0
                try { $totalElapsed = [int]$result.ElapsedMilliseconds } catch {}

                # cache behavior
                $cacheBehavior = "miss"
                $cacheHit = $false
                $coalesced = $false
                try {
                    $diag = [string]$result.DiagnosticsSummary
                    if (-not [string]::IsNullOrWhiteSpace($diag)) {
                        if ($diag.IndexOf("cache-hit", [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $diag.IndexOf("negative-cache-hit", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                            $cacheBehavior = "hit"
                            $cacheHit = $true
                        }
                        if ($diag.IndexOf("coalesced", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                            $coalesced = $true
                        }
                    }
                    # also check ProviderMetrics for Cache provider
                    foreach ($m in $result.ProviderMetrics) {
                        if ($null -eq $m) { continue }
                        if ([string]$m.ProviderName -eq "Cache" -and [string]$m.Outcome -eq "hit") {
                            $cacheBehavior = "hit"
                            $cacheHit = $true
                        }
                        if ([string]$m.ProviderName -eq "Coalesced") {
                            $coalesced = $true
                        }
                    }
                } catch {}

                # image fields
                $imageType = ""
                $imageWidth = 0
                $imageHeight = 0
                $imageByteSize = 0
                $imageValidation = ""
                try {
                    if ($null -ne $result.IconData -and $result.IconData.Length -gt 0) {
                        $imageByteSize = $result.IconData.Length
                        try {
                            $ms = New-Object System.IO.MemoryStream -ArgumentList @(,$result.IconData)
                            $img = [System.Drawing.Image]::FromStream($ms)
                            $imageWidth = $img.Width
                            $imageHeight = $img.Height
                            $fmt = $img.RawFormat
                            if ($fmt.Equals([System.Drawing.Imaging.ImageFormat]::Png)) { $imageType = "png" }
                            elseif ($fmt.Equals([System.Drawing.Imaging.ImageFormat]::Jpeg)) { $imageType = "jpeg" }
                            elseif ($fmt.Equals([System.Drawing.Imaging.ImageFormat]::Gif)) { $imageType = "gif" }
                            elseif ($fmt.Equals([System.Drawing.Imaging.ImageFormat]::Bmp)) { $imageType = "bmp" }
                            elseif ($fmt.Equals([System.Drawing.Imaging.ImageFormat]::Icon)) { $imageType = "ico" }
                            elseif ($fmt.Equals([System.Drawing.Imaging.ImageFormat]::Tiff)) { $imageType = "tiff" }
                            else { $imageType = "unknown" }
                            $imageValidation = "ok"
                            $img.Dispose()
                            $ms.Dispose()
                        } catch {
                            $imageValidation = "invalid"
                        }
                    } else {
                        $imageValidation = "no-image"
                        $imageByteSize = 0
                    }
                } catch {
                    $imageValidation = "unknown"
                }

                # Route through Add-KeeFetchResult
                $addParams = @{
                    RunDirectory = $runDir
                    FixtureId = $fixtureId
                    Category = $category
                    InputUrl = $inputUrl
                    SelectedProvider = $selectedProvider
                    Tier = $tierName
                    IsSynthetic = $isSynthetic
                    PlaceholderSuspected = $placeholderSuspected
                    BlankSuspected = $blankSuspected
                    MachineOutcome = $machineOutcome
                    PerProviderMetrics = $perProviderMetrics
                    ProviderMetrics = $perProviderMetrics
                    TotalElapsedMs = $totalElapsed
                    TotalElapsed = $totalElapsed
                    CacheBehavior = $cacheBehavior
                    CacheHit = $cacheHit
                    Coalesced = $coalesced
                    Coalescing = $coalesced
                    ImageType = $imageType
                    ImageWidth = $imageWidth
                    ImageHeight = $imageHeight
                    ImageByteSize = $imageByteSize
                    ImageValidation = $imageValidation
                    ExperimentId = $experiment.experiment_id
                    Profile = $profileName
                    Repetition = $repetition
                    RunId = $runId
                    CacheMode = $cacheMode
                }
                # CandidateCounts as hashtable
                if ($candidateCounts.Count -gt 0) {
                    $addParams['CandidateCounts'] = $candidateCounts
                    $addParams['CandidateCount'] = $candidateCounts.Count
                }

                Add-KeeFetchResult @addParams

                $completedCount++
                if (($completedCount % 25) -eq 0) {
                    Write-Host "Completed $completedCount/$($filteredRows.Count) for $profileName/$cacheMode rep $repetition"
                }
            }

            Complete-KeeFetchRun -RunDirectory $runDir
            Write-Host "Run completed: $runDir"
        }
    }
}

Write-Host ""
Write-Host "Experiment $($experiment.experiment_id) completed."
