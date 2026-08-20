param(
    [Parameter(Mandatory=$true)][string]$Experiment,
    [string]$ResumeRun = ''
)

$ErrorActionPreference = "Stop"

# Executes a benchmark experiment with explicit matrix-cell identity,
# fingerprinted provenance, a deterministic seeded schedule recorded in
# schedule.json, atomic warm-block methodology, and experiment-level resume.
# Every run directory records the experiment/corpus/binary/execution-harness
# fingerprints plus the per-candidate effective policy fingerprint resolved
# through the real FaviconDownloader path, so the selector can fail closed on
# any mismatch. Each matrix cell executes through exactly one FaviconDownloader
# instance so provider-health state transfers within a cell and resets between
# cells, matching production behavior.

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
        # Bracket type literals do not reliably resolve for LoadFrom-context
        # assemblies in a fresh PowerShell process; resolve via reflection.
        $foundProvider = $null
        if ($null -ne $script:keefetchAssembly) {
            $catalogForValidation = $script:keefetchAssembly.GetType('KeeFetch.FetchProfiles.FetchProfileCatalog', $false)
            if ($null -ne $catalogForValidation) {
                $findProviderForValidation = $catalogForValidation.GetMethod('FindProvider')
                $foundProvider = $findProviderForValidation.Invoke($null, @($pidText))
            }
        }
        if ($null -eq $foundProvider) {
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
# Fail closed: the execution schedule is part of the methodology. An
# experiment without an explicit schedule_seed must never run, because its
# candidate ordering could not be reproduced or audited.
if (-not ($rawExperiment.PSObject.Properties.Name -contains 'schedule_seed')) {
    throw "Experiment '$experimentPath' must declare schedule_seed explicitly; the seeded schedule is mandatory."
}
$scheduleSeed = 0
try { $scheduleSeed = [int64]$rawExperiment.schedule_seed } catch { $scheduleSeed = 0 }
if ($scheduleSeed -le 0) { throw "schedule_seed must be a positive integer (got '$($rawExperiment.schedule_seed)')." }

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
$expectedFixtureIds = [string[]]@($filteredRows | ForEach-Object { [string]$_.fixture_id })

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
    # correspond to the executable chain exactly. Type resolved via reflection
    # (LoadFrom-context assemblies are invisible to bracket type literals in a
    # fresh PowerShell process).
    $catalogType = $script:keefetchAssembly.GetType('KeeFetch.FetchProfiles.FetchProfileCatalog', $true)
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

# The execution harness itself is fingerprinted: runner plus module code.
# Evidence produced by a different harness is a different experiment.
$harnessFingerprint = Get-KeeFetchHarnessFingerprint -RepoRoot $repoRoot
$harnessEnvironment = [ordered]@{
    powershell_version = [string]$PSVersionTable.PSVersion
    os_version = [System.Environment]::OSVersion.VersionString
    is_64bit_process = [bool][System.Environment]::Is64BitProcess
    machine_name = [System.Environment]::MachineName
}
if ($PSVersionTable.ContainsKey('ClrVersion')) {
    $harnessEnvironment['clr_version'] = [string]$PSVersionTable.ClrVersion
}

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
Write-Host "Harness fingerprint:    $harnessFingerprint"
Write-Host "Schedule seed:          $scheduleSeed"
foreach ($profileName in $profileOrder) {
    Write-Host ("Policy fingerprint {0}: {1}" -f $profileName, $profilePolicyFingerprints[$profileName])
}

function Start-DownloadTask {
    param(
        # The single cell downloader: exactly one FaviconDownloader instance is
        # created per matrix cell and shared by every fixture in it, matching
        # production behavior where one plugin instance serves many entries.
        # Provider health therefore transfers within a cell and resets between
        # cells; the static cache is unaffected because it is keyed by origin.
        [object]$Downloader,
        [string]$Url,
        [string]$FixtureId = "",
        [string]$Category = "",
        [string]$InputUrl = ""
    )

    $task = $downloadMethod.Invoke($Downloader, @($Url, [Threading.CancellationToken]::None))
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
        [Parameter(Mandatory=$true)][bool]$Resumed,
        [Parameter(Mandatory=$true)][string]$RunKind
    )

    # Exactly one downloader per matrix cell: provider-health state (failure
    # counters, cooldown) transfers across fixtures within the cell, exactly
    # as it would inside the plugin during real use, and resets for the next
    # cell. Creating one downloader per fixture would erase that state and
    # make the measurement unrepresentative of production execution.
    $cellDownloader = $downloaderCtor.Invoke([object[]] @($Config.PSObject.BaseObject))

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
                $taskObj = Start-DownloadTask -Downloader $cellDownloader -Url $row.input_url -FixtureId $row.fixture_id -Category $row.category -InputUrl $row.input_url
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

    # Fail closed on incomplete cells: the exact matrix validation replaces the
    # old row-count-only check. A run that does not contain exactly the corpus
    # fixture set, with per-row metadata matching run.json, is invalid evidence.
    if ($RunKind -eq "measured" -and $CacheMode -eq "cold") {
        # Measured cold cells are the scoring evidence: every success must also
        # carry a non-empty artifact hash and a real artifact on disk.
        Assert-KeeFetchRunRows -RunDirectory $RunDirectory `
            -ExpectedFixtureIds $expectedFixtureIds `
            -CurrentCorpusFingerprint $corpusFingerprint `
            -ExpectedExperimentId $experimentConfig.experiment_id `
            -ExpectedProfile $ProfileName `
            -ExpectedCacheMode $CacheMode `
            -ExpectedRepetition $Repetition `
            -ExpectedRunId $RunId `
            -ExpectedConcurrency ([int]$experimentConfig.concurrency) `
            -RequireSuccessArtifacts
    } else {
        Assert-KeeFetchRunRows -RunDirectory $RunDirectory `
            -ExpectedFixtureIds $expectedFixtureIds `
            -CurrentCorpusFingerprint $corpusFingerprint `
            -ExpectedExperimentId $experimentConfig.experiment_id `
            -ExpectedProfile $ProfileName `
            -ExpectedCacheMode $CacheMode `
            -ExpectedRepetition $Repetition `
            -ExpectedRunId $RunId `
            -ExpectedConcurrency ([int]$experimentConfig.concurrency)
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
        execution_harness_fingerprint = $harnessFingerprint
        harness_environment = $harnessEnvironment
        schedule_seed = $scheduleSeed
    }
    $runDir = $run.Directory
    $runId = $run.RunId

    Write-Host ""
    Write-Host ("=== Experiment {0} | {1} | {2} | rep {3} | {4} | Run {5} ===" -f `
        $experimentConfig.experiment_id, $CandidateId, $CacheMode, $Repetition, $RunKind, $runId)

    Invoke-RunDirectoryWork -RunDirectory $runDir -RunId $runId -ProfileName $CandidateId `
        -Repetition $Repetition -CacheMode $CacheMode -Config $config -ActiveElapsedSeed 0 -Resumed $false `
        -RunKind $RunKind
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
        -ActiveElapsedSeed $priorActiveMs -Resumed $true -RunKind "measured"
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
$warmRunsByCandidate = @{}

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

    # The execution harness fingerprint is mandatory provenance: runs produced
    # by a different runner/module revision are a different experiment.
    $recordedHarnessFp = ""
    if ($meta.PSObject.Properties.Name -contains 'execution_harness_fingerprint') { $recordedHarnessFp = [string]$meta.execution_harness_fingerprint }
    if ([string]::IsNullOrWhiteSpace($recordedHarnessFp)) {
        throw "Run $($dir.Name) predates execution-harness fingerprint provenance. Pre-repair evidence must not be combined; quarantine it."
    }
    if ($recordedHarnessFp -ne $harnessFingerprint) {
        throw "Run $($dir.Name) was produced by a different execution harness (found $recordedHarnessFp, current $harnessFingerprint). Do not resume across harness revisions; quarantine it."
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

    # Warm runs (warm-up plus measured warm repetitions) are collected per
    # candidate and validated as one atomic block below; they never register
    # into completedMeasured/incompleteCells individually because a partially
    # executed warm block is invalid as a whole.
    if ($runKind -eq "warmup") {
        if ($cacheMode -ne "warm") {
            throw "Run $($dir.Name) is a warmup with cache mode '$cacheMode'; warm-ups exist only inside warm blocks."
        }
        if (-not $warmRunsByCandidate.ContainsKey($candidateId)) { $warmRunsByCandidate[$candidateId] = @() }
        $warmRunsByCandidate[$candidateId] = @($warmRunsByCandidate[$candidateId] + [PSCustomObject]@{
            RunKind = $runKind; Status = $status; CacheMode = $cacheMode
            Repetition = $repetition; Directory = $dir.FullName; Name = $dir.Name
            RunId = [string]$meta.run_id
        })
        continue
    }

    if ($cacheModes -notcontains $cacheMode) {
        throw "Run $($dir.Name) has cache mode '$cacheMode' outside the experiment definition."
    }

    if ($cacheMode -eq "warm") {
        if (-not $warmRunsByCandidate.ContainsKey($candidateId)) { $warmRunsByCandidate[$candidateId] = @() }
        $warmRunsByCandidate[$candidateId] = @($warmRunsByCandidate[$candidateId] + [PSCustomObject]@{
            RunKind = $runKind; Status = $status; CacheMode = $cacheMode
            Repetition = $repetition; Directory = $dir.FullName; Name = $dir.Name
            RunId = [string]$meta.run_id
        })
        continue
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
        # Exact matrix validation replaces the old row-count-only check.
        Assert-KeeFetchRunRows -RunDirectory $dir.FullName `
            -ExpectedFixtureIds $expectedFixtureIds `
            -CurrentCorpusFingerprint $corpusFingerprint `
            -ExpectedExperimentId $experimentConfig.experiment_id `
            -ExpectedProfile $candidateId `
            -ExpectedCacheMode $cacheMode `
            -ExpectedRepetition $repetition `
            -ExpectedRunId ([string]$meta.run_id) `
            -ExpectedConcurrency ([int]$meta.concurrency) `
            -RequireSuccessArtifacts
        $completedMeasured[$cellKey] = $dir.FullName
    } elseif ($status -eq "incomplete") {
        $incompleteCells[$cellKey] = $dir.FullName
    } else {
        throw "Run $($dir.Name) has unrecognized status '$status'."
    }
}

# --- Atomic warm-block validation (quarantine-rebuild) ------------------------
# A warm block counts only as one complete atomic unit: exactly one complete
# warm-up plus complete measured repetitions 1..N. Any deviation (missing,
# duplicate, or incomplete part) invalidates the whole block: the runs are
# quarantined under the benchmark-runs quarantine root and the block is
# rebuilt from scratch by the schedule below. Warm cells are never resumed
# individually because the production cache state between them cannot be
# reconstructed.

$quarantineRoot = Join-Path (Split-Path -Parent $outputRoot) "quarantine"
if ($hasWarm) {
    foreach ($cid in $profileOrder) {
        $warmRuns = @()
        if ($warmRunsByCandidate.ContainsKey($cid)) { $warmRuns = @($warmRunsByCandidate[$cid]) }
        if ($warmRuns.Count -eq 0) { continue }

        $warmups = @($warmRuns | Where-Object { $_.RunKind -eq "warmup" })
        $measured = @($warmRuns | Where-Object { $_.RunKind -ne "warmup" })
        $reason = Get-WarmBlockInvalidReason -Warmups $warmups -Measured $measured -Repetitions $repetitions

        if ($null -ne $reason) {
            $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmssZ")
            $quarantineDir = Join-Path $quarantineRoot "$($experimentConfig.experiment_id)-warmblock-rebuild-$cid-$stamp"
            New-Item -ItemType Directory -Path $quarantineDir -Force | Out-Null
            $movedNames = @()
            foreach ($wr in $warmRuns) {
                Move-Item -LiteralPath $wr.Directory -Destination $quarantineDir
                $movedNames += $wr.Name
            }
            $reasonText = @(
                "Warm-block quarantine rebuild"
                "Experiment: $($experimentConfig.experiment_id)"
                "Candidate:  $cid"
                "Reason:     $reason"
                "Quarantined (UTC): $stamp"
                "Moved runs: $($movedNames -join ', ')"
                ""
                "The schedule rebuilds this candidate's complete warm block from scratch;"
                "warm cells are never resumed individually and quarantined runs are never merged."
            ) -join [Environment]::NewLine
            [System.IO.File]::WriteAllText((Join-Path $quarantineDir "quarantine-reason.txt"), $reasonText,
                (New-Object System.Text.UTF8Encoding $false))
            Write-Host "Warm block for $cid is invalid ($reason); quarantined $($warmRuns.Count) run(s) to $quarantineDir for rebuild."
            continue
        }

        # Complete atomic block: validate every run's rows exactly (no artifact
        # requirement for warm evidence) and register the cells so the
        # schedule skips them.
        foreach ($wr in $warmups) {
            Assert-KeeFetchRunRows -RunDirectory $wr.Directory `
                -ExpectedFixtureIds $expectedFixtureIds `
                -CurrentCorpusFingerprint $corpusFingerprint `
                -ExpectedExperimentId $experimentConfig.experiment_id `
                -ExpectedProfile $cid `
                -ExpectedCacheMode "warm" `
                -ExpectedRepetition 0 `
                -ExpectedRunId ([string]$wr.RunId) `
                -ExpectedConcurrency ([int]$experimentConfig.concurrency)
            $completedWarmups[$cid] = $wr.Directory
        }
        foreach ($wr in $measured) {
            Assert-KeeFetchRunRows -RunDirectory $wr.Directory `
                -ExpectedFixtureIds $expectedFixtureIds `
                -CurrentCorpusFingerprint $corpusFingerprint `
                -ExpectedExperimentId $experimentConfig.experiment_id `
                -ExpectedProfile $cid `
                -ExpectedCacheMode "warm" `
                -ExpectedRepetition ([int]$wr.Repetition) `
                -ExpectedRunId ([string]$wr.RunId) `
                -ExpectedConcurrency ([int]$experimentConfig.concurrency)
            $completedMeasured["$cid|warm|$($wr.Repetition)"] = $wr.Directory
        }
    }
}

# --- Deterministic seeded schedule -------------------------------------------
# Cold cells interleave across candidates per repetition in a seeded order so
# candidate ordering cannot systematically favor early or late execution, and
# the exact order is reproducible from the recorded schedule_seed. Warm blocks
# are contiguous per candidate: the production cache is keyed by origin, so a
# candidate's measured warm repetitions must follow its own warm-up without
# any other candidate's runs (or a cache clear) in between.

$scheduledCells = @()
for ($rep = 1; $rep -le $repetitions; $rep++) {
    $coldOrder = Get-SeededScheduleOrder -Items $profileOrder -Seed $scheduleSeed -Phase "cold" -Repetition $rep
    foreach ($cid in $coldOrder) {
        if ($hasCold) {
            $scheduledCells += [PSCustomObject]@{ CandidateId = $cid; CacheMode = "cold"; Repetition = $rep; Kind = "measured" }
        }
    }
}
if ($hasWarm) {
    $warmOrder = Get-SeededScheduleOrder -Items $profileOrder -Seed $scheduleSeed -Phase "warm" -Repetition 0
    foreach ($cid in $warmOrder) {
        $scheduledCells += [PSCustomObject]@{ CandidateId = $cid; CacheMode = "warm"; Repetition = 0; Kind = "warmup" }
        for ($rep = 1; $rep -le $repetitions; $rep++) {
            $scheduledCells += [PSCustomObject]@{ CandidateId = $cid; CacheMode = "warm"; Repetition = $rep; Kind = "measured" }
        }
    }
}

# --- schedule.json: the executed order is recorded and verified ---------------
# The complete schedule identity (seed, fingerprints, cell order) is written to
# the output root before any cell executes. A later invocation over the same
# output root must compute the identical schedule or fail closed: resuming a
# study under a different schedule, seed, or component fingerprint would mix
# methodologies.

$scheduleIdentity = [ordered]@{
    version = 1
    schedule_seed = $scheduleSeed
    experiment_fingerprint = $experimentFingerprint
    corpus_fingerprint = $corpusFingerprint
    binary_hash = $binaryHash
    execution_harness_fingerprint = $harnessFingerprint
    cells = [string[]]@($scheduledCells | ForEach-Object { "$($_.CandidateId)|$($_.CacheMode)|$($_.Repetition)|$($_.Kind)" })
}
$schedulePath = Join-Path $outputRoot "schedule.json"
if (Test-Path -LiteralPath $schedulePath) {
    $recordedSchedule = $null
    try {
        $recordedSchedule = Get-Content -Raw -LiteralPath $schedulePath | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "Unreadable schedule.json in output root: $($_.Exception.Message). Quarantine the output root and start clean."
    }
    foreach ($field in @('schedule_seed','experiment_fingerprint','corpus_fingerprint','binary_hash','execution_harness_fingerprint')) {
        $recordedValue = ""
        if ($recordedSchedule.PSObject.Properties.Name -contains $field) { $recordedValue = [string]$recordedSchedule.$field }
        if ($recordedValue -ne [string]$scheduleIdentity[$field]) {
            throw "schedule.json $field mismatch (recorded '$recordedValue', current '$($scheduleIdentity[$field])'). The existing output root belongs to a different schedule; quarantine it and start clean."
        }
    }
    $recordedCells = @()
    if ($recordedSchedule.PSObject.Properties.Name -contains 'cells') { $recordedCells = @($recordedSchedule.cells) }
    $currentCells = @($scheduleIdentity['cells'])
    if ($recordedCells.Count -ne $currentCells.Count) {
        throw "schedule.json cell count mismatch (recorded $($recordedCells.Count), current $($currentCells.Count)). The existing output root belongs to a different schedule; quarantine it and start clean."
    }
    for ($i = 0; $i -lt $currentCells.Count; $i++) {
        if ([string]$recordedCells[$i] -ne [string]$currentCells[$i]) {
            throw "schedule.json cell order mismatch at position $($i + 1) (recorded '$($recordedCells[$i])', current '$($currentCells[$i])'). The existing output root belongs to a different schedule; quarantine it and start clean."
        }
    }
    Write-Host "Schedule verified against schedule.json ($($currentCells.Count) cells, seed $scheduleSeed)."
} else {
    if (-not (Test-Path -LiteralPath $outputRoot)) {
        New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    }
    Write-JsonFileUtf8NoBom -Path $schedulePath -Object $scheduleIdentity -Depth 20
    Write-Host "Schedule recorded to schedule.json ($($scheduleIdentity['cells'].Count) cells, seed $scheduleSeed)."
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
