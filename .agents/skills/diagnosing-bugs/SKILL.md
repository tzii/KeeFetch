---
name: diagnosing-bugs
description: Diagnose hard bugs, regressions, flaky behavior, and performance problems by building a reproduction before proposing a fix.
---

# Diagnosing bugs

Start with a feedback loop, not a theory.

1. Create one command, test, script, or harness that can fail on the exact reported symptom. Run it at least once. If the environment cannot reproduce the symptom, say what evidence is missing instead of guessing.
2. Make the reproduction smaller and more deterministic until unrelated setup is gone.
3. Rank several falsifiable hypotheses. For each, state what observation would rule it in or out.
4. Test one variable at a time. Prefer debugger inspection or narrow instrumentation over broad logging. Tag temporary logs so they can be removed.
5. When the cause is known, add a regression test at the real behavioral seam if one exists. Watch it fail before the fix, then pass after the fix.
6. Re-run the original reproduction and the repository's required verification gates. Remove temporary probes before handoff.

Report the reproduced symptom, root cause, fix, commands run, results, and anything still unverified. A missing or unavailable check is blocked, not pass.
