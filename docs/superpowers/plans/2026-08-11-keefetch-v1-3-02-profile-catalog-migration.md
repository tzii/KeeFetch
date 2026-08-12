# KeeFetch v1.3 Profile Study, Catalog, and Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Select evidence-backed fetch profiles, centralize provider/profile behavior in a C# catalog, and migrate every v1.2 configuration without silently losing custom settings.

**Architecture:** Introduce focused provider and profile catalog types that are the only production source of provider order, timeout budgets, privacy flags, and display metadata. Use benchmark summaries plus a deterministic selector to generate a reviewed C# profile-definition file; migrate legacy strings to stable IDs through an idempotent configuration adapter.

**Tech Stack:** C# 5, .NET Framework 4.8, MSTest, PowerShell 5.1, existing KeePass `AceCustomConfig`, benchmark artifacts from Plan 1.

---

## File map

- Create `FetchProfiles/ProviderDefinition.cs` — stable provider metadata and factory key.
- Create `FetchProfiles/FetchProfileDefinition.cs` — immutable profile contract.
- Create `FetchProfiles/FetchProfileCatalog.cs` — lookup, validation, and stable IDs.
- Create `FetchProfiles/FetchProfileCatalog.Generated.cs` — reviewed definitions generated from evidence.
- Create `FetchProfiles/LegacyProfileMigration.cs` — legacy value mapping and schema version.
- Create `KeeFetch.Tests/FetchProfileCatalogTests.cs` — catalog invariants.
- Create `KeeFetch.Tests/LegacyProfileMigrationTests.cs` — migration matrix and idempotency.
- Create `KeeFetch.Tests/ProviderOrderTests.cs` — deduplication and normalization contract.
- Modify `Configuration.cs` — persist `FetchProfileId` and migration version; delegate profile helpers.
- Modify `FaviconDownloader.cs` — use catalog provider definitions and selected profile.
- Modify `SettingsForm.cs` — temporary catalog adapter until Plan 3 replaces the form.
- Modify `KeeFetch.plgx.csproj` — explicitly include new production files.
- Modify `.github/workflows/build.yml` — verify profile export consistency.
- Create `eng/benchmark/select-profiles.ps1` — deterministic profile selection and C# generation.
- Create `eng/benchmark/prepare-review.ps1` — deterministic human-review queue and label validation.
- Create `eng/export-profile-data.ps1` — export catalog data for the website.
- Create `site/data/profiles.json` — checked generated output.
- Create `docs/benchmarks/v1.3-provider-study.md` — reviewed methodology, results, and decisions.

### Task 1: Define provider metadata independently of the downloader

**Files:**
- Create: `FetchProfiles/ProviderDefinition.cs`
- Create: `FetchProfiles/FetchProfileCatalog.cs`
- Create: `KeeFetch.Tests/FetchProfileCatalogTests.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing provider-catalog tests**

Create tests that require stable IDs, unique names, and the current order:

```csharp
[TestMethod]
public void Providers_HaveUniqueStableIdsAndPreserveV12DefaultOrder()
{
    var providers = FetchProfileCatalog.Providers;
    CollectionAssert.AreEqual(
        new[] { "direct-site", "twenty-icons", "duckduckgo", "google", "yandex", "favicone", "icon-horse" },
        providers.Select(p => p.Id).ToArray());
    Assert.AreEqual(providers.Count,
        providers.Select(p => p.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
}

[TestMethod]
public void FindProvider_AcceptsLegacyDisplayNamesAndIds()
{
    Assert.AreEqual("direct-site", FetchProfileCatalog.FindProvider("Direct Site").Id);
    Assert.AreEqual("direct-site", FetchProfileCatalog.FindProvider("direct-site").Id);
    Assert.IsNull(FetchProfileCatalog.FindProvider("unknown-provider"));
}
```

Run the focused test and expect compile failure because the catalog does not exist.

- [ ] **Step 2: Implement `ProviderDefinition`**

```csharp
namespace KeeFetch.FetchProfiles
{
    internal sealed class ProviderDefinition
    {
        public ProviderDefinition(string id, string displayName, bool isThirdParty,
            bool isSyntheticCapable, bool isPlaceholderProne)
        {
            Id = id;
            DisplayName = displayName;
            IsThirdParty = isThirdParty;
            IsSyntheticCapable = isSyntheticCapable;
            IsPlaceholderProne = isPlaceholderProne;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsThirdParty { get; private set; }
        public bool IsSyntheticCapable { get; private set; }
        public bool IsPlaceholderProne { get; private set; }
    }
}
```

- [ ] **Step 3: Implement provider lookup in `FetchProfileCatalog`**

Declare `FetchProfileCatalog` as `internal static partial class`. Use a read-only list in the exact v1.2 order. `FindProvider` trims input and compares both ID and display name case-insensitively. Return `null` for unknown values.

- [ ] **Step 4: Add new files to both project paths**

SDK-style compilation discovers the files automatically. Add explicit `<Compile Include="FetchProfiles\ProviderDefinition.cs" />` and `<Compile Include="FetchProfiles\FetchProfileCatalog.cs" />` entries to `KeeFetch.plgx.csproj`.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~FetchProfileCatalogTests
git add FetchProfiles KeeFetch.Tests/FetchProfileCatalogTests.cs KeeFetch.plgx.csproj
git commit -m "refactor: centralize provider metadata"
```

Expected: focused tests pass.

### Task 2: Define immutable profiles and catalog invariants

**Files:**
- Create: `FetchProfiles/FetchProfileDefinition.cs`
- Create: `FetchProfiles/FetchProfileCatalog.Generated.cs`
- Modify: `FetchProfiles/FetchProfileCatalog.cs`
- Modify: `KeeFetch.Tests/FetchProfileCatalogTests.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing profile invariant tests**

```csharp
[TestMethod]
public void ManagedProfiles_HaveStableIdsAndValidProviderReferences()
{
    CollectionAssert.AreEqual(
        new[] { "bulk-fast", "everyday", "privacy", "max-coverage" },
        FetchProfileCatalog.ManagedProfiles.Select(p => p.Id).ToArray());

    foreach (var profile in FetchProfileCatalog.ManagedProfiles)
    {
        Assert.IsTrue(profile.ProviderIds.Count > 0);
        Assert.IsTrue(profile.PrimaryTimeoutMs > 0);
        Assert.IsTrue(profile.FallbackTimeoutMs > 0);
        Assert.IsTrue(profile.CumulativeTimeoutMs >= profile.PrimaryTimeoutMs);
        foreach (string providerId in profile.ProviderIds)
            Assert.IsNotNull(FetchProfileCatalog.FindProvider(providerId));
    }
}

[TestMethod]
public void PrivacyProfile_UsesNoThirdPartyProviders()
{
    var profile = FetchProfileCatalog.GetRequiredProfile("privacy");
    Assert.IsTrue(profile.ProviderIds.All(id => !FetchProfileCatalog.FindProvider(id).IsThirdParty));
    Assert.IsFalse(profile.AllowSyntheticFallbacks);
}
```

- [ ] **Step 2: Implement the immutable definition**

`FetchProfileDefinition` constructor parameters are:

```csharp
string id, string displayName, string description, string intendedUse,
IEnumerable<string> providerIds, int primaryTimeoutMs, int fallbackTimeoutMs,
int cumulativeTimeoutMs, bool allowSyntheticFallbacks, bool isVisible,
string evidenceReport
```

Copy `providerIds` into a private `List<string>` and expose it as `IList<string>` through `AsReadOnly()`.

- [ ] **Step 3: Add provisional generated definitions**

Create a partial `FetchProfileCatalog` method `CreateManagedProfiles()` in `FetchProfileCatalog.Generated.cs`. Until Task 6 runs the study selector, use the current v1.2 chains for `bulk-fast`, `everyday`, and `max-coverage`; define `privacy` as Direct Site only with synthetic fallback disabled. Mark all four visible and set `evidenceReport` to `docs/benchmarks/v1.3-provider-study.md`.

The provisional values are intentionally executable baseline inputs, not final claims. Task 5 replaces this entire generated method with measured definitions.

- [ ] **Step 4: Validate the full catalog at static initialization**

Throw `InvalidOperationException` for duplicate profile IDs, duplicate providers within a profile, unknown providers, empty display text, invalid timeout relationships, a privacy profile containing a third party, or a visible profile with no providers.

- [ ] **Step 5: Run focused tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~FetchProfileCatalogTests
git add FetchProfiles KeeFetch.Tests/FetchProfileCatalogTests.cs KeeFetch.plgx.csproj
git commit -m "feat: add validated fetch profile catalog"
```

### Task 3: Implement idempotent legacy configuration migration

**Files:**
- Create: `FetchProfiles/LegacyProfileMigration.cs`
- Create: `KeeFetch.Tests/LegacyProfileMigrationTests.cs`
- Modify: `Configuration.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write the migration matrix tests**

Use one test row per approved mapping:

```csharp
[DataTestMethod]
[DataRow("Fast", "bulk-fast")]
[DataRow("Balanced", "everyday")]
[DataRow("Thorough", "max-coverage")]
[DataRow("Custom", "custom")]
[DataRow("future-value", "custom")]
public void MapLegacyValue_ReturnsStableProfileId(string legacy, string expected)
{
    Assert.AreEqual(expected, LegacyProfileMigration.MapLegacyValue(legacy, false));
}

[TestMethod]
public void MapLegacyValue_MissingNewInstallUsesEveryday()
{
    Assert.AreEqual("everyday", LegacyProfileMigration.MapLegacyValue(null, true));
}
```

Add an integration test with `AceCustomConfig` that reads twice and asserts the same `FetchProfileId`, migration version, provider toggles, and provider order after both reads.

- [ ] **Step 2: Run tests and verify failure**

Expected: compile failure because `LegacyProfileMigration` and `Configuration.FetchProfileId` do not exist.

- [ ] **Step 3: Implement the pure migration mapper**

```csharp
internal static class LegacyProfileMigration
{
    internal const int CurrentSchemaVersion = 1;

    internal static string MapLegacyValue(string raw, bool isNewInstall)
    {
        if (string.IsNullOrWhiteSpace(raw)) return isNewInstall ? "everyday" : "custom";
        string value = raw.Trim();
        if (value.Equals("Fast", StringComparison.OrdinalIgnoreCase)) return "bulk-fast";
        if (value.Equals("Balanced", StringComparison.OrdinalIgnoreCase)) return "everyday";
        if (value.Equals("Thorough", StringComparison.OrdinalIgnoreCase)) return "max-coverage";
        if (value.Equals("Custom", StringComparison.OrdinalIgnoreCase)) return "custom";
        return "custom";
    }
}
```

- [ ] **Step 4: Add configuration keys and one-time migration**

Add cached fields/properties for `FetchProfileId` and `ProfileSchemaVersion`. On first read:

1. Return a valid stored `FetchProfileId` unchanged.
2. If absent, read the legacy `FetchPresetMode` key with a `null` default; a non-empty result means the legacy key exists.
3. Map the legacy value with `isNewInstall` set from key presence.
4. Write `FetchProfileId` and schema version 1.
5. Do not delete the legacy key or alter provider toggles/order.

Unknown stored `FetchProfileId` values resolve to `custom` in memory and are not rewritten until the user saves settings.

- [ ] **Step 5: Keep a temporary enum adapter**

Retain `FetchPresetMode` for binary/source transition. Map profile IDs back to the closest legacy enum when old callers read it, and map enum writes through `FetchProfileId`. Mark helper methods for later removal only after all production callers move to the catalog; do not remove them in the same step.

- [ ] **Step 6: Run focused and full tests**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~LegacyProfileMigrationTests
dotnet test KeeFetch.sln --no-restore
```

Expected: migration tests and the full suite pass.

- [ ] **Step 7: Commit**

```powershell
git add FetchProfiles/LegacyProfileMigration.cs Configuration.cs KeeFetch.Tests/LegacyProfileMigrationTests.cs KeeFetch.plgx.csproj
git commit -m "feat: migrate legacy presets to stable profile IDs"
```

### Task 4: Preserve provider-order normalization while moving defaults into the catalog

**Files:**
- Create: `KeeFetch.Tests/ProviderOrderTests.cs`
- Modify: `Configuration.cs`
- Modify: `FaviconDownloader.cs`
- Modify: `SettingsForm.cs`

- [ ] **Step 1: Lock the existing order behavior with failing tests**

```csharp
[TestMethod]
public void ProviderOrder_TrimsDeduplicatesAndAppendsMissingKnownProviders()
{
    var ace = new AceCustomConfig();
    var config = new Configuration(ace);
    config.ProviderOrder = " google,Direct Site,GOOGLE,unknown-provider ";
    CollectionAssert.AreEqual(
        new[] { "Google", "Direct Site", "unknown-provider", "Twenty Icons", "DuckDuckGo", "Yandex", "Favicone", "Icon Horse" },
        config.GetProviderOrderList().ToArray());
}
```

The test initially passes against v1.2 behavior; temporarily point defaults at an empty catalog list to prove it fails, then restore and implement the new path. This red check proves the test protects the migration.

- [ ] **Step 2: Add catalog normalization**

Implement `FetchProfileCatalog.NormalizeProviderOrder(IEnumerable<string>)` so it:

1. Trims values.
2. Converts recognized IDs/display names to canonical display names.
3. Preserves unknown non-empty names.
4. Deduplicates case-insensitively in first-seen order.
5. Appends missing known providers in catalog order.

Delegate `Configuration.GetProviderOrderList()` to this method.

- [ ] **Step 3: Move downloader selection to stable IDs**

Replace uses of `Configuration.GetPreset*`, `IsProviderEnabledByPreset`, and `DefaultProviderOrder` with `FetchProfileCatalog.GetRequiredProfile(config.FetchProfileId)` and provider catalog lookup. Custom mode continues to use stored toggles/order/timeouts.

Provider factories remain in `FaviconDownloader`, keyed by stable provider ID. Convert profile IDs to factories directly and convert legacy display names only at configuration boundaries.

- [ ] **Step 4: Keep the current settings form functional**

Until Plan 3 replaces the form, populate its combo box from visible catalog profiles plus Custom and display `FetchProfileDefinition.DisplayName`. Add a private nested `ProfileComboItem` class inside `SettingsForm.cs` with `Id`, `DisplayName`, and `ToString()`; store the stable ID there and do not use `Enum.ToString()`. Plan 3 replaces this nested adapter with `Settings/ProfileListItem.cs`.

- [ ] **Step 5: Run behavior tests**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~ProviderOrderTests|FullyQualifiedName~FaviconDownloaderTests|FullyQualifiedName~ConfigurationTests"
dotnet test KeeFetch.sln --no-restore
```

Expected: focused and full suites pass with existing download semantics preserved.

- [ ] **Step 6: Commit**

```powershell
git add FetchProfiles Configuration.cs FaviconDownloader.cs SettingsForm.cs KeeFetch.Tests
git commit -m "refactor: drive downloads from the profile catalog"
```

### Task 5: Implement deterministic evidence-based profile selection

**Files:**
- Create: `eng/benchmark/select-profiles.ps1`
- Create: `eng/benchmark/prepare-review.ps1`
- Create: `eng/benchmark/experiments/profile-candidates-v13.json`
- Create: `docs/benchmarks/v1.3-provider-study.md`

- [ ] **Step 1: Define the candidate experiment**

List every provider order worth testing without enumerating all 7! permutations. Include:

- Direct Site only.
- Each direct-plus-one-resolver pair.
- Each direct-plus-one-resolver-plus-one-fallback chain.
- Current v1.2 Fast, Balanced, and Thorough chains.
- Full chain without each single third-party provider.
- Timeout budgets at the Fast, Balanced, and Thorough levels.

Set three repetitions, concurrency 8, and both cold/warm modes. The selector reads only finalized runs.

- [ ] **Step 2: Implement the review queue and validation contract**

`prepare-review.ps1` reads finalized `rows.csv` files and writes `review-queue.csv` with columns `run_id`, `fixture_id`, `profile_id`, `artifact_hash`, `review_label`, `reviewer`, `reviewed_at_utc`, and `notes`. It includes every synthetic, placeholder-suspected, blank-suspected, or profile-differing selection plus a deterministic category/profile-stratified 10% sample of remaining successes.

Support `-Validate` to reject labels outside `correct`, `acceptable-synthetic`, `generic`, `wrong-brand`, `blank`, `unusable`, `ambiguous`, and `not-reviewed`; reviewed rows missing reviewer/timestamp; or artifact hashes that no longer match the run. Preserve labels when regenerating a queue with unchanged `(fixture_id, profile_id, artifact_hash)` keys.

- [ ] **Step 3: Write selector self-tests using a three-candidate fixture**

Create temporary summary rows where the intended winners are unambiguous. Assert:

- `privacy` selects the best Direct Site-only candidate.
- `max-coverage` selects highest reviewed usable rate, then correctness, then lower p95.
- `bulk-fast` selects lowest batch duration among candidates with usable rate at least 90% of the max-coverage winner and wrong-brand rate no greater than 1%.
- `everyday` selects the highest score using `0.40*correctness + 0.25*coverage + 0.15*latency_score + 0.10*reliability + 0.10*privacy_score`.

Normalize each component to 0–1 within the candidate set; define latency and privacy scores so lower values score higher. Break final ties by fewer providers, then lexicographic experiment ID.

- [ ] **Step 4: Implement selection and report generation**

`select-profiles.ps1` joins finalized results to a validated `review-queue.csv` and rejects incomplete runs, missing review coverage, or ambiguous results capable of reversing a winner. It writes:

- A Markdown comparison table and decision rationale.
- A machine-readable `profile-decisions.json` attached to the reviewed result directory.
- A complete `FetchProfileCatalog.Generated.cs` method with selected provider IDs, budgets, descriptions, visibility, and evidence-report path.

The privacy profile must contain no third-party providers regardless of score.

- [ ] **Step 5: Run the live study**

Run the candidate experiment against the validated 300-row corpus. Generate `review-queue.csv`, inspect the referenced local artifacts, fill required human labels/reviewer/timestamp, validate the queue, rerun aggregation, and run the selector. Commit only the experiment definition, reviewed aggregate/decision report, review/selector scripts, and generated C# catalog; do not commit raw downloaded icons, review queues containing local artifact paths, or generated run directories.

- [ ] **Step 6: Rebuild and test generated profiles**

```powershell
dotnet build KeeFetch.sln --configuration Release --no-restore -warnaserror
dotnet test KeeFetch.sln --configuration Release --no-build
```

Expected: catalog invariants and all existing tests pass. If two managed IDs select identical chains and budgets, hide the lower-scoring non-migrated profile and document why.

- [ ] **Step 7: Commit**

```powershell
git add eng/benchmark/select-profiles.ps1 eng/benchmark/prepare-review.ps1 eng/benchmark/experiments/profile-candidates-v13.json `
  FetchProfiles/FetchProfileCatalog.Generated.cs docs/benchmarks/v1.3-provider-study.md
git commit -m "feat: select evidence-backed v1.3 fetch profiles"
```

### Task 6: Export profile data and enforce cross-surface consistency

**Files:**
- Create: `eng/export-profile-data.ps1`
- Create: `site/data/profiles.json`
- Create: `KeeFetch.Tests/ProfileExportTests.cs`
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Write a failing export consistency test**

The test loads `site/data/profiles.json`, compares stable IDs, display text, providers, budgets, privacy flags, visibility, and evidence path against `FetchProfileCatalog.ManagedProfiles`, and fails on any difference.

- [ ] **Step 2: Implement the exporter**

Build the release assembly, load it with KeePass, reflect the internal catalog, and serialize visible managed profiles to deterministic indented JSON sorted by catalog order. Write UTF-8 without a byte-order mark. Support `-Check` to compare generated content without writing.

CLI:

```powershell
param([switch]$Check)
```

- [ ] **Step 3: Generate and verify the checked data**

```powershell
dotnet build KeeFetch.csproj --configuration Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1 -Check
```

Expected: the check exits 0 and produces no diff.

- [ ] **Step 4: Add CI consistency gate**

Add the `-Check` command after the Release build and before website checks.

- [ ] **Step 5: Run the plan exit gate**

```powershell
dotnet build KeeFetch.sln --configuration Release --no-restore -warnaserror
dotnet test KeeFetch.sln --configuration Release --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1 -Check
git diff --check
```

Expected: zero failures, generated profile data matches the C# catalog, migration tests cover all matrix rows, and the evidence report names every visible profile.

- [ ] **Step 6: Commit**

```powershell
git add eng/export-profile-data.ps1 site/data/profiles.json KeeFetch.Tests/ProfileExportTests.cs .github/workflows/build.yml
git commit -m "ci: enforce profile catalog consistency"
```
