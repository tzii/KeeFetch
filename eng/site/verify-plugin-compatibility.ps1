# Read-only integration gates for the website branch. No tagging or publication.
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$KeePassPath)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $root
try {
    $keepassExe = Join-Path $KeePassPath 'KeePass.exe'
    if (!(Test-Path -LiteralPath $keepassExe)) { throw "KeePass.exe not found: $keepassExe" }

    & ./eng/check-version.ps1
    & ./eng/check-plgx-manifest.ps1
    & ./eng/test-release-workflow.ps1

    dotnet restore KeeFetch.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Plugin restore failed.' }
    dotnet restore KeeFetch.Tests/KeeFetch.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Test restore failed.' }

    dotnet build KeeFetch.csproj --configuration Release --no-restore "-p:KeePassPath=$KeePassPath" -p:LangVersion=5 -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'C# 5 Release build failed.' }
    dotnet build KeeFetch.Tests/KeeFetch.Tests.csproj --configuration Release --no-restore "-p:KeePassPath=$KeePassPath" -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Release test build failed.' }
    Copy-Item -LiteralPath $keepassExe -Destination 'KeeFetch.Tests/bin/Release/net48/'
    dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --configuration Release --no-build --logger 'trx;LogFileName=plugin-compatibility.trx' --results-directory site-qa/plugin-tests
    if ($LASTEXITCODE -ne 0) { throw 'Full MSTest suite failed.' }

    powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1 -Check
    if ($LASTEXITCODE -ne 0) { throw 'Profile export consistency failed.' }
    powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/test-benchmark-harness.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Benchmark harness self-tests failed.' }

    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace validation failed.' }
} finally {
    Pop-Location
}
