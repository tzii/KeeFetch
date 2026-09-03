# KeeFetch v1.3 Release Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Use `superpowers:using-git-worktrees` before changing a branch, `superpowers:test-driven-development` for every code or behavior change, `superpowers:systematic-debugging` for any failure, `superpowers:requesting-code-review` before each PR is marked ready, and `superpowers:verification-before-completion` before every success claim. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete, validate, merge, package, and publish KeeFetch v1.3.0 with the guided-native plugin UX, an evidence-consistent seven-page website, verified DLL and PLGX artifacts, exact hashes, complete release documentation, and an explicitly authorized GitHub release.

**Architecture:** Finish the work through four bounded branches in strict sequence: guided-native UX closure, provider-study closure, website completion, and release-candidate validation. Every branch has its own deterministic gate, manual evidence gate, review gate, and merge checkpoint. The release is published manually from the exact locally verified artifacts; tag CI validates the tagged source but does not rebuild and silently substitute different release files.

**Tech Stack:** Windows, PowerShell 5.1+, Git/GitHub CLI, .NET 8 SDK targeting .NET Framework 4.8, C# 5 production code, C# 7.3 MSTest code, KeePass 2.60, KeePass PLGX tooling, Python 3 standard library, dependency-free static HTML/CSS/JavaScript, Microsoft Edge headless mode, GitHub Actions, and GitHub Pages.

## Authoritative starting state

This plan is pinned to the live repository state verified on 2026-08-27:

- `origin/master` is `462fb25ef8588c3990e348dde10692f4b4087d31`.
- `origin/codex/v1-3-guided-native-ux` is `8f5bd717bd0ba718e2cf37d6c638e0904a4abe67`.
- PR #4 is merged and contains the benchmark foundation, profile catalog, migration, execution policy, completed profile study, generated profile data, and catalog drift gate.
- The guided-native UX branch contains Plan 03 Tasks 1–7 and the automated portion of Task 8. Its checked-in evidence records 201 passing MSTest tests, warning-free Release/C# 5 builds, and successful PLGX creation. Those historical results must be rerun before they are claimed in a new execution.
- The guided-native UX branch has no pull request and no GitHub Actions run.
- The remaining UI gate is real-host validation in visible KeePass, including High Contrast, keyboard traversal, first install, upgrade, Custom migration, cancellation, partial failure, one retry, full success, long diagnostics paths, and real-host DPI checks.
- The website still consists primarily of `site/index.html`, generated `site/data/profiles.json`, and icons. The required six additional pages, shared assets, verifiers, smoke renderer, final v1.3 media, and website evidence do not exist.
- `version.txt`, `Properties/AssemblyInfo.cs`, `CHANGELOG.md`, README/site release labels, and the latest GitHub release remain at v1.2.0.
- No `v1.3.0` tag or GitHub release exists.
- Draft PRs #5 and #6 modify only `site/index.html`; neither is Plan 04 completion and neither may be merged as a substitute for the website tasks in this plan.
- The checked-out workspace that existed when this plan was written was stale. Never infer the execution base from the current checkout; verify remote refs first.

## Global constraints

1. Production source must remain C# 5 compatible. Do not use string interpolation, expression-bodied members, null-conditional operators, pattern matching, tuples, local functions, records, or newer syntax in production files.
2. Test-only source may use C# 7.3 and must remain compatible with the existing MSTest packages.
3. The plugin continues to target `.NET Framework 4.8` and must load in KeePass 2.60.
4. `KeeFetch.csproj` and `KeeFetch.plgx.csproj` must include equivalent production source and resources. The SDK project may discover files implicitly; the legacy PLGX project must list them explicitly.
5. Do not add website package managers, JavaScript frameworks, CSS frameworks, Python packages, browser downloads, analytics, telemetry, trackers, or externally hosted runtime assets.
6. Website CI must run offline using Python's standard library and the Edge installation already present on the runner.
7. JavaScript is progressive enhancement. Navigation, installation, privacy, troubleshooting, release verification, and profile comparison remain usable when JavaScript is disabled.
8. All profile names, provider chains, timeouts, synthetic-fallback behavior, Android-store behavior, privacy descriptions, and evidence links must agree with `FetchProfileCatalog.ManagedProfiles` and generated `site/data/profiles.json`.
9. The stable profile ID remains `max-coverage`; the user-facing display name on the guided UX branch is `Precise`. Do not restore the misleading `Thorough` display name unless a new approved provider study changes the facts.
10. The v1.3 provider census was machine-reviewed, not human-reviewed. Reviewer provenance is `machine:antigravity-1.1.18/gemini-3.7-flash-high`, owner-approved after the recorded pilot, arbitration, focused re-asks, and spot-check. Never describe it as human review.
11. Preserve the existing v1.3 evidence and raw-run provenance. Do not edit `eng/benchmark-presets.ps1` or `eng/benchmark/BenchmarkHarness.psm1` before deciding the provider-study closure route; those files participate in the execution-harness fingerprint.
12. Raw benchmark runs, downloaded icons, review queues containing local paths, local smoke screenshots, generated PLGX staging directories, `bin/`, `obj/`, temporary KeePass profiles, and release-candidate binaries remain untracked.
13. Real product screenshots must come from the final release-candidate plugin running in KeePass. Do not fabricate, AI-generate, or edit screenshots in ways that misrepresent the shipped UI.
14. Do not tag, publish a GitHub release, deploy an unreviewed release candidate, merge a PR, or close draft PRs #5/#6 without explicit owner authorization at the corresponding gate.
15. Every failure is either fixed and rerun or recorded as a linked, owner-accepted known limitation. No unexplained failed matrix row may be marked release-ready.
16. Use exact paths and exact commit SHAs in validation evidence. Do not write machine-specific absolute paths into user-facing documentation.
17. Keep `.agent/STATE.md` focused on one current task and prepend concise transitions to `.agent/HANDOFF_LOG.md` as required by `AGENTS.md`.
18. Commit frequently at the task boundaries specified below. Do not combine multiple delivery branches into one PR.

## Branch and dependency sequence

```text
origin/codex/v1-3-guided-native-ux @ 8f5bd71
    -> finish host UI/PLGX evidence
    -> PR and merge to master
    -> resolve provider-study scope/provenance
    -> PR and merge to master
    -> implement complete website and website gates
    -> PR and merge to master
    -> freeze v1.3.0 release source
    -> build and manually validate exact artifacts
    -> commit artifact evidence
    -> PR and merge to master
    -> owner publication authorization
    -> annotated v1.3.0 tag
    -> tag CI validation
    -> attach the exact verified DLL and PLGX
    -> post-release download/hash/site verification
```

Do not start final website media capture until the plugin UX branch is merged. Do not freeze release metadata until the website branch is merged. Do not create the tag until the release-candidate PR is merged and publication is explicitly authorized.

## Planned file map

### Guided-native UX closure

- Modify `.agent/STATE.md` and `.agent/HANDOFF_LOG.md` — current focus and handoff state.
- Modify `docs/validation/v1.3-ui-matrix.md` — real-host DPI, High Contrast, keyboard, migration, progress, completion, diagnostics, cancellation, and retry evidence.
- Modify plugin/test/workflow files only when a manual failure proves a defect.

### Provider-study closure

- Create `docs/benchmarks/v1.3-study-scope-decision.md` — explicit decision on the omitted full-chain-minus-Yandex candidate and review provenance amendment.
- Modify `docs/superpowers/specs/2026-08-11-keefetch-v1-3-polish-design.md` — record the approved machine-census amendment accurately.
- Modify `docs/superpowers/plans/2026-08-11-keefetch-v1-3-02-profile-catalog-migration.md` — record the final review method and scope decision.
- Modify `docs/benchmarks/v1.3-provider-study.md` — add the scope decision or replace study identity/results if a strict rerun is selected.
- Strict-rerun route only: create `eng/benchmark/experiments/profile-candidates-v13-complete.json`, update selector tests, regenerate `FetchProfiles/FetchProfileCatalog.Generated.cs`, and regenerate `site/data/profiles.json`.

### Website completion

- Modify `site/index.html` — concise home page using shared assets and generated release/profile fallbacks.
- Create `site/getting-started.html` — install, update, uninstall, rollback, first run, and usage.
- Create `site/profiles.html` — generated profile comparison and provider roles.
- Create `site/privacy.html` — exact network disclosure, local-data behavior, TLS behavior, logs, and telemetry statement.
- Create `site/troubleshooting.html` — loading, PLGX, missing icons, performance, TLS/proxy, diagnostics, and bug-report checklist.
- Create `site/benchmarks.html` — corpus, method, reviewer provenance, limitations, evidence, and reproduction.
- Create `site/contributing.html` — build, test, architecture, PLGX, provider contribution, and PR workflow.
- Create `site/assets/css/site.css` — shared responsive and accessible visual system.
- Create `site/assets/js/site.js` — progressive navigation, release metadata, and profile enhancement.
- Create `site/data/release.json` — one checked release-data source.
- Create `eng/sync-site-profiles.py` — deterministic no-JavaScript fallback generator with `--check`.
- Create `eng/verify-site.py` — offline semantic, metadata, link, asset, release-data, and profile-data verifier.
- Create `eng/test-verify-site.py` — standard-library unit tests for the verifier.
- Create `eng/smoke-site.ps1` — local HTTP server and Edge rendering at required viewports.
- Create `site/assets/media/settings-overview.png`.
- Create `site/assets/media/settings-providers.png`.
- Create `site/assets/media/first-run.png`.
- Create `site/assets/media/completion-summary.png`.
- Replace `docs/usage-single.gif` and `docs/usage-group.gif` only when freshly captured final recordings are clearer and do not expose real vault data.
- Create `docs/validation/v1.3-website-matrix.md` — offline, responsive, accessibility, no-JavaScript, link, and deployment evidence.
- Modify `.github/workflows/build.yml` — profile sync check, verifier tests, real-site verifier, and Edge smoke.
- Modify `.github/workflows/pages.yml` only if the complete static tree is not deployed.
- Modify `README.md` and `CONTRIBUTING.md` — accurate v1.3 architecture, profiles, privacy, build, and links.

### Release candidate and publication

- Modify `version.txt` — `1.3.0.0`.
- Modify `Properties/AssemblyInfo.cs` — assembly and file version `1.3.0.0`.
- Modify `CHANGELOG.md` — final `1.3.0` section.
- Modify `README.md` — final release, profiles, privacy, installation, verification, and known limitations.
- Modify `site/data/release.json` — final v1.3.0 release metadata.
- Create `KeeFetch.Tests/VersionConsistencyTests.cs` — cross-surface version and stale-claim gate.
- Create `docs/releases/v1.3.0.md` — complete GitHub release notes.
- Create `docs/validation/v1.3-release-validation.md` — environment, commands, matrices, artifact identity, hashes, and final decision.
- Modify `.github/workflows/build.yml` — keep tag CI as validation and Actions-artifact upload, but prevent it from automatically publishing rebuilt files over the manually verified assets.
- Modify `.agent/STATE.md` and `.agent/HANDOFF_LOG.md` — release-review and release-complete states.

---

### Task 0: Establish an isolated, correct execution base

**Files:**
- Read: `AGENTS.md`
- Read: `.agent/STATE.md`
- Read: `.agent/HANDOFF_LOG.md`
- Modify: `.agent/STATE.md`

**Interfaces:**
- Consumes: live GitHub refs and the exact guided UX commit listed in this plan.
- Produces: one isolated worktree on local branch `codex/v1-3-guided-native-ux`, tracking the existing remote branch and verified at the expected SHA.

- [ ] **Step 1: Invoke the required isolation skill**

Invoke `superpowers:using-git-worktrees`. Use the platform's native worktree mechanism when available. Do not edit the stale primary checkout.

- [ ] **Step 2: Fetch and verify live remote state**

Run from the repository checkout:

```powershell
git fetch --prune --tags origin
git status --short --branch
git ls-remote origin HEAD refs/heads/master refs/heads/codex/v1-3-guided-native-ux refs/tags/v1.3.0 'refs/tags/v1.3.0^{}'
gh pr list --repo tzii/KeeFetch --state all --limit 20 --json number,title,state,isDraft,headRefName,baseRefName,headRefOid,updatedAt,url
gh release list --repo tzii/KeeFetch --limit 10
```

Expected before work begins:

- Remote master contains PR #4.
- The guided UX branch exists and is not behind master.
- No v1.3.0 tag or release exists.
- No other PR already supersedes the guided UX branch.

If a newer commit or PR exists, read its changed files and `.agent/STATE.md`, reconcile it against this plan, and update the plan's recorded execution SHA before modifying code. Do not reset or overwrite newer work.

- [ ] **Step 3: Verify the branch relationship**

```powershell
git merge-base --is-ancestor origin/master origin/codex/v1-3-guided-native-ux
if ($LASTEXITCODE -ne 0) { throw 'Guided UX branch is not based on current master.' }

$guidedSha = (git rev-parse origin/codex/v1-3-guided-native-ux).Trim()
if ($guidedSha -ne '8f5bd717bd0ba718e2cf37d6c638e0904a4abe67') {
    throw "Guided UX tip changed from the planned SHA: $guidedSha"
}
```

If the SHA changed because authorized work advanced the branch, inspect the commits and deliberately replace the pinned SHA in the execution copy of this plan. The throw is a stale-plan guard, not permission to discard remote work.

- [ ] **Step 4: Create or attach the isolated worktree**

If native worktree tooling has not already created it, choose a path outside the repository so no `.gitignore` edit is required:

```powershell
$uxWorktree = 'C:\Users\simon\Documents\Projects\KeeFetch-v1-3-guided-native-ux'
$existing = git worktree list --porcelain
if ($existing -match [regex]::Escape($uxWorktree)) {
    Write-Output "Using existing worktree: $uxWorktree"
} else {
    if (Test-Path -LiteralPath $uxWorktree) {
        throw "Target path exists but is not a registered worktree: $uxWorktree"
    }
    $localBranch = git branch --list codex/v1-3-guided-native-ux
    if ([string]::IsNullOrWhiteSpace(($localBranch | Out-String))) {
        git worktree add -b codex/v1-3-guided-native-ux $uxWorktree origin/codex/v1-3-guided-native-ux
    } else {
        git worktree add $uxWorktree codex/v1-3-guided-native-ux
    }
}
```

- [ ] **Step 5: Verify the isolated branch**

Run inside the worktree:

```powershell
git status --short --branch
git rev-parse HEAD
git rev-parse --abbrev-ref --symbolic-full-name '@{u}'
git diff --check
```

Expected: clean branch, exact guided SHA, upstream `origin/codex/v1-3-guided-native-ux`, and no whitespace errors.

- [ ] **Step 6: Read repository instructions and current evidence**

Read completely:

```powershell
Get-Content -LiteralPath AGENTS.md -Raw
Get-Content -LiteralPath .agent\STATE.md -Raw
Get-Content -LiteralPath docs\validation\v1.3-ui-matrix.md -Raw
Get-Content -LiteralPath docs\superpowers\plans\2026-08-11-keefetch-v1-3-03-guided-native-ux.md -Raw
```

- [ ] **Step 7: Set the singular current focus**

Edit `.agent/STATE.md` so it says:

- Status: `WORKING`
- Agent: the executing agent's actual name
- Focus: `Plan 03 Task 8 real-host KeePass validation and final PLGX load proof`
- Next: `Run the complete fresh automated baseline, then execute every pending host-manual row against one exact PLGX.`
- As-of: current UTC date and current branch SHA

Do not mark the task `REVIEW` during setup.

### Task 1: Re-establish the full guided-UX automated baseline

**Files:**
- No planned source changes.
- Temporary logs: an explicit directory under `$env:TEMP`.

**Interfaces:**
- Consumes: guided UX worktree and a KeePass 2.60 installation.
- Produces: fresh logs proving restore, Release build, C# 5 compatibility, 201-test baseline, harness self-tests, corpus validation, profile export consistency, and clean diff.

- [ ] **Step 1: Resolve KeePass and create a temporary evidence directory**

```powershell
$keepassCandidates = @(
    $env:KEEFETCH_KEEPASS_PATH,
    'C:\Program Files\KeePass Password Safe 2\KeePass.exe',
    'C:\Dev\tools\KeePass-2.60\KeePass.exe'
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$keepassExe = $keepassCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($keepassExe)) { throw 'KeePass 2.60 executable was not found.' }

$keepassDir = Split-Path -Parent $keepassExe
$validationStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$uiEvidenceDir = Join-Path $env:TEMP ("KeeFetch-v13-ui-validation-" + $validationStamp)
New-Item -ItemType Directory -Path $uiEvidenceDir | Out-Null
$env:KEEFETCH_KEEPASS_PATH = $keepassExe
```

Record the KeePass file version, Windows version, PowerShell version, .NET SDK version, and UTC time:

```powershell
(Get-Item -LiteralPath $keepassExe).VersionInfo.FileVersion
[System.Environment]::OSVersion.VersionString
$PSVersionTable | Format-List
dotnet --info
(Get-Date).ToUniversalTime().ToString('o')
```

- [ ] **Step 2: Restore from the branch lock state**

```powershell
dotnet restore KeeFetch.csproj 2>&1 | Tee-Object -FilePath (Join-Path $uiEvidenceDir 'restore-plugin.log')
if ($LASTEXITCODE -ne 0) { throw 'Plugin restore failed.' }

dotnet restore KeeFetch.Tests\KeeFetch.Tests.csproj 2>&1 | Tee-Object -FilePath (Join-Path $uiEvidenceDir 'restore-tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Test restore failed.' }
```

- [ ] **Step 3: Run warning-free Release builds**

```powershell
dotnet build KeeFetch.sln --configuration Release --no-restore -p:KeePassPath="$keepassDir" -warnaserror 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'build-solution-release.log')
if ($LASTEXITCODE -ne 0) { throw 'Release solution build failed.' }

dotnet build KeeFetch.csproj --configuration Release --no-restore -p:KeePassPath="$keepassDir" -p:LangVersion=5 -warnaserror 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'build-plugin-csharp5.log')
if ($LASTEXITCODE -ne 0) { throw 'C# 5 compatibility build failed.' }
```

Expected: zero warnings and zero errors in both logs.

- [ ] **Step 4: Run the full test suite**

Copy KeePass into the test output only after the Release test build exists:

```powershell
dotnet build KeeFetch.Tests\KeeFetch.Tests.csproj --configuration Release --no-restore -p:KeePassPath="$keepassDir" -warnaserror 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'build-tests-release.log')
if ($LASTEXITCODE -ne 0) { throw 'Release test build failed.' }

Copy-Item -LiteralPath $keepassExe -Destination 'KeeFetch.Tests\bin\Release\net48\KeePass.exe' -Force

dotnet test KeeFetch.Tests\KeeFetch.Tests.csproj --configuration Release --no-build 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'tests-release.log')
if ($LASTEXITCODE -ne 0) { throw 'MSTest suite failed.' }
```

Expected baseline at the pinned branch: 201 passed, zero failed, zero skipped. If the total differs, explain the exact added or removed tests before continuing.

- [ ] **Step 5: Run repository-specific gates**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark\test-benchmark-harness.ps1 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'benchmark-self-tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark harness self-tests failed.' }

powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module '.\eng\benchmark\BenchmarkHarness.psm1' -Force; Test-KeeFetchCorpus -CsvPath '.\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\public-sites.csv' -VocabularyPath '.\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json' | Out-Null" 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'corpus-validation.log')
if ($LASTEXITCODE -ne 0) { throw 'Provider corpus validation failed.' }

powershell -NoProfile -ExecutionPolicy Bypass -File eng\export-profile-data.ps1 -Check 2>&1 |
    Tee-Object -FilePath (Join-Path $uiEvidenceDir 'profile-export-check.log')
if ($LASTEXITCODE -ne 0) { throw 'Profile export consistency failed.' }

git diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' }

git status --short
```

Expected: every command exits zero and no tracked file changes are produced.

- [ ] **Step 6: Stop on any baseline failure**

If any command fails, invoke `superpowers:systematic-debugging`. Do not proceed to manual UI validation while the baseline is red. Fix only the proven defect, add or update a regression test, rerun the focused test, then rerun this complete task.

### Task 2: Build one final PLGX and complete the real-host UI matrix

**Files:**
- Modify: `docs/validation/v1.3-ui-matrix.md`
- Modify after a proven defect only: responsible production/test/workflow files.
- Modify: `.agent/STATE.md` and `.agent/HANDOFF_LOG.md`

**Interfaces:**
- Consumes: the green automated baseline and exact branch SHA.
- Produces: one identifiable PLGX, visible KeePass load proof, completed manual matrix, and no pending host-manual rows.

- [ ] **Step 1: Freeze the PLGX source SHA**

```powershell
$uiSourceSha = (git rev-parse HEAD).Trim()
$uiSourceShort = (git rev-parse --short=12 HEAD).Trim()
git status --short
if (-not [string]::IsNullOrWhiteSpace((git status --porcelain | Out-String))) {
    throw 'Working tree must be clean before PLGX creation.'
}
```

- [ ] **Step 2: Reproduce CI staging exactly**

```powershell
$plgxStage = Join-Path $env:TEMP ("KeeFetch-v13-ui-plgx-source-" + $uiSourceShort)
$generatedPlgx = Join-Path $env:TEMP ("KeeFetch-v13-ui-" + $uiSourceShort + '.plgx')

if (Test-Path -LiteralPath $plgxStage) {
    $resolvedStage = (Resolve-Path -LiteralPath $plgxStage).Path
    if (-not $resolvedStage.StartsWith((Resolve-Path -LiteralPath $env:TEMP).Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary stage: $resolvedStage"
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
if (Test-Path -LiteralPath $generatedPlgx) { Remove-Item -LiteralPath $generatedPlgx -Force }

New-Item -ItemType Directory -Path $plgxStage | Out-Null
Copy-Item -Path '*.cs' -Destination $plgxStage -Exclude 'TestCompile.cs'
Copy-Item -Path '*Form.resx' -Destination $plgxStage
Copy-Item -LiteralPath Properties -Destination $plgxStage -Recurse
Copy-Item -LiteralPath IconProviders -Destination $plgxStage -Recurse
Copy-Item -LiteralPath FetchProfiles -Destination $plgxStage -Recurse
Copy-Item -LiteralPath IconSelection -Destination $plgxStage -Recurse
Copy-Item -LiteralPath Batch -Destination $plgxStage -Recurse
Copy-Item -LiteralPath Settings -Destination $plgxStage -Recurse
Copy-Item -LiteralPath Assets -Destination $plgxStage -Recurse
Copy-Item -LiteralPath KeeFetch.plgx.csproj -Destination (Join-Path $plgxStage 'KeeFetch.csproj')
```

Compare the staged production list to both projects and `.github/workflows/build.yml`. At minimum verify every root form, `FetchProfiles`, `IconProviders`, `IconSelection`, `Batch`, `Settings`, `Properties`, root production source, `.resx`, and icon resource is present.

- [ ] **Step 3: Create and identify the PLGX**

KeePass names the generated file after the source directory. To avoid ambiguity, create with a source folder named `KeeFetch`, then rename only after existence and timestamp checks:

```powershell
$plgxParent = Join-Path $env:TEMP ("KeeFetch-v13-ui-plgx-parent-" + $uiSourceShort)
$plgxSourceDir = Join-Path $plgxParent 'KeeFetch'
New-Item -ItemType Directory -Path $plgxParent -Force | Out-Null
Move-Item -LiteralPath $plgxStage -Destination $plgxSourceDir
$keepassGeneratedPath = Join-Path $plgxParent 'KeeFetch.plgx'
if (Test-Path -LiteralPath $keepassGeneratedPath) { Remove-Item -LiteralPath $keepassGeneratedPath -Force }

$beforeUtc = (Get-Date).ToUniversalTime()
$process = Start-Process -FilePath $keepassExe -ArgumentList '--plgx-create', ('"' + $plgxSourceDir + '"') -Wait -PassThru -NoNewWindow
if ($process.ExitCode -ne 0) { throw "KeePass PLGX creation failed: $($process.ExitCode)" }
if (-not (Test-Path -LiteralPath $keepassGeneratedPath)) { throw 'Expected PLGX was not generated.' }

$plgxInfo = Get-Item -LiteralPath $keepassGeneratedPath
if ($plgxInfo.LastWriteTimeUtc -lt $beforeUtc.AddSeconds(-2)) { throw 'Generated PLGX timestamp is stale.' }
Copy-Item -LiteralPath $keepassGeneratedPath -Destination $generatedPlgx -Force
Get-FileHash -Algorithm SHA256 -LiteralPath $generatedPlgx
Get-Item -LiteralPath $generatedPlgx | Select-Object FullName,Length,CreationTimeUtc,LastWriteTimeUtc
```

- [ ] **Step 4: Prepare a disposable visible KeePass instance**

```powershell
$portableRoot = Join-Path $env:TEMP ("KeeFetch-v13-ui-portable-" + $uiSourceShort)
if (Test-Path -LiteralPath $portableRoot) {
    $resolvedPortable = (Resolve-Path -LiteralPath $portableRoot).Path
    if (-not $resolvedPortable.StartsWith((Resolve-Path -LiteralPath $env:TEMP).Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary portable root: $resolvedPortable"
    }
    Remove-Item -LiteralPath $resolvedPortable -Recurse -Force
}
New-Item -ItemType Directory -Path $portableRoot | Out-Null
Copy-Item -Path (Join-Path $keepassDir '*') -Destination $portableRoot -Recurse
$portablePlugins = Join-Path $portableRoot 'Plugins'
New-Item -ItemType Directory -Path $portablePlugins -Force | Out-Null
Copy-Item -LiteralPath $generatedPlgx -Destination (Join-Path $portablePlugins 'KeeFetch.plgx')
```

Copy the synthetic regression database to a disposable path; never open the tracked fixture directly:

```powershell
$regressionSource = 'KeeFetch.Tests\Fixtures\Regression\KeeFetch-Test-Database.kdbx'
$regressionCopy = Join-Path $portableRoot 'KeeFetch-v13-ui-regression.kdbx'
Copy-Item -LiteralPath $regressionSource -Destination $regressionCopy
```

- [ ] **Step 5: Prove plugin load before testing flows**

Launch the portable instance visibly with local configuration:

```powershell
$portableKeePass = Join-Path $portableRoot 'KeePass.exe'
Start-Process -FilePath $portableKeePass -ArgumentList '-cfg-local', ('"' + $regressionCopy + '"')
```

In KeePass:

1. Confirm the process opens without a PLGX compile error dialog.
2. Confirm `Tools -> KeeFetch` exists.
3. Open `Tools -> KeeFetch -> Settings` and traverse all four tabs.
4. Record the KeePass version, plugin artifact SHA-256, source SHA, and UTC timestamp in `docs/validation/v1.3-ui-matrix.md`.
5. If KeePass creates a compiled plugin cache or diagnostic entry, record its exact evidence path without committing machine-specific files.

- [ ] **Step 6: Execute real-host DPI and color checks**

Run every form at 100%, 125%, 150%, and 200% display scaling. Restart KeePass after changing scaling when Windows requires it. For each scaling level inspect:

- Settings Overview, Downloads, Providers, and Advanced tabs.
- First Run default and privacy choices.
- Active Progress state.
- Cancelling and Cancelled wording.
- Completion states: success, partial success, and retry disabled after retry.
- Minimum and manually enlarged window sizes.
- Long profile descriptions and long diagnostics paths.

Repeat all forms with Windows High Contrast enabled. Confirm text, focus, checked states, error states, and disabled controls remain distinguishable without relying only on color or icons.

For every row record `PASS` plus date, KeePass version, scaling/theme, and concise observation. A row remains pending until the real host was observed.

- [ ] **Step 7: Execute keyboard-only checks**

Without using the mouse, verify:

- `Tab` and `Shift+Tab` visit each enabled interactive control once in logical order.
- `Ctrl+Tab` or native tab-page keyboard navigation changes Settings pages.
- Access-key mnemonics focus or invoke their controls.
- `Space` toggles checkboxes and checked providers.
- Arrow keys move the expected selection.
- `Enter` invokes the documented default button.
- `Escape` cancels Settings and First Run without saving.
- Validation moves focus to the offending tab and control.
- Completion defaults to Close, not Retry.
- The second completion summary after a retry does not offer another retry.

Record the exact traversal result in the UI matrix.

- [ ] **Step 8: Execute first-install and migration checks**

Use separate disposable local KeePass configurations for each case:

1. New install with no KeeFetch keys: First Run appears before the first download; cancellation writes nothing and aborts the run; confirmation persists the selected profile.
2. v1.2 Fast: migrates to stable ID `bulk-fast` and preserves provider toggles/order.
3. v1.2 Balanced: migrates to `everyday` and preserves custom values.
4. v1.2 Thorough: migrates to `max-coverage`, displayed as `Precise`, and preserves custom values.
5. v1.2 Custom: migrates to `custom` and preserves provider toggles, normalized order, timeouts, synthetic option, and TLS option.
6. Unknown legacy value: resolves to `custom` without silently discarding stored provider choices.
7. Run each migration twice and confirm the second read is idempotent.

Do not use real credentials or a real vault. Record the initial keys, resulting stable ID, preserved values, and second-run result in the matrix.

- [ ] **Step 9: Execute progress, completion, cancellation, and retry checks**

Use disposable copies of the synthetic database:

1. Full success: verify counts, elapsed time, selected profile, diagnostics path, and Close behavior.
2. Mixed result: include update, skipped, not-found, and recoverable failure outcomes; verify totals equal processed entries and diagnostics actions work.
3. Cancellation: cancel while network work is active; verify Running -> Cancelling -> Cancelled wording and ensure cancelled entries are not counted as ordinary failures.
4. Long diagnostics path: place the portable instance under a deeply nested temporary directory; verify display ellipsis does not alter copied path text and Open Diagnostics reaches the exact file.
5. Retry eligible: verify only `NotFound` and recoverable provider/network failures are retried.
6. Retry exclusions: verify successful, skipped, invalid-input, and cancelled entries are absent from the retry set.
7. Retry bound: execute Retry Eligible once and confirm the second summary has Retry hidden or disabled.
8. Icon preservation: successful icons from the first run are not reapplied or duplicated during retry.

Record source database copy, selected profile, counts, diagnostic paths, and retry result. Do not commit downloaded icons or temporary databases.

- [ ] **Step 10: Resolve every manual failure**

For each failure:

1. Capture the exact reproduction, scale/theme, form state, and observed result.
2. Invoke `superpowers:systematic-debugging`.
3. Add a failing automated test when the behavior is testable.
4. Run the focused test and confirm failure.
5. Implement the smallest responsible fix in C# 5-compatible code.
6. Run the focused test and confirm pass.
7. Rebuild the PLGX from a clean tree.
8. Repeat the failed real-host row.
9. Rerun Task 1 in full.

If a failure cannot be fixed in release scope, create a GitHub issue only with owner authorization, link it from the matrix, document user impact and workaround, and obtain explicit owner acceptance before marking the row accepted.

- [ ] **Step 11: Finalize the UI matrix and state**

The document must contain:

- Source commit SHA.
- PLGX filename, size, UTC timestamp, and SHA-256.
- KeePass version and portable-instance path category without a user-specific absolute path.
- PASS or accepted linked issue for every row.
- Explicit visible-load proof for Settings, First Run, Progress, Completion, and Retry.
- Final automated command results and test count.

Update `.agent/STATE.md` to `REVIEW` only after every gate is green. Prepend one handoff line to `.agent/HANDOFF_LOG.md`.

- [ ] **Step 12: Commit UI validation closure**

```powershell
git add docs\validation\v1.3-ui-matrix.md .agent\STATE.md .agent\HANDOFF_LOG.md
git add KeeFetch.Tests .github\workflows\build.yml
git diff --cached --check
git diff --cached --stat
git commit -m "test: complete v1.3 guided UX host validation"
```

Stage production files only when Task 2 Step 10 produced an actual fix. Confirm no PLGX, database, screenshots, logs, `bin/`, `obj/`, or temporary paths are staged.

### Task 3: Review, CI, and merge the guided-native UX branch

**Files:**
- No planned new files.
- Modify review fixes only when findings are proven.

**Interfaces:**
- Consumes: completed UI validation commit and green local gates.
- Produces: one reviewed, CI-green PR merged into master.

- [ ] **Step 1: Invoke pre-PR review**

Invoke `superpowers:requesting-code-review`. Review the full diff from master:

```powershell
git fetch origin
git diff --stat origin/master...HEAD
git diff --check origin/master...HEAD
git log --oneline origin/master..HEAD
```

Review specifically:

- C# 5 compatibility.
- settings Save/Cancel atomicity.
- first-run no-write-before-confirm behavior.
- profile/privacy copy accuracy.
- outcome aggregation and retry identity deduplication.
- non-recursive retry bound.
- cancellation distinction.
- PLGX project and staging parity.
- accessibility names, tab order, DPI containment, and minimum-size copy.
- no stale `Thorough` display claim for `max-coverage`.

- [ ] **Step 2: Rerun the complete gate after review fixes**

Repeat Task 1 Steps 2–5. Rebuild and visibly load the PLGX again if any production source, resource, project file, or staging workflow changed.

- [ ] **Step 3: Push and create the PR**

Owner amendment (2026-08-28): a draft PR may be opened before the host-manual
rows are complete so review and CI can proceed. While any row remains pending,
the PR stays draft and `.agent/STATE.md` stays `WORKING`; this does not authorize
marking the PR ready, merging it, or beginning the next delivery branch.

```powershell
git push -u origin codex/v1-3-guided-native-ux
gh pr create --draft --repo tzii/KeeFetch --base master --head codex/v1-3-guided-native-ux --title "Complete v1.3 guided-native plugin UX" --body-file docs\validation\v1.3-ui-matrix.md
```

If a PR already exists, do not create a duplicate. Read it with `gh pr view`, push the verified commit, and update the description to summarize implementation, automated gates, manual matrix, PLGX identity, and known limitations.

- [ ] **Step 4: Wait for CI and inspect exact results**

```powershell
gh pr checks --repo tzii/KeeFetch --watch
gh pr view --repo tzii/KeeFetch --json number,state,isDraft,mergeable,mergeStateStatus,statusCheckRollup,reviews,url
```

Do not mark ready while any host-manual row or CI check is missing, pending,
cancelled, or failed.

- [ ] **Step 5: Stop for merge authorization**

Report PR URL, head SHA, test count, warning count, PLGX visible-load result, and manual matrix result. Merge only after explicit owner authorization. After merge, fetch and verify the merge commit is contained in `origin/master`.

### Task 4: Resolve provider-study scope and review-provenance deviations

**Files:**
- Create: `docs/benchmarks/v1.3-study-scope-decision.md`
- Modify: `docs/superpowers/specs/2026-08-11-keefetch-v1-3-polish-design.md`
- Modify: `docs/superpowers/plans/2026-08-11-keefetch-v1-3-02-profile-catalog-migration.md`
- Modify: `docs/benchmarks/v1.3-provider-study.md`
- Strict route: create `eng/benchmark/experiments/profile-candidates-v13-complete.json`
- Strict route: modify `eng/benchmark/test-benchmark-harness.ps1`, `eng/benchmark/select-profiles.ps1`, `FetchProfiles/FetchProfileCatalog.Generated.cs`, tests, and `site/data/profiles.json` only as required by new evidence.

**Interfaces:**
- Consumes: merged guided UX master and existing 18-candidate study.
- Produces: an explicit owner-approved study closure with no silent deviation from the release claims.

- [ ] **Step 1: Create a fresh study-closure branch from merged master**

```powershell
git fetch origin
$studyWorktree = 'C:\Users\simon\Documents\Projects\KeeFetch-v1-3-study-closure'
if (Test-Path -LiteralPath $studyWorktree) { throw "Study worktree target already exists: $studyWorktree" }
git worktree add -b codex/v1-3-study-closure $studyWorktree origin/master
```

Read `AGENTS.md`, `.agent/STATE.md`, the study report, the experiment JSON, the Plan 02 study requirements, and the v1.3 design spec.

- [ ] **Step 2: Prove the two deviations from repository evidence**

Record in `docs/benchmarks/v1.3-study-scope-decision.md`:

1. The final study has 18 candidates.
2. It contains full-chain-minus Twenty Icons, DuckDuckGo, Google, Favicone, and Icon Horse variants.
3. It does not contain `cand-full-minus-yandex-thorough-synth`, although Plan 02 requested the full chain without each third-party provider.
4. The census labels were produced by the named machine reviewer and owner-approved; they were not human labels.
5. The published 126 measured cells had `resumed_any=false`, so the parked resume-accounting defect did not affect v1.3 results.
6. The guided UX branch changes the `max-coverage` display name to `Precise` without changing its stable ID or selected policy.

Include commands and output summaries used to establish these facts.

- [ ] **Step 3: Stop for the study-closure decision**

Present two routes and recommend Route A for strict conformance:

- **Route A — strict rerun:** run a new complete 19-candidate study including full-minus-Yandex, review its entire cold-artifact census, rerun selection, and republish the generated catalog/report.
- **Route B — explicit waiver:** owner accepts that the omitted ablation was not evaluated, records why release claims remain appropriately limited, and prohibits claims that every single-provider ablation was measured.

Do not choose Route B without an explicit owner statement. Do not launch Route A before recording the exact experiment and binary fingerprints.

- [ ] **Step 4A: Implement Route A experiment definition**

Create `eng/benchmark/experiments/profile-candidates-v13-complete.json` by copying the finalized experiment and changing:

```json
{
  "experiment_id": "profile-candidates-v13-complete",
  "schedule_seed": 20260827,
  "output_root": "eng/benchmark-runs/profile-candidates-v13-complete"
}
```

Retain the 300-fixture corpus, 3 repetitions, concurrency 8, and cold/warm modes. Add this exact candidate to both `profiles` and `candidates`:

```json
{
  "id": "cand-full-minus-yandex-thorough-synth",
  "providerIds": [
    "direct-site",
    "twenty-icons",
    "duckduckgo",
    "google",
    "favicone",
    "icon-horse"
  ],
  "primaryTimeout": 10000,
  "fallbackTimeout": 5000,
  "cumulativeTimeout": 45000,
  "allowSynthetic": true,
  "stopAfterStrongResolved": false,
  "allowAndroidStoreLookup": true,
  "notes": "full chain minus Yandex. Thorough budget."
}
```

The `profiles` and `candidates` ID sets must be equal, ordered, unique, and contain 19 IDs.

- [ ] **Step 5A: Add failing experiment-contract tests**

Extend `eng/benchmark/test-benchmark-harness.ps1` with assertions that:

- The complete experiment has exactly 19 unique candidate IDs.
- `cand-full-minus-yandex-thorough-synth` resolves through the real catalog and execution-policy path.
- Its policy fingerprint differs from full-chain and every other minus-one candidate.
- Its provider list contains all configured third parties except Yandex.
- The experiment fingerprint changes when the candidate is removed.

Run the harness self-tests and verify the new assertion fails before adding the candidate, then passes after the definition is complete.

- [ ] **Step 6A: Freeze and record the strict-study launch gate**

Run the complete Release build, C# 5 build, MSTest suite, harness self-tests, profile export check, and `git diff --check`. Commit the experiment/test change before launch:

```powershell
git add eng\benchmark\experiments\profile-candidates-v13-complete.json eng\benchmark\test-benchmark-harness.ps1 docs\benchmarks\v1.3-study-scope-decision.md
git diff --cached --check
git commit -m "test: complete v1.3 provider ablation matrix"
```

Record commit SHA, binary SHA-256, experiment fingerprint, corpus fingerprint, harness fingerprint, schedule seed, KeePass version, OS, PowerShell, .NET SDK, and UTC launch time.

- [ ] **Step 7A: Run the complete strict study**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark-presets.ps1 -Experiment eng\benchmark\experiments\profile-candidates-v13-complete.json
```

Expected matrix: 19 candidates x 300 fixtures x 3 repetitions x 2 measured cache modes, plus required warm-up cells. Use the harness's checkpoint/resume behavior; never copy runs between experiment roots. If any cell is resumed, do not use its latency evidence until the parked resume accumulator is fixed and covered, because bulk-fast ranking uses active batch time.

- [ ] **Step 8A: Generate and validate the complete cold-artifact census**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark\prepare-review.ps1 -RunDir eng\benchmark-runs\profile-candidates-v13-complete
```

Stop at the review gate. The owner must explicitly choose and authorize human review or a named machine-review process. Record reviewer identity, process version/model, prompts, timestamps, pilot/arbitration method, and spot-check evidence. Every unique cold `(fixture_id, artifact_hash)` unit must receive an allowed label and provenance; no sampled or implicitly-correct units are permitted.

Validate against the exact run directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark\prepare-review.ps1 -RunDir eng\benchmark-runs\profile-candidates-v13-complete -Validate
```

- [ ] **Step 9A: Select and publish strict-study winners**

Run selection without repository mutation first, inspect all output, then run `-Publish` only when provenance and ambiguity replay pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark\select-profiles.ps1 -RunDir eng\benchmark-runs\profile-candidates-v13-complete
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark\select-profiles.ps1 -RunDir eng\benchmark-runs\profile-candidates-v13-complete -Publish
```

Verify:

- All four winner rules are stable under ambiguity-as-failure and ambiguity-as-usable.
- Privacy has zero observed third-party disclosures and no Android-store lookup.
- Generated C# and report contain no local paths.
- Review provenance is described exactly.
- If a winner changes, every affected test, profile description, website export, UI label, and privacy statement is regenerated or updated from policy facts.

Then build, test, export, and diff-check in full.

- [ ] **Step 4B: Implement Route B explicit waiver**

When the owner explicitly chooses Route B, write `docs/benchmarks/v1.3-study-scope-decision.md` with:

- Owner decision date and exact authorization statement.
- Missing candidate ID and exact omitted provider chain.
- Reason the experiment is accepted without that ablation.
- Risk: an unmeasured minus-Yandex chain might have ranked differently for a non-privacy role.
- Claim boundary: v1.3 claims only compare the 18 measured candidates and must not state that every single-provider ablation was evaluated.
- Confirmation that no profile policy is changed by the waiver.
- Confirmation that the parked harness issues did not affect the zero-resume study.

Update the plan, spec, and study Limitations section to match. Replace human-review language with the exact approved machine-census provenance. Do not rewrite historical handoff text; add dated amendments where needed.

- [ ] **Step 10: Review and merge study closure**

For either route, run all deterministic gates, request code/evidence review, push `codex/v1-3-study-closure`, create a focused PR, wait for CI, and stop for merge authorization. After merge, verify `origin/master` contains the closure commit before website work begins.

### Task 5: Build the shared website foundation and seven-page information architecture

**Files:**
- Modify: `site/index.html`
- Create: six required HTML pages under `site/`
- Create: `site/assets/css/site.css`
- Create: `site/assets/js/site.js`
- Create: `site/data/release.json`
- Modify: `README.md`
- Modify: `CONTRIBUTING.md`
- Modify: `.agent/STATE.md` and `.agent/HANDOFF_LOG.md`

**Interfaces:**
- Consumes: merged final profile catalog, `site/data/profiles.json`, final plugin UI names and flows.
- Produces: dependency-free static pages with one navigation contract, one release-data source, shared styling, progressive JavaScript, and complete no-JavaScript content.

- [ ] **Step 1: Create a website branch from current master**

```powershell
git fetch origin
$siteWorktree = 'C:\Users\simon\Documents\Projects\KeeFetch-v1-3-website'
if (Test-Path -LiteralPath $siteWorktree) { throw "Website worktree target already exists: $siteWorktree" }
git worktree add -b codex/v1-3-website-completion $siteWorktree origin/master
```

Read `AGENTS.md`, Plan 04, the design spec's website sections, generated profile data, final provider report, README, CONTRIBUTING, SECURITY, build workflow, and Pages workflow.

- [ ] **Step 2: Record baseline screenshots and content inventory outside Git**

Serve `site/` locally and capture the current home page at 390x844, 768x1024, and 1440x1000 into a temporary evidence directory. Record existing headings, release links, profile/privacy claims, assets, and navigation. Do not commit baseline screenshots.

- [ ] **Step 3: Define one navigation contract**

Every page must include, in this order:

1. Skip link to `#main-content`.
2. Site identity linking to `index.html`.
3. Primary links: Home, Getting Started, Profiles, Privacy, Troubleshooting, Benchmarks, Contributing.
4. GitHub repository link.
5. Main element with `id="main-content"`.
6. Footer links to Releases, Issues, Security, License, and source.

The active page uses `aria-current="page"`. Mobile navigation remains keyboard operable and cannot hide links when JavaScript is disabled.

- [ ] **Step 4: Create shared CSS**

Move reusable inline styles into `site/assets/css/site.css`. The stylesheet must provide:

- System-first font stack with readable line lengths.
- Visible focus indicators at least 2 CSS pixels thick.
- Responsive layouts for 390, 768, and 1440 pixel viewports.
- System-color-compatible controls and links.
- `prefers-reduced-motion: reduce` handling that removes nonessential transitions and animations.
- Accessible tables with horizontal overflow containers on narrow screens.
- Skip-link styling.
- Navigation open/closed states that remain usable without JavaScript.
- Screenshot/media sizing without layout shift where dimensions are known.
- No gradients or motion that reduce text contrast or obscure content.

Do not copy either draft site PR wholesale. The existing master site is the default visual baseline; isolated style ideas from PR #5 or #6 require owner approval and factual revalidation.

- [ ] **Step 5: Create progressive JavaScript**

`site/assets/js/site.js` may:

- Enhance mobile navigation while preserving static links.
- Load `data/profiles.json` and replace marked profile table bodies.
- Load `data/release.json` and update elements carrying explicit `data-release-*` attributes.
- Add a copied-hash acknowledgement.

It must not:

- Be required for core content.
- contact analytics or third-party APIs.
- fetch cross-origin resources.
- inject unsanitized HTML from JSON.
- alter documented profile/provider facts.

Use DOM text nodes and attribute assignment, not raw `innerHTML`, for data-derived values.

- [ ] **Step 6: Create release data**

Create `site/data/release.json` initially representing the currently published release until the release-candidate task updates it:

```json
{
  "schema": 1,
  "version": "1.2.0",
  "tag": "v1.2.0",
  "releaseUrl": "https://github.com/tzii/KeeFetch/releases/latest",
  "plgxUrl": "https://github.com/tzii/KeeFetch/releases/latest/download/KeeFetch.plgx",
  "dllUrl": "https://github.com/tzii/KeeFetch/releases/latest/download/KeeFetch.dll"
}
```

Use LF line endings, UTF-8 without BOM, deterministic two-space indentation, and one trailing newline.

- [ ] **Step 7: Rewrite the home page**

`site/index.html` must include:

- One-sentence value proposition for ordinary KeePass users.
- Current release and primary PLGX download.
- KeePass 2.x, Windows, and .NET Framework 4.8 compatibility.
- Key capabilities: direct-site discovery, evidence-backed profiles, bulk progress, diagnostics, retry, deduplication, Android URL handling.
- Concise generated profile comparison with link to Profiles.
- Exact privacy summary with link to Privacy.
- Getting Started and Troubleshooting calls to action.
- Trust links: source, changelog, benchmark evidence, security policy, release verification.

Remove stale v1.2 provider-chain marketing claims that conflict with generated profile data.

- [ ] **Step 8: Create Getting Started**

`site/getting-started.html` must cover:

- Requirements.
- PLGX and DLL installation paths.
- Upgrade from v1.2 without deleting settings.
- Uninstall.
- Rollback to v1.2, including restoring the older artifact and restarting KeePass.
- First-run profile/privacy choice.
- Single-entry, group, and whole-database operations.
- Progress, cancellation, completion, diagnostics, and one bounded retry.
- Safe recommendation to back up the database before bulk changes.
- Links to Profiles, Privacy, Troubleshooting, and Releases.

- [ ] **Step 9: Create Profiles and Providers**

`site/profiles.html` must include:

- A statically generated fallback table for every visible managed profile.
- Stable display names Fast, Balanced, Privacy, and Precise, plus a Custom explanation.
- Intended use, ordered providers, relative behavior, cumulative budget, synthetic fallback, early-stop behavior, Android-store behavior, and privacy exposure.
- Provider role table for Direct Site, Twenty Icons, DuckDuckGo, Google, Yandex, Favicone, and Icon Horse.
- Explicit distinction between configured chain and observed provider calls.
- Link to the versioned evidence report and benchmark page.
- Clear statement that timings are measurements, not guarantees.

All managed-profile facts must be generated from `site/data/profiles.json`.

- [ ] **Step 10: Create Privacy and Security**

`site/privacy.html` must state exactly:

- KeeFetch can contact the entry's site directly.
- Selected third-party providers can receive domain names when their profile/policy calls them.
- Privacy uses Direct Site only, disables synthetic fallbacks, and disables Android-store lookup.
- KeeFetch does not transmit usernames, passwords, notes, complete entries, master keys, or complete database contents.
- KeeFetch has no telemetry or analytics.
- Diagnostics and logs are local unless the user chooses to share them.
- The self-signed-certificate option changes TLS certificate validation and carries risk.
- Android Play lookup behavior depends on the selected profile policy.
- Provider behavior and availability can change outside KeeFetch.
- Security reports use GitHub private vulnerability reporting.

Avoid absolute privacy language that contradicts third-party profile behavior.

- [ ] **Step 11: Create Troubleshooting**

`site/troubleshooting.html` must include diagnosis and resolution for:

- Plugin menu absent.
- PLGX compile/load error.
- Wrong plugin directory.
- DLL vs PLGX choice.
- No icon found.
- Generic or wrong-brand icon.
- Slow batch.
- Proxy, TLS, and certificate failures.
- Android app URL limitations.
- Cancellation behavior.
- Diagnostics log/CSV location and opening failures.
- Retry eligibility.
- Clean reproduction with portable KeePass.
- Bug-report checklist: version, KeePass version, profile, sanitized URL/domain, diagnostics, steps, expected/actual behavior, and confirmation that credentials were removed.

- [ ] **Step 12: Create Benchmarks**

`site/benchmarks.html` must include:

- 300-fixture public corpus description.
- Candidate count and selected study-closure route.
- Cold/warm modes and three repetitions.
- Machine availability vs reviewed usability distinction.
- Exact machine-review provenance and owner approval.
- Ambiguity replay.
- Provider-call/disclosure evidence.
- Live-network and reviewer-judgment limitations.
- The omitted-ablation waiver if Route B was selected, or the new 19-candidate identity if Route A was selected.
- Reproduction commands that match the committed experiment/report.
- Links to the evidence report and harness follow-up document.

- [ ] **Step 13: Create Contributing**

`site/contributing.html` and root `CONTRIBUTING.md` must agree on:

- .NET 8 SDK, .NET Framework 4.8 targeting pack, and KeePass 2.60 prerequisites.
- Restore, Release build, C# 5 build, full tests, harness tests, profile export check, site verifier, and Edge smoke commands.
- Current directory architecture including `FetchProfiles`, `IconSelection`, `IconProviders`, `Batch`, and `Settings`.
- SDK project vs legacy PLGX project.
- Provider contribution contract and required deterministic tests.
- Benchmark evidence rules and prohibition on committing raw runs.
- Website no-dependency/offline-CI rules.
- Pull request and security-reporting links.

- [ ] **Step 14: Validate manually without JavaScript**

Disable JavaScript in the browser and open all seven pages. Confirm navigation, profile table, installation, privacy, troubleshooting, benchmark limitations, and release links remain present and readable.

- [ ] **Step 15: Commit the website foundation**

```powershell
git add site\index.html site\getting-started.html site\profiles.html site\privacy.html site\troubleshooting.html site\benchmarks.html site\contributing.html site\assets\css\site.css site\assets\js\site.js site\data\release.json README.md CONTRIBUTING.md .agent\STATE.md .agent\HANDOFF_LOG.md
git diff --cached --check
git commit -m "docs: build v1.3 documentation site foundation"
```

### Task 6: Generate profile fallbacks and enforce cross-surface site data

**Files:**
- Create: `eng/sync-site-profiles.py`
- Modify: `site/index.html`
- Modify: `site/profiles.html`
- Test through script self-check and site verifier tests in Task 7.

**Interfaces:**
- Consumes: `site/data/profiles.json` schema 2 and `site/data/release.json` schema 1.
- Produces: deterministic HTML between explicit generated markers and a zero-diff `--check` gate.

- [ ] **Step 1: Define generated markers**

Use these exact markers in both pages:

```html
<!-- BEGIN GENERATED PROFILE TABLE -->
<div class="profile-table-wrap" data-profile-fallback>
  <table class="profile-table">
    <thead>
      <tr><th>Profile</th><th>Best for</th><th>Providers</th><th>Behavior</th><th>Privacy</th></tr>
    </thead>
    <tbody data-profile-table-body></tbody>
  </table>
</div>
<!-- END GENERATED PROFILE TABLE -->
```

The generator replaces the complete region, including populated rows. JavaScript may enhance the body but is not responsible for initial rows.

- [ ] **Step 2: Implement deterministic generator interfaces**

`eng/sync-site-profiles.py` must support:

```text
python eng/sync-site-profiles.py
python eng/sync-site-profiles.py --check
python eng/sync-site-profiles.py --profiles site/data/profiles.json --release site/data/release.json --site-root site
```

Implementation requirements:

- Python standard library only.
- Validate schemas, required keys, types, unique IDs, visible profiles, provider IDs, positive budgets, and privacy invariants.
- Escape all text with `html.escape`.
- Render providers in stored order.
- Derive privacy text from policy facts, not profile names.
- Derive behavior text from cumulative timeout, early-stop, synthetic, and Android-store flags.
- Use LF, UTF-8 without BOM, and one trailing newline.
- Fail if either marker is missing, duplicated, reversed, or nested.
- `--check` performs no writes and emits a useful line-oriented diff on drift.
- Running write mode twice produces byte-identical files.

- [ ] **Step 3: Run red/green drift proof**

1. Deliberately leave the marker body stale.
2. Run `python eng/sync-site-profiles.py --check` and verify nonzero exit.
3. Run `python eng/sync-site-profiles.py`.
4. Run `python eng/sync-site-profiles.py --check` and verify zero exit.
5. Hash both HTML files, rerun write mode, and confirm hashes remain unchanged.

- [ ] **Step 4: Commit generated fallback tooling**

```powershell
git add eng\sync-site-profiles.py site\index.html site\profiles.html
git diff --cached --check
git commit -m "build: generate website profile fallbacks"
```

### Task 7: Implement the offline semantic, link, release, and profile verifier

**Files:**
- Create: `eng/verify-site.py`
- Create: `eng/test-verify-site.py`

**Interfaces:**
- Consumes: site root, seven HTML pages, local assets, release JSON, and profile JSON.
- Produces: deterministic error list and exit 0 only for a complete, internally consistent site.

- [ ] **Step 1: Write verifier unit tests first**

Use `tempfile.TemporaryDirectory`, `unittest`, `pathlib`, `json`, and `importlib.util` to load the hyphenated verifier path. Include one passing fixture and separate failing tests for:

- Missing required page.
- Duplicate HTML ID.
- Broken relative link.
- Broken fragment.
- Missing local asset.
- Image without meaningful `alt`.
- Missing or duplicate `<title>`.
- Missing description, canonical URL, viewport, `lang`, H1, main landmark, or skip link.
- Missing primary navigation item.
- Wrong `aria-current` page.
- HTTP external link.
- Release URL not using HTTPS.
- Release JSON schema/type error.
- Stale hard-coded version outside an explicitly historical section.
- Profile fallback missing an exported visible profile.
- Profile provider order or privacy statement differing from JSON.
- JavaScript required to expose a required heading.

Run:

```powershell
python eng\test-verify-site.py -v
```

Expected before implementation: import failure because `eng/verify-site.py` does not exist.

- [ ] **Step 2: Implement the parser and result model**

Use `html.parser.HTMLParser` to collect:

- page title, metadata, canonical link, `html[lang]`, headings, IDs, links, images, scripts, stylesheets, landmarks, skip links, navigation labels, and `aria-current`.
- visible text needed for required-content checks.

Expose these Python interfaces:

```python
def verify_site(site_root: pathlib.Path) -> list[str]:
    """Return sorted human-readable errors; return an empty list for success."""

def main(argv: list[str] | None = None) -> int:
    """Print every error and return 1 on failure, 0 on success."""
```

The script may run under the repository's available Python version. If Python earlier than 3.9 is supported by CI, replace built-in generic annotations with `typing.List` and `typing.Optional` consistently.

- [ ] **Step 3: Enforce exact required pages and navigation**

Required files:

```python
REQUIRED_PAGES = (
    "index.html",
    "getting-started.html",
    "profiles.html",
    "privacy.html",
    "troubleshooting.html",
    "benchmarks.html",
    "contributing.html",
)
```

Required primary labels: Home, Getting Started, Profiles, Privacy, Troubleshooting, Benchmarks, Contributing.

Canonical URLs use `https://tzii.github.io/KeeFetch/`, with `index.html` represented by the site root and other pages by their filename.

- [ ] **Step 4: Enforce links and assets**

- Resolve relative paths against the current page.
- Ignore `mailto:` only when syntactically valid.
- Require HTTPS for external links except explicit local test URLs supplied by unit fixtures.
- Verify fragments against target-page IDs.
- Reject path traversal outside `site/`.
- Verify linked local CSS, JavaScript, JSON, image, GIF, and icon files exist.
- Require non-empty meaningful alt text for content images; empty alt is allowed only for explicitly decorative images.

- [ ] **Step 5: Enforce profile and release consistency**

- Parse `site/data/profiles.json` and `site/data/release.json`.
- Require every visible profile in both fallback tables in source order.
- Require display name, intended use, provider display sequence, synthetic behavior, early-stop behavior, Android-store behavior, and privacy behavior to match generated data.
- Require release version/tag/download URLs shown on Home and Getting Started to match release JSON.
- Reject `v1.2` profile claims outside elements marked `data-historical-version="1.2"` after the final version task changes release JSON to 1.3.0.
- Require benchmark page to contain `machine review` and reject language claiming the v1.3 census was human-reviewed.

- [ ] **Step 6: Run unit tests and real-site verification**

```powershell
python eng\test-verify-site.py -v
if ($LASTEXITCODE -ne 0) { throw 'Site verifier unit tests failed.' }

python eng\verify-site.py site
if ($LASTEXITCODE -ne 0) { throw 'Real site verification failed.' }
```

- [ ] **Step 7: Commit the verifier**

```powershell
git add eng\verify-site.py eng\test-verify-site.py site
git diff --cached --check
git commit -m "test: verify website semantics and data consistency"
```

### Task 8: Add deterministic Edge viewport smoke rendering and CI gates

**Files:**
- Create: `eng/smoke-site.ps1`
- Modify: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: complete verified static site, local Python, and system Edge.
- Produces: 21 non-empty page/viewport screenshots in a temporary directory and CI failure on any load/render prerequisite error.

- [ ] **Step 1: Implement strict parameters and browser discovery**

`eng/smoke-site.ps1` accepts:

```powershell
param(
    [string]$SiteRoot = 'site',
    [string]$OutputDirectory = '',
    [int]$Port = 0
)
```

Requirements:

- Resolve `SiteRoot` and require all seven pages.
- Discover Edge at the documented Program Files paths.
- Do not download a browser.
- Choose a free loopback port when `Port` is zero.
- Default output to a unique `$env:TEMP` directory.
- Use a `try/finally` block to stop the local server.
- Start background helper processes with `-WindowStyle Hidden`.
- Fail with explicit prerequisite messages.

- [ ] **Step 2: Serve and render the exact matrix**

Start `python -m http.server` bound to `127.0.0.1`. Render every required page at:

- `390,844`
- `768,1024`
- `1440,1000`

Use Edge arguments that include headless mode, window size, screenshot output, disabled first-run UI, and a unique temporary user-data directory. Require each Edge process exit code to be zero and each PNG to exceed 1,000 bytes.

- [ ] **Step 3: Detect server and page failures**

Before rendering, poll the local home URL with `Invoke-WebRequest` for a bounded period. After rendering, require the expected 21 filenames and print a concise summary. Preserve output on failure for diagnosis; remove only temporary browser profiles created by the script.

- [ ] **Step 4: Run locally**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\smoke-site.ps1
if ($LASTEXITCODE -ne 0) { throw 'Edge site smoke failed.' }
```

Open at least the home, profiles, privacy, and troubleshooting screenshots from each viewport and inspect text clipping, navigation, tables, and media placement.

- [ ] **Step 5: Add offline CI steps**

After the profile export check in `.github/workflows/build.yml`, add:

```yaml
      - name: Check generated website profile fallbacks
        shell: pwsh
        run: python eng/sync-site-profiles.py --check

      - name: Test website verifier
        shell: pwsh
        run: python eng/test-verify-site.py -v

      - name: Verify static website
        shell: pwsh
        run: python eng/verify-site.py site

      - name: Smoke website viewports in Edge
        shell: pwsh
        run: powershell -NoProfile -ExecutionPolicy Bypass -File eng/smoke-site.ps1
```

No step may install site-specific dependencies or contact external sites.

- [ ] **Step 6: Commit smoke and CI**

```powershell
git add eng\smoke-site.ps1 .github\workflows\build.yml
git diff --cached --check
git commit -m "ci: validate v1.3 documentation site offline"
```

### Task 9: Capture final product media and complete the website matrix

**Files:**
- Create: four PNG files under `site/assets/media/`
- Optionally replace: `docs/usage-single.gif`, `docs/usage-group.gif`
- Modify: relevant `site/*.html`
- Create: `docs/validation/v1.3-website-matrix.md`
- Modify: `.agent/STATE.md` and `.agent/HANDOFF_LOG.md`

**Interfaces:**
- Consumes: merged/final guided UX PLGX and complete site.
- Produces: truthful final media and complete website evidence.

- [ ] **Step 1: Prepare sanitized capture data**

Use a disposable KeePass 2.60 instance with the synthetic regression database. Replace any visible machine-specific paths, usernames, or host details by configuring the temporary environment before capture; do not retouch the resulting product UI to hide defects.

- [ ] **Step 2: Capture exact PNG states**

Capture at native 100% scaling with normal Windows colors:

- `settings-overview.png`: Overview tab with recommended profile visible.
- `settings-providers.png`: Providers tab showing ordering, enabled state, and privacy explanation.
- `first-run.png`: initial profile/privacy choice before confirmation.
- `completion-summary.png`: mixed-result summary with diagnostics and retry eligibility visible.

Crop only non-product desktop background. Preserve complete dialog borders, title, controls, focus state where relevant, and readable text. Use PNG without lossy recompression.

- [ ] **Step 3: Capture usage recordings when they improve accuracy**

Replace `docs/usage-single.gif` and `docs/usage-group.gif` only with recordings from the final UX that:

- contain synthetic entries only.
- show the current menu labels and completion behavior.
- avoid flashing sensitive local paths.
- remain understandable without audio.
- use a reasonable frame rate and dimensions.

If current GIFs are retained, document why they remain factually accurate.

- [ ] **Step 4: Integrate media accessibly**

Add width/height attributes where known, descriptive alt text, captions that state the UI state, and links to nearby textual instructions. No information may exist only in an image.

- [ ] **Step 5: Execute the full website manual matrix**

Record PASS or linked accepted issue for:

- Seven pages at all three required viewports.
- Keyboard-only navigation and visible focus.
- Skip links.
- JavaScript enabled.
- JavaScript disabled.
- Reduced motion.
- Windows High Contrast or forced-colors review.
- Screen-reader-oriented heading/landmark order inspection.
- Table readability and horizontal scrolling on phone width.
- All internal links and fragments.
- Primary PLGX/DLL/release links.
- Profile and privacy agreement with generated data.
- Final screenshots and recordings.
- GitHub Pages static-tree deployment behavior.

- [ ] **Step 6: Run the complete site gate**

```powershell
dotnet build KeeFetch.csproj --configuration Release --no-restore -warnaserror
powershell -NoProfile -ExecutionPolicy Bypass -File eng\export-profile-data.ps1 -Check
python eng\sync-site-profiles.py --check
python eng\test-verify-site.py -v
python eng\verify-site.py site
powershell -NoProfile -ExecutionPolicy Bypass -File eng\smoke-site.ps1
git diff --check
```

- [ ] **Step 7: Commit media and evidence**

```powershell
git add site docs\validation\v1.3-website-matrix.md docs\usage-single.gif docs\usage-group.gif .agent\STATE.md .agent\HANDOFF_LOG.md
git diff --cached --check
git commit -m "docs: complete v1.3 website media and validation"
```

### Task 10: Review, CI, Pages preview, and merge the website branch

**Files:**
- Modify only proven review fixes.

**Interfaces:**
- Consumes: complete website branch and evidence.
- Produces: CI-green website PR merged into master and verified Pages deployment.

- [ ] **Step 1: Request full website review**

Review content accuracy, privacy claims, profile generation, no-JavaScript fallback, semantic parser coverage, Edge cleanup/error handling, CSS responsiveness, reduced motion, accessibility, release links, and absence of external runtime dependencies.

- [ ] **Step 2: Run complete repository gates**

Run Release solution build, explicit C# 5 build, full MSTest, harness self-tests, corpus validation, profile export check, profile fallback check, verifier unit tests, real-site verifier, Edge smoke, and `git diff --check`.

- [ ] **Step 3: Push and create a focused PR**

```powershell
git push -u origin codex/v1-3-website-completion
gh pr create --repo tzii/KeeFetch --base master --head codex/v1-3-website-completion --title "Complete the KeeFetch v1.3 documentation site" --body-file docs\validation\v1.3-website-matrix.md
gh pr checks --repo tzii/KeeFetch --watch
```

- [ ] **Step 4: Stop for merge authorization**

Report the PR, site gate results, screenshot matrix path, and Pages-impact summary. Merge only after authorization.

- [ ] **Step 5: Verify Pages after merge**

After authorized merge:

```powershell
gh run list --repo tzii/KeeFetch --branch master --limit 10 --json databaseId,workflowName,status,conclusion,headSha,url
```

Wait for the Pages run for the merge commit. Open every live page, confirm static assets and navigation, and record deployment URL/time in the website matrix or release-validation document. The site still advertises v1.2.0 until the release candidate updates release data.

### Task 11: Freeze v1.3.0 metadata and add cross-surface version tests

**Files:**
- Create: `KeeFetch.Tests/VersionConsistencyTests.cs`
- Modify: `version.txt`
- Modify: `Properties/AssemblyInfo.cs`
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Modify: `site/data/release.json`
- Modify: generated site fallbacks.
- Create: `docs/releases/v1.3.0.md`
- Create: `docs/validation/v1.3-release-validation.md`
- Modify: `.github/workflows/build.yml`
- Modify: `.agent/STATE.md` and `.agent/HANDOFF_LOG.md`

**Interfaces:**
- Consumes: merged plugin, study closure, website, and final profile data.
- Produces: one release-source commit with consistent 1.3.0 metadata and complete release text, before artifact hashes are known.

- [ ] **Step 1: Create the release-candidate branch from current master**

```powershell
git fetch origin
$releaseWorktree = 'C:\Users\simon\Documents\Projects\KeeFetch-v1-3-release'
if (Test-Path -LiteralPath $releaseWorktree) { throw "Release worktree target already exists: $releaseWorktree" }
git worktree add -b codex/v1-3-release-candidate $releaseWorktree origin/master
```

Verify no v1.3.0 tag/release and no newer open release PR.

- [ ] **Step 2: Write failing version-consistency tests**

Create `KeeFetch.Tests/VersionConsistencyTests.cs` with tests that:

- Locate repository root four levels above test output, matching `ProfileExportTests`.
- Parse the non-header version line from `version.txt`.
- Read the plugin assembly version.
- Parse `site/data/release.json` with `JavaScriptSerializer`.
- Require assembly/version file `1.3.0.0`.
- Require release JSON version `1.3.0` and tag `v1.3.0`.
- Require changelog heading `## [1.3.0] - YYYY-MM-DD`, using the actual release-preparation date.
- Require README and Home to reference the generated release data path and contain no current-profile v1.2 claims outside explicitly historical sections.
- Require all four generated profile display names and evidence links to match the in-memory catalog.

Use named helper methods `RepoPath`, `ReadVersionFile`, and `LoadReleaseData` so failure messages identify the mismatched surface.

Use this complete test shape, adjusting only namespaces if the production branch has deliberately renamed them:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using KeeFetch.FetchProfiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeeFetch.Tests
{
    [TestClass]
    public class VersionConsistencyTests
    {
        private const string ProductVersion = "1.3.0";
        private const string FourPartVersion = "1.3.0.0";

        private static string RepoPath(params string[] parts)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            foreach (string part in parts) path = Path.Combine(path, part);
            return path;
        }

        private static string ReadVersionFile()
        {
            string path = RepoPath("version.txt");
            Assert.IsTrue(File.Exists(path), "version.txt is missing.");
            string[] values = File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith(":", StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(1, values.Length, "version.txt must contain exactly one product version line.");
            return values[0];
        }

        private static Dictionary<string, object> LoadReleaseData()
        {
            string path = RepoPath("site", "data", "release.json");
            Assert.IsTrue(File.Exists(path), "site/data/release.json is missing.");
            var serializer = new JavaScriptSerializer();
            return serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        }

        [TestMethod]
        public void VersionFileAndAssembly_AreV130()
        {
            Assert.AreEqual(FourPartVersion, ReadVersionFile(), "version.txt is not frozen at v1.3.0.");

            Version assemblyVersion = typeof(KeeFetchExt).Assembly.GetName().Version;
            Assert.AreEqual(FourPartVersion, assemblyVersion.ToString(), "AssemblyVersion does not match version.txt.");

            var fileVersion = typeof(KeeFetchExt).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyFileVersionAttribute), false)
                .Cast<System.Reflection.AssemblyFileVersionAttribute>()
                .Single();
            Assert.AreEqual(FourPartVersion, fileVersion.Version, "AssemblyFileVersion does not match version.txt.");
        }

        [TestMethod]
        public void ReleaseData_DeclaresV130AndStableAssetRoutes()
        {
            Dictionary<string, object> release = LoadReleaseData();
            Assert.AreEqual(1, Convert.ToInt32(release["schema"]), "Unexpected release-data schema.");
            Assert.AreEqual(ProductVersion, (string)release["version"], "Website release version is stale.");
            Assert.AreEqual("v" + ProductVersion, (string)release["tag"], "Website release tag is stale.");
            Assert.AreEqual(
                "https://github.com/tzii/KeeFetch/releases/latest/download/KeeFetch.plgx",
                (string)release["plgxUrl"],
                "PLGX download route is inconsistent.");
            Assert.AreEqual(
                "https://github.com/tzii/KeeFetch/releases/latest/download/KeeFetch.dll",
                (string)release["dllUrl"],
                "DLL download route is inconsistent.");
        }

        [TestMethod]
        public void CurrentReleaseDocumentation_DeclaresV130()
        {
            string changelog = File.ReadAllText(RepoPath("CHANGELOG.md"));
            string readme = File.ReadAllText(RepoPath("README.md"));
            string home = File.ReadAllText(RepoPath("site", "index.html"));
            string releaseNotes = File.ReadAllText(RepoPath("docs", "releases", "v1.3.0.md"));

            Assert.IsTrue(
                Regex.IsMatch(changelog, @"(?m)^## \[1\.3\.0\] - \d{4}-\d{2}-\d{2}$"),
                "CHANGELOG.md has no dated v1.3.0 section.");
            StringAssert.Contains(readme, "1.3.0", "README.md does not identify the current release.");
            StringAssert.Contains(home, "v1.3.0", "Home-page no-JavaScript fallback is stale.");
            StringAssert.Contains(releaseNotes, "KeeFetch v1.3.0", "Release-note title is stale.");
        }

        [TestMethod]
        public void ExportedVisibleProfileNames_MatchCatalog()
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(RepoPath("site", "data", "profiles.json")));
            var exported = (System.Collections.ArrayList)root["profiles"];
            string[] exportedNames = exported.Cast<Dictionary<string, object>>()
                .Select(profile => (string)profile["displayName"])
                .ToArray();
            string[] catalogNames = FetchProfileCatalog.ManagedProfiles
                .Where(profile => profile.IsVisible)
                .Select(profile => profile.DisplayName)
                .ToArray();

            CollectionAssert.AreEqual(catalogNames, exportedNames, "Website profile display names drifted from the catalog.");
            CollectionAssert.AreEqual(
                new[] { "Fast", "Balanced", "Privacy", "Precise" },
                exportedNames,
                "Unexpected visible v1.3 profile display names.");
        }
    }
}
```

Run:

```powershell
dotnet test KeeFetch.Tests\KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~VersionConsistencyTests
```

Expected: FAIL because metadata remains 1.2.0.

- [ ] **Step 3: Update version metadata**

Set:

```text
version.txt product line: 1.3.0.0
AssemblyVersion: 1.3.0.0
AssemblyFileVersion: 1.3.0.0
release.json version: 1.3.0
release.json tag: v1.3.0
```

Keep stable latest-release URLs unless the release strategy explicitly uses versioned URLs. Regenerate website fallbacks and run their `--check` gate.

- [ ] **Step 4: Write the 1.3.0 changelog section**

Include concrete user-visible bullets under Added, Changed, and Fixed for:

- Evidence-backed Fast, Balanced, Privacy, and Precise profiles.
- Stable profile IDs and idempotent v1.2 migration.
- Hard execution-policy deadlines and Android-store privacy control.
- Guided four-page settings with atomic Save/Cancel.
- Explicit first-run profile/privacy selection.
- Structured progress, cancellation, completion, diagnostics, and one bounded retry.
- Benchmark/corpus/harness coverage and machine-review provenance.
- Seven-page documentation site and offline verification.
- PLGX staging/source parity and C# 5 compatibility coverage.

Do not claim human review, guaranteed timings, universal icon correctness, or zero network disclosure for non-Privacy profiles.

- [ ] **Step 5: Update README release text**

README must accurately describe:

- Current four managed display names and Custom.
- Default profile and third-party domain-sharing behavior.
- Privacy profile behavior.
- First Run, Settings, Completion, diagnostics, and retry.
- KeePass/.NET requirements.
- PLGX and DLL installation.
- Update, uninstall, rollback, and verification links.
- Final documentation pages.
- Machine-reviewed benchmark evidence and limitations.

Remove stale fixed provider-chain claims that imply one chain applies to every profile.

- [ ] **Step 6: Write complete release notes before hashes**

Create `docs/releases/v1.3.0.md` with these complete sections:

1. Highlights.
2. Upgrade and migration behavior.
3. Profiles and intended use.
4. Privacy and network disclosure.
5. Guided settings, first run, progress, completion, diagnostics, and retry.
6. Installation and update.
7. Uninstall and rollback.
8. Compatibility.
9. Benchmark methodology and exact machine-review provenance.
10. Known limitations, including any accepted study waiver and live-network variability.
11. Verification procedure explaining SHA-256 commands and that exact hashes appear in the final evidence commit and GitHub release.
12. Full changelog and contributor acknowledgements.

Do not insert fake hash values. Before hashes exist, phrase the Verification section as a procedure and state that the release remains a candidate; Task 14 replaces this candidate wording with exact values before publication.

- [ ] **Step 7: Create the release-validation document structure**

Create `docs/validation/v1.3-release-validation.md` with fully written sections and empty tables containing explicit `NOT RUN` status values rather than invented results:

- Release source identity.
- Environment.
- Deterministic command gate.
- Artifact manifest.
- PLGX source/manifest/load evidence.
- 71-entry regression matrix.
- Migration matrix.
- UI/accessibility matrix cross-reference and RC rerun.
- Website matrix cross-reference and RC rerun.
- Artifact hashes.
- Known limitations.
- Final release decision.

`NOT RUN` is a real status, not a success claim. Task 14 must remove every `NOT RUN` before release readiness.

- [ ] **Step 8: Prevent tag CI from substituting unverified artifacts**

Modify `.github/workflows/build.yml` so tag pushes still restore, build, test, validate, create PLGX, and upload Actions artifacts, but do not automatically create or mutate a GitHub release. Remove the `softprops/action-gh-release` step. Publication in Task 16 uses the exact artifact files validated and hashed in Tasks 13–14.

Add a workflow comment explaining that release publication is deliberately manual because PLGX generation metadata can change the file hash between builds.

- [ ] **Step 9: Run focused version tests**

```powershell
dotnet test KeeFetch.Tests\KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~VersionConsistencyTests
```

Expected: PASS after all cross-surfaces are updated.

- [ ] **Step 10: Commit the release source freeze**

Run the full deterministic gate in Task 12 first. Then:

```powershell
git add version.txt Properties\AssemblyInfo.cs CHANGELOG.md README.md site\data\release.json site KeeFetch.Tests\VersionConsistencyTests.cs docs\releases\v1.3.0.md docs\validation\v1.3-release-validation.md .github\workflows\build.yml .agent\STATE.md .agent\HANDOFF_LOG.md
git diff --cached --check
git diff --cached --stat
git commit -m "chore: freeze KeeFetch v1.3.0 release source"
```

Record this commit as `$releaseSourceSha`. All exact release artifacts are built from this commit.

### Task 12: Run the complete deterministic release-source gate

**Files:**
- Modify only to fix proven failures.
- Update command results in `docs/validation/v1.3-release-validation.md` after the source-freeze commit is created; those evidence edits belong to Task 14.

**Interfaces:**
- Consumes: version-frozen release source.
- Produces: complete fresh command logs with no failures, warnings, or tracked drift.

- [ ] **Step 1: Create a fresh temporary log directory and record environment**

Record source SHA, UTC time, Windows, PowerShell, .NET SDK, Python, KeePass, Edge, corpus fingerprint/count, profile-data SHA-256, release-data SHA-256, and Git version.

- [ ] **Step 2: Clean only build outputs through build tooling**

Do not use destructive Git reset/checkout commands. Run:

```powershell
dotnet clean KeeFetch.sln --configuration Release
dotnet restore KeeFetch.csproj
dotnet restore KeeFetch.Tests\KeeFetch.Tests.csproj
```

- [ ] **Step 3: Build and test**

```powershell
dotnet build KeeFetch.sln --configuration Release --no-restore -p:KeePassPath="$keepassDir" -warnaserror
dotnet build KeeFetch.csproj --configuration Release --no-restore -p:KeePassPath="$keepassDir" -p:LangVersion=5 -warnaserror
dotnet build KeeFetch.Tests\KeeFetch.Tests.csproj --configuration Release --no-restore -p:KeePassPath="$keepassDir" -warnaserror
Copy-Item -LiteralPath $keepassExe -Destination 'KeeFetch.Tests\bin\Release\net48\KeePass.exe' -Force
dotnet test KeeFetch.Tests\KeeFetch.Tests.csproj --configuration Release --no-build
```

Expected: zero warnings, zero errors, zero failed tests, zero skipped tests unless an explicitly documented existing test is designed to skip.

- [ ] **Step 4: Run benchmark, corpus, profile, website, and whitespace gates**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng\benchmark\test-benchmark-harness.ps1
powershell -NoProfile -ExecutionPolicy Bypass -Command "Import-Module '.\eng\benchmark\BenchmarkHarness.psm1' -Force; Test-KeeFetchCorpus -CsvPath '.\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\public-sites.csv' -VocabularyPath '.\KeeFetch.Tests\Fixtures\ProviderCorpus\v1\categories.json' | Out-Null"
powershell -NoProfile -ExecutionPolicy Bypass -File eng\export-profile-data.ps1 -Check
python eng\sync-site-profiles.py --check
python eng\test-verify-site.py -v
python eng\verify-site.py site
powershell -NoProfile -ExecutionPolicy Bypass -File eng\smoke-site.ps1
git diff --check
git status --short
```

Expected: every command exits 0 and tracked files remain unchanged.

- [ ] **Step 5: Inspect stale release claims**

```powershell
rg -n "v1\.2|1\.2\.0|Thorough|human.review|human-reviewed" README.md CHANGELOG.md site docs\releases\v1.3.0.md docs\validation\v1.3-release-validation.md
```

Classify every match as historical, current-but-wrong, or prohibited provenance language. Mark historical website text with the verifier's historical-version attribute. Remove or correct current-but-wrong and prohibited matches.

- [ ] **Step 6: Rerun the affected complete gate after any fix**

Any source, test, generated data, site, workflow, or release-text change invalidates the prior corresponding result. Commit the fix, update `$releaseSourceSha`, and rerun Tasks 12–14 from the new source commit.

### Task 13: Build exact release artifacts and execute release-artifact matrices

**Files:**
- Temporary release directory outside the repository.
- Modify later: `docs/validation/v1.3-release-validation.md`.

**Interfaces:**
- Consumes: clean release-source commit and exact KeePass installation.
- Produces: `KeeFetch.dll` and `KeeFetch.plgx` that pass manifest, load, regression, migration, UI, and website checks.

- [ ] **Step 1: Verify immutable source identity**

```powershell
$releaseSourceSha = (git rev-parse HEAD).Trim()
$releaseSourceShort = (git rev-parse --short=12 HEAD).Trim()
if (-not [string]::IsNullOrWhiteSpace((git status --porcelain | Out-String))) {
    throw 'Release source worktree must be clean.'
}
```

- [ ] **Step 2: Create an exact external release-candidate directory**

```powershell
$releaseCandidateDir = Join-Path $env:TEMP ("KeeFetch-v1.3.0-rc-" + $releaseSourceShort)
if (Test-Path -LiteralPath $releaseCandidateDir) {
    $resolvedRc = (Resolve-Path -LiteralPath $releaseCandidateDir).Path
    if (-not $resolvedRc.StartsWith((Resolve-Path -LiteralPath $env:TEMP).Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove non-temporary RC directory: $resolvedRc"
    }
    Remove-Item -LiteralPath $resolvedRc -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseCandidateDir | Out-Null
```

- [ ] **Step 3: Copy the exact Release DLL**

After Task 12's clean Release build:

```powershell
$releaseDll = 'bin\Release\net48\KeeFetch.dll'
if (-not (Test-Path -LiteralPath $releaseDll)) { throw 'Release DLL is missing.' }
Copy-Item -LiteralPath $releaseDll -Destination (Join-Path $releaseCandidateDir 'KeeFetch.dll')
```

- [ ] **Step 4: Recreate CI-equivalent PLGX staging**

Use the exact staging process from Task 2 against the release-source commit. Before generation:

- Compare every `<Compile Include>` and `<EmbeddedResource Include>` in `KeeFetch.plgx.csproj` to staged files.
- Confirm root forms/resources, FetchProfiles, IconProviders, IconSelection, Batch, Settings, Properties, and Assets.
- Confirm test files, benchmark files, docs, site, raw artifacts, and local paths are absent.
- Confirm no production file implicitly compiled by `KeeFetch.csproj` is absent from the PLGX project.

Create the PLGX with KeePass 2.60 and copy the newly timestamped output to `$releaseCandidateDir\KeeFetch.plgx`.

- [ ] **Step 5: Inspect artifact identity before runtime tests**

Record:

```powershell
Get-Item -LiteralPath (Join-Path $releaseCandidateDir 'KeeFetch.dll') | Select-Object Name,Length,CreationTimeUtc,LastWriteTimeUtc
Get-Item -LiteralPath (Join-Path $releaseCandidateDir 'KeeFetch.plgx') | Select-Object Name,Length,CreationTimeUtc,LastWriteTimeUtc
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseCandidateDir 'KeeFetch.dll')
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $releaseCandidateDir 'KeeFetch.plgx')
```

Do not insert hashes into tracked docs yet; first complete all runtime matrices against these exact files.

- [ ] **Step 6: Load-test exact DLL and PLGX separately**

Use two fresh disposable KeePass instances:

1. PLGX installation under `Plugins\KeeFetch.plgx`.
2. DLL installation under `Plugins\KeeFetch.dll`.

For each:

- Start KeePass with local config.
- Confirm no compile/load error.
- Confirm KeeFetch menu.
- Open Settings.
- Trigger First Run with a fresh config.
- Run one small synthetic batch.
- Reach Completion.
- Open diagnostics.
- Exercise Retry Eligible when a recoverable failure is available.
- Close cleanly.

Record separate PASS evidence. A successful process-start smoke alone is insufficient.

- [ ] **Step 7: Execute the 71-entry regression database matrix**

Copy the tracked KDBX to a fresh disposable file. Follow every group and expectation in `KeeFetch-Test-README.txt` and `KeeFetch-Test-Manifest.csv`. Record:

- profile and settings.
- entries processed.
- updated, skipped, not-found, recoverable error, invalid, cancelled counts.
- crashes or hangs.
- custom-icon preservation.
- deduplication observations.
- diagnostics/log/CSV paths.
- one bounded retry result.
- deviations from each manifest behavior class.

Do not treat network-dependent not-found variation as automatic failure; investigate every mismatch and distinguish external availability from plugin behavior using diagnostics. No crash, hang, incorrect database write, retry duplication, or privacy-policy violation is acceptable.

- [ ] **Step 8: Execute the release-artifact migration matrix**

Against the exact artifact, run each scenario twice:

- Missing new-install config -> `everyday` after explicit confirmation.
- Legacy Fast -> `bulk-fast`.
- Legacy Balanced -> `everyday`.
- Legacy Thorough -> stable ID `max-coverage`, display `Precise`.
- Legacy Custom -> `custom` with all custom values preserved.
- Duplicate provider order -> canonical first-seen deduplicated order plus missing known providers appended.
- Unknown stored legacy value -> `custom` without silent provider-value loss.
- Unknown stable ID -> in-memory Custom behavior until explicit save.

Record before, first-read result, second-read result, provider order, toggles, timeouts, synthetic, Android-store, and TLS settings.

- [ ] **Step 9: Repeat the UI matrix against the exact release artifact**

Do not rely solely on Task 2's development PLGX. Repeat Settings, First Run, progress, cancellation, Completion, diagnostics, retry, keyboard, High Contrast, and required DPI checks against `$releaseCandidateDir\KeeFetch.plgx`. Cross-reference detailed Task 2 evidence and record the exact RC artifact hash.

- [ ] **Step 10: Repeat the website release matrix**

Verify all seven local pages and deployed Pages content against the RC:

- Profile names and behaviors match the artifact.
- Screenshots match the artifact.
- Installation paths and filenames are exact.
- Hash-verification instructions work.
- Download links use the intended stable routes.
- JavaScript-disabled fallback works.
- No current page advertises v1.2.0 as the release candidate.

Before publication, versioned download links may not resolve. Record them as syntactically correct and defer reachability PASS to Task 16.

- [ ] **Step 11: Stop and rebuild on any artifact-affecting fix**

Any production source, resource, project, staging, workflow, or version metadata change invalidates both exact artifacts. Commit the fix, set a new release-source SHA, rerun Task 12, delete only the validated temporary RC directory, rebuild both files, and rerun every affected runtime matrix.

### Task 14: Finalize hashes, evidence, release notes, and release-candidate PR

**Files:**
- Modify: `docs/validation/v1.3-release-validation.md`
- Modify: `docs/releases/v1.3.0.md`
- Modify: `.agent/STATE.md` and `.agent/HANDOFF_LOG.md`

**Interfaces:**
- Consumes: exact fully validated artifacts and release-source SHA.
- Produces: docs-only evidence commit, reviewed PR, and an owner-authorizable release candidate.

- [ ] **Step 1: Recompute hashes after all runtime tests**

```powershell
$dllPath = Join-Path $releaseCandidateDir 'KeeFetch.dll'
$plgxPath = Join-Path $releaseCandidateDir 'KeeFetch.plgx'
$dllInfo = Get-Item -LiteralPath $dllPath
$plgxInfo = Get-Item -LiteralPath $plgxPath
$dllHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dllPath).Hash.ToLowerInvariant()
$plgxHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $plgxPath).Hash.ToLowerInvariant()
$dllHash
$plgxHash
```

- [ ] **Step 2: Replace every candidate/not-run state with exact evidence**

In `docs/validation/v1.3-release-validation.md`, record:

- Release-source SHA.
- Evidence commit relationship.
- Environment versions and UTC timestamps.
- Every command and exit code.
- Final test count and warning count.
- Corpus and generated-data hashes.
- DLL/PLGX filenames, bytes, SHA-256, build timestamps, and paths described without user-specific absolutes.
- PLGX staging/manifest parity result.
- DLL and PLGX load results.
- 71-entry matrix summary and deviations.
- Migration matrix.
- RC UI matrix.
- Website matrix and pre-publication link status.
- Known limitations and accepted issue links.
- Final status `READY FOR OWNER PUBLICATION AUTHORIZATION` only when every required gate passes.

No `NOT RUN`, pending host-manual row, unexplained failure, or fake value may remain.

- [ ] **Step 3: Insert exact hashes into release notes**

The Verification section in `docs/releases/v1.3.0.md` must list:

- `KeeFetch.dll`, byte length, SHA-256.
- `KeeFetch.plgx`, byte length, SHA-256.
- Release-source SHA.
- PowerShell verification command.
- Statement that GitHub release assets must match these exact values.

Remove release-candidate procedure wording that implies hashes are still pending.

- [ ] **Step 4: Commit evidence only**

```powershell
git add docs\validation\v1.3-release-validation.md docs\releases\v1.3.0.md .agent\STATE.md .agent\HANDOFF_LOG.md
git diff --cached --check
git diff --cached --name-only
git commit -m "test: record verified KeeFetch v1.3.0 release candidate"
```

Verify the commit changes only evidence/state/release-note files. Production source between `$releaseSourceSha` and this evidence commit must be byte-identical:

```powershell
git diff --name-only $releaseSourceSha..HEAD
```

If a build-affecting file appears, the artifacts no longer correspond to the final release source; return to Task 12.

- [ ] **Step 5: Run final deterministic verification from the evidence commit**

Repeat Task 12. The artifacts need not be rebuilt because the evidence commit is docs/state only, but the deterministic gate must remain green.

- [ ] **Step 6: Request final release-candidate review**

Review:

- Version consistency.
- Changelog/release claims.
- Machine-review provenance.
- Study scope closure.
- Website/profile/privacy consistency.
- Artifact/source relationship.
- Hash transcription.
- Manual matrices.
- Workflow change preventing auto-substitution of release assets.
- No staged/generated artifacts.

- [ ] **Step 7: Push and create the release-candidate PR**

```powershell
git push -u origin codex/v1-3-release-candidate
gh pr create --repo tzii/KeeFetch --base master --head codex/v1-3-release-candidate --title "Prepare KeeFetch v1.3.0 release candidate" --body-file docs\validation\v1.3-release-validation.md
gh pr checks --repo tzii/KeeFetch --watch
```

- [ ] **Step 8: Stop for merge authorization**

Report PR URL, source SHA, evidence SHA, test/warning counts, artifact paths, sizes, hashes, manual results, known limitations, and exact publication command preview. Merge only after explicit authorization.

### Task 15: Merge the release candidate and prepare the exact publication set

**Files:**
- No source changes.
- Exact external artifact directory must be preserved.

**Interfaces:**
- Consumes: owner-approved release PR and exact artifacts.
- Produces: master commit ready for tag, exact local publication files, and no ambiguity about source/evidence identity.

- [ ] **Step 1: Merge only after owner authorization**

Use the repository's established merge convention. Fetch master and verify it contains both the release-source and evidence commits.

- [ ] **Step 2: Verify the exact files survived the review period**

Recompute byte lengths and SHA-256 from `$releaseCandidateDir`. Compare them character-for-character to merged `docs/releases/v1.3.0.md` and `docs/validation/v1.3-release-validation.md`.

If the temporary artifacts are missing or differ, do not regenerate and assume equivalence. Rebuild from the recorded release-source SHA in a clean worktree, rerun required load/smoke checks, and update evidence through a new PR if hashes change.

- [ ] **Step 3: Verify GitHub publication preconditions**

```powershell
git fetch --tags origin
git ls-remote origin refs/tags/v1.3.0 'refs/tags/v1.3.0^{}'
gh release view v1.3.0 --repo tzii/KeeFetch
```

Expected: tag and release absent. A pre-existing unexpected tag/release is a hard stop requiring owner direction.

- [ ] **Step 4: Record the exact tag target**

The annotated tag targets the merged release evidence commit on master. The release notes explicitly identify the earlier release-source SHA used to build the binaries and state that the commits differ only by evidence/state documentation.

### Task 16: Publish v1.3.0 under explicit authorization and verify it end to end

**Files:**
- Exact verified external artifacts.
- Merged `docs/releases/v1.3.0.md`.
- Post-release state commit after successful publication.

**Interfaces:**
- Consumes: owner publication authorization, merged master, exact artifacts, and exact hashes.
- Produces: annotated tag, green tag CI, GitHub release with exact assets, correct live site, and final task state.

- [ ] **Step 1: Obtain explicit publication authorization**

The authorization request must show:

- Tag: `v1.3.0`.
- Tag target SHA.
- Release-source SHA.
- DLL and PLGX hashes.
- Known limitations.
- Confirmation that publication is externally visible and the release becomes the latest supported version.

Do not infer authorization from approval of an earlier PR.

- [ ] **Step 2: Create and inspect the annotated tag locally**

```powershell
git fetch --prune --tags origin
$tagTarget = (git rev-parse origin/master).Trim()
git merge-base --is-ancestor HEAD origin/master
if ($LASTEXITCODE -ne 0) {
    throw 'The reviewed release evidence commit is not contained in origin/master.'
}
git tag -a v1.3.0 -m "KeeFetch v1.3.0" $tagTarget
git show --no-patch --decorate v1.3.0
```

- [ ] **Step 3: Push only the tag**

```powershell
git push origin v1.3.0
```

- [ ] **Step 4: Wait for tag CI validation**

Find the tag-triggered Build KeeFetch run and wait for completion:

```powershell
gh run list --repo tzii/KeeFetch --event push --limit 20 --json databaseId,headBranch,headSha,workflowName,status,conclusion,url
```

Use `gh run watch <database-id> --repo tzii/KeeFetch --exit-status` for the exact `v1.3.0` run. Do not publish the release while tag CI is red.

- [ ] **Step 5: Create the GitHub release with the exact verified assets**

```powershell
gh release create v1.3.0 $dllPath $plgxPath --repo tzii/KeeFetch --verify-tag --title "KeeFetch v1.3.0" --notes-file docs\releases\v1.3.0.md
```

Confirm the release contains exactly `KeeFetch.dll` and `KeeFetch.plgx` as binary assets, plus GitHub-generated source archives.

- [ ] **Step 6: Download and hash the public assets independently**

```powershell
$publicVerifyDir = Join-Path $env:TEMP ("KeeFetch-v1.3.0-public-verify-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $publicVerifyDir | Out-Null
gh release download v1.3.0 --repo tzii/KeeFetch --pattern 'KeeFetch.dll' --pattern 'KeeFetch.plgx' --dir $publicVerifyDir

$publicDllHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $publicVerifyDir 'KeeFetch.dll')).Hash.ToLowerInvariant()
$publicPlgxHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $publicVerifyDir 'KeeFetch.plgx')).Hash.ToLowerInvariant()
if ($publicDllHash -ne $dllHash) { throw 'Published DLL hash mismatch.' }
if ($publicPlgxHash -ne $plgxHash) { throw 'Published PLGX hash mismatch.' }
```

- [ ] **Step 7: Verify release metadata and latest routes**

```powershell
gh release view v1.3.0 --repo tzii/KeeFetch --json tagName,name,isDraft,isPrerelease,publishedAt,assets,url
```

Verify in a browser or with read-only HTTP checks:

- GitHub marks v1.3.0 as the latest stable release.
- `/releases/latest` resolves to v1.3.0.
- `/releases/latest/download/KeeFetch.plgx` downloads the expected PLGX.
- `/releases/latest/download/KeeFetch.dll` downloads the expected DLL.
- Release notes display the exact hashes and known limitations.

- [ ] **Step 8: Verify live GitHub Pages after release**

Wait for the Pages workflow on the release-candidate merge commit. Check all seven live pages, current 1.3.0 release label, downloads, screenshots, profile data, privacy, benchmark provenance, internal navigation, and verification instructions.

- [ ] **Step 9: Perform one public-asset clean install**

Install the downloaded public `KeeFetch.plgx` into a fresh disposable KeePass 2.60 instance. Confirm menu, Settings, First Run, one synthetic download, Completion, diagnostics, and clean close. This proves the publicly downloadable bytes—not merely the local originals—load successfully.

- [ ] **Step 10: Record final repository state**

On a new post-release branch from master, update `.agent/STATE.md` to:

- Status: `DONE`
- Focus: `KeeFetch v1.3.0 published and independently verified`
- Next: `None`
- Pointer: GitHub release URL
- As-of: release timestamp and tag SHA

Prepend a handoff log line with release URL, tag SHA, public asset hashes, CI result, Pages result, and public-asset load result. Commit and merge this state-only change through the repository's normal authorization flow; do not move the v1.3.0 tag.

## Final definition of done

The release is complete only when every item below is true:

- [ ] Guided-native UX code and host-manual evidence are merged.
- [ ] Final PLGX visibly loads Settings, First Run, Progress, Completion, diagnostics, and Retry in KeePass 2.60.
- [ ] Real-host DPI, High Contrast, keyboard, first-install, migration, cancellation, partial-failure, retry, success, and long-path rows pass or have explicitly accepted linked issues.
- [ ] Provider-study machine-review provenance is accurate everywhere.
- [ ] The omitted full-minus-Yandex candidate is resolved by strict rerun or explicit owner waiver.
- [ ] Profile display names and policies agree across C#, tests, JSON, plugin UI, README, website, evidence, and release notes.
- [ ] All seven website pages exist and work without JavaScript.
- [ ] Shared CSS/JS, profile fallback generator, verifier tests, real-site verifier, and Edge viewport smoke pass offline.
- [ ] Final screenshots come from the exact release-candidate UX and expose no real vault data.
- [ ] Website accessibility/responsive/no-JavaScript/deployment matrix is complete.
- [ ] Version file, assembly metadata, changelog, README, release JSON, website, release notes, and tag all say 1.3.0 consistently.
- [ ] Release solution and explicit C# 5 builds have zero warnings and errors.
- [ ] Full MSTest, benchmark harness, corpus, profile export, site sync, verifier, Edge smoke, and whitespace gates pass from the final release source.
- [ ] Exact DLL and PLGX pass separate load tests.
- [ ] The 71-entry regression database matrix and migration matrix are complete against the exact release artifacts.
- [ ] DLL and PLGX byte lengths and SHA-256 values are recorded in validation and release notes.
- [ ] The GitHub release contains the exact verified bytes, not a later rebuild.
- [ ] Publicly downloaded asset hashes match the documented hashes.
- [ ] Publicly downloaded PLGX passes a fresh KeePass load and basic flow.
- [ ] Tag CI and Pages deployment are green.
- [ ] GitHub latest-release and latest-download routes resolve to v1.3.0.
- [ ] No unexplained failure, pending manual row, stale current claim, raw artifact, temporary database, machine path, or generated staging directory is committed.
- [ ] `.agent/STATE.md` records the final published/verified state after release without moving the release tag.

## Agent execution instruction

Execute this plan from Task 0 in order. Treat every owner authorization gate as a hard stop. Preserve completed evidence, report exact command output and commit SHAs at each checkpoint, and never advance merely because a prior agent or state file said a gate had passed. Fresh evidence is required for every new completion claim.
