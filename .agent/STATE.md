# KeeFetch current state

Status:  REVIEW
Agent:   Hoplite

Focus:   Repository hardening pass on master: scope self-signed-cert acceptance to KeeFetch's HttpClient (no ServicePointManager mutation), stop forcing TLS 1.0/1.1 process-wide, close private-address/redirect gaps, add fail-closed CI gates (PLGX manifest, version/tag), least-privilege workflow token, SHA-pinned release action, SHA256SUMS release asset, Dependabot, and bring CHANGELOG/README/CONTRIBUTING back in line with the shipped profile catalog.
Next:    Human review and merge decision on the hardening PR; then rebase PR #8 (guided-native UX) onto master — its `KeeFetch.plgx.csproj` additions will now be verified by `eng/check-plgx-manifest.ps1`.
Pointer: hoplite/kalchedon-1ba50bf1 (PR opened from this branch)
As-of:   2026-09-06 · branched from master 462fb25

Notes:
- PR #8 (`codex/v1-3-guided-native-ux`, draft, CI green) remains the in-flight v1.3 UX work; its own `.agent/STATE.md` on that branch tracks the host-manual validation rows. It touches `.github/workflows/build.yml` (PLGX staging list) and will need a small merge with this branch's workflow edits.
- PRs #5 and #6 both rewrite `site/index.html` against a stale base and conflict with each other; one should be chosen and rebased, the other closed.
- Gates on this branch (Linux sandbox, Mono runtime): Release build 0/0 with `-p:LangVersion=5 -warnaserror`; MSTest 166/173 — the 7 failures are identical on unmodified master under Mono (WinForms layout metrics + libgdiplus 1x1 PNG) and pass on Windows CI; `eng/check-plgx-manifest.ps1` and `eng/check-version.ps1` green (negative cases verified to fail); `git diff --check` clean. Harness self-tests and `export-profile-data.ps1 -Check` need Windows/KeePass and were not run here.
- Review provenance of the v1.3 study is unchanged: census labels are MACHINE review (`machine:antigravity-1.1.18/gemini-3.7-flash-high`), owner-approved; never present them as human review.
- Parked harness fixes remain in docs/benchmarks/v1.3-harness-followups.md.
