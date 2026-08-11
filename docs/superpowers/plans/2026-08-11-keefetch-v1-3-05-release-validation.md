# KeeFetch v1.3 Release Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove that v1.3 provider claims, migrated settings, guided-native UX, website content, DLL, and PLGX artifacts agree and are ready for an explicitly authorized release.

**Architecture:** Freeze a release candidate commit, rerun every deterministic gate from Plans 1–4, execute manual KeePass and website matrices against generated artifacts, and record hashes/evidence in one release-validation document. Stop after creating verified local artifacts and release notes; tagging, pushing, and publishing require separate user authorization.

**Tech Stack:** .NET 8 SDK targeting .NET Framework 4.8, C# 5 production build, MSTest, PowerShell, KeePass PLGX tooling, Python standard library, GitHub Actions/Pages.

---

## File map

- Modify `version.txt` — set v1.3.0 metadata.
- Modify `Properties/AssemblyInfo.cs` — align assembly/plugin version.
- Modify `CHANGELOG.md` — final user-visible changes.
- Modify `README.md` — final profile, UX, website, privacy, and install descriptions.
- Create `docs/validation/v1.3-release-validation.md` — commands, environment, results, hashes, and manual evidence.
- Create `docs/releases/v1.3.0.md` — release notes ready to paste into GitHub.
- Do not commit `bin/`, `artifacts/`, PLGX staging directories, raw benchmark runs, or local screenshots from smoke tests.

### Task 1: Freeze version and cross-surface release text

**Files:**
- Modify: `version.txt`
- Modify: `Properties/AssemblyInfo.cs`
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Create: `docs/releases/v1.3.0.md`

- [ ] **Step 1: Add a failing version-consistency check**

Extend the existing test suite or add `KeeFetch.Tests/VersionConsistencyTests.cs` to read `version.txt` and assembly attributes, then assert product version `1.3.0` and matching major/minor/patch. The test must also assert README latest-release text and website profile data do not contain v1.2 profile claims outside historical sections.

- [ ] **Step 2: Run the focused test and verify failure**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~VersionConsistencyTests
```

Expected: FAIL because current metadata is v1.2.0.

- [ ] **Step 3: Update version and user-facing release text**

Set v1.3.0 consistently. Add changelog sections for evidence-backed profiles, catalog/migration, guided-native settings, first run, structured completion/retry, provider/corpus testing, and complete website. State privacy behavior using final generated profile data; do not copy provisional chains.

- [ ] **Step 4: Write release notes**

Include Highlights, Upgrade/migration behavior, Profiles, Privacy, Installation, Known limitations, Full changelog link, and contributor acknowledgments when applicable. Task 4 adds the Verification hashes section only after exact artifacts exist; do not commit the file before that section is complete.

- [ ] **Step 5: Run focused tests**

Expected: version consistency passes after metadata changes.

### Task 2: Run the complete deterministic release gate

**Files:**
- Create: `docs/validation/v1.3-release-validation.md`

- [ ] **Step 1: Record immutable environment metadata**

Record release-candidate commit SHA, Windows version, .NET SDK, KeePass version/path, PowerShell version, Python version, Edge version, corpus version/counts, profile-data hash, and UTC timestamp.

- [ ] **Step 2: Run clean restore/build/test gates**

```powershell
dotnet restore KeeFetch.csproj
dotnet restore KeeFetch.Tests/KeeFetch.Tests.csproj
dotnet build KeeFetch.sln --configuration Release --no-restore -warnaserror
dotnet build KeeFetch.csproj --configuration Release --no-restore `
  -p:LangVersion=5 -p:KeePassPath="C:\Program Files\KeePass Password Safe 2" -warnaserror
dotnet test KeeFetch.sln --configuration Release --no-build
```

Expected: zero warnings and failures.

- [ ] **Step 3: Run benchmark/catalog/site offline gates**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/benchmark/test-benchmark-harness.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1 -Check
python eng/sync-site-profiles.py --check
python eng/test-verify-site.py -v
python eng/verify-site.py site
powershell -NoProfile -ExecutionPolicy Bypass -File eng/smoke-site.ps1
git diff --check
```

Expected: every command exits 0 and tracked files remain unchanged.

- [ ] **Step 4: Record exact outputs**

In `v1.3-release-validation.md`, record command, exit code, test count, warning count, and artifact/evidence path. Do not summarize a failed command as passed; fix the defect and rerun the complete affected gate.

### Task 3: Create and load-test release artifacts

**Files:**
- Update: `docs/validation/v1.3-release-validation.md`

- [ ] **Step 1: Rehearse the CI PLGX staging flow locally**

Create `$plgxSourceDir = Join-Path $env:TEMP 'KeeFetch'`, copy exactly the source/resources listed by `build.yml`, replace the SDK project with `KeeFetch.plgx.csproj`, and run KeePass `--plgx-create`. KeePass writes `$generatedPlgxPath = Join-Path $env:TEMP 'KeeFetch.plgx'`. Remove an older file at that exact path before generation and verify the new file exists afterward. Do not stage from the repository root.

- [ ] **Step 2: Inspect the PLGX manifest**

Verify every file under `FetchProfiles/`, `Settings/`, and `Batch/`, plus `FirstRunForm`, `CompletionForm`, resources, icons, and existing provider/selection files are present. Fail if any production source in `KeeFetch.csproj` is absent from the legacy project or staging list.

- [ ] **Step 3: Load-test in a disposable portable KeePass instance**

Install the generated PLGX, start KeePass, and verify plugin compilation/cache creation. Open the synthetic KDBX and exercise Settings, first run, single entry, group, whole database, cancellation, partial failure, diagnostics, and one bounded retry.

- [ ] **Step 4: Build the release DLL artifact**

Define the release-candidate directory from the frozen commit and copy both artifacts there:

```powershell
$commit = (git rev-parse --short=12 HEAD).Trim()
$releaseCandidateDir = Join-Path $env:TEMP ("KeeFetch-v1.3.0-rc-" + $commit)
New-Item -ItemType Directory -Path $releaseCandidateDir -Force | Out-Null
Copy-Item -LiteralPath 'bin\Release\net48\KeeFetch.dll' -Destination $releaseCandidateDir
Copy-Item -LiteralPath $generatedPlgxPath -Destination (Join-Path $releaseCandidateDir 'KeeFetch.plgx')
```

`$generatedPlgxPath` is the explicit path verified in Task 3 Step 1. Do not commit artifacts.

- [ ] **Step 5: Record artifact sizes and paths**

Add sizes, UTC timestamps, PLGX manifest result, runtime-load result, and disposable environment details to the validation document.

### Task 4: Hash artifacts and finalize release notes

**Files:**
- Modify: `docs/releases/v1.3.0.md`
- Modify: `docs/validation/v1.3-release-validation.md`

- [ ] **Step 1: Compute SHA-256 hashes**

```powershell
$commit = (git rev-parse --short=12 HEAD).Trim()
$releaseCandidateDir = Join-Path $env:TEMP ("KeeFetch-v1.3.0-rc-" + $commit)
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseCandidateDir 'KeeFetch.dll')
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseCandidateDir 'KeeFetch.plgx')
```

- [ ] **Step 2: Insert exact hashes into both documents**

Record filename, byte length, SHA-256, source commit, and build timestamp. Remove the empty hash-section state from Task 1.

- [ ] **Step 3: Cross-check release claims**

Compare release notes and README against `site/data/profiles.json`, provider evidence report, UI names, download asset names, migration matrix, and privacy page. Search for obsolete v1.2 claims and old MessageBox instructions.

### Task 5: Execute final manual matrices

**Files:**
- Modify: `docs/validation/v1.3-release-validation.md`

- [ ] **Step 1: Execute the 71-entry regression database matrix**

Run every group described in `KeeFetch-Test-README.txt`. Record updated/skipped/not-found/error counts, crashes/hangs, custom icon preservation, deduplication observation, diagnostics paths, and retry result.

- [ ] **Step 2: Execute migration scenarios**

For legacy Fast, Balanced, Thorough, Custom, missing new install, duplicate provider order, and unknown stored value, record resulting stable ID and preserved custom values. Run each scenario twice to prove idempotency.

- [ ] **Step 3: Execute UI/accessibility matrix**

Repeat the approved DPI, High Contrast, keyboard, content-stress, first-run, cancellation, partial failure, retry, and success cases against the release artifact—not a development DLL.

- [ ] **Step 4: Execute website matrix**

Verify all seven pages at phone/tablet/desktop widths, JavaScript disabled fallback, profile content, privacy statements, internal links, download routes, release verification instructions, and final media.

- [ ] **Step 5: Resolve every failed row**

Do not mark release-ready with unexplained failures. Fix the smallest responsible implementation, rerun its focused tests, then rerun the affected full gate and update evidence.

### Task 6: Commit the verified release-candidate documentation

**Files:**
- Stage only tracked source/document/workflow changes from Tasks 1–5

- [ ] **Step 1: Verify no generated artifacts are staged**

```powershell
git status --short
git diff --cached --name-only
```

Expected: no `bin/`, `obj/`, `artifacts/`, PLGX, benchmark-run, temporary screenshot, `.commandcode/`, or `.superpowers/` paths.

- [ ] **Step 2: Run the final deterministic gate again**

Repeat Task 2 Steps 2–3 from the final working tree. Expected: all commands pass and no tracked output changes.

- [ ] **Step 3: Stage exact release files and inspect**

```powershell
git add version.txt Properties/AssemblyInfo.cs CHANGELOG.md README.md `
  docs/releases/v1.3.0.md docs/validation/v1.3-release-validation.md
git diff --cached --check
git diff --cached --stat
```

- [ ] **Step 4: Commit**

```powershell
git commit -m "chore: prepare KeeFetch v1.3.0 release candidate"
```

- [ ] **Step 5: Stop for release authorization**

Report the commit SHA, test count, build warnings, PLGX load result, website gate result, artifact paths/hashes, and any known limitations. Do not tag, push, create a GitHub release, or deploy Pages until the user explicitly authorizes publication.
