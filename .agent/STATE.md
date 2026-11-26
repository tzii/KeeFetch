# KeeFetch current state

Status: WORKING
Agent: Claude

Focus: v1.4 launch polish - privacy, update-check, undo and UI fixes so KeeFetch can be announced (keepass.info, SourceForge forum, awesome-keepass, r/KeePass, AlternativeTo).
Next: Get CI green on the v1.4 launch-polish PR against master.
Pointer: branch claude/v1-4-launch-polish (worktree artifacts/v1-4, based on master 4396ded)
As-of: 2026-11-26 Europe/Rome

Notes:
- Verified against GitHub: v1.3.0 is the latest release; master 4396ded; PRs #19-#22 merged; only dependabot PR #23 open. The previous state (repository cleanup, PR20) is complete.
- Owner decisions (2026-11-26): ship only Balanced, Privacy and Custom (retire Fast and Precise); Balanced stays the default; measure stored icon size before changing MaxIconSize; skip entries with existing custom icons by default.
- Plan source: two-round design review with Gemini 4 Neon, every code claim verified on master; see the v1.4 entry in CHANGELOG.md as items land.
- Implementation complete, committed and pushed for PR (2026-11-26, owner go-ahead): Fast/Precise retired with migration to Balanced (schema 2), radio first-run dialog, skip-existing on by default for group/database runs, Recycle Bin excluded, entry history + undo run, in-memory redacted diagnostics with explicit export, KeePass version.txt format, pixel-flood guard, 429/503 backoff fix, KeePass message/window integration.
- Icon size measured on the 300-site corpus: 128 px ~2x the bytes of 64 px (~2 KiB per entry); MaxIconSize stays 128.
- PR #24 first CI run failed one DPI test: on the runner's small screen the results window is width-capped at 200% and the action buttons overflowed; fixed by moving the main actions to a second row when they do not fit (regression test added).
- Local gates green: Release build -warnaserror, 272/272 MSTest, benchmark harness self-tests, export -Check, check-version (-Tag v1.4.0), site sync/verify, release-workflow test, git diff --check; check-plgx-manifest passes once the five new files are tracked. CI pending on PR #24 (https://github.com/tzii/KeeFetch/pull/24).
- Release-time follow-ups: profiles-v1.4.json site snapshot, release.json version/URLs/SHA, profiles/privacy prose and screenshots, CHANGELOG date; tag v1.4.0 right after merge because master's version.txt advertises 1.4.0 to KeePass.
- GLM 5.4 review (2026-11-26) verified and fixed: undo no longer leaves icon deletion records and new icons get a modification time (sync could strip re-downloaded icons); IsValidImage checks size before decoding; Custom hint corrected; retired-profile notice persisted until confirmed; label accessible names; cmd:// and file addresses redacted to their scheme. Site finding rejected: pages consistently render the v1.3 snapshot until release. DeepSeek V4.1 review produced nothing (stopped, then free quota exhausted).
