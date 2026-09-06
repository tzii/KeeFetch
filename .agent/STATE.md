# KeeFetch current state

Status:  WORKING
Agent:   Hoplite

Focus:   Final merge-readiness corrections for PR #9: release-job permission isolation, trailing-root-dot host classification, and precise security/compatibility documentation.
Next:    Confirm the complete Windows CI gate on the updated PR head before the human merge decision.
Pointer: https://github.com/tzii/KeeFetch/pull/9
As-of:   2026-09-06 · final corrections verified locally; new-head Windows CI pending; do not merge or claim REVIEW from the older 3bdb271 check

Notes:
- Final pass: build/test/package now uses contents: read and non-persisted checkout credentials; only a tag-push release job has contents: write. Artifact checksums run on PRs; executable self-tests cover valid, tampered, missing, duplicate, malformed, and traversal cases. Root-dot classifier/transport regressions reproduced two failures before the fix; security subset now 23/23. README/CONTRIBUTING/CHANGELOG accurately describe post-response redirect filtering, DNS limitations, site-linked Privacy assets, TLS fallback, and unreleased profiles.
- Fresh local verification: official .NET SDK 8.0.424 + KeePass 2.60, production C# 5 and test builds -warnaserror 0/0; all 177 tests exercised in complementary Mono UI/non-UI partitions (175 pass, 2 fail). Untouched 3bdb271 produces the same failures (172/174): missing Mono Microsoft.VisualBasic implementation and libgdiplus PNG fixture rejection. Unpartitioned suites stall under Mono; the diagnostic run stopped at StalledResponseStream_BecomesProviderTimeoutWithinBudget, which passes in isolation. No tests were weakened or omitted from the partition comparison.
- Release-workflow self-tests, YAML permission/artifact/order validation, version/manifest gates, export -Check, 300-row corpus validation, and diff checks pass. Benchmark self-tests fail identically on this head and 3bdb271 under PowerShell 7 because they require Windows PowerShell JSON-array semantics; fingerprinted scripts remain unchanged. Original-head Windows CI is green but is not evidence for the new head.
- Linux tooling is now reproducible through .hoplite/settings.json and eng/setup-sandbox.sh. The platform setup lifecycle tool failed; the same script was verified through explicit shell execution. New-head CI verification remains required.
- The prior repository review is preserved on thread branch hoplite/ainos-d4393498 at 118759c. Preexisting diagnostics, destination-policy/DNS, image resource limits, and retry issues remain separate work; this PR must not claim to solve them.
- PR #8 (`codex/v1-3-guided-native-ux`, draft, CI green) remains the in-flight v1.3 UX work; its own `.agent/STATE.md` on that branch tracks the host-manual validation rows. It touches `.github/workflows/build.yml` (PLGX staging list) and will need a small merge with this branch's workflow edits.
- PRs #5 and #6 both rewrite `site/index.html` against a stale base and conflict with each other; one should be chosen and rebased, the other closed.
- Historical initial pass used another Linux sandbox: 166/173 tests (7 Mono failures). The fresh comparison above supersedes those local counts; the original PR head later passed 174/174 on Windows CI.
- Review provenance of the v1.3 study is unchanged: census labels are MACHINE review (`machine:antigravity-1.1.18/gemini-3.7-flash-high`), owner-approved; never present them as human review.
- Parked harness fixes remain in docs/benchmarks/v1.3-harness-followups.md.
