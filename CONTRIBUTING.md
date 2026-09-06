# Contributing to KeeFetch

Thank you for your interest in contributing to KeeFetch!

## How to Contribute

1. **Fork the Repository**: Create your own fork of the project.
2. **Clone the Fork**: `git clone https://github.com/YOUR_USERNAME/KeeFetch.git`
3. **Create a Branch**: `git checkout -b feature/your-feature-name`
4. **Make Changes**: Implement your feature or fix.
5. **Add Tests**: Ensure your changes are covered by unit tests in `KeeFetch.Tests`.
6. **Run Tests**: Use `dotnet test` to verify everything is working.
7. **Commit Changes**: `git commit -m 'feat: add amazing feature'`
8. **Push to GitHub**: `git push origin feature/your-feature-name`
9. **Open a Pull Request**: Submit your PR for review.

## Coding Standards

- Follow existing code style (see `.editorconfig`).
- Use XML documentation for public and internal members.
- Keep methods small and focused.
- Avoid external dependencies unless absolutely necessary.
- **All code must be C# 5 compatible** — the PLGX is compiled by KeePass using `CSharpCodeProvider` (legacy `csc.exe`). This means: no string interpolation, no expression-bodied members, no null-conditional operators, no pattern matching.

**Why C# 5?** KeePass compiles PLGX files at runtime using CSharpCodeProvider, which on some systems still invokes the legacy C# 5 compiler. CI enforces this with a `LangVersion=5` build step.

## Development Environment

- Visual Studio 2022 or VS Code with C# Dev Kit.
- .NET 8 SDK (required for SDK-style project support).
- .NET Framework 4.8 targeting pack (the plugin targets .NET Framework 4.8).
- KeePass 2.x installed for testing and PLGX creation.

For an Ubuntu 24.04 x64 sandbox, `.hoplite/settings.json` runs `eng/setup-sandbox.sh` to install the official .NET 8 SDK, Mono/Xvfb, PowerShell, and a checksum-verified KeePass 2.60 reference. The official SDK is required because Ubuntu's SDK package omits the WindowsDesktop targets used by this project.

```sh
dotnet build KeeFetch.Tests/KeeFetch.Tests.csproj -c Release -p:KeePassPath=/opt/keepass/2.60 -warnaserror
xvfb-run -a dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj -c Release --no-build
KEEFETCH_KEEPASS_PATH=/opt/keepass/2.60/KeePass.exe pwsh -File eng/export-profile-data.ps1 -Check
```

Linux checks supplement, not replace, the Windows gate. Mono WinForms/image behavior differs, and the benchmark self-tests require Windows PowerShell's JSON-array semantics rather than PowerShell 7. Keep those failures visible; do not change fingerprinted benchmark code to accommodate the sandbox.

## Building the PLGX

The PLGX is built using the `KeeFetch.plgx.csproj` file which is a legacy-style project file required by KeePass. The main `KeeFetch.csproj` is an SDK-style project used for modern development and testing.

Every production `.cs`, `.resx`, and embedded asset must be listed in `KeeFetch.plgx.csproj`; `eng/check-plgx-manifest.ps1` (run in CI) fails when the project and the tracked sources drift apart.

## Repository Gates

CI runs these on every pull request; run them locally before opening one:

```powershell
dotnet build KeeFetch.csproj -c Release -p:LangVersion=5 -warnaserror   # production must stay C# 5 and warning-free
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj -c Release
./eng/check-version.ps1                                                  # version.txt == AssemblyInfo.cs
./eng/check-plgx-manifest.ps1                                            # PLGX project lists exactly the tracked sources
./eng/test-release-workflow.ps1                                         # permission isolation and checksum negative cases
./eng/export-profile-data.ps1 -Check                                     # site/data/profiles.json matches the catalog
powershell -File eng/benchmark/test-benchmark-harness.ps1                # benchmark harness self-tests (offline)
git diff --check
```

Releases are cut by pushing a `v*` tag; the workflow checks that the tag matches `version.txt` and that `CHANGELOG.md` has a section for it. The read-only build job produces the DLL, PLGX, and `SHA256SUMS.txt` on PRs too. A separate tag-push-only release job downloads those artifacts, verifies their hashes, and publishes them with repository write permission. It does not check out or rebuild source. A manual workflow dispatch validates/packages but does not publish a release; rerun a failed tag-push workflow to retry publication.

## Project Structure

```
KeeFetch/
├── KeeFetchExt.cs                # Plugin entry point — registers menus, first-run disclosure, update URL
├── FaviconDownloader.cs          # Runs the provider chain under one execution policy; caching, coalescing, health
├── FaviconDialog.cs              # Progress dialog — concurrent downloads with SemaphoreSlim, UI-thread DB writes
├── FaviconDiagnostics.cs         # Per-entry diagnostics log and CSV export
├── Configuration.cs              # Plugin settings stored in KeePass custom config; preset/profile mapping
├── SettingsForm.cs / .Designer.cs / .resx   # Settings UI (WinForms)
├── SharedHttp.cs                 # Single HttpClient; scoped self-signed certificate policy
├── Logger.cs                     # Thread-safe in-memory log with level filtering
├── Util.cs                       # URL parsing, private-host detection, image validation, hashing
├── AndroidAppMapper.cs           # Maps androidapp:// URLs to web domains + Play Store scraping
├── FetchProfiles/
│   ├── ProviderDefinition.cs             # Stable provider ids (direct-site … icon-horse) and display names
│   ├── FetchProfileDefinition.cs         # Immutable managed-profile record
│   ├── FetchProfileCatalog.cs            # Catalog lookup / normalization
│   ├── FetchProfileCatalog.Generated.cs  # Managed profiles written by eng/benchmark/select-profiles.ps1 — do not hand-edit
│   ├── FetchExecutionPolicy.cs           # Resolves the single policy (providers, timeouts, flags) a run executes with
│   └── LegacyProfileMigration.cs         # Maps pre-1.3 preset values to profile ids
├── IconSelection/
│   ├── IconSelector.cs           # Tier/confidence ranking of collected candidates
│   ├── IconCandidate.cs, IconRequest.cs, IconSelectionResult.cs, IconTier.cs
│   └── ProviderCapabilities.cs   # Per-provider tier, confidence, concurrency, private-host policy
├── IconProviders/
│   ├── IIconProvider.cs          # Interface — GetCandidatesAsync(request, token)
│   ├── IconProviderBase.cs       # Shared HTTP download, retry, redirect guard, candidate scoring
│   ├── DirectSiteProvider.cs     # Primary — parses HTML/manifest for icons, apple-touch-icon, og:image
│   ├── TwentyIconsProvider.cs, DuckDuckGoProvider.cs, GoogleProvider.cs,
│   │   YandexProvider.cs, FaviconeProvider.cs, IconHorseProvider.cs   # Third-party resolvers
├── Assets/Icons/                 # Embedded menu icons
├── KeeFetch.Tests/               # MSTest suite (offline; HTTP goes through a fake transport)
│   └── Fixtures/                 # Regression database + manifest, provider corpus
├── eng/
│   ├── check-version.ps1, check-plgx-manifest.ps1, export-profile-data.ps1   # CI gates
│   ├── benchmark-presets.ps1     # Benchmark runner CLI
│   └── benchmark/                # Harness module, self-tests, review prep, profile selector, experiments
├── docs/                         # README media, benchmark study, plans and validation records
├── site/                         # GitHub Pages site; site/data/profiles.json is generated from the catalog
├── KeeFetch.csproj               # SDK-style project (development & testing)
├── KeeFetch.plgx.csproj          # Legacy-style project (PLGX creation only)
└── .github/workflows/
    ├── build.yml                 # CI: gates, build, test, PLGX creation, release publishing
    └── pages.yml                 # GitHub Pages deployment
```

### Architecture Overview

A download run executes one **`FetchExecutionPolicy`**, resolved once from the selected fetch profile (`Fast`, `Balanced`, `Privacy`, `Thorough`) or from the Custom configuration. The policy fixes the provider order, per-provider and cumulative timeouts, whether synthetic fallbacks are allowed, and whether the run stops at the first strong resolver hit.

1. **`DirectSiteProvider`** fetches the site itself and parses `<head>`, the web manifest, `apple-touch-icon`, and `og:image` into candidates.
2. **Resolver providers** (`TwentyIcons`, `DuckDuckGo`, `Google`, `Yandex`, `Favicone`, `IconHorse`) each return at most one candidate. They are skipped for targets recognized by the lexical private-host classifier. Their final-response guard discards private-host responses after automatic redirects; it does not prevent network contact, inspect intermediate hops, or validate DNS answers. See the README privacy limitations.
3. **`IconSelector`** ranks all surviving candidates by tier (`SiteCanonical` → `StrongResolved` → `SyntheticFallback`) and confidence, so a synthetic or placeholder-prone result only wins when nothing stronger survived.

`FaviconDownloader` orchestrates this with a shared cumulative deadline, per-origin caching and negative caching, in-flight coalescing, and per-provider health cooldowns. `FaviconDialog` runs entries concurrently (up to 8 in parallel via `SemaphoreSlim`) and marshals database writes to the UI thread.

Managed profiles are not hand-tuned: `eng/benchmark/select-profiles.ps1` writes `FetchProfileCatalog.Generated.cs` from study evidence, and `eng/export-profile-data.ps1 -Check` keeps the website data in sync.
