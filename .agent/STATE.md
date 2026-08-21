# KeeFetch current state

Status:  NEXT
Agent:   ZCode

Focus:   Launch gate of the v1.3 evidence-methodology repair — at one exact commit rerun every check (Release -warnaserror, MSTest, harness self-tests, policy smokes via eng/benchmark/test-smoke-policy.ps1, export -Check, git diff --check) and record binary/experiment/corpus/harness fingerprints before launching the final study.
Next:    Run the launch gate at HEAD and record the four fingerprints; then launch `profile-candidates-v13` from a clean output root per handoff-2026-08-17.md items 5-6.
Pointer: https://github.com/tzii/KeeFetch/pull/4
As-of:   2026-08-21 · 1a6cb3e

Notes:
- Commits #1-#5 are on this branch; commit 5 (1a6cb3e) added the adversarial self-test battery, the policy-smoke gate, the selector v2-fingerprint/fixture-filter repair (real evidence could never pass the old v1 gate), and swept stale sampling claims from docs.
- The human-review census gate after study launch mandates STOP — labels are never fabricated; the smoke's `smoke-automated` labels are mechanism-only and never study evidence.
- No study is running and none may be launched until the launch gate passes at one exact revision.
