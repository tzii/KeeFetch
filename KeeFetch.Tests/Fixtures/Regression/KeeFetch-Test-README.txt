KeeFetch Test Database
======================

Database: KeeFetch-Test-Database.kdbx
Password: keefetch-test
Format: KeePass KDBX 3.1 (AES-256-CBC + AES-KDF, GZip)
Entries: 71
Generated: 2026-08-11

IMPORTANT
---------
- Contains NO real credentials. All usernames/passwords are fake test values.
- This is intentionally disposable. KeeFetch should modify entry custom icons.
- The KDBX file was independently decrypted and block/hash-validated after generation.

Suggested test passes
---------------------
1. Open database with password: keefetch-test
2. Tools -> KeeFetch -> Settings...
3. Enable default resolver/fallback behavior.
4. Test one item from "01 Happy Paths" with the entry context menu.
5. Run a group fetch on "03 Android App URLs".
6. Run a group fetch on "06 Issue 1 Regression Corpus".
7. Enable "skip entries that already have custom icons" and run "07 Existing Custom Icon".
   - "HAS CUSTOM ICON — should skip" begins with a purple KF custom icon.
   - "NO CUSTOM ICON — should fetch" should be updated.
8. Run "08 Bulk / Concurrency" to exercise parallel fetching.
9. Inspect "05 Deduplication" after fetching; repeated domains should not create unnecessary duplicate icon payloads.
10. Inspect malformed/empty entries in "04 REF & Edge Cases"; they should fail/skip without crashing.

Special cases included
----------------------
- Exact 23-URL Issue #1 regression corpus from the KeeFetch repository.
- KeePass field reference URL: {REF:A@I:46C9B1FFBD4ABC4BBB260C6190BAD20C}
- androidapp:// known mappings + nonexistent package.
- Scheme-less URLs, deep paths, queries, HTTP, localhost, malformed URLs, mailto/file URLs.
- Duplicate-domain entries.
- Pre-existing custom icon skip test.
- 12-entry concurrency group.

Manifest
--------
KeeFetch-Test-Manifest.csv lists every fixture and the intended behavior.
KeeFetch-Test-Database.xml is included as a plaintext inspection/recovery copy; do NOT use it for real secrets.

Validation details
------------------
KDBX bytes: 6382
Decrypted XML bytes: 96563
Hashed data blocks: 1
