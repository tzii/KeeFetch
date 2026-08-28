# AGENTS.md

## Task State

`.agent/STATE.md` holds this repository's current focus only — never a task list.

- Status words: BACKLOG · NEXT · WORKING · REVIEW · BLOCKED · DONE
- At the start of work, read `.agent/STATE.md` and verify it against git and GitHub before trusting it.
- Git and GitHub win when they disagree with the state file; correct the state file first.
- BLOCKED means deliberately parked by an explicit decision; ordinary review or QA remains REVIEW.
- Status describes the task, never the session; switching agents changes only `Agent`.
- At handoff, update `.agent/STATE.md` and prepend one line to `.agent/HANDOFF_LOG.md`.
- Keep `Focus` singular and `Next` limited to the one action needed to advance it.
- Plans, issues, roadmaps, and backlogs belong elsewhere.
- REVIEW for this repository means: implementation is complete and every gate is green — Release build with `-warnaserror`, the full MSTest suite and `eng/benchmark/test-benchmark-harness.ps1` self-tests pass, export `-Check` and `git diff --check` are clean, and CI on the PR is green — leaving only human review or the merge decision.

## Repo-local skills

Procedural skills live under `.agents/skills/`. Use them when the request explicitly
matches the skill or the task clearly needs that workflow.

- `grilling`: stress-test a design before implementation.
- `diagnosing-bugs`: build a tight reproduction before forming a fix theory.
- `blast-radius`: check downstream breakage beyond the diff before merge.

These skills supplement the state/handoff rules above. They do not replace REVIEW gates
or the evidence required to move a task to REVIEW.
