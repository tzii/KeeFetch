# KeeFetch current state

Status:  WORKING
Agent:   Codex

Focus:   Integrate PR #9 hardening (f252f8d) into the PR #8 guided-native UX branch and re-verify the combined Windows gate.
Next:    Run the complete combined gate and the agy adversarial review on the merged tree, then resume the real-host UI matrix.
Pointer: https://github.com/tzii/KeeFetch/pull/8
As-of:   2026-09-07 · Merge of origin/master into codex/v1-3-guided-native-ux in progress; only .agent state/handoff files conflicted and are resolved. Host-manual rows from the 2026-09-03 pass remain valid for the 7eea645 artifact only; they must be repeated on the combined artifact before release.

Notes:
- Preserved UX-branch history: real-host passes at 7eea645 (first run, persistence, 100% traversal, cancellation/mixed arithmetic, one bounded retry) and the persistence fix 1ab7379 remain described below and in docs/validation/v1.3-ui-matrix.md.
- Preserved master history: PR #9 merged as f252f8d with scoped TLS/certificate policy, widened private-host classification, read-only build job, tag-gated release job, and new CI gates.
- Full v1.3 release gaps are assessed in docs/validation/2026-09-07-v1.3-release-gaps.md on branch codex/v1-3-release-gap-audit.
- 2026-09-03 (Codex): Reconciled clean branch 7eea645 and green draft PR #8; owner host-verified first-run cancel/confirm persistence, Settings traversal/atomicity, cancellation arithmetic (71=22+2+3+44), mixed arithmetic (71=59+7+5, retry 10), and exactly-one retry (10=5+5). Remaining: keyboard-only traversal, 125/150/200% DPI, High Contrast, migrations, skipped-entry, full-success, long-path, cancellation-wording rows.
- 2026-08-28 (ZCode): Persistence defect fixed at 1ab7379 — AceCustomConfig writes are never flushed at exit; both commit points now persist via AppConfigSerializer.Save(Program.Config), failure-tolerant and logged. Three regressions added; gates 211/211.
- 2026-08-28 (Codex): Tab-caption literal-& defect fixed test-first at e0051fd (plain captions, Ctrl+Tab retained); 208/208 gates green.
- 2026-08-28 (Codex): Pre-PR review fixed four Important issues at d515aeb (first-run preview persistence, fault retryability, Custom hidden-gate mismatch, missing mnemonics); six regressions; 207/207.
