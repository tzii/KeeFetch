# KeeFetch current state

Status: WORKING
Agent: Codex

Focus: Publish KeeFetch v1.3.0 with the accepted packages and update the official website.
Next: Complete PR18 CI, merge the stable website update and verify GitHub Pages.
Pointer: https://tzii.github.io/KeeFetch/
As-of: 2026-09-11 Europe/Rome · PR16 merged as a5c3ef6 and Pages deployment 34536083847 succeeded. All 34 public files match Git; 14 live browser cases and download/checksum checks pass. Final PR16 and PR8 website CI each pass 384 page cases, 123 machine cases and 84 axe scans; 20 static fixtures pass. PR8 at 15dc7d3 is mergeable/CLEAN with green Windows CI, 237 tests and all source/harness/export/package gates. The owner authorized completion on September 11. PR8 merged as 0c3f15c; merge/tag CI passed. v1.3.0 is published as latest stable, and all three public downloads match the retained candidate. PR18 carries the stable website cutover.

Notes:
- Official hosting is GitHub Pages only. The four original screenshots are published without pixel edits. The clean completion shows the verified six-entry Privacy demo; mixed-outcome functional evidence remains preserved.
- Stable downloads remain v1.2.0. The website's v1.3 profile preview is the checksum-bound 9f0b43f export in profiles-v1.3.json. profiles.json still follows the compiled branch catalog; no export gate was weakened.
- Accepted candidate source is 9f0b43f64335803d24ca99d6ccc7e0e7abac3bb8. Retained DLL SHA-256: 924c6c5c1a38b99e8805b01e20569f3fdb50c2941576c567f5e1f406e4311ad5; PLGX: 4742355b5cadb089c7898b99d7a6ff95d068f8319ad97b3a1eb15b1d186a53a8. No package, plugin source or release tag was changed by website publication. Do not replace the host-tested payload with CI rebuilds.
- Study provenance and original inputs remain preserved: 560 agy and 267 ZCode labels, reconstructed transcripts, four ambiguous app identities scored conservatively by owner decision. Native host acceptance is owner-reported; WebKit lacks forced-color-adjust support, explicitly recorded in browser QA.
- Evidence: artifacts/v1.3-preparation/official-website-ci-final-20260911. The source ZIP intake and earlier QA failures remain archived. Main checkout stays on site/redesign-v1-3-release with unrelated index.html and site-next.css edits preserved. Release integration uses website-integration-worktree at 15dc7d3; the website-only worktree retains PR16 source. Local preview remains http://127.0.0.1:60250/.
