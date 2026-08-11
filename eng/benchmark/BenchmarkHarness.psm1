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
Export-ModuleMember -Function Test-KeeFetchCorpus
