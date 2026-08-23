# KeeFetch current state

Status:  DONE
Agent:   Codex

Focus:   PR #4 merged into master as e2bee2d (2026-08-23) after the completed review pass: the reviewed head de4f871 was mergeable with CI green, all local gates had been re-verified, and all four published winners reproduced end-to-end.
Next:    None - this focus is complete. Parked harness fixes for a future study remain in docs/benchmarks/v1.3-harness-followups.md.
Pointer: https://github.com/tzii/KeeFetch/pull/4
As-of:   2026-08-23 · merged as e2bee2d

Notes:
- Review provenance: census labels are MACHINE review (`machine:antigravity-1.1.18/gemini-3.7-flash-high`), owner-approved after the dual-lane pilot + pixel arbitration and an owner spot-check; disclosed in `docs/benchmarks/v1.3-provider-study.md` and `machine-review/`. Never present them as human review.
- Winners: bulk-fast -> cand-direct-google-twenty-fast; everyday -> cand-full-minus-favicone-thorough-synth; privacy -> cand-direct-only-balanced; max-coverage -> cand-direct-yandex-balanced; all stable under ambiguity replay after the 3 pub-216 units were resolved to `unusable` (focused re-ask, live-verified).
- Gates at this state: MSTest 161/161, Release build -warnaserror 0/0, harness self-tests green, export -Check green, git diff --check clean, prepare-review -Validate green (456 rows), selector -Publish write-back verified.
- Selector repairs this session (android-store ctor arg, provenance-derived wording, eng\** compile exclusion in KeeFetch.csproj) are outside the harness-fingerprint scope; recorded fingerprints in the evidence remain valid.
- Evidence root backup: review-queue.pre-machine-20260822.bak.csv (all not-reviewed) next to the labeled queue.
- Review verified the published study is untainted by the known resume-accumulator defect: resumed_any is false for every cell in the final evidence (0 of 126 measured cells resumed).
