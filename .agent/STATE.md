# KeeFetch current state

Status:  WORKING
Agent:   ZCode

Focus:   Machine census labeling complete (owner-approved amendment): all 456 units labeled by agy/antigravity 1.1.18 gemini-3.7-flash-high with pixel ground truth and guardrails; awaiting owner spot-check of `machine-review/spot-check.html` before the labels are copied into the real review queue and `-Validate` runs.
Next:    On owner OK: copy `machine-review/review-queue.machine.csv` over `eng/benchmark-runs/profile-candidates-v13/review-queue.csv`, run `prepare-review.ps1 -Validate`, then handoff-2026-08-17.md item 8 (selector dry-run, winners + ambiguity replay, `-Publish`, export + `-Check`, docs incl. machine-review methodology disclosure, PR body refresh, mark PR #4 ready).
Pointer: https://github.com/tzii/KeeFetch/pull/4
As-of:   2026-08-22 · machine-review committed

Notes:
- Methodology amendment recorded in `machine-review/PILOT-REPORT.md` + prompts: dual-lane pilot (gemini-3.7-flash-high vs independent vision API, 70% exact agreement, all 3 hard contradictions pixel-arbitrated in Gemini's favor), then owner said go with agy as the labeler.
- Full run: 328 unique hashes in 14 batches of 24, deterministic pixel stats embedded in manifests, labels per (fixture,hash) unit, same-hash+same-domain consistency enforced, reviewer column = `machine:antigravity-1.1.18/gemini-3.7-flash-high`; 14/14 batches OK, collector clean.
- Label distribution: correct 407, acceptable-synthetic 22, generic 17, wrong-brand 6 (Google-G on Gmail/Maps packages, Disney icon on Hulu), ambiguous 3 (neverssl banner), blank 1.
- The real `review-queue.csv` in the evidence root stays all `not-reviewed` until the owner confirms the spot-check; machine labels are never presented as human review.
- Parallel session's human-labeling aid kept: `eng/benchmark-runs/profile-candidates-v13/review-kit/index.html` (commit e74a343, offline contact sheet + CSV export, gate unchanged).
- Monitor automation-07cd5199 deleted 2026-08-22; no automated relaunch/queue-rewrite risk.
