$ErrorActionPreference = 'Stop'

$workflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../.github/workflows/build.yml') -Raw
$build = [regex]::Match($workflow, '(?ms)^  build:\r?\n(.*?)(?=^  release:)').Value
$release = [regex]::Match($workflow, '(?ms)^  release:\r?\n.*').Value
if (-not $build -or -not $release) { throw 'Expected separate build and release jobs.' }
if ($build -match 'contents: write' -or $build -notmatch 'contents: read') {
    throw 'Build must use read-only repository permissions.'
}
if ($build -notmatch 'persist-credentials: false') { throw 'Checkout must not persist credentials.' }
if ($release -notmatch "(?m)^    if: github.event_name == 'push' && startsWith\(github.ref, 'refs/tags/v'\)\r?$" -or
    $release -notmatch '(?m)^    needs: build\r?$' -or $release -notmatch 'contents: write') {
    throw 'Release must be tag-push-only, depend on build, and own write permission.'
}
if ($release -match 'actions/checkout@') { throw 'Release must consume artifacts, not rebuild source.' }

function Get-WorkflowScript([string]$name) {
    $pattern = '(?ms)^      - name: ' + [regex]::Escape($name) + '\r?\n(?<step>.*?)(?=^      - name:|^  \S|\z)'
    $step = [regex]::Match($workflow, $pattern).Groups['step'].Value
    $run = [regex]::Match($step, '(?ms)^        run: \|\r?\n(?<code>(?:^          .*\r?\n?)+)').Groups['code'].Value
    if (-not $run) { throw "Missing workflow script: $name" }
    if ($name -eq 'Compute release checksums' -and $step -match '(?m)^        if:') {
        throw 'Checksum generation must run on PRs too.'
    }
    return [scriptblock]::Create(($run -replace '(?m)^          ', ''))
}

$generate = Get-WorkflowScript 'Compute release checksums'
$verify = Get-WorkflowScript 'Verify release checksums'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('keefetch-release-test-' + [guid]::NewGuid().ToString('N'))
$savedRunnerTemp = $env:RUNNER_TEMP
$savedPlgx = $env:PLGX_PATH
$savedGithubEnv = $env:GITHUB_ENV

function Assert-Rejected([string]$case) {
    $rejected = $false
    try { & $verify } catch { $rejected = $true }
    if (-not $rejected) { throw "Release verification accepted $case." }
}

New-Item -ItemType Directory -Path $temp | Out-Null
Push-Location $temp
try {
    New-Item -ItemType Directory -Path 'bin/Release/net48', 'release' | Out-Null
    [IO.File]::WriteAllText((Join-Path $temp 'bin/Release/net48/KeeFetch.dll'), 'synthetic DLL')
    [IO.File]::WriteAllText((Join-Path $temp 'KeeFetch.plgx'), 'synthetic PLGX')
    $env:RUNNER_TEMP = $temp
    $env:PLGX_PATH = Join-Path $temp 'KeeFetch.plgx'
    $env:GITHUB_ENV = Join-Path $temp 'github-env.txt'
    & $generate
    Copy-Item 'bin/Release/net48/KeeFetch.dll', 'KeeFetch.plgx', 'SHA256SUMS.txt' -Destination 'release'
    & $verify
    $original = @(Get-Content 'release/SHA256SUMS.txt')

    Add-Content 'release/KeeFetch.dll' 'tampered'
    Assert-Rejected 'tampered DLL'
    Copy-Item 'bin/Release/net48/KeeFetch.dll' 'release/KeeFetch.dll' -Force
    Remove-Item 'release/KeeFetch.plgx'
    Assert-Rejected 'missing PLGX'
    Copy-Item 'KeeFetch.plgx' 'release/KeeFetch.plgx'

    foreach ($case in @('duplicate', 'missing', 'malformed', 'path traversal')) {
        switch ($case) {
            'duplicate' { $lines = @($original[0], $original[0]) }
            'missing' { $lines = @($original[0]) }
            'malformed' { $lines = @('not-a-hash  KeeFetch.dll', $original[1]) }
            'path traversal' { $lines = @($original[0].Replace('KeeFetch.dll', '../KeeFetch.dll'), $original[1]) }
        }
        Set-Content 'release/SHA256SUMS.txt' $lines
        Assert-Rejected $case
    }
    Set-Content 'release/SHA256SUMS.txt' $original
    & $verify
    Write-Output 'Release workflow self-tests passed (permissions, tag gate, checksums and negative cases).'
}
finally {
    Pop-Location
    $env:RUNNER_TEMP = $savedRunnerTemp
    $env:PLGX_PATH = $savedPlgx
    $env:GITHUB_ENV = $savedGithubEnv
    Remove-Item -LiteralPath $temp -Recurse -Force
}
