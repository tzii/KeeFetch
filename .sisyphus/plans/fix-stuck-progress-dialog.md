# Fix Stuck Progress Dialog - Threading/Timeout Issue

## TL;DR

> **Issue**: Progress dialog gets stuck even though icons download successfully. The `doneEvent.WaitOne()` in `FaviconDialog.cs` can hang indefinitely if a network request doesn't complete.
>
> **Deliverables**:
> - Add timeout to `doneEvent.WaitOne()` to prevent indefinite blocking
> - Add periodic progress updates to detect stalled operations
> - Ensure dialog closes even if some downloads hang
>
> **Estimated Effort**: Quick (15-30 minutes)
> **Parallel Execution**: NO - single sequential task
> **Critical Path**: Analyze issue → Implement fix → Test

---

## Context

### Original Request
User reports that after fixing the duplicate key bug, Android icons download successfully, but the progress dialog often gets stuck and doesn't close, even though the downloads complete.

### Root Cause Analysis

In `FaviconDialog.cs` (lines 95-155), the download logic uses:

1. **ThreadPool.QueueUserWorkItem** - Queues each entry download as a separate thread pool task
2. **ManualResetEvent (doneEvent)** - Signals when all downloads complete
3. **doneEvent.WaitOne()** (line 154) - Blocks until all threads signal completion

**The Problem:**
- If ANY single download hangs (network timeout not respected, DNS resolution hangs, etc.), that thread never reaches the `finally` block
- The `finally` block contains `Interlocked.Decrement(ref remaining)` and `doneEvent.Set()`
- If `remaining` never reaches 0, `doneEvent` is never signaled
- `doneEvent.WaitOne()` blocks indefinitely
- The dialog stays open forever

**Current timeout handling in FetchGooglePlayIcon:**
```csharp
request.Timeout = timeoutMs;  // This doesn't always work for all scenarios
request.ReadWriteTimeout = timeoutMs * 2;
```

HttpWebRequest.Timeout doesn't cover:
- DNS resolution delays
- SSL handshake hangs
- Proxy auto-detection hangs

### Why It Happens Now
Before the duplicate key fix, the `TypeInitializationException` caused downloads to fail fast. Now that downloads actually work, they occasionally encounter network conditions that cause hangs.

---

## Work Objectives

### Core Objective
Fix the stuck progress dialog by adding proper timeout handling to prevent `doneEvent.WaitOne()` from blocking indefinitely.

### Concrete Deliverables
- Modified `FaviconDialog.cs` with timeout-safe waiting
- Progress dialog that closes even if some downloads hang
- Better user feedback for stalled operations
- More accurate progress bar that reflects actual work completion
- Thread-safe progress updates to prevent race conditions

### Definition of Done
- [x] Progress dialog closes within reasonable time even if downloads hang
- [x] User can cancel operation if needed
- [x] Successfully downloaded icons are still saved
- [x] Build succeeds

### Must Have
- Timeout on `doneEvent.WaitOne()` to prevent indefinite blocking
- Periodic UI updates during the wait
- Graceful handling of incomplete downloads

### Must NOT Have (Guardrails)
- Do NOT change the download logic (keep ThreadPool approach)
- Do NOT remove the concurrent download capability
- Do NOT introduce new dependencies

---

## Execution Strategy

### Sequential Tasks (No Parallelization)
All tasks must run sequentially due to dependencies.

---

## TODOs

- [x] 1. Implement timeout fix in FaviconDialog.cs

  **What to do**:
  
  Modify the `BgWorker_DoWork` method in `FaviconDialog.cs` to add a timeout to prevent indefinite blocking.
  
  **Current code (around line 154):**
  ```csharp
  doneEvent.WaitOne();
  ```
  
  **Change to:**
  ```csharp
  // Wait with timeout and periodic UI updates to prevent dialog from appearing stuck
  int waitTimeoutMs = 1000; // 1 second intervals
  int maxTotalWaitMs = timeoutMs * 3 + (entries.Length * 100); // Generous timeout based on config
  int totalWaitedMs = 0;
  
  while (!doneEvent.WaitOne(waitTimeoutMs))
  {
      totalWaitedMs += waitTimeoutMs;
      
      // Check if user cancelled
      if (!logger.ContinueWork())
      {
          e.Cancel = true;
          break;
      }
      
      // Update UI to show we're still alive
      try
      {
          logger.SetText(string.Format(
              "Processed {0}/{1} — OK: {2}, Skipped: {3}, Not found: {4}, Errors: {5}",
              processedCount, totalCount, successCount, skippedCount, notFoundCount, errorCount),
              LogStatusType.Info);
      }
      catch { }
      
      // Safety timeout - force exit if we've waited too long
      if (totalWaitedMs >= maxTotalWaitMs)
      {
          lock (errorLogLock)
          {
              errorLog.Add(string.Format("Operation timed out after {0}ms. Some downloads may not have completed.", totalWaitedMs));
          }
          break;
      }
  }
  ```
  
  **Additional change needed:**
  The `timeoutMs` variable is not currently accessible in `BgWorker_DoWork`. You need to either:
  - Pass it as a parameter
  - Or calculate it from `config.Timeout * 1000`
  
  **Recommended approach** - Add at the beginning of `BgWorker_DoWork`:
  ```csharp
  int timeoutMs = config.Timeout * 1000;
  int maxTotalWaitMs = Math.Max(timeoutMs * 5, entries.Length * 500); // At least 500ms per entry
  ```
  
  **Must NOT do**:
  - Do NOT remove the ThreadPool approach
  - Do NOT change the concurrent download logic
  - Do NOT break the cancellation support

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None
  - **Reason**: Simple code modification with clear requirements

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: Task 2
  - **Blocked By**: None

  **References**:
  - `FaviconDialog.cs:154` - Location of `doneEvent.WaitOne()`
  - `FaviconDialog.cs:95-155` - Full threading context
  - `Configuration.cs` - Contains `Timeout` property (default 10 seconds)

  **Acceptance Criteria**:
  - [x] `doneEvent.WaitOne()` has timeout mechanism
  - [x] Progress dialog updates periodically during wait
  - [x] User cancellation is checked during wait
  - [x] Safety timeout prevents indefinite blocking
  - [x] Code compiles without errors

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Verify code changes**
  Tool: Read
  Preconditions: File has been edited
  Steps:
    1. Open `FaviconDialog.cs` 
    2. Find `doneEvent.WaitOne()` call
    3. Verify it's replaced with a timeout-based wait loop
    4. Verify timeout calculation uses `config.Timeout`
    5. Verify user cancellation is checked in the loop
  Expected Result: Code includes timeout mechanism with periodic UI updates

  **Commit**: NO (part of single atomic change)

---

- [x] 2. Fix progress bar accuracy and thread safety

  **What to do**:
  
  The current progress bar has accuracy issues due to race conditions. Multiple threads update progress simultaneously without proper synchronization.
  
  **Current issues:**
  1. Progress updates happen in finally blocks AFTER download completes (not during)
  2. Race condition: `processedCount` is incremented in threads, but percentage is calculated independently
  3. No tracking of active/in-progress downloads
  4. Progress text shows stale counts due to capture of closure variables
  
  **Fix in the finally block (around line 133-150):**
  
  **Current code:**
  ```csharp
  finally
  {
      Interlocked.Increment(ref processedCount);

      int pct = (int)(Interlocked.CompareExchange(ref processedCount, 0, 0) * 100.0 / totalCount);
      try { logger.SetProgress((uint)Math.Min(pct, 100)); } catch { }
      try
      {
          logger.SetText(string.Format(
              "Processed {0}/{1} — OK: {2}, Skipped: {3}, Not found: {4}, Errors: {5}",
              processedCount, totalCount, successCount, skippedCount, notFoundCount, errorCount),
              LogStatusType.Info);
      }
      catch { }

      if (Interlocked.Decrement(ref remaining) <= 0)
          doneEvent.Set();
  }
  ```
  
  **Problems with this code:**
  1. `processedCount` in format string captures the value at closure creation, not the current value
  2. No synchronization between SetProgress and SetText
  3. Percentage calculated separately from text update
  
  **Improved code:**
  ```csharp
  finally
  {
      // Atomically increment and get the new value
      int currentProcessed = Interlocked.Increment(ref processedCount);
      
      // Calculate percentage atomically
      int pct = (int)(currentProcessed * 100.0 / totalCount);
      uint progressValue = (uint)Math.Min(Math.Max(pct, 0), 100);
      
      // Get current counts atomically
      int currentSuccess = Interlocked.CompareExchange(ref successCount, 0, 0);
      int currentSkipped = Interlocked.CompareExchange(ref skippedCount, 0, 0);
      int currentNotFound = Interlocked.CompareExchange(ref notFoundCount, 0, 0);
      int currentErrors = Interlocked.CompareExchange(ref errorCount, 0, 0);
      
      // Update progress bar
      try { logger.SetProgress(progressValue); } catch { }
      
      // Update status text with accurate counts
      try
      {
          logger.SetText(string.Format(
              "Processed {0}/{1} ({2}%) — OK: {3}, Skipped: {4}, Not found: {5}, Errors: {6}",
              currentProcessed, totalCount, pct,
              currentSuccess, currentSkipped, currentNotFound, currentErrors),
              LogStatusType.Info);
      }
      catch { }

      // Signal completion
      if (Interlocked.Decrement(ref remaining) <= 0)
          doneEvent.Set();
  }
  ```
  
  **Additional improvement - Add at line 67 (after logger.SetProgress(0)):**
  Initialize with better initial message:
  ```csharp
  logger.SetText(string.Format(
      "Starting download for {0} entries...", totalCount),
      LogStatusType.Info);
  ```

  **Must NOT do**:
  - Do NOT change the threading model
  - Do NOT remove concurrent downloads
  - Do NOT add locks that could cause deadlocks

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None
  - **Reason**: Thread-safe code modifications

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential  
  - **Blocks**: Task 3
  - **Blocked By**: Task 1

  **References**:
  - `FaviconDialog.cs:133-150` - Finally block with progress updates
  - `Interlocked` class documentation for thread-safe operations

  **Acceptance Criteria**:
  - [x] Progress bar updates use atomic read of processed count
  - [x] Status text shows consistent values (not stale captured variables)
  - [x] Percentage is calculated once and used consistently
  - [x] All counter reads use `Interlocked.CompareExchange`
  - [x] Progress percentage is clamped between 0-100

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Verify thread-safe progress updates**
  Tool: Read
  Preconditions: File has been edited
  Steps:
    1. Open `FaviconDialog.cs`
    2. Find the finally block (around line 133)
    3. Verify `Interlocked.Increment` result is captured in a local variable
    4. Verify percentage calculation uses the local variable
    5. Verify all counter reads use `Interlocked.CompareExchange`
    6. Verify format string uses local variables, not captured closure variables
  Expected Result: Progress updates are thread-safe and accurate

  **Commit**: NO (part of single atomic change)

---

- [x] 3. Build the plugin

  **What to do**:
  Build the KeeFetch plugin to verify the fix compiles correctly.
  
  **Command**:
  ```bash
  dotnet build KeeFetch.csproj -c Release
  ```

  **Must NOT do**:
  - Do NOT build Debug configuration

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: Task 3
  - **Blocked By**: Task 1

  **Acceptance Criteria**:
  - [x] Build succeeds with 0 errors
  - [x] `KeeFetch.dll` exists in `bin\Release\`

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Verify build succeeds**
  Tool: Bash
  Preconditions: Code changes saved
  Steps:
    1. Run `dotnet build KeeFetch.csproj -c Release`
    2. Check exit code is 0
    3. Verify DLL exists
  Expected Result: Clean build

  **Commit**: NO (part of single atomic change)

---

- [x] 3. Test the fix

  **What to do**:
  Verify the dialog no longer gets stuck. Since this requires KeePass runtime, do a code review to ensure the logic is correct.
  
  **Verification checklist**:
  - [x] Timeout calculation is reasonable (at least 500ms per entry)
  - [x] UI updates happen every second
  - [x] User cancellation is respected
  - [x] Safety timeout prevents infinite wait

  **Must NOT do**:
  - Do NOT skip verification

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: None
  - **Blocked By**: Task 3

  **Acceptance Criteria**:
  - [x] Code review confirms timeout logic is correct
  - [x] Build succeeds
  - [x] No compilation warnings

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Code review**
  Tool: Read
  Preconditions: Build succeeded
  Steps:
    1. Review the timeout logic
    2. Verify timeout values are reasonable
    3. Check that all code paths handle completion properly
  Expected Result: Logic is sound and prevents indefinite blocking

  **Commit**: YES
  - Message: `fix(dialog): prevent stuck dialog and improve progress accuracy`
  - Files: `FaviconDialog.cs`
  - Pre-commit: Build succeeds

---

## Commit Strategy

| After Task | Message | Files | Verification |
|------------|---------|-------|--------------|
| 4 | `fix(dialog): prevent stuck dialog and improve progress accuracy` | FaviconDialog.cs | Build + Code Review |

---

## Success Criteria

### Verification Commands
```bash
# Build
dotnet build KeeFetch.csproj -c Release

# Code review
grep -A 20 "doneEvent.WaitOne" FaviconDialog.cs
```

### Final Checklist
- [x] Timeout added to prevent indefinite `doneEvent.WaitOne()` blocking
- [x] Periodic UI updates during wait
- [x] User cancellation respected
- [x] Safety timeout as fallback
- [x] Progress bar uses thread-safe atomic reads
- [x] Progress percentage calculation is accurate and consistent
- [x] Status text shows current values (not stale captured variables)
- [x] Build succeeds with 0 errors

---

## Technical Notes

### Why This Fix Works
The original code:
```csharp
doneEvent.WaitOne(); // Blocks FOREVER if any thread hangs
```

The fix:
```csharp
// Wait with timeout, check cancellation, update UI
while (!doneEvent.WaitOne(1000)) // 1 second timeout
{
    if (!logger.ContinueWork()) break; // Check cancellation
    // Update UI
    // Check safety timeout
}
```

This ensures:
1. **Responsiveness**: UI updates every second so dialog doesn't appear frozen
2. **Cancellability**: User can cancel even during the wait
3. **Safety**: Maximum wait time prevents infinite blocking
4. **Graceful degradation**: Even if some downloads hang, the dialog will close

### Timeout Calculation
```csharp
int maxTotalWaitMs = Math.Max(timeoutMs * 5, entries.Length * 500);
```
- Minimum 5x the per-download timeout
- OR 500ms per entry (whichever is larger)
- For 100 entries: max 50 seconds
- For 10 entries with 10s timeout: max 50 seconds

This is generous but prevents infinite hangs.

### Progress Accuracy Fix

**Original problem:**
```csharp
finally
{
    Interlocked.Increment(ref processedCount);
    int pct = (int)(Interlocked.CompareExchange(ref processedCount, 0, 0) * 100.0 / totalCount);
    // Problem: processedCount in format string is captured at closure creation time!
    logger.SetText(string.Format("...{0}...", processedCount), ...);
}
```

**Issues:**
1. Race condition between increment and percentage calculation
2. `processedCount` in format string captures stale value
3. Multiple atomic reads can get different values

**Fixed version:**
```csharp
finally
{
    // Single atomic increment + capture result
    int currentProcessed = Interlocked.Increment(ref processedCount);
    
    // Calculate percentage from captured value
    int pct = (int)(currentProcessed * 100.0 / totalCount);
    
    // Read all counters atomically for consistent display
    int currentSuccess = Interlocked.CompareExchange(ref successCount, 0, 0);
    // ... etc
    
    // Use captured values in format string
    logger.SetText(string.Format("...{0}...", currentProcessed), ...);
}
```

**Benefits:**
1. **Consistency**: Percentage and text use the same count value
2. **Accuracy**: No race between increment and read
3. **Clarity**: Shows percentage alongside counts
4. **Thread-safety**: All reads use `Interlocked` operations
