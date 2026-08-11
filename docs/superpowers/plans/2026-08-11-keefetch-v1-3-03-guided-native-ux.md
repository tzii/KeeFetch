# KeeFetch v1.3 Guided-Native Plugin UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dense settings, first-run, and completion message-box flows with accessible native WinForms surfaces driven by structured profile and batch-result models.

**Architecture:** Keep one modal `SettingsForm` with a standard four-page `TabControl`, page-specific `UserControl` classes, and a draft configuration that commits atomically. Preserve KeePass `StatusUtil` for progress, return an immutable structured batch result from `FaviconDialog`, and show a native completion form that exposes diagnostics and bounded retry.

**Tech Stack:** C# 5, .NET Framework 4.8 WinForms, KeePass 2.x plugin APIs, MSTest, existing profile catalog and diagnostics pipeline.

---

## File map

- Create `Settings/SettingsDraft.cs` — copy, validate, and commit settings atomically.
- Create `Settings/ProfileListItem.cs` — stable ID/display wrapper for native controls.
- Create `Settings/OverviewSettingsPage.cs` and `.Designer.cs` — profile choice and common options.
- Create `Settings/DownloadSettingsPage.cs` and `.Designer.cs` — download behavior.
- Create `Settings/ProviderSettingsPage.cs` and `.Designer.cs` — provider order/privacy controls.
- Create `Settings/AdvancedSettingsPage.cs` and `.Designer.cs` — timeouts, TLS, diagnostics, reset.
- Rewrite `SettingsForm.cs` and `SettingsForm.Designer.cs` — host pages and shared actions.
- Create `FirstRunForm.cs` and `.Designer.cs` — explicit first-run profile/privacy decision.
- Create `Batch/BatchEntryOutcome.cs` — immutable per-entry outcome.
- Create `Batch/BatchRunResult.cs` — aggregate counts, diagnostics paths, and retry selection.
- Create `CompletionForm.cs` and `.Designer.cs` — native summary/actions.
- Modify `FaviconDialog.cs` — build and return structured results; improve stable progress text.
- Modify `KeeFetchExt.cs` — show first-run and completion forms; start bounded retry.
- Modify `KeeFetch.plgx.csproj` — include all new production files/resources.
- Modify `.github/workflows/build.yml` — stage new production directories and verify representative files.
- Create tests under `KeeFetch.Tests/` for settings draft/pages, batch results, first run, completion, progress, and layout.

### Task 1: Introduce immutable batch outcomes and retry selection

**Files:**
- Create: `Batch/BatchEntryOutcome.cs`
- Create: `Batch/BatchRunResult.cs`
- Create: `KeeFetch.Tests/BatchRunResultTests.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing retry-set tests**

```csharp
[TestMethod]
public void RetryEntries_ContainsOnlyNotFoundAndRecoverableErrors()
{
    var success = Outcome(BatchEntryStatus.Updated, false, "ok");
    var notFound = Outcome(BatchEntryStatus.NotFound, false, "miss");
    var networkError = Outcome(BatchEntryStatus.RecoverableError, true, "timeout");
    var invalid = Outcome(BatchEntryStatus.InvalidInput, false, "invalid-input");
    var skipped = Outcome(BatchEntryStatus.Skipped, false, "existing-icon");
    var cancelled = Outcome(BatchEntryStatus.Cancelled, false, "cancelled");

    var result = new BatchRunResult(new[] { success, notFound, networkError, invalid, skipped, cancelled },
        false, TimeSpan.FromSeconds(3), "everyday", "log.txt", "rows.csv");

    CollectionAssert.AreEqual(new[] { notFound.Entry, networkError.Entry }, result.RetryEntries.ToArray());
}
```

Define a new `BatchEntryStatus` enum with exactly `Updated`, `Skipped`, `NotFound`, `RecoverableError`, `InvalidInput`, and `Cancelled`; the existing `FaviconStatus` contains only `Success` and `NotFound` and remains the download-selection status.

- [ ] **Step 2: Run the focused test and verify compile failure**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~BatchRunResultTests
```

- [ ] **Step 3: Implement immutable result types**

`BatchEntryOutcome` constructor takes `PwEntry entry`, `string title`, `string resolvedUrl`, `BatchEntryStatus status`, `string providerId`, `IconTier tier`, `bool synthetic`, `bool recoverable`, `long elapsedMilliseconds`, and `string diagnostic`. All properties have private setters.

`BatchRunResult` copies outcomes to a read-only list, calculates counts once, exposes `RetryEntries` as distinct `PwEntry` references for `NotFound` and `RecoverableError`, and stores `WasCancelled`, `Elapsed`, `ProfileId`, `DiagnosticsLogPath`, and `DiagnosticsCsvPath`.

- [ ] **Step 4: Add production files to PLGX and run tests**

Add explicit compile entries, then run the focused test. Expected: pass.

- [ ] **Step 5: Commit**

```powershell
git add Batch KeeFetch.Tests/BatchRunResultTests.cs KeeFetch.plgx.csproj
git commit -m "feat: add structured batch run results"
```

### Task 2: Add an atomic settings draft

**Files:**
- Create: `Settings/SettingsDraft.cs`
- Create: `KeeFetch.Tests/SettingsDraftTests.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing copy/cancel/commit tests**

Tests must prove:

```csharp
[TestMethod]
public void Draft_DoesNotMutateConfigurationUntilApply()
{
    var config = new Configuration(new AceCustomConfig());
    var draft = SettingsDraft.FromConfiguration(config);
    draft.ProfileId = "privacy";
    draft.AutoSave = true;
    Assert.AreEqual("everyday", config.FetchProfileId);
    Assert.IsFalse(config.AutoSave);
    draft.ApplyTo(config);
    Assert.AreEqual("privacy", config.FetchProfileId);
    Assert.IsTrue(config.AutoSave);
}
```

Add validation cases for invalid profile ID, timeout outside 5–60, icon size outside the existing supported range, duplicate provider IDs, and a privacy profile with third-party overrides.

- [ ] **Step 2: Implement `SettingsDraft`**

Mirror every persisted `Configuration` property. `Validate()` returns a read-only list of `SettingsValidationError` objects with `PageId`, `ControlKey`, and `Message`. `ApplyTo` throws when validation errors exist and otherwise writes all properties in one method.

- [ ] **Step 3: Run tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~SettingsDraftTests
git add Settings KeeFetch.Tests/SettingsDraftTests.cs KeeFetch.plgx.csproj
git commit -m "feat: add atomic settings draft"
```

### Task 3: Build the four native settings pages

**Files:**
- Create: `Settings/ProfileListItem.cs`
- Create: `Settings/OverviewSettingsPage.cs`
- Create: `Settings/OverviewSettingsPage.Designer.cs`
- Create: `Settings/DownloadSettingsPage.cs`
- Create: `Settings/DownloadSettingsPage.Designer.cs`
- Create: `Settings/ProviderSettingsPage.cs`
- Create: `Settings/ProviderSettingsPage.Designer.cs`
- Create: `Settings/AdvancedSettingsPage.cs`
- Create: `Settings/AdvancedSettingsPage.Designer.cs`
- Create: `KeeFetch.Tests/SettingsPageTests.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing page-binding tests**

Instantiate each page with a draft and assert:

- Overview lists visible catalog profiles plus Custom, marks `everyday` recommended, and updates only the draft.
- Downloads binds URL prefix, title fallback, skip-existing, auto-save, and max icon size.
- Providers disables order/toggle editing for managed profiles and enables it for Custom.
- Advanced binds timeout, self-signed certificate behavior, synthetic fallback, and reset actions.
- Every interactive control has a non-empty accessible name and deterministic tab index.

- [ ] **Step 2: Implement `ProfileListItem`**

```csharp
internal sealed class ProfileListItem
{
    public ProfileListItem(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }
    public string Id { get; private set; }
    public string DisplayName { get; private set; }
    public override string ToString() { return DisplayName; }
}
```

- [ ] **Step 3: Implement each page as a focused `UserControl`**

Every page exposes `LoadFromDraft(SettingsDraft draft)`, `SaveToDraft(SettingsDraft draft)`, and `FocusControl(string controlKey)`. Use standard controls, `TableLayoutPanel`, system colors, and `Dock=Fill`. Providers uses `CheckedListBox` plus Up, Down, and Reset buttons; no drag-and-drop or custom paint.

- [ ] **Step 4: Run focused tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~SettingsPageTests
git add Settings KeeFetch.Tests/SettingsPageTests.cs KeeFetch.plgx.csproj
git commit -m "feat: add guided settings pages"
```

### Task 4: Replace the settings form with a single resizable tab host

**Files:**
- Rewrite: `SettingsForm.cs`
- Rewrite: `SettingsForm.Designer.cs`
- Modify: `SettingsForm.resx`
- Replace: `KeeFetch.Tests/SettingsFormLayoutTests.cs`

- [ ] **Step 1: Write failing form behavior tests**

Tests assert a `TabControl` with exactly `Overview`, `Downloads`, `Providers`, and `Advanced`; `AutoScaleMode.Font`; a nonzero `MinimumSize`; Save/Cancel buttons outside the tab control; Escape cancels; Save commits; Cancel does not; validation selects the correct tab and focuses the offending control.

- [ ] **Step 2: Implement the host form**

Constructor flow:

```csharp
public SettingsForm(Configuration config)
{
    this.config = config;
    draft = SettingsDraft.FromConfiguration(config);
    InitializeComponent();
    overviewPage.LoadFromDraft(draft);
    downloadsPage.LoadFromDraft(draft);
    providersPage.LoadFromDraft(draft);
    advancedPage.LoadFromDraft(draft);
}
```

On Save, call each page's `SaveToDraft`, validate once, present errors with `ErrorProvider` and a summary label, then apply and close with `DialogResult.OK`. On Cancel or close, never call `ApplyTo`.

- [ ] **Step 3: Replace coordinate-only layout tests**

Keep containment/overlap assertions, and add tests for tab names/order, tab traversal uniqueness, accessible names, minimum size, AutoScaleMode, long-description wrapping, and Save/Cancel draft semantics.

- [ ] **Step 4: Run tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~SettingsFormLayoutTests|FullyQualifiedName~SettingsPageTests|FullyQualifiedName~SettingsDraftTests"
git add SettingsForm.cs SettingsForm.Designer.cs SettingsForm.resx KeeFetch.Tests/SettingsFormLayoutTests.cs
git commit -m "feat: add guided native settings form"
```

### Task 5: Replace first-run disclosure with an explicit profile/privacy form

**Files:**
- Create: `FirstRunForm.cs`
- Create: `FirstRunForm.Designer.cs`
- Create: `FirstRunForm.resx`
- Create: `KeeFetch.Tests/FirstRunFormTests.cs`
- Modify: `KeeFetchExt.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing first-run tests**

Prove that no setting changes before confirmation, confirm persists the selected visible profile and `HasSeenFirstRunDisclosure`, Cancel aborts the download and leaves both unchanged, privacy text includes `domain` and excludes claims that credentials are transmitted, and the privacy profile choice is available when visible.

- [ ] **Step 2: Implement `FirstRunForm`**

Use a native modal form with profile radio buttons/list, plain-language third-party disclosure, a link to the website privacy page, Confirm and Cancel. Store only `SelectedProfileId` and `Confirmed`; do not write `Configuration` from inside the form.

- [ ] **Step 3: Replace `EnsureFirstRunDisclosure`**

In `KeeFetchExt`, show `FirstRunForm` before the first download. On Confirm, set `FetchProfileId`, then `HasSeenFirstRunDisclosure=true`; on Cancel return false and do not start `FaviconDialog`.

- [ ] **Step 4: Run tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~FirstRunFormTests
git add FirstRunForm* KeeFetchExt.cs KeeFetch.Tests/FirstRunFormTests.cs KeeFetch.plgx.csproj
git commit -m "feat: add guided first-run privacy choice"
```

### Task 6: Return structured results and stabilize progress text

**Files:**
- Modify: `FaviconDialog.cs`
- Create: `KeeFetch.Tests/FaviconDialogResultTests.cs`
- Modify: `KeeFetch.Tests/FaviconDialogTests.cs` if present

- [ ] **Step 1: Write failing aggregation/progress tests**

Extract pure internal helpers `BuildProgressText(BatchProgressSnapshot)` and `BuildBatchRunResult(...)`. Tests assert stable wording for running, cancelling, and cancelled states; exact count aggregation; diagnostic paths; and immutable outcome order matching input entry order.

- [ ] **Step 2: Change the execution contract**

Replace completion message display inside `FaviconDialog` with a returned `Task<BatchRunResult>` or an equivalent stored `Result` exposed after `RunAsync`. Keep KeePass `StatusUtil`; do not introduce a custom progress form.

Record one `BatchEntryOutcome` for every input entry, including skipped/invalid/cancelled cases. Export diagnostics once and attach paths to the result. Remove `ShowCompletionMessage()` after callers use the structured result.

- [ ] **Step 3: Stabilize status updates**

Only update the provider/action detail when it changes or when completed count advances. Primary status format is `Processed X of Y — updated U, skipped S, not found N, errors E`. Use `Cancelling…` after cancellation is requested and `Cancelled after X of Y` in the final snapshot.

- [ ] **Step 4: Run focused and full tests**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~FaviconDialogResultTests|FullyQualifiedName~FaviconDiagnosticsTests"
dotnet test KeeFetch.sln --no-restore
```

- [ ] **Step 5: Commit**

```powershell
git add FaviconDialog.cs KeeFetch.Tests
git commit -m "refactor: return structured favicon batch results"
```

### Task 7: Add the completion summary and bounded retry flow

**Files:**
- Create: `CompletionForm.cs`
- Create: `CompletionForm.Designer.cs`
- Create: `CompletionForm.resx`
- Create: `KeeFetch.Tests/CompletionFormTests.cs`
- Modify: `KeeFetchExt.cs`
- Modify: `KeeFetch.plgx.csproj`

- [ ] **Step 1: Write failing summary/action tests**

Tests assert displayed counts/profile/elapsed time; Retry enabled only when `RetryEntries.Count>0` and not cancelled; Open diagnostics enabled only for an existing path; Copy summary produces deterministic plain text; Close is the default safe action.

- [ ] **Step 2: Implement `CompletionForm`**

Use `TableLayoutPanel`, system colors, count labels, a partial-success explanation label, and standard buttons. Expose an enum result `Close` or `RetryEligible`. Opening diagnostics uses `ProcessStartInfo` with `UseShellExecute=true`; catch failures and show/copy the exact path without changing the completed result.

- [ ] **Step 3: Implement retry orchestration in `KeeFetchExt`**

After a batch, show `CompletionForm`. If Retry Eligible is selected, construct a new `FaviconDialog` with only `result.RetryEntries`, run once, and show a new completion result. Do not recurse indefinitely: after one retry, the next completion form hides Retry and instructs the user to inspect diagnostics for remaining misses.

- [ ] **Step 4: Run tests and commit**

```powershell
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj --no-restore --filter FullyQualifiedName~CompletionFormTests
git add CompletionForm* KeeFetchExt.cs KeeFetch.Tests/CompletionFormTests.cs KeeFetch.plgx.csproj
git commit -m "feat: add completion summary and bounded retry"
```

### Task 8: Run accessibility, DPI, PLGX, and full regression gates

**Files:**
- Create: `docs/validation/v1.3-ui-matrix.md`
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Add automated accessibility/layout assertions**

For every new form/control, verify non-empty accessible names, unique nonnegative tab indices among siblings, correct Accept/Cancel buttons, containment at 100/125/150/200 simulated scale factors where testable, and no provider/profile description clipping at the form minimum size.

- [ ] **Step 2: Run the complete automated gate**

```powershell
dotnet build KeeFetch.sln --configuration Release --no-restore -warnaserror
dotnet build KeeFetch.csproj --configuration Release --no-restore -p:LangVersion=5 -warnaserror
dotnet test KeeFetch.sln --configuration Release --no-build
```

Expected: zero warnings and failures.

- [ ] **Step 3: Execute the manual matrix**

Record pass/fail/evidence for 100%, 125%, 150%, and 200% DPI; normal and High Contrast; keyboard-only traversal; long text/paths; first install; v1.2 upgrade; Custom migration; cancellation; partial failure; retry; and success. Every failure receives an issue reference or is fixed before exit.

- [ ] **Step 4: Create and load-test PLGX**

Update CI staging to copy `FetchProfiles`, `Settings`, and `Batch` recursively. Extend `$expectedFiles` with `FetchProfiles\FetchProfileCatalog.cs`, `Settings\SettingsDraft.cs`, `Batch\BatchRunResult.cs`, `FirstRunForm.cs`, and `CompletionForm.cs`. Use the CI-equivalent PLGX staging flow, confirm every new source/resource appears in the manifest, install into a disposable portable KeePass instance, and verify Settings, First Run, Progress, Completion, and Retry load without compile/runtime errors.

- [ ] **Step 5: Commit validation evidence**

```powershell
git add docs/validation/v1.3-ui-matrix.md .github/workflows/build.yml KeeFetch.Tests
git commit -m "test: validate guided native plugin UX"
```
