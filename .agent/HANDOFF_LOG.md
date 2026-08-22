# Handoff log — newest first, keep the last 20
- 2026-08-22 · ZCode · 0840588 · Study complete & verified: 126/126 cells uniform on gate fingerprints (0 resumed); census queue (456 units) independently regenerated - identical keys/occurrences, all not-reviewed; monitor automation-07cd5199 deleted per owner agreement; STOPPED at the human-review census gate awaiting labels.

- 2026-08-21 · ZCode · b96b1ef · Launch gate recorded at b96b1ef (binary/experiment/corpus/harness fingerprints in handoff item 5; smokes rerun green on the recorded binary); next action: launch the profile-candidates-v13 study from a clean root, then STOP at the human-review census gate.
- 2026-08-21 · ZCode · 1a6cb3e · Commit #5 done: adversarial self-test battery, policy-smoke gate (test-smoke-policy.ps1), selector v2-fingerprint + fixture-filter repair, docs sampling sweep; all gates green incl. MSTest 161/161 and live smokes. Next: launch gate at one exact commit, then the study.
- 2026-08-20 · ZCode · 386f54f · Verified task state against git/GitHub (clean tree, PR #4 open); corrected status WORKING→NEXT and As-of SHA; no code changes.
- 2026-08-20 · ZCode · c2e41cb · Commit #4 selector rework done (strict metrics, cold-only census scoring, shared replayed winner fn, no Wilson, fail-closed publish; c689bed runner repairs); commit #5 adversarial tests + policy smokes + docs next.
- 2026-08-20 · ZCode · c689bed · Set up `.agent` task-state tracking; committed dangling runner repairs as c689bed; commit #4 selector rework (select-profiles.ps1) starts now.
