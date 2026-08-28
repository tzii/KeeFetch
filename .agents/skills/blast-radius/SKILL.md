---
name: blast-radius
description: Review a diff for downstream breakage that direct call-site search may miss. Use for "blast radius", "what could this break", or a change that is small but risky.
disable-model-invocation: true
---

# Blast radius

Do more than list callers.

1. Pin the exact diff or commit range and state what behavior changed.
2. Search direct references, then follow boundaries a grep can miss: serialized data, file formats, config, build/release scripts, external interfaces, lifecycle order, generated output, and consumers in other modules or languages.
3. Identify the few facts the change is safe only if true.
4. Prove those facts as cheaply as possible with real code: a focused test, script, build, or reproduction. Cite code when execution is not possible and mark the fact unproven.
5. Separate confirmed risks from cases you investigated and cleared. Give likelihood and impact only when evidence supports them.
6. Finish with the cheapest pre-merge check that would catch the most plausible real regression.

Do not inflate the report with generic possibilities. If a safety assumption was not tested, say so.
