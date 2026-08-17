param(
    [Parameter(Mandatory=$true)][string]$Experiment,
    [string]$ResumeRun = ''
)

$ErrorActionPreference = "Stop"

# Executes a benchmark experiment with explicit matrix-cell identity,
# fingerprinted provenance, a deterministic interleaved schedule, warm-up
# methodology, and experiment-level resume. Every run directory records the
# experiment/corpus/binary fingerprints plus the per-candidate effective
# policy fingerprint resolved through the real FaviconDownloader path, so the
# selector can fail closed on any mismatch.

$repoRoot = Split-Path -Parent $PSScriptRoot
$keepassPath = "C:\Program Files\KeePass Password Safe 2\KeePass.exe"
$keepassPathEnv = [Environment]::GetEnvironmentVariable('KEEFETCH_KEEPASS_PATH')
if (-not [string]::IsNullOrWhiteSpace($keepassPathEnv)) {
    $keepassPath = $keepassPathEnv
} elseif (-not (Test-Path -LiteralPath $keepassPath)) {
    $localFallback = "C:\Dev\tools\KeePass-2.60\KeePass.exe"
    if (Test-Path -LiteralPath $localFallback) {
        $keepassPath = $localFallback
    }
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

function Get-Sha256HexFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $hash = $sha.ComputeHash($stream)
        } finally {
            $stream.Dispose()
        }
        return [BitConverter]::ToString($hash).Replace("-","").ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

function Assert-CandidateDefinition {
    param([Parameter(Mandatory=$true)][object]$Candidate)

    $id = [string]$Candidate.id
    if ([string]::IsNullOrWhiteSpace($id)) { throw "Candidate definition missing id." }

    $providerIds = @()
    if ($Candidate.PSObject.Properties.Name -contains 'providerIds') { $providerIds = @($Candidate.providerIds) }
    if ($providerIds.Count -eq 0) { throw "Candidate '$id' must declare at least one providerId." }
    $seenProviders = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($providerId in $providerIds) {
        $pidText = ([string]$providerId).Trim()
        if ([string]::IsNullOrWhiteSpace($pidText)) { throw "Candidate '$id' has an empty providerId." }
        if (-not $seenProviders.Add($pidText)) { throw "Candidate '$id' declares duplicate providerId '$pidText'." }
        if ($null -eq [KeeFetch.FetchProfiles.FetchProfileCatalog]::FindProvider($pidText)) {
            throw "Candidate '$id' references unknown providerId '$pidText'."
        }
    }

    foreach ($field in @('primaryTimeout','fallbackTimeout','cumulativeTimeout')) {
        if (-not ($Candidate.PSObject.Properties.Name -contains $field)) {
            throw "Candidate '$id' must declare $field explicitly."
        }
        $raw = $Candidate.$field
        if (($raw -isnot [int]) -and ($raw -isnot [long])) {
            throw "Candidate '$id' has a non-integral $field."
        }
        $value = [long]$raw
        if ($value -lt 250 -or $value -gt 300000) {
            throw "Candidate '$id' has an out-of-range ${field}: $value ms."
        }
    }
    if ([long]$Candidate.cumulativeTimeout -lt [long]$Candidate.primaryTimeout) {
        throw "Candidate '$id' cumulativeTimeout must be >= primaryTimeout."
    }

    foreach ($field in @('allowSynthetic','stopAfterStrongResolved','allowAndroidStoreLookup')) {
        if (-not ($Candidate.PSObject.Properties.Name -contains $field)) {
            throw "Candidate '$id' must declare $field explicitly; behavior is never inferred from the candidate name."
        }
        if ($Candidate.$field -isnot [bool]) {
            throw "Candidate '$id' has a non-boolean $field."
        }
    }
}

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

# Candidate authority: every candidate is a CUSTOM configuration with the
# full execution policy recorded in the experiment definition. Managed
# profile lookups are never used for candidates. Candidate validation runs
# after the KeeFetch assembly load below so provider ids resolve through the
# real catalog.
$script:candidateMap = @{}
$rawExperiment = Get-Content -Raw -LiteralPath $experimentPath | ConvertFrom-Json
$scheduleSeed = 0
if ($rawExperiment.PSObject.Properties.Name -contains 'schedule_seed') {
    try { $scheduleSeed = [int]$rawExperiment.schedule_seed } catch { $scheduleSeed = 0 }
}

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
if ($experimentConfig.PSObject.Properties.Name -contains 'fixture_ids' -and $null -ne $experimentConfig.fixture_ids -and @($experimentConfig.fixture_ids).Count -gt 0) {
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
    if (Test-Path -LiteralPath $vocabPath) {
        Test-KeeFetchCorpus -CsvPath $corpusPath -VocabularyPath $vocabPath | Out-Null
    }
    $filteredRows = @(Import-Csv -LiteralPath $corpusPath)
}
$expectedRowCount = $filteredRows.Count
if ($expectedRowCount -eq 0) { throw "Corpus is empty." }

if (-not (Test-Path -LiteralPath $keepassPath)) {
    throw "KeePass.exe not found at $keepassPath (set KEEFETCH_KEEPASS_PATH to override)."
}
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build KeeFetch first. Missing $assemblyPath"
}

[void][Reflection.Assembly]::LoadFrom($keepassPath)
$keefetchAssembly = [Reflection.Assembly]::LoadFrom($assemblyPath)

# Strict candidate validation (fail closed): unique candidate ids, unique
# catalog-resolvable provider ids, integral timeouts in sane ranges, real
# booleans for every behavior flag.
$candidateIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
if ($rawExperiment.PSObject.Properties.Name -contains 'candidates') {
    foreach ($c in @($rawExperiment.candidates)) {
        Assert-CandidateDefinition -Candidate $c
        if (-not $candidateIds.Add([string]$c.id)) {
            throw "Duplicate candidate id '$($c.id)' in experiment definition."
        }
        $script:candidateMap[[string]$c.id] = $c
    }
}

# profiles[] and candidates[] must describe the exact same candidate ID set.
$profileOrder = @()
$profileIdSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($p in @($experimentConfig.profiles)) {
    $name = [string]$p
    if ([string]::IsNullOrWhiteSpace($name)) { throw "Experiment profiles[] contains an empty entry." }
    if (-not $profileIdSet.Add($name)) { throw "Duplicate profile id '$name' in experiment profiles[]." }
    $profileOrder += $name
}
foreach ($name in $profileOrder) {
    if (-not $script:candidateMap.ContainsKey($name)) {
        throw "Unknown benchmark candidate '$name'. Candidates must be defined in the experiment file."
    }
}
foreach ($cid in $candidateIds) {
    if (-not $profileIdSet.Contains($cid)) {
        throw "Candidate '$cid' is defined in candidates[] but missing from profiles[]."
    }
}

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
$resolvedPolicyProperty = $downloaderType.GetProperty("ResolvedPolicy",
    [Reflection.BindingFlags] "Instance, Public, NonPublic")
if ($null -eq $resolvedPolicyProperty) {
    throw "FaviconDownloader.ResolvedPolicy not found; rebuild required for policy fingerprints."
}

function New-CustomConfigForCandidate {
    param(
        [Parameter(Mandatory=$true)][string]$CandidateId,
        [Parameter(Mandatory=$true)][object]$CandidateDef
    )
    # Constructs a CUSTOM Configuration carrying the candidate's exact
    # execution policy via the benchmark override keys. The policy is resolved
    # and fingerprinted through the real FaviconDownloader path.

    $ace = New-Object KeePass.App.Configuration.AceCustomConfig
    $config = New-Object KeeFetch.Configuration -ArgumentList $ace

    $config.FetchProfileId = "custom"

    $ids = @()
    if ($CandidateDef.PSObject.Properties.Name -contains "providerIds") { $ids = @($CandidateDef.providerIds) }
    elseif ($CandidateDef.PSObject.Properties.Name -contains "provider_ids") { $ids = @($CandidateDef.provider_ids) }
    if ($ids.Count -eq 0) { throw "Candidate '$CandidateId' must declare at least one providerId." }

    # Unknown/typo provider ids fail immediately; the policy fingerprint must
    # correspond to the executable chain exactly.
    $catalogType = [KeeFetch.FetchProfiles.FetchProfileCatalog]
    $displayOrder = @()
    $hasThird = $false
    foreach ($rawId in $ids) {
        $trim = ([string]$rawId).Trim()
        if ([string]::IsNullOrWhiteSpace($trim)) { continue }
        $found = $catalogType::FindProvider($trim)
        if ($null -eq $found) {
            throw "Candidate '$CandidateId' references unknown providerId '$trim'."
        }
        $displayOrder += $found.DisplayName
        if ($found.IsThirdParty) { $hasThird = $true }
    }

    $config.ProviderOrder = [string]::Join(",", $displayOrder)

    $enabledSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($d in $displayOrder) { [void]$enabledSet.Add($d) }
    foreach ($providerName in $providerNames) {
        $config.SetProviderEnabled($providerName, $enabledSet.Contains($providerName))
    }

    # All behavior fields were validated fail-closed by Assert-CandidateDefinition;
    # apply them verbatim so the resolved policy matches the recorded fingerprint.
    $config.AllowSyntheticFallbacks = [bool]$CandidateDef.allowSynthetic
    $config.UseThirdPartyFallbacks = $hasThird

    $primary = [int]$CandidateDef.primaryTimeout
    $fallback = [int]$CandidateDef.fallbackTimeout
    $cumulative = [int]$CandidateDef.cumulativeTimeout
    $stop = [bool]$CandidateDef.stopAfterStrongResolved
    $allowAndroidStore = [bool]$CandidateDef.allowAndroidStoreLookup

    $config.Timeout = [Math]::Max(5, [Math]::Min(60, [int]([Math]::Ceiling($cumulative / 1000.0))))
    $ace.SetLong("KeeFetch.CustomPrimaryTimeoutMs", $primary)
    $ace.SetLong("KeeFetch.CustomFallbackTimeoutMs", $fallback)
    $ace.SetLong("KeeFetch.CustomCumulativeTimeoutMs", $cumulative)
    $ace.SetLong("KeeFetch.CustomStopAfterStrongResolved", [int]$stop)
    $ace.SetLong("KeeFetch.CustomAllowAndroidStoreLookup", [int]$allowAndroidStore)
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
            return @{ Name = "Fast"; BaseMode = "Fast" }
        }
        "balanced" {
            return @{ Name = "Balanced"; BaseMode = "Balanced" }
        }
        "thorough" {
            return @{ Name = "Thorough"; BaseMode = "Thorough" }
        }
        default {
            throw "Unknown profile '$ProfileName'"
        }
    }
}

function New-ConfigForProfile {
    param([string]$ProfileName)

    if ($ProfileName.ToLowerInvariant().StartsWith("cand-")) {
        if ($script:candidateMap.ContainsKey($ProfileName)) {
            return New-CustomConfigForCandidate -CandidateId $ProfileName -CandidateDef $script:candidateMap[$ProfileName]
        }
        throw "Unknown benchmark candidate '$ProfileName'. Candidates must be defined in the experiment file."
    }
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
    $config.ProviderOrder = [string]::Join(",", $providerOrder)

    return [PSCustomObject]@{
        Name = $definition.Name
        Config = $config
        BaseMode = $definition.BaseMode
    }
}

# Provenance fingerprints, computed once for the whole experiment.
$experimentFingerprint = Get-Sha256HexFile -Path $experimentPath
$corpusFingerprint = Get-Sha256HexFile -Path $corpusPath
$binaryHash = Get-Sha256HexFile -Path $assemblyPath

# Resolve every profile's effective policy through the real downloader path.
$profileConfigs = @{}
$profilePolicyFingerprints = @{}
foreach ($profileName in $profileOrder) {
    $profile = New-ConfigForProfile -ProfileName $profileName
    $profileConfigs[$profileName] = $profile.Config
    $probeDownloader = $downloaderCtor.Invoke([object[]] @($profile.Config.PSObject.BaseObject))
    $policy = $resolvedPolicyProperty.GetValue($probeDownloader, $null)
    $fingerprintMethod = $policy.GetType().GetMethod("Fingerprint")
    $profilePolicyFingerprints[$profileName] = [string]$fingerprintMethod.Invoke($policy, $null)
}

Write-Host "Experiment fingerprint: $experimentFingerprint"
Write-Host "Corpus fingerprint:     $corpusFingerprint"
Write-Host "Binary hash:            $binaryHash"
Write-Host "Schedule seed:          $scheduleSeed"
foreach ($profileName in $profileOrder) {
    Write-Host ("Policy fingerprint {0}: {1}" -f $profileName, $profilePolicyFingerprints[$profileName])
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

function Get-MachineOutcome {
    param([object]$FaviconResult)

    if ($null -eq $FaviconResult) { return "harness-error" }
    $statusName = $FaviconResult.Status.ToString()
    if ($statusName -eq "Success") {
        return "success"
    }
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
    try {
        $diag = [string]$FaviconResult.DiagnosticsSummary
        if ($diag -match "invalid") { return "invalid-image" }
    } catch {}
    return "not-found"
}

function ConvertAndAdd-Result {
    param(
        [object]$Wrapped,
        [string]$RunDirectory,
        [string]$RunId,
        [string]$ProfileName,
        [int]$Repetition,
        [string]$CacheMode,
        [int]$Concurrency
    )

    if ($null -ne $Wrapped.PSObject.Properties['IsHarnessError'] -and [bool]$Wrapped.IsHarnessError) {
        $msg = ""
        if ($null -ne $Wrapped.PSObject.Properties['HarnessError']) { $msg = [string]$Wrapped.HarnessError }
        Convert-HarnessError -RunDirectory $RunDirectory -FixtureId ([string]$Wrapped.FixtureId) -Category ([string]$Wrapped.Category) -InputUrl ([string]$Wrapped.InputUrl) -ExceptionMessage $msg -ExperimentId $experimentConfig.experiment_id -Profile $ProfileName -Repetition $Repetition -RunId $RunId -CacheMode $CacheMode -Concurrency $Concurrency
        return
    }
    $result = $Wrapped.Result
    $fixtureId = [string]$Wrapped.FixtureId
    $category = [string]$Wrapped.Category
    $inputUrl = [string]$Wrapped.InputUrl
    $statusName = ""
    try { $statusName = $result.Status.ToString() } catch { $statusName = [string]$result.Status }
    $selectedProvider = ""
    try { $selectedProvider = [string]$result.Provider } catch {}
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
    $isSuccessForArtifact = ($machineOutcome -eq "success") -or ($statusName -eq "Success")
    if ($isSuccessForArtifact) {
        $bytesForArtifact = $null
        try { $bytesForArtifact = $result.IconData } catch { $bytesForArtifact = $null }
        if ($null -ne $bytesForArtifact -and $bytesForArtifact.Length -gt 0) {
            $artifactsDir = Join-Path $RunDirectory "artifacts"
            if (-not (Test-Path -LiteralPath $artifactsDir)) {
                New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
            }
            $ext = Get-ArtifactExtension -ImageType $imageType
            $fileName = "$fixtureId-r$Repetition.$ext"
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
        RunDirectory = $RunDirectory
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
        Profile = $ProfileName
        Repetition = $Repetition
        RunId = $RunId
        CacheMode = $CacheMode
        Concurrency = $Concurrency
    }
    if ($candidateCounts.Count -gt 0) {
        $addParams['CandidateCounts'] = $candidateCounts
        $addParams['CandidateCount'] = $candidateCounts.Count
    }
    Add-KeeFetchResult @addParams
}

function Invoke-RunDirectoryWork {
    param(
        [Parameter(Mandatory=$true)][string]$RunDirectory,
        [Parameter(Mandatory=$true)][string]$RunId,
        [Parameter(Mandatory=$true)][string]$ProfileName,
        [Parameter(Mandatory=$true)][int]$Repetition,
        [Parameter(Mandatory=$true)][string]$CacheMode,
        [Parameter(Mandatory=$true)][object]$Config,
        [Parameter(Mandatory=$true)][long]$ActiveElapsedSeed,
        [Parameter(Mandatory=$true)][bool]$Resumed
    )

    # Already-completed fixtures in this run directory are skipped; the
    # harness deduplicates by (fixture_id, repetition) as a second guard.
    $completedFids = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $resultsNdjson = Join-Path $RunDirectory "results.ndjson"
    if (Test-Path -LiteralPath $resultsNdjson) {
        foreach ($line in @(Get-Content -LiteralPath $resultsNdjson -Encoding UTF8 -ErrorAction SilentlyContinue)) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $obj = ConvertFrom-Json -InputObject $line -ErrorAction Stop
                [void]$completedFids.Add([string]$obj.fixture_id)
            } catch { continue }
        }
    }

    $pendingRows = @()
    foreach ($row in $filteredRows) {
        if ($completedFids.Contains([string]$row.fixture_id)) { continue }
        $pendingRows += $row
    }

    $concurrencyLimit = [int]$experimentConfig.concurrency
    if ($concurrencyLimit -lt 1) { $concurrencyLimit = 1 }
    $pending = New-Object System.Collections.ArrayList
    $nextIndex = 0
    $completedCount = 0
    $totalPending = $pendingRows.Count
    $cellStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while ($nextIndex -lt $pendingRows.Count -or $pending.Count -gt 0) {
        while ($nextIndex -lt $pendingRows.Count -and $pending.Count -lt $concurrencyLimit) {
            $row = $pendingRows[$nextIndex]
            $taskObj = $null
            $enqueueFailed = $false
            $enqueueMsg = ""
            try {
                $taskObj = Start-DownloadTask -Config $Config -Url $row.input_url -FixtureId $row.fixture_id -Category $row.category -InputUrl $row.input_url
            } catch {
                $enqueueFailed = $true
                $enqueueMsg = $_.Exception.Message
                if ([string]::IsNullOrWhiteSpace($enqueueMsg) -and $null -ne $_.Exception.InnerException) { $enqueueMsg = $_.Exception.InnerException.Message }
            }
            if ($enqueueFailed) {
                Convert-HarnessError -RunDirectory $RunDirectory -FixtureId ([string]$row.fixture_id) -Category ([string]$row.category) -InputUrl ([string]$row.input_url) -ExceptionMessage $enqueueMsg -ExperimentId $experimentConfig.experiment_id -Profile $ProfileName -Repetition $Repetition -RunId $RunId -CacheMode $CacheMode -Concurrency $experimentConfig.concurrency
                $nextIndex++
                $completedCount++
                if (($completedCount % 25) -eq 0 -or $completedCount -eq $totalPending) {
                    Write-Host "Completed $completedCount/$totalPending for $ProfileName/$CacheMode rep $Repetition"
                }
                continue
            }
            [void]$pending.Add($taskObj)
            $nextIndex++
        }
        if ($pending.Count -eq 0) { break }
        $wrapped = Wait-OneDownloadTask -Pending $pending
        try {
            ConvertAndAdd-Result -Wrapped $wrapped -RunDirectory $RunDirectory -RunId $RunId -ProfileName $ProfileName -Repetition $Repetition -CacheMode $CacheMode -Concurrency $experimentConfig.concurrency
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
                Convert-HarnessError -RunDirectory $RunDirectory -FixtureId $fidOuter -Category $catOuter -InputUrl $urlOuter -ExceptionMessage $msgOuter -ExperimentId $experimentConfig.experiment_id -Profile $ProfileName -Repetition $Repetition -RunId $RunId -CacheMode $CacheMode -Concurrency $experimentConfig.concurrency
            } else {
                throw
            }
        }
        $completedCount++
        if (($completedCount % 25) -eq 0 -or $completedCount -eq $totalPending) {
            Write-Host "Completed $completedCount/$totalPending for $ProfileName/$CacheMode rep $Repetition"
        }
    }

    $cellStopwatch.Stop()
    $activeElapsedMs = $ActiveElapsedSeed + $cellStopwatch.ElapsedMilliseconds
    Complete-KeeFetchRun -RunDirectory $RunDirectory -ActiveElapsedMs $activeElapsedMs -Resumed $Resumed

    # Fail closed on incomplete cells: a run that completes with the wrong
    # number of records is invalid evidence, not a partial success.
    $rowsCsv = Join-Path $RunDirectory "rows.csv"
    $recordCount = 0
    if (Test-Path -LiteralPath $rowsCsv) {
        $recordCount = @(@(Import-Csv -LiteralPath $rowsCsv)).Count
    }
    if ($recordCount -ne $expectedRowCount) {
        throw "Row count mismatch for run $RunId : expected $expectedRowCount but recorded $recordCount. Evidence is invalid; inspect the run directory."
    }

    Write-Host "Run completed: $RunDirectory"
}

function Invoke-RunCell {
    param(
        [Parameter(Mandatory=$true)][string]$CandidateId,
        [Parameter(Mandatory=$true)][string]$CacheMode,
        [Parameter(Mandatory=$true)][int]$Repetition,
        [Parameter(Mandatory=$true)][string]$RunKind
    )

    $config = $profileConfigs[$CandidateId]

    $run = New-KeeFetchRun -OutputRoot $outputRoot -ExperimentId $experimentConfig.experiment_id -CorpusPath $corpusPath -CorpusVersion "v1" -Concurrency $experimentConfig.concurrency -CacheMode $CacheMode -Profiles @($CandidateId) -Repetitions $experimentConfig.repetitions -CacheModes $experimentConfig.cache_modes -ExtraMetadata @{
        candidate_id = $CandidateId
        repetition = $Repetition
        run_kind = $RunKind
        policy_fingerprint = $profilePolicyFingerprints[$CandidateId]
        experiment_fingerprint = $experimentFingerprint
        corpus_fingerprint = $corpusFingerprint
        binary_hash = $binaryHash
        schedule_seed = $scheduleSeed
    }
    $runDir = $run.Directory
    $runId = $run.RunId

    Write-Host ""
    Write-Host ("=== Experiment {0} | {1} | {2} | rep {3} | {4} | Run {5} ===" -f `
        $experimentConfig.experiment_id, $CandidateId, $CacheMode, $Repetition, $RunKind, $runId)

    Invoke-RunDirectoryWork -RunDirectory $runDir -RunId $runId -ProfileName $CandidateId `
        -Repetition $Repetition -CacheMode $CacheMode -Config $config -ActiveElapsedSeed 0 -Resumed $false
}

function Invoke-ResumeCell {
    param(
        [Parameter(Mandatory=$true)][string]$RunDirectory,
        [Parameter(Mandatory=$true)][string]$CandidateId
    )

    $runJsonPath = Join-Path $RunDirectory "run.json"
    $meta = Get-Content -Raw -LiteralPath $runJsonPath | ConvertFrom-Json
    $runId = [string]$meta.run_id
    $cacheMode = [string]$meta.cache_mode
    $repetition = 1
    try { $repetition = [int]$meta.repetition } catch { $repetition = 1 }
    $priorActiveMs = 0
    try { $priorActiveMs = [long]$meta.active_elapsed_ms } catch { $priorActiveMs = 0 }

    Write-Host ""
    Write-Host ("=== Resuming {0} | {1} | {2} | rep {3} | Run {4} ===" -f `
        $experimentConfig.experiment_id, $CandidateId, $cacheMode, $repetition, $runId)

    Invoke-RunDirectoryWork -RunDirectory $RunDirectory -RunId $runId -ProfileName $CandidateId `
        -Repetition $repetition -CacheMode $cacheMode -Config $profileConfigs[$CandidateId] `
        -ActiveElapsedSeed $priorActiveMs -Resumed $true
}

# --- Experiment-level pre-scan: matrix identity, fingerprints, resume -------

$cacheModes = @($experimentConfig.cache_modes)
$repetitions = [int]$experimentConfig.repetitions
$hasWarm = $cacheModes -contains "warm"
$hasCold = $cacheModes -contains "cold"

$existingRuns = @()
if (Test-Path -LiteralPath $outputRoot) {
    $existingRuns = @(Get-ChildItem -LiteralPath $outputRoot -Directory -ErrorAction SilentlyContinue)
}

$completedMeasured = @{}
$completedWarmups = @{}
$incompleteCells = @{}
$cellsSeen = New-Object 'System.Collections.Generic.HashSet[string]'

foreach ($dir in $existingRuns) {
    $runJsonPath = Join-Path $dir.FullName "run.json"
    if (-not (Test-Path -LiteralPath $runJsonPath)) {
        throw "Unidentified run directory without run.json: $($dir.FullName). Remove or quarantine it before relaunching."
    }
    $meta = $null
    try { $meta = Get-Content -Raw -LiteralPath $runJsonPath | ConvertFrom-Json } catch { $meta = $null }
    if ($null -eq $meta) {
        throw "Unreadable run.json: $runJsonPath"
    }

    foreach ($fpField in @('experiment_fingerprint','corpus_fingerprint','binary_hash')) {
        $value = ""
        if ($meta.PSObject.Properties.Name -contains $fpField) { $value = [string]$meta.$fpField }
        if ([string]::IsNullOrWhiteSpace($value)) {
            throw "Run $($dir.Name) predates fingerprint provenance (missing $fpField). Pre-repair evidence must not be combined; quarantine it."
        }
        $expectedValue = $experimentFingerprint
        if ($fpField -eq 'corpus_fingerprint') { $expectedValue = $corpusFingerprint }
        if ($fpField -eq 'binary_hash') { $expectedValue = $binaryHash }
        if ($value -ne $expectedValue) {
            throw "Run $($dir.Name) has a stale $fpField (found $value). Do not combine evidence across experiment/corpus/binary revisions."
        }
    }

    $candidateId = ""
    if ($meta.PSObject.Properties.Name -contains 'candidate_id') { $candidateId = [string]$meta.candidate_id }
    if ($profileOrder -notcontains $candidateId) {
        throw "Run $($dir.Name) references unknown candidate '$candidateId'."
    }
    $policyFp = ""
    if ($meta.PSObject.Properties.Name -contains 'policy_fingerprint') { $policyFp = [string]$meta.policy_fingerprint }
    if ($policyFp -ne $profilePolicyFingerprints[$candidateId]) {
        throw "Run $($dir.Name) policy fingerprint does not match candidate '$candidateId' definition (found $policyFp)."
    }

    $runKind = "measured"
    if ($meta.PSObject.Properties.Name -contains 'run_kind') { $runKind = [string]$meta.run_kind }
    $status = ""
    if ($meta.PSObject.Properties.Name -contains 'status') { $status = [string]$meta.status }
    $cacheMode = ""
    if ($meta.PSObject.Properties.Name -contains 'cache_mode') { $cacheMode = [string]$meta.cache_mode }
    $repetition = 0
    if ($meta.PSObject.Properties.Name -contains 'repetition') { try { $repetition = [int]$meta.repetition } catch { $repetition = 0 } }

    if ($runKind -eq "warmup") {
        if ($status -eq "complete") {
            if ($completedWarmups.ContainsKey($candidateId)) {
                throw "Duplicate completed warmup runs for candidate '$candidateId'."
            }
            $completedWarmups[$candidateId] = $dir.FullName
        }
        continue
    }

    if ($cacheModes -notcontains $cacheMode) {
        throw "Run $($dir.Name) has cache mode '$cacheMode' outside the experiment definition."
    }
    if ($repetition -lt 1 -or $repetition -gt $repetitions) {
        throw "Run $($dir.Name) has repetition $repetition outside 1..$repetitions."
    }

    $cellKey = "$candidateId|$cacheMode|$repetition"
    if ($cellsSeen.Contains($cellKey)) {
        throw "Duplicate matrix cell $cellKey in output root. Evidence must come from exactly one run per cell."
    }
    [void]$cellsSeen.Add($cellKey)

    if ($status -eq "complete") {
        $rowsCsvCheck = Join-Path $dir.FullName "rows.csv"
        $rowCountCheck = 0
        if (Test-Path -LiteralPath $rowsCsvCheck) {
            $rowCountCheck = @(@(Import-Csv -LiteralPath $rowsCsvCheck)).Count
        }
        if ($rowCountCheck -ne $expectedRowCount) {
            throw "Completed run $($dir.Name) has $rowCountCheck rows, expected $expectedRowCount."
        }
        $completedMeasured[$cellKey] = $dir.FullName
    } elseif ($status -eq "incomplete") {
        $incompleteCells[$cellKey] = $dir.FullName
    } else {
        throw "Run $($dir.Name) has unrecognized status '$status'."
    }
}

# --- Deterministic schedule --------------------------------------------------
# Cold cells interleave across candidates per repetition with a rotation so
# candidate ordering cannot systematically favor early or late execution.
# Warm blocks are contiguous per candidate: the production cache is keyed by
# origin, so a candidate's measured warm repetitions must follow its own
# warm-up without any other candidate's runs (or a cache clear) in between.

function Get-RotatedOrder {
    param([string[]]$Items, [int]$Offset)
    $count = $Items.Count
    $rotated = @()
    for ($i = 0; $i -lt $count; $i++) {
        $rotated += $Items[($i + $Offset) % $count]
    }
    return $rotated
}

$scheduledCells = @()
for ($rep = 1; $rep -le $repetitions; $rep++) {
    $rotated = Get-RotatedOrder -Items $profileOrder -Offset (($rep - 1) % $profileOrder.Count)
    foreach ($cid in $rotated) {
        if ($hasCold) {
            $scheduledCells += [PSCustomObject]@{ CandidateId = $cid; CacheMode = "cold"; Repetition = $rep; Kind = "measured" }
        }
    }
}
if ($hasWarm) {
    $warmOrder = Get-RotatedOrder -Items $profileOrder -Offset 0
    foreach ($cid in $warmOrder) {
        $scheduledCells += [PSCustomObject]@{ CandidateId = $cid; CacheMode = "warm"; Repetition = 0; Kind = "warmup" }
        for ($rep = 1; $rep -le $repetitions; $rep++) {
            $scheduledCells += [PSCustomObject]@{ CandidateId = $cid; CacheMode = "warm"; Repetition = $rep; Kind = "measured" }
        }
    }
}

$totalCells = $scheduledCells.Count
$skippedCells = 0
$resumedCells = 0
$executedCells = 0
$cellIndex = 0

foreach ($cell in $scheduledCells) {
    $cellIndex++

    if ($cell.Kind -eq "warmup") {
        if ($completedWarmups.ContainsKey($cell.CandidateId)) {
            Write-Host "[$cellIndex/$totalCells] Skipping warmup for $($cell.CandidateId) (already complete)"
            $skippedCells++
            continue
        }
        # Warm methodology: clear once, warm up over the full corpus unmeasured,
        # then the measured warm repetitions follow without clearing.
        $clearCacheMethod.Invoke($null, @()) | Out-Null
        Invoke-RunCell -CandidateId $cell.CandidateId -CacheMode "warm" -Repetition 0 -RunKind "warmup"
        $executedCells++
        continue
    }

    $cellKey = "$($cell.CandidateId)|$($cell.CacheMode)|$($cell.Repetition)"

    if ($completedMeasured.ContainsKey($cellKey)) {
        Write-Host "[$cellIndex/$totalCells] Skipping $cellKey (already complete)"
        $skippedCells++
        continue
    }

    if ($cell.CacheMode -eq "cold") {
        $clearCacheMethod.Invoke($null, @()) | Out-Null
    }

    if ($incompleteCells.ContainsKey($cellKey)) {
        Invoke-ResumeCell -RunDirectory $incompleteCells[$cellKey] -CandidateId $cell.CandidateId
        $resumedCells++
    } else {
        Invoke-RunCell -CandidateId $cell.CandidateId -CacheMode $cell.CacheMode -Repetition $cell.Repetition -RunKind "measured"
        $executedCells++
    }
}

Write-Host ""
Write-Host ("Experiment {0} completed: {1} executed, {2} resumed, {3} skipped (already complete)." -f `
    $experimentConfig.experiment_id, $executedCells, $resumedCells, $skippedCells)
