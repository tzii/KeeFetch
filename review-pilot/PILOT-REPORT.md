# Machine-labeling pilot - 20 census units, dual lane + pixel arbitration

Date: 2026-08-22. Question (project owner): may the human-review census gate be satisfied by
machine labeling (Antigravity / gemini-3.7-flash-high), disclosed as machine review?

## Design

- 20 units stratified over all 9 census categories, deliberately including flagged units
  (placeholder_suspected, synthetic, blank_suspected, profile-differing).
- Lane A: `crew review --workers google` -> Antigravity 1.1.18, model `gemini-3.7-flash-high`,
  effort high, isolated snapshot, live-web verification allowed. ~5.3 min for 20 units,
  401k input / 5.8M cache-read tokens, verified_live on 18/20.
- Lane B: independent vision API on the same 20 images.
- Arbitration: deterministic pixel analysis (PIL) on every inter-lane contradiction.

## Results

- Exact inter-lane agreement: 14/20 (70%).
- Hard contradictions (incompatible image descriptions): 3 - pub-255, pub-211, pub-216.
  Pixel arbitration resolved ALL THREE in favor of Lane A (gemini):
  - pub-255 (untrusted-root.badssl.com): 72% red pixels = badssl red padlock. Lane A `correct`
    (live-verified); Lane B had described a gray letter-mark (misread).
  - pub-211 (httpstat.us/302): 91% near-white + gray glyph = letter-mark. Lane A
    `acceptable-synthetic` confirmed.
  - pub-216 (neverssl.com): 35% sky-blue = cropped blue/white banner, NOT blank. Lane B's
    `blank` was wrong; Lane A `ambiguous` (conservative) stands; blank_suspected flag false.
- Conservative-vs-verified disagreements: 3 (pub-266, pub-089, pub-076) - Lane A verified live
  and committed; Lane B hedged to ambiguous.
- Consistency defect found: pub-032 and pub-289 share one artifact hash (both stackoverflow.com)
  but Lane B gave different labels in different prompt contexts. Full run must label per unique
  hash and force identical labels across units sharing a hash (same canonical site).
- Lane A quality signals: caught wrong-brand on the Gmail package (Google "G" is not the Gmail
  envelope-M icon); contradicted a false blank_suspected flag; surfaced manifest metadata
  anomalies (pub-211 byte/dim mismatch, pub-125 dim mismatch).

Arbitrated final labels (all 20): correct 14, acceptable-synthetic 3 (pub-246, pub-258, pub-211),
wrong-brand 1 (pub-266), ambiguous 1 (pub-216), blank 0, unusable 0, generic 0.

## Verdict

gemini-3.7-flash-high with live verification is a credible census labeler; the naive
single-shot vision lane is not. Recommended full-run pipeline (456 units):

1. Deterministic pixel pre-pass per unique hash: blank/transparent detection, dominant colors,
   glyph-on-plain-background detection. Arbitrates blank/unusable/synthetic-shape disputes.
2. Label once per unique artifact hash (not per unit); propagate to all units of that hash.
3. Lane A labeling via gemini-3.7-flash-high, batches of ~20, same rubric as this pilot.
4. Cross-check pass: label vs flags vs pixel stats; contradictions re-asked once, then
   conservative `ambiguous`.
5. Human stratified spot-check of ~25 arbitrated units before -Validate.
6. Provenance disclosure: reviewer column records the machine labeler verbatim
   (e.g. `machine:gemini-3.7-flash-high+pixel-arbiter`), evidence doc + PR body amended;
   labels are never presented as human review.

Estimated cost: ~23 batches x ~5 min (~2 h wall clock serial), ~9M input tokens mostly cache-read.

## Files

- manifest.json - the 20 pilot units
- laneA-output.json - raw crew review result (full JSON incl. command line)
- prompt-laneA.txt / prompt-laneA-oneline.txt - delegation prompts
- images/ - the 20 staged artifacts
