# Fix Android Icon Download - Learnings

## 2026-02-09 - Task Completed

### Root Cause
The `AndroidAppMapper` class had a duplicate dictionary key that caused a `TypeInitializationException` during static initialization:
- Line 82: `{ "com.slack", "slack.com" }`
- Line 83: `{ "com.Slack", "slack.com" }` ← DUPLICATE

Since the dictionary uses `StringComparer.OrdinalIgnoreCase`, these are treated as the same key.

### Fix Applied
1. Removed duplicate `"com.Slack"` entry
2. Added 8 new package mappings for apps the user has:
   - `com.tiktok.plus` → tiktok.com
   - `com.agilebits.onepassword` → 1password.com
   - `com.notion.id` → notion.so
   - `com.bitwarden.android` → bitwarden.com
   - `me.lyft.android` → lyft.com
   - `com.fitbit.FitbitMobile` → fitbit.com
   - `com.calm.android` → calm.com
   - `com.atlassian.android.trello` → trello.com

### Build Status
- Build succeeded with 0 warnings, 0 errors
- Output: `bin\Release\KeeFetch.dll`

### Key Insight
Dictionary initialization exceptions in C# static constructors are particularly nasty because:
1. They throw `TypeInitializationException` which wraps the real exception
2. They happen at class load time, not method call time
3. The error message "An item with the same key has already been added" is clear, but locating the duplicate in a large dictionary requires careful inspection

### Verification
- Confirmed no duplicate keys remain using grep
- Build completed successfully
- Static class can now initialize without throwing

