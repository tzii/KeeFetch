# KeeFetch v1.3 Benchmark Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a C# 5-safe, reproducible benchmark and corpus foundation that emits validated row-level evidence without changing production provider behavior.

**Architecture:** Keep live fetching in the existing plugin assembly and move experiment orchestration, schemas, validation, checkpointing, and aggregation into focused PowerShell modules under `eng/benchmark/`. Store safe fixture inputs in `KeeFetch.Tests/Fixtures/`, append checkpoint rows as NDJSON, and derive deterministic CSV/JSON summaries only after a run is complete.

**Tech Stack:** .NET Framework 4.8, C# 5 production sources, MSTest, PowerShell 5.1-compatible scripts, JSON/CSV, existing KeePass integration.

---

## File map

- Modify `KeeFetch.csproj` — make C# 5 the normal production language level.
- Modify `.github/workflows/build.yml` — run benchmark self-tests and corpus validation.
- Modify `.gitignore` — ignore generated benchmark run directories, not fixtures or reviewed reports.
- Create `KeeFetch.Tests/RegressionPackageTests.cs` — verify the committed synthetic package contents and manifest.
- Create `KeeFetch.Tests/Fixtures/Regression/` — hold the four safe synthetic fixture files.
- Create `KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv` — versioned public corpus.
- Create `KeeFetch.Tests/Fixtures/ProviderCorpus/v1/categories.json` — allowed category vocabulary and quotas.
- Create `eng/benchmark/BenchmarkHarness.psm1` — validate inputs, manage runs, write checkpoints, and aggregate results.
- Create `eng/benchmark/test-benchmark-harness.ps1` — dependency-free deterministic module tests.
- Create `eng/benchmark/experiments/baseline-v12.json` — declarative baseline experiment.
- Create `eng/benchmark/experiments/smoke-one-row.json` — deterministic single-fixture live smoke definition.
- Rewrite `eng/benchmark-presets.ps1` — thin CLI over the module and existing downloader assembly.

### Task 1: Enforce the production C# 5 boundary

**Files:**
- Modify: `KeeFetch.csproj`
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Prove the current production source is C# 5-compatible**

Run:

```powershell
dotnet build KeeFetch.csproj --configuration Release --no-restore `
  -p:KeePassPath="C:\Program Files\KeePass Password Safe 2" `
  -p:LangVersion=5 -warnaserror
```

Expected: `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.

- [ ] **Step 2: Make C# 5 the project default**

Change the production property to:

```xml
<LangVersion>5</LangVersion>
```

Do not change `KeeFetch.Tests/KeeFetch.Tests.csproj`; test-only code may remain C# 7.3.

- [ ] **Step 3: Verify ordinary and explicit compatibility builds**

Run:

```powershell
dotnet build KeeFetch.csproj --configuration Release --no-restore `
  -p:KeePassPath="C:\Program Files\KeePass Password Safe 2" -warnaserror
dotnet build KeeFetch.csproj --configuration Release --no-restore `
  -p:KeePassPath="C:\Program Files\KeePass Password Safe 2" `
  -p:LangVersion=5 -warnaserror
```

Expected: both commands succeed with zero warnings and errors.

- [ ] **Step 4: Keep the explicit CI compatibility gate**

Retain the existing `Verify C# 5 compatibility` step. Add `--no-restore` so both local and CI compatibility commands have the same dependency behavior.

- [ ] **Step 5: Commit**

```powershell
git add KeeFetch.csproj .github/workflows/build.yml
git commit -m "build: enforce C# 5 plugin compatibility"
```

### Task 2: Import and validate the 71-entry synthetic regression package

**Files:**
- Create: `KeeFetch.Tests/Fixtures/Regression/KeeFetch-Test-Database.kdbx`
- Create: `KeeFetch.Tests/Fixtures/Regression/KeeFetch-Test-Database.xml`
- Create: `KeeFetch.Tests/Fixtures/Regression/KeeFetch-Test-Manifest.csv`
- Create: `KeeFetch.Tests/Fixtures/Regression/KeeFetch-Test-README.txt`
- Create: `KeeFetch.Tests/RegressionPackageTests.cs`

- [ ] **Step 1: Extract only the four reviewed entries from the supplied archive**

Use the user-provided archive and an isolated temporary directory:

```powershell
$archive = Join-Path $env:USERPROFILE 'Downloads\KeeFetch-Test-Package.zip'
$destination = Join-Path $env:TEMP 'keefetch-v13-regression-import'
if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse }
Expand-Archive -LiteralPath $archive -DestinationPath $destination
Copy-Item -LiteralPath (Join-Path $destination 'KeeFetch-Test-Database.kdbx') -Destination 'KeeFetch.Tests\Fixtures\Regression\'
Copy-Item -LiteralPath (Join-Path $destination 'KeeFetch-Test-Database.xml') -Destination 'KeeFetch.Tests\Fixtures\Regression\'
Copy-Item -LiteralPath (Join-Path $destination 'KeeFetch-Test-Manifest.csv') -Destination 'KeeFetch.Tests\Fixtures\Regression\'
Copy-Item -LiteralPath (Join-Path $destination 'KeeFetch-Test-README.txt') -Destination 'KeeFetch.Tests\Fixtures\Regression\'
```

Expected: exactly four files exist under `KeeFetch.Tests/Fixtures/Regression/`; do not copy the outer ZIP.

- [ ] **Step 2: Write the failing fixture contract test**

Create `KeeFetch.Tests/RegressionPackageTests.cs` with this test shape:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class RegressionPackageTests
    {
        [TestMethod]
        public void RegressionManifest_HasStableIdsAndExpectedGroups()
        {
            string path = FixturePath("Regression", "KeeFetch-Test-Manifest.csv");
            string[] lines = File.ReadAllLines(path);
            Assert.AreEqual(72, lines.Length, "Header plus 71 fixtures expected.");

            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = ParseCsvLine(lines[index]);
                Assert.IsTrue(fields.Length >= 6, "fixture_id plus five legacy columns expected.");
                Assert.IsTrue(ids.Add(fields[0]), "Duplicate fixture_id: " + fields[0]);
                groups.Add(fields[1]);
            }

            Assert.IsTrue(groups.Contains("01 Happy Paths"));
            Assert.IsTrue(groups.Contains("03 Android App URLs"));
            Assert.IsTrue(groups.Contains("06 Issue 1 Regression Corpus"));
            Assert.IsTrue(groups.Contains("08 Bulk / Concurrency"));
        }

        private static string FixturePath(params string[] parts)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
            foreach (string part in parts) path = Path.Combine(path, part);
            return path;
        }

        private static string[] ParseCsvLine(string line)
        {
            using (var reader = new StringReader(line))
            using (var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(reader))
            {
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                return parser.ReadFields();
            }
        }
    }
}
```

Add this explicit reference to `KeeFetch.Tests/KeeFetch.Tests.csproj`:

```xml
<Reference Include="Microsoft.VisualBasic" />
```

- [ ] **Step 3: Run the test and confirm the missing ID failure**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore `
  --filter FullyQualifiedName~RegressionPackageTests
```

Expected: FAIL because the imported manifest has only the legacy five columns.

- [ ] **Step 4: Add deterministic fixture IDs**

Update the CSV header to `fixture_id,Group,Title,URL,Expected,Notes`. Assign IDs `reg-001` through `reg-071` in existing row order. Do not change the remaining cell values.

- [ ] **Step 5: Run the focused and full test suites**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore `
  --filter FullyQualifiedName~RegressionPackageTests
dotnet test KeeFetch.sln --no-restore
```

Expected: focused test passes; full suite reports at least 97 passing tests and zero failures.

- [ ] **Step 6: Commit**

```powershell
git add KeeFetch.Tests/Fixtures/Regression KeeFetch.Tests/RegressionPackageTests.cs KeeFetch.Tests/KeeFetch.Tests.csproj
git commit -m "test: add synthetic KeeFetch regression package"
```

### Task 3: Add corpus vocabularies and dependency-free validation

**Files:**
- Create: `KeeFetch.Tests/Fixtures/ProviderCorpus/v1/categories.json`
- Create: `KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv`
- Create: `eng/benchmark/BenchmarkHarness.psm1`
- Create: `eng/benchmark/test-benchmark-harness.ps1`

- [ ] **Step 1: Create the checked category vocabulary**

Use this complete JSON structure:

```json
{
  "version": 1,
  "expected_classes": [
    "usable-site-icon", "usable-icon", "graceful-not-found",
    "graceful-reject", "android-map", "resolve-reference",
    "skip-existing", "deduplicate", "concurrency-success"
  ],
  "categories": {
    "global-brand": 60,
    "regional-service": 60,
    "small-site": 45,
    "self-hosted-software": 30,
    "url-variant": 25,
    "svg-or-manifest": 25,
    "missing-or-invalid": 20,
    "android-app": 20,
    "deduplication": 15
  },
  "minimum_total": 300
}
```

- [ ] **Step 2: Write the failing validator self-test**

Create `eng/benchmark/test-benchmark-harness.ps1` that imports the future module, writes a temporary CSV with a duplicate ID and a private host, and asserts validation fails:

```powershell
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'BenchmarkHarness.psm1') -Force
$temp = Join-Path $env:TEMP ('keefetch-benchmark-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $csv = Join-Path $temp 'invalid.csv'
    @(
        [pscustomobject]@{ fixture_id='dup'; category='global-brand'; input_url='https://example.com'; expected_class='usable-icon'; expected_host='example.com'; review_required='false'; notes='' },
        [pscustomobject]@{ fixture_id='dup'; category='global-brand'; input_url='http://localhost'; expected_class='usable-icon'; expected_host='localhost'; review_required='false'; notes='' }
    ) | Export-Csv -LiteralPath $csv -NoTypeInformation -Encoding UTF8
    $failed = $false
    try { Test-KeeFetchCorpus -CsvPath $csv -VocabularyPath (Join-Path $PSScriptRoot '..\..\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json') }
    catch { $failed = $_.Exception.Message -match 'Duplicate fixture_id|private or loopback' }
    if (-not $failed) { throw 'Invalid corpus was accepted.' }
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
Write-Output 'Benchmark harness self-tests passed.'
```

- [ ] **Step 3: Run the self-test and verify it fails**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/test-benchmark-harness.ps1
```

Expected: FAIL because `BenchmarkHarness.psm1` does not exist.

- [ ] **Step 4: Implement `Test-KeeFetchCorpus`**

Create `BenchmarkHarness.psm1` with exported functions and explicit checks:

```powershell
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
    return $rows
}
Export-ModuleMember -Function Test-KeeFetchCorpus
```

- [ ] **Step 5: Rerun the self-test**

Expected: output ends with `Benchmark harness self-tests passed.` and exit code 0.

- [ ] **Step 6: Build the 300-row public corpus to the fixed quotas**

Populate `public-sites.csv` with the exact header:

```csv
fixture_id,category,input_url,expected_class,expected_host,review_required,notes
```

Use stable IDs `pub-001` through `pub-300`. Meet each quota in `categories.json` exactly. Use public unauthenticated targets only, set `review_required=true` for all synthetic/placeholder-prone study cases, and record a concise reason in `notes`. Run the validator after every category batch.

- [ ] **Step 7: Extend validation to enforce quotas and minimum total**

After row validation, group by category and throw unless every actual count equals the vocabulary quota and total rows are at least 300. Add valid and wrong-quota cases to `test-benchmark-harness.ps1`.

- [ ] **Step 8: Commit**

```powershell
git add KeeFetch.Tests/Fixtures/ProviderCorpus eng/benchmark
git commit -m "test: add versioned provider benchmark corpus"
```

### Task 4: Implement checkpointed row-level benchmark output

**Files:**
- Modify: `eng/benchmark/BenchmarkHarness.psm1`
- Modify: `eng/benchmark/test-benchmark-harness.ps1`
- Create: `eng/benchmark/experiments/baseline-v12.json`

- [ ] **Step 1: Write failing checkpoint and aggregation tests**

Add deterministic tests that call `New-KeeFetchRun`, `Add-KeeFetchResult`, and `Complete-KeeFetchRun`, then assert:

```powershell
if (-not (Test-Path (Join-Path $run.Directory 'results.ndjson'))) { throw 'Missing NDJSON checkpoint.' }
if (-not (Test-Path (Join-Path $run.Directory 'rows.csv'))) { throw 'Missing rows.csv.' }
if (-not (Test-Path (Join-Path $run.Directory 'summary.csv'))) { throw 'Missing summary.csv.' }
if (-not (Test-Path (Join-Path $run.Directory 'run.json'))) { throw 'Missing run.json.' }
if ((Import-Csv (Join-Path $run.Directory 'rows.csv')).Count -ne 2) { throw 'Expected two finalized rows.' }
```

Run the self-test and expect failure because the functions are undefined.

- [ ] **Step 2: Implement the run functions**

Add C# 5/PowerShell 5.1-compatible functions that:

- Create a unique run directory and immutable metadata object.
- Append one compact JSON object per completed fixture to `results.ndjson` using UTF-8 without truncation.
- On resume, load completed `(fixture_id, repetition)` keys and skip them.
- On completion, parse every NDJSON line, sort by fixture ID/repetition, export `rows.csv`, aggregate by experiment/profile into `summary.csv`, and write final `run.json` with `status: complete`.
- Leave interrupted `run.json` as `status: incomplete`.

The row object must include the fields named in spec §6.2, including `tier`, `is_synthetic`, `placeholder_suspected`, `blank_suspected`, `machine_outcome`, total elapsed time, serialized per-provider metrics, normalized icon artifact path, and SHA-256 artifact hash. Store icon artifacts only inside the ignored run directory; do not commit them.

- [ ] **Step 3: Add the baseline experiment definition**

Create:

```json
{
  "experiment_id": "baseline-v12",
  "corpus": "KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv",
  "profiles": ["Fast", "Balanced", "Thorough"],
  "repetitions": 3,
  "concurrency": 8,
  "cache_modes": ["cold", "warm"],
  "output_root": "eng/benchmark-runs/baseline-v12"
}
```

- [ ] **Step 4: Verify deterministic self-tests**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/test-benchmark-harness.ps1
```

Expected: exit 0 and `Benchmark harness self-tests passed.`

- [ ] **Step 5: Ignore generated runs**

Add only this pattern to `.gitignore`:

```gitignore
eng/benchmark-runs/
```

- [ ] **Step 6: Commit**

```powershell
git add eng/benchmark .gitignore
git commit -m "feat: add checkpointed benchmark result pipeline"
```

### Task 5: Convert the existing benchmark script into a thin CLI

**Files:**
- Modify: `eng/benchmark-presets.ps1`
- Modify: `eng/benchmark/BenchmarkHarness.psm1`
- Modify: `eng/benchmark/test-benchmark-harness.ps1`

- [ ] **Step 1: Add a failing experiment-definition test**

The test must load `baseline-v12.json`, assert every required property exists, and assert an invalid cache mode is rejected with `Unknown cache mode`.

- [ ] **Step 2: Add `Read-KeeFetchExperiment`**

Implement strict parsing for `experiment_id`, `corpus`, `profiles`, `repetitions`, `concurrency`, `cache_modes`, and `output_root`, plus optional `fixture_ids`. Reject missing required fields, repetitions below 1, concurrency below 1, cache modes outside `cold`/`warm`, or requested fixture IDs absent from the corpus.

- [ ] **Step 3: Replace hard-coded profile variants with the experiment loop**

Keep existing reflection-based assembly loading and `DownloadAsync` invocation, but route every completed `FaviconResult` through `Add-KeeFetchResult`. Before a cold run call `FaviconDownloader.ClearCache`; warm runs keep the cache between repetitions of the same profile. Do not add production behavior or provider changes in this task.

The CLI becomes:

```powershell
param(
    [Parameter(Mandatory=$true)][string]$Experiment,
    [string]$ResumeRun = ''
)
```

- [ ] **Step 4: Run a deterministic one-row smoke experiment**

Create `eng/benchmark/experiments/smoke-one-row.json`:

```json
{
  "experiment_id": "smoke-one-row",
  "corpus": "KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv",
  "fixture_ids": ["pub-001"],
  "profiles": ["Balanced"],
  "repetitions": 1,
  "concurrency": 1,
  "cache_modes": ["cold"],
  "output_root": "eng/benchmark-runs/smoke-one-row"
}
```

Run:

```powershell
dotnet build KeeFetch.csproj --configuration Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark-presets.ps1 `
  -Experiment eng/benchmark/experiments/smoke-one-row.json
```

Expected: one finalized row, `run.json` status `complete`, and no unhandled exception. Keep the definition as the standard opt-in live smoke; CI does not execute it.

- [ ] **Step 5: Commit**

```powershell
git add eng/benchmark-presets.ps1 eng/benchmark
git commit -m "refactor: drive benchmarks from experiment definitions"
```

### Task 6: Add CI validation and complete the checkpoint

**Files:**
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Add offline benchmark validation to CI**

After tests and before PLGX packaging, add:

```yaml
- name: Validate benchmark harness and corpora
  shell: pwsh
  run: |
    powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/test-benchmark-harness.ps1
    Import-Module .\eng\benchmark\BenchmarkHarness.psm1 -Force
    Test-KeeFetchCorpus `
      -CsvPath .\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\public-sites.csv `
      -VocabularyPath .\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json | Out-Null
```

Do not run live provider requests in CI.

- [ ] **Step 2: Run the complete local gate**

```powershell
dotnet build KeeFetch.sln --configuration Release --no-restore -warnaserror
dotnet test KeeFetch.sln --configuration Release --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/test-benchmark-harness.ps1
git diff --check
```

Expected: build succeeds, all tests pass, harness self-tests pass, and `git diff --check` produces no errors.

- [ ] **Step 3: Confirm plan exit criteria**

Verify:

- Production builds default to C# 5.
- The regression corpus has 71 stable fixture IDs.
- The public corpus validates at 300 entries and exact category quotas.
- The harness can resume NDJSON checkpoints and finalize deterministic exports.
- CI contains no live network benchmark.

- [ ] **Step 4: Commit**

```powershell
git add .github/workflows/build.yml
git commit -m "ci: validate benchmark harness and corpora"
```
