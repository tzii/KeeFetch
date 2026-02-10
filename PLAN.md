# KeeFetch Refactor Plan

This document describes every planned fix, the execution order, affected files, risks, and dependencies.

---

## Execution Order

Changes are grouped into **phases**. Each phase is safe to commit independently. Within a phase, items marked ⚡ can be done in parallel.

---

### Phase 1 — Small, behavior-preserving cleanups

These are low-risk, mechanical changes that don't alter control flow.

#### 1.1 ⚡ Remove dead code
- **File:** `KeeFetchExt.cs` line 73
- **What:** Delete the no-op `DropDownOpening += (s, e2) => { };` handler.

#### 1.2 ⚡ Convert public fields to properties
- **File:** `FaviconDownloader.cs` — `FaviconResult` class (lines 367–373)
  - `IconData`, `Status`, `Provider`, `Host` → auto-properties with `{ get; set; }`
- **File:** `IconProviders/DirectSiteProvider.cs` — `IconCandidate` class (lines 300–305)
  - `Url`, `Size`, `Priority` → auto-properties with `{ get; set; }`

#### 1.3 ⚡ Fix README documentation mismatch
- **File:** `README.md` line 11
- **What:** Change "MD5-hashes" to "SHA-256-hashes" to match the actual implementation in `Util.HashData()`.

#### 1.4 ⚡ Remove `FaviconStatus.Error`
- **File:** `FaviconDownloader.cs` — `FaviconStatus` enum (line 364)
- **What:** `Error` is the default but is never explicitly set or checked anywhere. The field default on `FaviconResult.Status` should become `FaviconStatus.NotFound` instead. Audit all call sites to confirm `Error` is unused.

---

### Phase 2 — Extract shared download logic (DRY providers)

#### 2.1 Create `IconProviders/IconProviderBase.cs`
- **What:** Abstract base class implementing `IIconProvider` with:
  - `protected byte[] DownloadBytes(string url, int timeoutMs, IWebProxy proxy)` — the shared HTTP download logic (request setup, UA, proxy, 512KB cap, `Util.IsValidImage` check).
  - Abstract `string Name { get; }` and abstract `byte[] GetIcon(...)`.
- **Preserve behavior exactly:** same UA string, same `AllowAutoRedirect = true`, same `ReadWriteTimeout = timeoutMs * 2`, same 512KB cap.

#### 2.2 Simplify each provider
- **Files:** `GoogleProvider.cs`, `DuckDuckGoProvider.cs`, `IconHorseProvider.cs`, `YandexProvider.cs`
- **What:** Each provider becomes ~10 lines: override `Name`, override `GetIcon` to build the URL and call `DownloadBytes(url, timeoutMs, proxy)`.
- **Estimated reduction:** ~120 lines of duplicated code removed.

#### 2.3 Update `KeeFetch.csproj`
- Add `<Compile Include="IconProviders\IconProviderBase.cs" />`.

---

### Phase 3 — Structured error handling

#### 3.1 Add an internal logger utility
- **New file:** `Logger.cs`
- **What:** Simple internal static class that collects categorized log entries (debug, warning, error) with timestamps. Not a full framework — just enough to replace bare `catch {}` blocks. Entries are surfaced in the completion dialog and error log file.
- **Shape:**
  ```csharp
  internal static class Logger
  {
      private static readonly List<LogEntry> entries = new List<LogEntry>();
      private static readonly object lockObj = new object();
      internal static void Warn(string context, Exception ex) { ... }
      internal static void Error(string context, Exception ex) { ... }
      internal static IReadOnlyList<LogEntry> GetEntries() { ... }
      internal static void Clear() { ... }
  }
  ```

#### 3.2 Replace bare `catch {}` blocks with categorized logging
- **Files (all occurrences):**
  | File | Lines | Context |
  |---|---|---|
  | `FaviconDownloader.cs` | 51 | `SetupTls()` — catch and log as warning |
  | `FaviconDownloader.cs` | 184 | Provider fallback loop — log provider name + exception type |
  | `FaviconDownloader.cs` | 257, 285, 314, 349 | Android icon paths — log package/domain + exception |
  | `FaviconDialog.cs` | 195–196 | Title/URL read — catch `InvalidOperationException` specifically |
  | `FaviconDialog.cs` | 354–361 | Reflection `Name` set — log as debug (expected on older KeePass) |
  | `FaviconDialog.cs` | 388 | `InvokeOnUI` — catch `ObjectDisposedException` specifically |
  | `FaviconDialog.cs` | 405 | UI update — catch `ObjectDisposedException` specifically |
  | `FaviconDialog.cs` | 430 | Error log file write — log to debug |
  | `DirectSiteProvider.cs` | 45, 67, 96, 135 | Download/parse failures — log as debug |
  | `AndroidAppMapper.cs` | 267 | Google Play fetch — log as debug |
  | `Util.cs` | 35, 79, 99, 115, 133, 222, 253, 263, 302 | Various parse failures — log as debug |
- **Rule:** Never swallow `OutOfMemoryException`, `StackOverflowException`, or `ThreadAbortException`. At minimum, catch `Exception ex` and pass to logger.

---

### Phase 4 — Thread-safety fix for certificate callback

#### 4.1 Make `SetupSelfSignedCerts` safer
- **File:** `FaviconDownloader.cs` lines 54–77
- **What:**
  - Store and restore the original callback using `Interlocked.CompareExchange` on a `static` field to avoid races with other plugins.
  - Wrap the callback assignment in a `lock` so concurrent `FaviconDialog` runs (if ever triggered) don't stomp each other.
  - Document the limitation: this is inherently process-global in .NET Framework.

---

### Phase 5 — Async/cancellation refactor

This is the highest-risk phase. Items 5.1 and 5.2 must be done together.

#### 5.1 Replace manual `int` flags with `CancellationTokenSource`
- **File:** `FaviconDialog.cs`
- **What:**
  - Remove `cancelled`, `workerDone`, `disposed` int fields and `IsCancelled()`/`IsDisposed()` methods.
  - Add a `CancellationTokenSource cts` field.
  - Pass `cts.Token` to `DoWork()` and propagate to each `ProcessEntry` call.
  - Cancel via `cts.Cancel()` when `logger.ContinueWork()` returns false.

#### 5.2 Replace `Application.DoEvents()` loop with `async Task`
- **File:** `FaviconDialog.cs` — `Run()` method
- **What:**
  - Rename `Run()` → `public async Task RunAsync()`.
  - Replace the manual `Thread` + `DoEvents` pump with:
    ```csharp
    var workTask = Task.Run(() => DoWork(cts.Token), cts.Token);
    while (!workTask.IsCompleted)
    {
        await Task.Delay(50);
        if (!logger.ContinueWork())
            cts.Cancel();
    }
    await workTask; // observe exceptions
    ```
  - Replace `CountdownEvent` + `ThreadPool.QueueUserWorkItem` with `Task.WhenAll` over a list of tasks, throttled by `SemaphoreSlim`.
- **File:** `KeeFetchExt.cs` — update callers
  - `RunDownload()` becomes `async void` (WinForms event handler pattern).
  - Wrap `await dialog.RunAsync()` in try/catch to prevent unobserved exceptions from crashing KeePass.

#### 5.3 Pass `CancellationToken` through to providers
- **Files:** `IIconProvider.cs`, all provider implementations, `FaviconDownloader.cs`
- **What:** Add optional `CancellationToken` parameter to `IIconProvider.GetIcon()` and `FaviconDownloader.Download()`. Use `token.ThrowIfCancellationRequested()` at key checkpoints. Wire `token` into `HttpWebRequest` via `token.Register(() => request.Abort())`.

---

### Phase 6 — Code quality improvements

#### 6.1 ⚡ Add XML doc comments
- **Files:** All public/internal classes and methods.
- **Priority targets:**
  - `IIconProvider` interface
  - `FaviconDownloader.Download()`
  - `Configuration` properties (explain default values and valid ranges)
  - `Util` static methods
  - `AndroidAppMapper.MapToWebDomain()`, `IsAndroidUrl()`
- **Style:** Brief `<summary>` only. No `<param>` unless non-obvious.

#### 6.2 ⚡ Migrate to SDK-style `.csproj`
- **File:** `KeeFetch.csproj`
- **What:** Replace legacy format with:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <TargetFramework>net48</TargetFramework>
      <OutputType>Library</OutputType>
      <RootNamespace>KeeFetch</RootNamespace>
      <AssemblyName>KeeFetch</AssemblyName>
      <UseWindowsForms>true</UseWindowsForms>
    </PropertyGroup>
  </Project>
  ```
- SDK-style auto-discovers `*.cs` files, eliminating manual `<Compile>` entries.
- **Risk:** PLGX build uses KeePass's own compiler, which may not fully support SDK-style. Test PLGX generation in CI after migration. If it breaks, keep legacy format and note why.

#### 6.3 ⚡ Add `CHANGELOG.md`
- **File:** New `CHANGELOG.md`
- **What:** Retroactive v1.0.0 entry plus a v1.1.0 section for this refactor.

---

### Phase 7 — Test project

#### 7.1 Create test project
- **New directory:** `KeeFetch.Tests/`
- **New file:** `KeeFetch.Tests/KeeFetch.Tests.csproj` — targeting `net48`, referencing `KeeFetch.csproj`, using MSTest or NUnit (whichever the .NET Framework ecosystem supports best without extra tooling).
- **What to add to solution:** Reference from a `.sln` file (create if one doesn't exist).

#### 7.2 Unit tests for `Util`
- `HashData` — verify 16-byte output, deterministic, different input → different output
- `ExtractHost` — with/without scheme, with port, malformed URLs, null/empty
- `ExtractHostWithPort` — default vs non-default ports
- `ExtractScheme` — http, https, no scheme, malformed
- `IsPrivateHost` — localhost, .local, .lan, RFC 1918 IPv4, IPv6 link-local, public IPs, public domains
- `GuessDomainFromTitle` — bare word → `.com`, URL-like strings pass through, internal-looking names → null
- `NormalizeUrl` — various edge cases
- `ResizeImage` — null, empty, valid PNG, oversized, already-small-enough
- `IsValidImage` — valid PNG bytes, garbage bytes, too-small data

#### 7.3 Unit tests for `AndroidAppMapper`
- `IsAndroidUrl` — positive and negative cases
- `GetPackageName` — valid URLs, edge cases (trailing slash, extra path)
- `MapToWebDomain` — known mappings, unknown packages (fallback to guess)
- `TryGuessFromPackage` — com.example → example.com, org.foo → foo.org, too few parts → null

#### 7.4 Unit tests for `DirectSiteProvider` HTML parsing
- Extract `ParseIconLinks` to be testable (currently private). Options:
  - Make it `internal` and use `[InternalsVisibleTo]`
  - Or test indirectly through a mock HTTP layer
- Test cases: apple-touch-icon, shortcut icon, favicon with sizes attribute, og:image, base tag, comments/scripts stripped, relative URLs resolved

#### 7.5 Unit tests for `Configuration`
- Mock `AceCustomConfig` or test with a real instance
- Verify default values, clamping on `Timeout` (5–60), prefix persistence

#### 7.6 Update CI
- **File:** `.github/workflows/build.yml`
- Add a test step: `dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj` after the build step.

---

## Dependency Graph

```
Phase 1 (cleanups)          — no dependencies, do first
    ↓
Phase 2 (DRY providers)    — independent of Phase 3–5
    ↓
Phase 3 (error handling)   — independent of Phase 2, but easier after Phase 2
    ↓
Phase 4 (cert thread-safety) — independent
    ↓
Phase 5 (async/cancellation) — depends on Phase 1.2 (properties), benefits from Phase 3
    ↓
Phase 6 (quality)           — do after APIs stabilize (Phase 5)
    ↓
Phase 7 (tests)             — do last, tests target final API shape
```

## Not in Scope (and why)

| Item | Reason |
|---|---|
| ICO multi-resolution parsing | Requires a dedicated ICO parser library or significant new code; KeePass's `GfxUtil.LoadImage` already handles basic ICO. Low ROI. |
| Full i18n/localization | KeePass plugin ecosystem doesn't have a standard localization pattern. Would add complexity for minimal user benefit given the plugin's scope. |
| Retry with exponential backoff | The fallback chain (5 providers) already serves as a retry mechanism across different sources. Adding per-provider retry would increase total timeout significantly. |
| Migration to `HttpClient` | .NET Framework 4.8's `HttpClient` has footgun lifetime issues. `HttpWebRequest` is fine here. Could revisit if targeting .NET 6+. |
