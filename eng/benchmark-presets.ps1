param(
    [Parameter(Mandatory=$true)][string]$Experiment,
    [string]$ResumeRun = ''
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$keepassPath = "C:\Program Files\KeePass Password Safe 2\KeePass.exe"
$keepassPathEnv = [Environment]::GetEnvironmentVariable('KEEFETCH_KEEPASS_PATH')
if (-not [string]::IsNullOrWhiteSpace($keepassPathEnv)) {
    $keepassPath = $keepassPathEnv
}
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

$experimentConfig = Read-KeeFetchExperiment -ExperimentPath $experimentPath

# Resolve corpus and output root
$corpusPath = $experimentConfig.corpus_path
if ([string]::IsNullOrWhiteSpace($corpusPath)) {
    $corpusPath = $experimentConfig.corpus
    if (-not [System.IO.Path]::IsPathRooted($corpusPath)) {
        $corpusPath = Join-Path $repoRoot $corpusPath
    }
}
$outputRoot = $experimentConfig.output_root
if (-not [System.IO.Path]::IsPathRooted($outputRoot)) {
    $outputRoot = Join-Path $repoRoot $outputRoot
}

# Validate corpus
$vocabPath = Join-Path $repoRoot "KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json"
$hasFixtureFilter = $false
if ($experimentConfig.PSObject.Properties.Name -contains 'fixture_ids' -and $null -ne $experimentConfig.fixture_ids -and $experimentConfig.fixture_ids.Count -gt 0) {
    $hasFixtureFilter = $true
}

$filteredRows = @()
if ($hasFixtureFilter) {
    $allRows = @(Import-Csv -LiteralPath $corpusPath)
    $filterSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($fid in $experimentConfig.fixture_ids) { [void]$filterSet.Add([string]$fid) }
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

# Load resume keys if ResumeRun provided and resolve resume run directory for reopen
$resumeKeys = New-Object 'System.Collections.Generic.HashSet[string]'
$resumeRunDir = $null
$resumeRunInfo = $null
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
    # If resumePath is a file (results.ndjson), resolve to its directory; if directory, use directly
    $resumeDirCandidate = $resumePath
    if (Test-Path -LiteralPath $resumePath) {
        $item = Get-Item -LiteralPath $resumePath -ErrorAction SilentlyContinue
        if ($null -ne $item -and -not $item.PSIsContainer) {
            $resumeDirCandidate = Split-Path -Parent $resumePath
        }
    }
    $resumeRunDir = $resumeDirCandidate
    # Try to open existing run for validation (experiment/profile/cache metadata will be validated per-run)
    if (Test-Path -LiteralPath $resumeRunDir) {
        $runJsonCandidate = Join-Path $resumeRunDir "run.json"
        if (Test-Path -LiteralPath $runJsonCandidate) {
            try {
                $resumeRunInfo = Open-KeeFetchRun -RunDirectory $resumeRunDir -ExperimentId $experimentConfig.experiment_id -Concurrency $experimentConfig.concurrency
            } catch {
                throw "Failed to open resume run '$resumeRunDir': $($_.Exception.Message)"
            }
        }
    }
    # If resumePath is a directory, look for results.ndjson inside; if file, use directly
    $resumeNdjson = $null
    if (Test-Path -LiteralPath $resumePath) {
        $item2 = Get-Item -LiteralPath $resumePath -ErrorAction SilentlyContinue
        if ($null -ne $item2 -and $item2.PSIsContainer) {
            $candidateNdjson = Join-Path $resumePath "results.ndjson"
            if (Test-Path -LiteralPath $candidateNdjson) { $resumeNdjson = $candidateNdjson }
        } else {
            $resumeNdjson = $resumePath
            # if it was a directory file path (results.ndjson), we already have dir; ensure ndjson path is correct
            if (-not $resumeNdjson.ToLowerInvariant().EndsWith(".ndjson")) {
                $candidateNdjson2 = Join-Path $resumeRunDir "results.ndjson"
                if (Test-Path -LiteralPath $candidateNdjson2) { $resumeNdjson = $candidateNdjson2 }
            }
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
                $key = "$prof|$cm|$fid|$rep"
                [void]$resumeKeys.Add($key)
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

#region Candidate Authority
# All benchmark candidates are CUSTOM configurations with explicit providerIds/order + timeouts + synthetic.
# Do NOT map candidate ids (cand-*) to managed profile IDs (bulk-fast/everyday/privacy/max-coverage) or FetchPresetMode.
# New-ConfigForProfile for candidates MUST use the custom path via New-CustomConfigForCandidate.
# Managed profiles are only winners; candidates are experiment scaffolding.
#endregion

$script:candidateMap = @{}

function Load-CandidateMap {
    param([string]$ExperimentJsonPath)
    $script:candidateMap = @{}
    try {
        $raw = Get-Content -Raw -LiteralPath $ExperimentJsonPath | ConvertFrom-Json
        if ($raw.PSObject.Properties.Name -contains "candidates") {
            foreach ($c in @($raw.candidates)) {
                $cid = ""
                if ($c.PSObject.Properties.Name -contains "id") { $cid = [string]$c.id }
                if ([string]::IsNullOrWhiteSpace($cid)) { continue }
                $script:candidateMap[$cid] = $c
            }
        }
    } catch {
    }
}

function New-CustomConfigForCandidate {
    param(
        [Parameter(Mandatory=$true)][string]$CandidateId,
        [Parameter(Mandatory=$true)][object]$CandidateDef
    )
    # Constructs a CUSTOM Configuration with explicit providerIds/order + timeouts + synthetic.
    # This is the ONLY path for benchmark candidates. Do not use FetchPresetMode or managed profile lookup.

    $ace = New-Object KeePass.App.Configuration.AceCustomConfig
    $config = New-Object KeeFetch.Configuration -ArgumentList $ace

    # Force custom mode
    $config.FetchProfileId = "custom"

    # Resolve provider ids array
    $ids = @()
    if ($CandidateDef.PSObject.Properties.Name -contains "providerIds") { $ids = @($CandidateDef.providerIds) }
    elseif ($CandidateDef.PSObject.Properties.Name -contains "provider_ids") { $ids = @($CandidateDef.provider_ids) }
    if ($ids.Count -eq 0) { $ids = @("direct-site") }

    # Normalize to display names for Configuration
    $displayOrder = @()
    foreach ($rawId in $ids) {
        $trim = ([string]$rawId).Trim()
        if ([string]::IsNullOrWhiteSpace($trim)) { continue }
        # Try to map stable id to display name via catalog if available, else keep as-is
        $found = $null
        try {
            $catalogType = [KeeFetch.FetchProfiles.FetchProfileCatalog]
            $found = $catalogType::FindProvider($trim)
        } catch {
            $found = $null
        }
        if ($null -ne $found) {
            $displayOrder += $found.DisplayName
        } else {
            # fallback: title-case mapping for known ids
            switch ($trim.ToLowerInvariant()) {
                "direct-site" { $displayOrder += "Direct Site" }
                "twenty-icons" { $displayOrder += "Twenty Icons" }
                "duckduckgo" { $displayOrder += "DuckDuckGo" }
                "google" { $displayOrder += "Google" }
                "yandex" { $displayOrder += "Yandex" }
                "favicone" { $displayOrder += "Favicone" }
                "icon-horse" { $displayOrder += "Icon Horse" }
                default { $displayOrder += $trim }
            }
        }
    }

    # ProviderOrder is explicit order
    $config.ProviderOrder = [string]::Join(",", $displayOrder)

    # Enable only candidate providers
    $enabledSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($d in $displayOrder) { [void]$enabledSet.Add($d) }
    foreach ($providerName in $providerNames) {
        $enabled = $enabledSet.Contains($providerName)
        $config.SetProviderEnabled($providerName, $enabled)
    }

    # Synthetic flag explicit
    $allowSyn = $false
    if ($CandidateDef.PSObject.Properties.Name -contains "allowSynthetic") {
        try { $allowSyn = [bool]$CandidateDef.allowSynthetic } catch { $allowSyn = $false }
    }
    $config.AllowSyntheticFallbacks = $allowSyn

    # Third-party fallback flag: true if any non-direct provider enabled
    $hasThird = $false
    foreach ($d in $displayOrder) {
        if (-not $d.Equals("Direct Site", [StringComparison]::OrdinalIgnoreCase)) { $hasThird = $true; break }
    }
    $config.UseThirdPartyFallbacks = $hasThird

    # Timeouts: map cumulative to Configuration.Timeout (seconds), and store primary/fallback/cumulative
    # in AceCustomConfig for potential harness use. FaviconDownloader custom defaults are overridden
    # only via these stored values when FetchProfileId=custom.
    $primary = 6000
    $fallback = 3500
    $cumulative = 22000
    if ($CandidateDef.PSObject.Properties.Name -contains "primaryTimeout") { try { $primary = [int]$CandidateDef.primaryTimeout } catch {} }
    if ($CandidateDef.PSObject.Properties.Name -contains "fallbackTimeout") { try { $fallback = [int]$CandidateDef.fallbackTimeout } catch {} }
    if ($CandidateDef.PSObject.Properties.Name -contains "cumulativeTimeout") { try { $cumulative = [int]$CandidateDef.cumulativeTimeout } catch {} }
    # Also support Ms-suffixed names
    if ($CandidateDef.PSObject.Properties.Name -contains "primaryTimeoutMs") { try { $primary = [int]$CandidateDef.primaryTimeoutMs } catch {} }
    if ($CandidateDef.PSObject.Properties.Name -contains "fallbackTimeoutMs") { try { $fallback = [int]$CandidateDef.fallbackTimeoutMs } catch {} }
    if ($CandidateDef.PSObject.Properties.Name -contains "cumulativeTimeoutMs") { try { $cumulative = [int]$CandidateDef.cumulativeTimeoutMs } catch {} }

    $config.Timeout = [Math]::Max(5, [Math]::Min(60, [int]([Math]::Ceiling($cumulative / 1000.0))))
    # Persist explicit budgets for harness inspection (not used by production FaviconDownloader custom defaults yet,
    # but available for benchmark-presets to honor without guessing)
    $ace.SetLong("KeeFetch.CustomPrimaryTimeoutMs", $primary)
    $ace.SetLong("KeeFetch.CustomFallbackTimeoutMs", $fallback)
    $ace.SetLong("KeeFetch.CustomCumulativeTimeoutMs", $cumulative)
    $ace.SetString("KeeFetch.CustomCandidateId", $CandidateId)

    return [PSCustomObject]@{
        Name = $CandidateId
        Config = $config
        BaseMode = "Custom"
        CandidateDef = $CandidateDef
        IsCandidate = $true
    }
}

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

    # CANDIDATE AUTHORITY: candidate ids (cand-*) must use custom path with explicit providerIds/timeouts/synthetic, not managed profile mapping.
    # If caller passes a candidate id, prefer custom candidate path when available in $script:candidateMap.
    if ($ProfileName.ToLowerInvariant().StartsWith("cand-")) {
        if ($script:candidateMap.ContainsKey($ProfileName)) {
            return New-CustomConfigForCandidate -CandidateId $ProfileName -CandidateDef $script:candidateMap[$ProfileName]
        }
        # Unknown cand-* id with no map loaded: construct a would-be custom config and throw with guidance
        throw "Unknown benchmark candidate '$ProfileName'. Candidates must be defined as CUSTOM configs with explicit providerIds/timeouts/synthetic via New-CustomConfigForCandidate, not as managed profile IDs. Ensure Load-CandidateMap was called for the experiment file."
    }
    # Also handle candidate ids that are keys in candidateMap even without cand- prefix
    if ($script:candidateMap.ContainsKey($ProfileName)) {
        return New-CustomConfigForCandidate -CandidateId $ProfileName -CandidateDef $script:candidateMap[$ProfileName]
    }

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

function Start-DownloadTask {
    param(
        [object]$Config,
        [string]$Url,
        [string]$FixtureId = "",
        [string]$Category = "",
        [string]$InputUrl = ""
    )

    $downloader = $downloaderCtor.Invoke([object[]] @($Config.PSObject.BaseObject))
    $task = $downloadMethod.Invoke($downloader, @($Url, [Threading.CancellationToken]::None))
    $obj = [PSCustomObject]@{
        Url = $Url
        Task = $task
    }
    $obj | Add-Member -NotePropertyName "FixtureId" -NotePropertyValue $FixtureId -Force
    $obj | Add-Member -NotePropertyName "Category" -NotePropertyValue $Category -Force
    $obj | Add-Member -NotePropertyName "InputUrl" -NotePropertyValue $InputUrl -Force
    return $obj
}

function Convert-HarnessError {
    param(
        [Parameter(Mandatory=$true)][string]$RunDirectory,
        [Parameter(Mandatory=$true)][string]$FixtureId,
        [Parameter(Mandatory=$false)][string]$Category = "",
        [Parameter(Mandatory=$false)][string]$InputUrl = "",
        [Parameter(Mandatory=$false)][string]$ExceptionMessage = "",
        [Parameter(Mandatory=$false)][string]$ExperimentId = "",
        [Parameter(Mandatory=$false)][string]$Profile = "",
        [Parameter(Mandatory=$false)][int]$Repetition = 1,
        [Parameter(Mandatory=$false)][string]$RunId = "",
        [Parameter(Mandatory=$false)][string]$CacheMode = "cold",
        [Parameter(Mandatory=$false)][int]$Concurrency = 8
    )

    $diag = "harness-exception: $ExceptionMessage"
    if ([string]::IsNullOrWhiteSpace($ExceptionMessage)) {
        $diag = "harness-exception: unknown harness error"
    }
    $harnessMetric = [PSCustomObject]@{
        provider = "Harness"
        calls = 1
        elapsed = 0
        elapsed_ms = 0
        candidate_count = 0
        outcome = "harness-error"
        errors = 1
        diagnostic = $diag
    }
    $resultOverlay = [ordered]@{
        diagnostic = $diag
        diagnostics_summary = $diag
        error = $diag
    }
    Add-KeeFetchResult -RunDirectory $RunDirectory -Result $resultOverlay -FixtureId $FixtureId -Category $Category -InputUrl $InputUrl -SelectedProvider "" -Tier "Rejected" -IsSynthetic $false -PlaceholderSuspected $false -BlankSuspected $false -MachineOutcome "harness-error" -PerProviderMetrics @($harnessMetric) -ProviderMetrics @($harnessMetric) -TotalElapsedMs 0 -TotalElapsed 0 -CacheBehavior "miss" -CacheHit $false -Coalesced $false -Coalescing $false -ImageType "" -ImageWidth 0 -ImageHeight 0 -ImageByteSize 0 -ImageValidation "no-image" -ArtifactPath "" -ArtifactHash "" -ExperimentId $ExperimentId -Profile $Profile -Repetition $Repetition -RunId $RunId -CacheMode $CacheMode -Concurrency $Concurrency -CandidateCounts @{}
}

function Wait-OneDownloadTask {
    param([System.Collections.ArrayList]$Pending)

    $tasks = New-Object 'System.Collections.Generic.List[System.Threading.Tasks.Task]'
    foreach ($item in $Pending) {
        $tasks.Add([System.Threading.Tasks.Task]$item.Task)
    }

    $completedTask = [System.Threading.Tasks.Task]::WhenAny($tasks).GetAwaiter().GetResult()
    for ($i = 0; $i -lt $Pending.Count; $i++) {
        if ([object]::ReferenceEquals($Pending[$i].Task, $completedTask)) {
            $item = $Pending[$i]
            $Pending.RemoveAt($i)
            $result = $null
            $exceptionMessage = $null
            try {
                $result = $item.Task.GetAwaiter().GetResult()
            } catch {
                $exceptionMessage = $_.Exception.Message
                if ([string]::IsNullOrWhiteSpace($exceptionMessage) -and $null -ne $_.Exception.InnerException) {
                    $exceptionMessage = $_.Exception.InnerException.Message
                }
                if ([string]::IsNullOrWhiteSpace($exceptionMessage) -and $null -ne $_.Exception.InnerException -and $null -ne $_.Exception.InnerException.InnerException) {
                    $exceptionMessage = $_.Exception.InnerException.InnerException.Message
                }
                return [PSCustomObject]@{
                    FixtureId = $item.FixtureId
                    Category = $item.Category
                    InputUrl = $item.InputUrl
                    Url = $item.Url
                    Result = $null
                    HarnessError = $exceptionMessage
                    IsHarnessError = $true
                }
            }
            return [PSCustomObject]@{
                FixtureId = $item.FixtureId
                Category = $item.Category
                InputUrl = $item.InputUrl
                Url = $item.Url
                Result = $result
                HarnessError = $null
                IsHarnessError = $false
            }
        }
    }

    throw "Completed download task was not found in the pending set."
}

function Get-ArtifactExtension {
    param([string]$ImageType)

    $ext = "png"
    if ([string]::IsNullOrWhiteSpace($ImageType)) {
        $ext = "png"
    } elseif ($ImageType.ToLowerInvariant() -eq "jpeg") {
        $ext = "jpg"
    } elseif ($ImageType.ToLowerInvariant() -eq "jpg") {
        $ext = "jpg"
    } elseif ($ImageType.ToLowerInvariant() -eq "png") {
        $ext = "png"
    } elseif ($ImageType.ToLowerInvariant() -eq "gif") {
        $ext = "gif"
    } elseif ($ImageType.ToLowerInvariant() -eq "bmp") {
        $ext = "bmp"
    } elseif ($ImageType.ToLowerInvariant() -eq "ico") {
        $ext = "ico"
    } elseif ($ImageType.ToLowerInvariant() -eq "tiff") {
        $ext = "tiff"
    } else {
        $ext = $ImageType.ToLowerInvariant()
    }
    return $ext
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

# Load candidate map for custom-config authority (profile-candidates-v13 etc.)
Load-CandidateMap -ExperimentJsonPath $experimentPath

# Resume single-run path: if ResumeRun points to an existing run, reopen it instead of creating new runs
if ($null -ne $resumeRunInfo) {
    $resumeMeta = $resumeRunInfo.Metadata
    $resumeProfileName = ""
    if ($resumeMeta.PSObject.Properties.Name -contains 'profiles' -and $null -ne $resumeMeta.profiles -and @($resumeMeta.profiles).Count -gt 0) {
        $resumeProfileName = [string]@($resumeMeta.profiles)[0]
    }
    elseif ($resumeMeta.PSObject.Properties.Name -contains 'profile') {
        $resumeProfileName = [string]$resumeMeta.profile
    }
    $resumeCacheMode = ""
    if ($resumeMeta.PSObject.Properties.Name -contains 'cache_mode') { $resumeCacheMode = [string]$resumeMeta.cache_mode }
    # Validate profile and cache_mode are in experiment
    $foundProfile = $false
    foreach ($p in $experimentConfig.profiles) { if ([string]$p -eq $resumeProfileName) { $foundProfile = $true; break } }
    if (-not $foundProfile) { throw "Resume profile '$resumeProfileName' not found in experiment profiles." }
    $foundCache = $false
    foreach ($m in $experimentConfig.cache_modes) { if ([string]$m -eq $resumeCacheMode) { $foundCache = $true; break } }
    if (-not $foundCache) { throw "Resume cache_mode '$resumeCacheMode' not found in experiment cache_modes." }
    $profile = New-ConfigForProfile -ProfileName $resumeProfileName
    $config = $profile.Config
    $cacheMode = $resumeCacheMode
    $repetition = 1
    if ($resumeMeta.PSObject.Properties.Name -contains 'repetitions') {
        try { $repetition = [int]$resumeMeta.repetitions } catch {}
    }
    # If run has explicit repetition field, prefer it (covers per-repetition runs)
    if ($resumeMeta.PSObject.Properties.Name -contains 'repetition') {
        try { $repetition = [int]$resumeMeta.repetition } catch {}
    }
    if ($repetition -lt 1) { $repetition = 1 }
    # Use existing run directory
    $runDir = $resumeRunInfo.Directory
    $runId = $resumeRunInfo.RunId
    Write-Host ""
    Write-Host "=== Resuming Experiment $($experimentConfig.experiment_id) | Profile $resumeProfileName | Cache $cacheMode | Run $runId ==="
    # Build pending list excluding already completed fixtures (resumeKeys already loaded, but also check run's ndjson via Add-KeeFetchResult dedup)
    $pendingRows = @()
    foreach ($row in $filteredRows) {
        $fid = [string]$row.fixture_id
        $keySimple = "$fid|$repetition"
        $keyFull = "$resumeProfileName|$cacheMode|$fid|$repetition"
        if ($resumeKeys.Contains($keySimple) -or $resumeKeys.Contains($keyFull)) {
            Write-Host "Skipping $fid (resume)"
            continue
        }
        $pendingRows += $row
    }
    $concurrencyLimit = [int]$experimentConfig.concurrency
    if ($concurrencyLimit -lt 1) { $concurrencyLimit = 1 }
    $pending = New-Object System.Collections.ArrayList
    $nextIndex = 0
    $completedCount = 0
    $totalPending = $pendingRows.Count
    # Helper to process completed result into benchmark record and append
    function ConvertAndAdd-Result {
        param([object]$Wrapped)
        if ($null -ne $Wrapped.PSObject.Properties['IsHarnessError'] -and [bool]$Wrapped.IsHarnessError) {
            $msg = ""
            if ($null -ne $Wrapped.PSObject.Properties['HarnessError']) { $msg = [string]$Wrapped.HarnessError }
            Convert-HarnessError -RunDirectory $runDir -FixtureId ([string]$Wrapped.FixtureId) -Category ([string]$Wrapped.Category) -InputUrl ([string]$Wrapped.InputUrl) -ExceptionMessage $msg -ExperimentId $experimentConfig.experiment_id -Profile $resumeProfileName -Repetition $repetition -RunId $runId -CacheMode $cacheMode -Concurrency $experimentConfig.concurrency
            return
        }
        $result = $Wrapped.Result
        $fixtureId = [string]$Wrapped.FixtureId
        $category = [string]$Wrapped.Category
        $inputUrl = [string]$Wrapped.InputUrl
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
        $candidateCounts = @{}
        try {
            foreach ($m in $result.ProviderMetrics) {
                if ($null -eq $m) { continue }
                $candidateCounts[[string]$m.ProviderName] = [int]$m.CandidateCount
            }
        } catch {}
        $totalElapsed = 0
        try { $totalElapsed = [int]$result.ElapsedMilliseconds } catch {}
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
        $artifactPath = ""
        $artifactHash = ""
        $isSuccessForArtifact = $false
        if ($machineOutcome -eq "success") {
            $isSuccessForArtifact = $true
        } else {
            try {
                $sName = $result.Status.ToString()
                if ($sName -eq "Success") { $isSuccessForArtifact = $true }
            } catch {
                if ($statusName -eq "Success") { $isSuccessForArtifact = $true }
            }
        }
        if ($isSuccessForArtifact) {
            $bytesForArtifact = $null
            try { $bytesForArtifact = $result.IconData } catch { $bytesForArtifact = $null }
            if ($null -ne $bytesForArtifact -and $bytesForArtifact.Length -gt 0) {
                $artifactsDir = Join-Path $runDir "artifacts"
                if (-not (Test-Path -LiteralPath $artifactsDir)) {
                    New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
                }
                $ext = Get-ArtifactExtension -ImageType $imageType
                $fileName = "$fixtureId-r$repetition.$ext"
                $fullPath = Join-Path $artifactsDir $fileName
                try {
                    [System.IO.File]::WriteAllBytes($fullPath, $bytesForArtifact)
                    $artifactPath = "artifacts/$fileName"
                    $sha = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $hashBytes = $sha.ComputeHash($bytesForArtifact)
                        $artifactHash = [BitConverter]::ToString($hashBytes).Replace("-","").ToLowerInvariant()
                    } finally {
                        if ($null -ne $sha) { $sha.Dispose() }
                    }
                } catch {
                    $artifactPath = ""
                    $artifactHash = ""
                }
            }
        }
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
            ArtifactPath = $artifactPath
            ArtifactHash = $artifactHash
            ExperimentId = $experimentConfig.experiment_id
            Profile = $resumeProfileName
            Repetition = $repetition
            RunId = $runId
            CacheMode = $cacheMode
            Concurrency = $experimentConfig.concurrency
        }
        if ($candidateCounts.Count -gt 0) {
            $addParams['CandidateCounts'] = $candidateCounts
            $addParams['CandidateCount'] = $candidateCounts.Count
        }
        Add-KeeFetchResult @addParams
    }
    while ($nextIndex -lt $pendingRows.Count -or $pending.Count -gt 0) {
        while ($nextIndex -lt $pendingRows.Count -and $pending.Count -lt $concurrencyLimit) {
            $row = $pendingRows[$nextIndex]
            $taskObj = $null
            $enqueueFailed = $false
            $enqueueMsg = ""
            try {
                $taskObj = Start-DownloadTask -Config $config -Url $row.input_url -FixtureId $row.fixture_id -Category $row.category -InputUrl $row.input_url
            } catch {
                $enqueueFailed = $true
                $enqueueMsg = $_.Exception.Message
                if ([string]::IsNullOrWhiteSpace($enqueueMsg) -and $null -ne $_.Exception.InnerException) { $enqueueMsg = $_.Exception.InnerException.Message }
            }
            if ($enqueueFailed) {
                $fidEnq = [string]$row.fixture_id
                $catEnq = [string]$row.category
                $urlEnq = [string]$row.input_url
                Convert-HarnessError -RunDirectory $runDir -FixtureId $fidEnq -Category $catEnq -InputUrl $urlEnq -ExceptionMessage $enqueueMsg -ExperimentId $experimentConfig.experiment_id -Profile $resumeProfileName -Repetition $repetition -RunId $runId -CacheMode $cacheMode -Concurrency $experimentConfig.concurrency
                $nextIndex++
                $completedCount++
                if (($completedCount % 25) -eq 0 -or $completedCount -eq $totalPending) {
                    Write-Host "Completed $completedCount/$totalPending for $resumeProfileName/$cacheMode rep $repetition"
                }
                continue
            }
            [void]$pending.Add($taskObj)
            $nextIndex++
        }
        if ($pending.Count -eq 0) { break }
        $wrapped = Wait-OneDownloadTask -Pending $pending
        try {
            ConvertAndAdd-Result -Wrapped $wrapped
        } catch {
            $msgOuter = $_.Exception.Message
            if ([string]::IsNullOrWhiteSpace($msgOuter) -and $null -ne $_.Exception.InnerException) { $msgOuter = $_.Exception.InnerException.Message }
            $fidOuter = ""
            $catOuter = ""
            $urlOuter = ""
            try { $fidOuter = [string]$wrapped.FixtureId } catch {}
            try { $catOuter = [string]$wrapped.Category } catch {}
            try { $urlOuter = [string]$wrapped.InputUrl } catch {}
            if (-not [string]::IsNullOrWhiteSpace($fidOuter)) {
                Convert-HarnessError -RunDirectory $runDir -FixtureId $fidOuter -Category $catOuter -InputUrl $urlOuter -ExceptionMessage $msgOuter -ExperimentId $experimentConfig.experiment_id -Profile $resumeProfileName -Repetition $repetition -RunId $runId -CacheMode $cacheMode -Concurrency $experimentConfig.concurrency
            } else {
                throw
            }
        }
        $completedCount++
        if (($completedCount % 25) -eq 0 -or $completedCount -eq $totalPending) {
            Write-Host "Completed $completedCount/$totalPending for $resumeProfileName/$cacheMode rep $repetition"
        }
    }
    Complete-KeeFetchRun -RunDirectory $runDir
    Write-Host "Run completed (resumed): $runDir"
} else {
    foreach ($profileName in $experimentConfig.profiles) {
        $profile = New-ConfigForProfile -ProfileName $profileName
        $config = $profile.Config

        foreach ($cacheMode in $experimentConfig.cache_modes) {
            for ($repetition = 1; $repetition -le $experimentConfig.repetitions; $repetition++) {

                if ($cacheMode -eq "cold") {
                    $clearCacheMethod.Invoke($null, @()) | Out-Null
                } elseif ($repetition -eq 1) {
                    $clearCacheMethod.Invoke($null, @()) | Out-Null
                }

                $run = New-KeeFetchRun -OutputRoot $outputRoot -ExperimentId $experimentConfig.experiment_id -CorpusPath $corpusPath -CorpusVersion "v1" -Concurrency $experimentConfig.concurrency -CacheMode $cacheMode -Profiles @($profileName) -Repetitions $experimentConfig.repetitions -CacheModes $experimentConfig.cache_modes
                $runDir = $run.Directory
                $runId = $run.RunId
                Write-Host ""
                Write-Host "=== Experiment $($experimentConfig.experiment_id) | Profile $profileName | Cache $cacheMode | Repetition $repetition | Run $runId ==="

                $pendingRows = @()
                foreach ($row in $filteredRows) {
                    $fid = [string]$row.fixture_id
                    $keyFull = "$profileName|$cacheMode|$fid|$repetition"
                    $keySimple = "$fid|$repetition"
                    if ($resumeKeys.Contains($keyFull) -or $resumeKeys.Contains($keySimple)) {
                        Write-Host "Skipping $fid (resume)"
                        continue
                    }
                    $pendingRows += $row
                }

                $concurrencyLimit = [int]$experimentConfig.concurrency
                if ($concurrencyLimit -lt 1) { $concurrencyLimit = 1 }
                $pending = New-Object System.Collections.ArrayList
                $nextIndex = 0
                $completedCount = 0
                $totalPending = $pendingRows.Count

                function ConvertAndAdd-ResultInner {
                    param([object]$Wrapped)
                    if ($null -ne $Wrapped.PSObject.Properties['IsHarnessError'] -and [bool]$Wrapped.IsHarnessError) {
                        $msgInner = ""
                        if ($null -ne $Wrapped.PSObject.Properties['HarnessError']) { $msgInner = [string]$Wrapped.HarnessError }
                        Convert-HarnessError -RunDirectory $runDir -FixtureId ([string]$Wrapped.FixtureId) -Category ([string]$Wrapped.Category) -InputUrl ([string]$Wrapped.InputUrl) -ExceptionMessage $msgInner -ExperimentId $experimentConfig.experiment_id -Profile $profileName -Repetition $repetition -RunId $runId -CacheMode $cacheMode -Concurrency $experimentConfig.concurrency
                        return
                    }
                    $result = $Wrapped.Result
                    $fixtureId = [string]$Wrapped.FixtureId
                    $category = [string]$Wrapped.Category
                    $inputUrl = [string]$Wrapped.InputUrl
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
                    $candidateCounts = @{}
                    try {
                        foreach ($m in $result.ProviderMetrics) {
                            if ($null -eq $m) { continue }
                            $candidateCounts[[string]$m.ProviderName] = [int]$m.CandidateCount
                        }
                    } catch {}
                    $totalElapsed = 0
                    try { $totalElapsed = [int]$result.ElapsedMilliseconds } catch {}
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
                    $artifactPath = ""
                    $artifactHash = ""
                    $isSuccessForArtifactInner = $false
                    if ($machineOutcome -eq "success") {
                        $isSuccessForArtifactInner = $true
                    } else {
                        try {
                            $sN = $result.Status.ToString()
                            if ($sN -eq "Success") { $isSuccessForArtifactInner = $true }
                        } catch {
                            if ($statusName -eq "Success") { $isSuccessForArtifactInner = $true }
                        }
                    }
                    if ($isSuccessForArtifactInner) {
                        $bytesInner = $null
                        try { $bytesInner = $result.IconData } catch { $bytesInner = $null }
                        if ($null -ne $bytesInner -and $bytesInner.Length -gt 0) {
                            $artifactsDirInner = Join-Path $runDir "artifacts"
                            if (-not (Test-Path -LiteralPath $artifactsDirInner)) {
                                New-Item -ItemType Directory -Path $artifactsDirInner -Force | Out-Null
                            }
                            $extInner = Get-ArtifactExtension -ImageType $imageType
                            $fileNameInner = "$fixtureId-r$repetition.$extInner"
                            $fullPathInner = Join-Path $artifactsDirInner $fileNameInner
                            try {
                                [System.IO.File]::WriteAllBytes($fullPathInner, $bytesInner)
                                $artifactPath = "artifacts/$fileNameInner"
                                $shaInner = [System.Security.Cryptography.SHA256]::Create()
                                try {
                                    $hashInner = $shaInner.ComputeHash($bytesInner)
                                    $artifactHash = [BitConverter]::ToString($hashInner).Replace("-","").ToLowerInvariant()
                                } finally {
                                    if ($null -ne $shaInner) { $shaInner.Dispose() }
                                }
                            } catch {
                                $artifactPath = ""
                                $artifactHash = ""
                            }
                        }
                    }
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
                        ArtifactPath = $artifactPath
                        ArtifactHash = $artifactHash
                        ExperimentId = $experimentConfig.experiment_id
                        Profile = $profileName
                        Repetition = $repetition
                        RunId = $runId
                        CacheMode = $cacheMode
                        Concurrency = $experimentConfig.concurrency
                    }
                    if ($candidateCounts.Count -gt 0) {
                        $addParams['CandidateCounts'] = $candidateCounts
                        $addParams['CandidateCount'] = $candidateCounts.Count
                    }
                    Add-KeeFetchResult @addParams
                }

                while ($nextIndex -lt $pendingRows.Count -or $pending.Count -gt 0) {
                    while ($nextIndex -lt $pendingRows.Count -and $pending.Count -lt $concurrencyLimit) {
                        $row = $pendingRows[$nextIndex]
                        $taskObjInner = $null
                        $enqueueInnerFailed = $false
                        $enqueueInnerMsg = ""
                        try {
                            $taskObjInner = Start-DownloadTask -Config $config -Url $row.input_url -FixtureId $row.fixture_id -Category $row.category -InputUrl $row.input_url
                        } catch {
                            $enqueueInnerFailed = $true
                            $enqueueInnerMsg = $_.Exception.Message
                            if ([string]::IsNullOrWhiteSpace($enqueueInnerMsg) -and $null -ne $_.Exception.InnerException) { $enqueueInnerMsg = $_.Exception.InnerException.Message }
                        }
                        if ($enqueueInnerFailed) {
                            $fidInnerEnq = [string]$row.fixture_id
                            $catInnerEnq = [string]$row.category
                            $urlInnerEnq = [string]$row.input_url
                            Convert-HarnessError -RunDirectory $runDir -FixtureId $fidInnerEnq -Category $catInnerEnq -InputUrl $urlInnerEnq -ExceptionMessage $enqueueInnerMsg -ExperimentId $experimentConfig.experiment_id -Profile $profileName -Repetition $repetition -RunId $runId -CacheMode $cacheMode -Concurrency $experimentConfig.concurrency
                            $nextIndex++
                            $completedCount++
                            if (($completedCount % 25) -eq 0 -or $completedCount -eq $totalPending) {
                                Write-Host "Completed $completedCount/$totalPending for $profileName/$cacheMode rep $repetition"
                            }
                            continue
                        }
                        [void]$pending.Add($taskObjInner)
                        $nextIndex++
                    }
                    if ($pending.Count -eq 0) { break }
                    $wrapped = Wait-OneDownloadTask -Pending $pending
                    try {
                        ConvertAndAdd-ResultInner -Wrapped $wrapped
                    } catch {
                        $msgInnerOuter = $_.Exception.Message
                        if ([string]::IsNullOrWhiteSpace($msgInnerOuter) -and $null -ne $_.Exception.InnerException) { $msgInnerOuter = $_.Exception.InnerException.Message }
                        $fidIO = ""
                        $catIO = ""
                        $urlIO = ""
                        try { $fidIO = [string]$wrapped.FixtureId } catch {}
                        try { $catIO = [string]$wrapped.Category } catch {}
                        try { $urlIO = [string]$wrapped.InputUrl } catch {}
                        if (-not [string]::IsNullOrWhiteSpace($fidIO)) {
                            Convert-HarnessError -RunDirectory $runDir -FixtureId $fidIO -Category $catIO -InputUrl $urlIO -ExceptionMessage $msgInnerOuter -ExperimentId $experimentConfig.experiment_id -Profile $profileName -Repetition $repetition -RunId $runId -CacheMode $cacheMode -Concurrency $experimentConfig.concurrency
                        } else {
                            throw
                        }
                    }
                    $completedCount++
                    if (($completedCount % 25) -eq 0 -or $completedCount -eq $totalPending) {
                        Write-Host "Completed $completedCount/$totalPending for $profileName/$cacheMode rep $repetition"
                    }
                }

                Complete-KeeFetchRun -RunDirectory $runDir
                Write-Host "Run completed: $runDir"
            }
        }
    }
}

Write-Host ""
Write-Host "Experiment $($experimentConfig.experiment_id) completed."