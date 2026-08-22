# KeeFetch current state

Status:  REVIEW
Agent:   ZCode

Focus:   v1.3 study complete end to end: machine-labeled census (owner-approved, disclosed), winners published, all local gates green. Awaiting CI on the final push and then human review / the merge decision on PR #4.
Next:    Watch CI on the final commit; if green, the PR is ready for human review. Optional follow-ups live elsewhere (site PRs #5/#6 visual decision; plgx packaging if the release flow needs it).
Pointer: https://github.com/tzii/KeeFetch/pull/4
As-of:   2026-08-22 · item 8 complete

Notes:
- Review provenance: census labels are MACHINE review (`machine:antigravity-1.1.18/gemini-3.7-flash-high`), owner-approved after the dual-lane pilot + pixel arbitration and an owner spot-check; disclosed in `docs/benchmarks/v1.3-provider-study.md` and `machine-review/`. Never present them as human review.
- Winners: bulk-fast -> cand-direct-google-twenty-fast; everyday -> cand-full-minus-favicone-thorough-synth; privacy -> cand-direct-only-balanced; max-coverage -> cand-direct-yandex-balanced; all stable under ambiguity replay after the 3 pub-216 units were resolved to `unusable` (focused re-ask, live-verified).
- Gates at this state: MSTest 161/161, Release build -warnaserror 0/0, harness self-tests green, export -Check green, git diff --check clean, prepare-review -Validate green (456 rows), selector -Publish write-back verified.
- Selector repairs this session (android-store ctor arg, provenance-derived wording, eng\** compile exclusion in KeeFetch.csproj) are outside the harness-fingerprint scope; recorded fingerprints in the evidence remain valid.
- Evidence root backup: review-queue.pre-machine-20260822.bak.csv (all not-reviewed) next to the labeled queue.
