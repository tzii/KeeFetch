param(
    [string]$UrlFile = "KeeFetch.Tests\Fixtures\Issue1RegressionUrls.txt",
    [string[]]$Presets = @("Fast", "Balanced", "Thorough"),
    [int]$Limit = 0,
    [string]$CsvUrlColumn = "url",
    [string]$SummaryCsv = "",
    [int]$Concurrency = 1
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

if (-not (Test-Path -LiteralPath $keepassPath)) {
    throw "KeePass.exe not found at $keepassPath"
}

if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Build KeeFetch first. Missing $assemblyPath"
}

if ([System.IO.Path]::IsPathRooted($UrlFile)) {
    $urlFilePath = $UrlFile
}
else {
    $urlFilePath = Join-Path $repoRoot $UrlFile
}

$resolvedUrlFile = Resolve-Path -LiteralPath $urlFilePath

if ([System.IO.Path]::GetExtension($resolvedUrlFile.Path).Equals(".csv", [System.StringComparison]::OrdinalIgnoreCase)) {
    $urls = Import-Csv -LiteralPath $resolvedUrlFile |
        ForEach-Object { $_.$CsvUrlColumn } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}
else {
    $urls = Get-Content -LiteralPath $resolvedUrlFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

if ($Limit -gt 0) {
    $urls = $urls | Select-Object -First $Limit
}

$Concurrency = [Math]::Max(1, $Concurrency)

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

function Start-DownloadTask {
    param(
        [object]$Config,
        [string]$Url
    )

    $downloader = $downloaderCtor.Invoke([object[]] @($Config.PSObject.BaseObject))
    $task = $downloadMethod.Invoke($downloader, @($Url, [Threading.CancellationToken]::None))
    return [PSCustomObject]@{
        Url = $Url
        Task = $task
    }
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
            return $item.Task.GetAwaiter().GetResult()
        }
    }

    throw "Completed download task was not found in the pending set."
}

function New-ProviderAggregate {
    return [ordered]@{
        Calls = 0
        TotalMs = 0L
        Candidates = 0
        Errors = 0
    }
}

$summaries = @()

foreach ($preset in $Presets) {
    Write-Host ""
    Write-Host "=== Preset: $preset ==="

    $profile = New-ConfigForProfile -ProfileName $preset
    $config = $profile.Config
    $clearCacheMethod.Invoke($null, @()) | Out-Null

    $success = 0
    $notFound = 0
    $synthetic = 0
    $cacheHits = 0
    $totalMs = 0L
    $slowestMs = 0L
    $providerTotals = @{}

    $pending = New-Object System.Collections.ArrayList
    $nextUrlIndex = 0
    $completedUrlCount = 0

    while ($nextUrlIndex -lt $urls.Count -or $pending.Count -gt 0) {
        while ($nextUrlIndex -lt $urls.Count -and $pending.Count -lt $Concurrency) {
            [void]$pending.Add((Start-DownloadTask -Config $config -Url $urls[$nextUrlIndex]))
            $nextUrlIndex++
        }

        $result = Wait-OneDownloadTask -Pending $pending
        $completedUrlCount++

        if (($completedUrlCount % 25) -eq 0 -or $completedUrlCount -eq $urls.Count) {
            Write-Host ("Completed {0}/{1}" -f $completedUrlCount, $urls.Count)
        }

        $statusName = $result.Status.ToString()
        $elapsed = [int64]$result.ElapsedMilliseconds

        if ($statusName -eq "Success") { $success++ } else { $notFound++ }
        if ($result.WasSyntheticFallback) { $synthetic++ }
        if ($result.DiagnosticsSummary -and $result.DiagnosticsSummary.IndexOf("cache-hit", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $cacheHits++
        }

        $totalMs += $elapsed
        if ($elapsed -gt $slowestMs) { $slowestMs = $elapsed }

        foreach ($metric in $result.ProviderMetrics) {
            $name = $metric.ProviderName
            if (-not $providerTotals.Contains($name)) {
                $providerTotals[$name] = New-ProviderAggregate
            }

            $aggregate = $providerTotals[$name]
            $aggregate.Calls++
            $aggregate.TotalMs += [int64]$metric.ElapsedMilliseconds
            $aggregate.Candidates += [int]$metric.CandidateCount
            if ($metric.Outcome -eq "error") {
                $aggregate.Errors++
            }
        }
    }

    $averageMs = if ($urls.Count -gt 0) { [int]($totalMs / $urls.Count) } else { 0 }
    $summary = [PSCustomObject]@{
        Preset = $profile.Name
        BaseMode = $profile.BaseMode
        Urls = $urls.Count
        Success = $success
        NotFound = $notFound
        Synthetic = $synthetic
        CacheHits = $cacheHits
        AvgMs = $averageMs
        SlowestMs = $slowestMs
    }
    $summaries += $summary

    $summary | Format-List
    if ($providerTotals.Count -gt 0) {
        $providerTotals.GetEnumerator() |
            ForEach-Object {
                [PSCustomObject]@{
                    Provider = $_.Key
                    Calls = $_.Value.Calls
                    TotalMs = $_.Value.TotalMs
                    AvgMs = if ($_.Value.Calls -gt 0) { [int]($_.Value.TotalMs / $_.Value.Calls) } else { 0 }
                    Candidates = $_.Value.Candidates
                    Errors = $_.Value.Errors
                }
            } |
            Sort-Object -Property TotalMs -Descending |
            Format-Table -AutoSize
    }
}

Write-Host ""
Write-Host "=== Summary ==="
$summaries | Format-Table -AutoSize

if (-not [string]::IsNullOrWhiteSpace($SummaryCsv)) {
    $summaryPath = if ([System.IO.Path]::IsPathRooted($SummaryCsv)) {
        $SummaryCsv
    }
    else {
        Join-Path $repoRoot $SummaryCsv
    }

    $summaries | Export-Csv -LiteralPath $summaryPath -NoTypeInformation
    Write-Host ""
    Write-Host "Summary CSV written to: $summaryPath"
}
