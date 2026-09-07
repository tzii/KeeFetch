# KeeFetch current state

Status:  WORKING
Agent:   Codex

Focus:   Expanded v1.3 provider study: owner selected the complete 19-candidate rerun.
Next:    Freeze launch fingerprints, run the clean expanded matrix, then review its full cold-artifact census.
Pointer: https://github.com/tzii/KeeFetch/pull/8
As-of:   2026-09-07 · GitHub confirms PR #8 OPEN/DRAFT at 18d49093d10e0dd819eea8a21e52cad4cc91f6ac with green CI 34160791331; master is f252f8d; latest release v1.2.0 and no v1.3.0 tag. Merge f21180f verified (227/227 tests, CI green, PLGX CDDCD67A loaded in KeePass 2.60). agy adversarial review (gemini-3.8-flash-high) returned 10 findings; 8 verified and fixed at this head with 7 new regressions (234/234). Diagnostics redaction and pwsh benchmark-harness semantics remain documented separate follow-ups.

Notes:
- Preserved UX-branch history: real-host passes at 7eea645 (first run, persistence, 100% traversal, cancellation/mixed arithmetic, one bounded retry) and the persistence fix 1ab7379 remain described below and in docs/validation/v1.3-ui-matrix.md.
- Preserved master history: PR #9 merged as f252f8d with scoped TLS/certificate policy, widened private-host classification, read-only build job, tag-gated release job, and new CI gates.
- Full v1.3 release gaps are assessed in docs/validation/2026-09-07-v1.3-release-gaps.md on branch codex/v1-3-release-gap-audit.
- 2026-09-03 (Codex): Reconciled clean branch 7eea645 and green draft PR #8; owner host-verified first-run cancel/confirm persistence, Settings traversal/atomicity, cancellation arithmetic (71=22+2+3+44), mixed arithmetic (71=59+7+5, retry 10), and exactly-one retry (10=5+5). Remaining: keyboard-only traversal, 125/150/200% DPI, High Contrast, migrations, skipped-entry, full-success, long-path, cancellation-wording rows.
- 2026-08-28 (ZCode): Persistence defect fixed at 1ab7379 — AceCustomConfig writes are never flushed at exit; both commit points now persist via AppConfigSerializer.Save(Program.Config), failure-tolerant and logged. Three regressions added; gates 211/211.
- 2026-08-28 (Codex): Tab-caption literal-& defect fixed test-first at e0051fd (plain captions, Ctrl+Tab retained); 208/208 gates green.
- 2026-08-28 (Codex): Pre-PR review fixed four Important issues at d515aeb (first-run preview persistence, fault retryability, Custom hidden-gate mismatch, missing mnemonics); six regressions; 207/207.
