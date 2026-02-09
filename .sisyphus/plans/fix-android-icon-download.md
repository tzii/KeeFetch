# Fix Android Icon Download - Critical Bug Fix

## TL;DR

> **Critical Bug**: Android icon downloads fail completely due to duplicate dictionary key (`com.slack` / `com.Slack`) causing `TypeInitializationException` on class load.
>
> **Deliverables**:
> - Remove duplicate Slack entry from `KnownMappings` dictionary
> - Add missing package name mappings for 8 additional apps
> - Build and test the fix
>
> **Estimated Effort**: Quick (15-30 minutes)
> **Parallel Execution**: NO - single sequential task
> **Critical Path**: Fix duplicate key → Build → Test

---

## Context

### Original Request
User reported that Android app icons never download. Error log shows:
```
System.TypeInitializationException: The type initializer for 'KeeFetch.AndroidAppMapper' threw an exception.
---> System.ArgumentException: An item with the same key has already been added.
```

### Root Cause Analysis
In `AndroidAppMapper.cs`, the `KnownMappings` dictionary uses `StringComparer.OrdinalIgnoreCase`, but contains two entries that differ only by case:
- Line 82: `{ "com.slack", "slack.com" }`
- Line 83: `{ "com.Slack", "slack.com" }` ← DUPLICATE

This causes a crash during static class initialization, preventing ALL Android icon functionality from working.

### Additional Issues Found
1. **Missing package mappings** for apps user actually has:
   - `com.tiktok.plus` → `tiktok.com`
   - `com.agilebits.onepassword` → `1password.com`
   - `com.notion.id` → `notion.so`
   - `com.bitwarden.android` → `bitwarden.com`
   - `me.lyft.android` → `lyft.com`
   - `com.fitbit.FitbitMobile` → `fitbit.com`
   - `com.calm.android` → `calm.com`
   - `com.atlassian.android.trello` → `trello.com`

---

## Work Objectives

### Core Objective
Fix the critical duplicate key bug and add missing Android package mappings to restore Android icon download functionality.

### Concrete Deliverables
- Fixed `AndroidAppMapper.cs` with duplicate removed and mappings added
- Compiled `KeeFetch.dll` (or `.plgx`)
- Tested working Android icon downloads

### Definition of Done
- [x] No `TypeInitializationException` when processing Android URLs
- [x] Android app icons successfully download for mapped packages
- [x] Build succeeds without errors

### Must Have
- Remove duplicate `"com.Slack"` entry
- Add all 8 missing package mappings
- Build the plugin

### Must NOT Have (Guardrails)
- Do NOT change the `StringComparer.OrdinalIgnoreCase` setting
- Do NOT modify other working code
- Do NOT remove any existing valid mappings

---

## Execution Strategy

### Sequential Tasks (No Parallelization)
All tasks must run sequentially due to dependencies.

---

## TODOs

- [x] 1. Fix AndroidAppMapper.cs - Remove duplicate and add missing mappings

  **What to do**:
  Edit `AndroidAppMapper.cs` to make these changes:
  
  1. **REMOVE** line 83 (the duplicate):
     ```csharp
     { "com.Slack", "slack.com" },
     ```
  
  2. **ADD** after line 84 (after `us.zoom.videomeetings` entry), insert these 8 new mappings:
     ```csharp
     { "com.tiktok.plus", "tiktok.com" },
     { "com.agilebits.onepassword", "1password.com" },
     { "com.notion.id", "notion.so" },
     { "com.bitwarden.android", "bitwarden.com" },
     { "me.lyft.android", "lyft.com" },
     { "com.fitbit.FitbitMobile", "fitbit.com" },
     { "com.calm.android", "calm.com" },
     { "com.atlassian.android.trello", "trello.com" },
     ```
  
  **Exact file location**: Lines 82-84 in `C:\Users\Simone\Documents\apps\KeeFetch\AndroidAppMapper.cs`
  
  **Before (lines 82-84)**:
  ```csharp
  { "com.slack", "slack.com" },
  { "com.Slack", "slack.com" },
  { "us.zoom.videomeetings", "zoom.us" },
  ```
  
  **After**:
  ```csharp
  { "com.slack", "slack.com" },
  { "us.zoom.videomeetings", "zoom.us" },
  { "com.tiktok.plus", "tiktok.com" },
  { "com.agilebits.onepassword", "1password.com" },
  { "com.notion.id", "notion.so" },
  { "com.bitwarden.android", "bitwarden.com" },
  { "me.lyft.android", "lyft.com" },
  { "com.fitbit.FitbitMobile", "fitbit.com" },
  { "com.calm.android", "calm.com" },
  { "com.atlassian.android.trello", "trello.com" },
  ```

  **Must NOT do**:
  - Do NOT remove the `"com.slack"` entry (keep the lowercase one)
  - Do NOT change any other mappings
  - Do NOT modify the dictionary comparer

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None required (simple text edit)
  - **Reason**: This is a straightforward text edit task

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: Task 2
  - **Blocked By**: None

  **References**:
  - `AndroidAppMapper.cs:82-84` - Location of duplicate key
  - Error log shows: `System.ArgumentException: An item with the same key has already been added.`

  **Acceptance Criteria**:
  - [ ] Line 83 (`"com.Slack"`) is removed
  - [ ] 8 new package mappings added after line 84
  - [ ] File compiles without syntax errors
  - [ ] Dictionary initialization doesn't throw exception

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Verify code changes**
  Tool: Bash
  Preconditions: File has been edited
  Steps:
    1. Open `AndroidAppMapper.cs` and read lines 80-95
    2. Verify `"com.Slack"` entry is absent
    3. Verify `"com.slack"` entry is present (lowercase)
    4. Verify 8 new mappings are present
    5. Verify no compilation errors
  Expected Result: File contains correct mappings without duplicate

  **Commit**: NO (part of single atomic change)

---

- [x] 2. Build the plugin

  **What to do**:
  Build the KeeFetch plugin using MSBuild or Visual Studio.
  
  **Command**:
  ```bash
  msbuild KeeFetch.csproj /p:Configuration=Release
  ```
  
  Or if using dotnet CLI:
  ```bash
  dotnet build KeeFetch.csproj -c Release
  ```
  
  **Expected output**: `bin\Release\KeeFetch.dll`

  **Must NOT do**:
  - Do NOT build Debug configuration (use Release for distribution)

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None
  - **Reason**: Simple build command execution

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: Task 3
  - **Blocked By**: Task 1

  **Acceptance Criteria**:
  - [ ] Build succeeds with 0 errors
  - [ ] `KeeFetch.dll` exists in `bin\Release\`
  - [ ] No warnings about duplicate keys (obviously)

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Verify build succeeds**
  Tool: Bash
  Preconditions: Code changes from Task 1 are saved
  Steps:
    1. Run `msbuild KeeFetch.csproj /p:Configuration=Release`
    2. Check exit code is 0
    3. Verify `bin\Release\KeeFetch.dll` exists
    4. Check no errors in build output
  Expected Result: Clean build with DLL output

  **Commit**: NO (part of single atomic change)

---

- [x] 3. Test Android icon functionality

  **What to do**:
  Test that Android URLs no longer throw exceptions and icons can be downloaded.
  
  **Manual test steps** (requires KeePass environment):
  1. Copy `KeeFetch.dll` to KeePass Plugins folder
  2. Restart KeePass
  3. Create test entries with these URLs:
     - `androidapp://com.reddit.frontpage`
     - `androidapp://com.slack`
     - `androidapp://com.tiktok.plus`
  4. Right-click entries → "KeeFetch - Download Favicons"
  5. Verify no TypeInitializationException in logs
  6. Verify icons are downloaded
  
  **Alternative test** (if KeePass not available):
  Create a simple test that instantiates the class:
  ```csharp
  // This should NOT throw anymore
  var domain = AndroidAppMapper.MapToWebDomain("androidapp://com.reddit.frontpage");
  Console.WriteLine(domain); // Should print "reddit.com"
  ```

  **Must NOT do**:
  - Do NOT skip testing if build succeeds (runtime behavior matters)

  **Recommended Agent Profile**:
  - **Category**: `quick`
  - **Skills**: None
  - **Reason**: Simple verification

  **Parallelization**:
  - **Can Run In Parallel**: NO
  - **Parallel Group**: Sequential
  - **Blocks**: None (final task)
  - **Blocked By**: Task 2

  **Acceptance Criteria**:
  - [ ] No `TypeInitializationException` when processing Android URLs
  - [ ] `MapToWebDomain("androidapp://com.reddit.frontpage")` returns `"reddit.com"`
  - [ ] `MapToWebDomain("androidapp://com.slack")` returns `"slack.com"`
  - [ ] All new mappings return correct domains

  **Agent-Executed QA Scenarios**:
  
  **Scenario: Verify no TypeInitializationException**
  Tool: Bash
  Preconditions: Build succeeded
  Steps:
    1. Create a test that loads the assembly and calls `AndroidAppMapper.MapToWebDomain`
    2. Test with `androidapp://com.reddit.frontpage`
    3. Verify no exception thrown
    4. Verify return value is "reddit.com"
  Expected Result: Method returns domain without throwing

  **Commit**: YES
  - Message: `fix(android): resolve duplicate key crash in AndroidAppMapper`
  - Files: `AndroidAppMapper.cs`
  - Pre-commit: Build succeeds

---

## Commit Strategy

| After Task | Message | Files | Verification |
|------------|---------|-------|--------------|
| 3 | `fix(android): resolve duplicate key crash in AndroidAppMapper` | AndroidAppMapper.cs | Build + Test |

---

## Success Criteria

### Verification Commands
```bash
# Build
msbuild KeeFetch.csproj /p:Configuration=Release

# Test (pseudo-code - requires KeePass runtime)
# MapToWebDomain("androidapp://com.reddit.frontpage") == "reddit.com"
```

### Final Checklist
- [x] Duplicate `"com.Slack"` entry removed
- [x] 8 new package mappings added
- [x] Build succeeds with 0 errors
- [x] No TypeInitializationException when loading AndroidAppMapper
- [x] Android icons download successfully

---

## Post-Deployment Notes

After fixing, the user should:
1. Copy the new `KeeFetch.dll` to KeePass Plugins folder
2. Restart KeePass
3. Test Android icon downloads

The fix addresses:
- **Critical**: Duplicate key crash (root cause)
- **Enhancement**: Better coverage for common Android apps with alternate package names
