# Retro website review: 2026-09-10

Draft PR: https://github.com/tzii/KeeFetch/pull/15

Implementation commit: `687620018155c960bb89283ee209b2c61cd90301`.
Final browser evidence: https://github.com/tzii/KeeFetch/actions/runs/34522271010
The pull-request workflow checked merge ref `f2c7a0bb5678d2e5424bfa3f28f8292a055060a4`; all website source hashes match the implementation commit and the delivered standalone file.

## Verified results

| Engine | Version | Checks | axe scans | Automatic violations |
| --- | --- | --- | --- | --- |
| Chromium | 143.0.7499.4 | 112 passed, 0 failed | 4 | 0 |
| Firefox | 144.0.2 | 112 passed, 0 failed | 4 | 0 |
| WebKit | 26.0 | 112 passed, 0 failed | 4 | 0 |

All runs used Playwright 1.57.0, axe-core 4.10.3 and normal HTTP under `/KeeFetch/retro/`. Eleven viewport widths from 320 to 1920, normal and 200% text, system appearance preferences, JS on/off, keyboard controls, denied storage, missing/legacy media APIs, reduced motion, long labels, forced colors, printing, real reload persistence and cross-tab appearance updates passed. The no-JavaScript view intentionally uses the light fallback.

The separate local Chromium 144.0.7559.96 packaged-document run passed 108 checks without axe. Clipboard success is a handler test double; the browser-denial path selects the checksum for manual copying. These are not physical clipboard integration tests.

## Evidence review

Downloaded final artifacts 10170165570 (Chromium), 10170170971 (Firefox), and 10170182416 (WebKit). Parsed their result JSON, confirmed all cases passed, compared source hashes, and verified all three packaged HTML files were byte-identical to the delivered preview. Actual desktop/light, desktop/dark and mobile screenshots were reviewed during the local and cross-browser passes.

```
987d819bbe616160b3e8e47aec39e1f83b61b03d0cd7e563c2cd504dd0cdbb78  index.html
1e1b66194a4c7320c8d251c400b2ed4fb9d0f40408480f926cbfa95dc9130b03  style.css
450e2575ce066fb68df5d9c43845eb6cebd964608d54c03f86408e6310455748  app.js
```

The earlier run flagged two generic labelled containers for manual ARIA review. The current markup gives them explicit region/group roles; those findings are absent in every final scan.

The remaining axe incomplete items concern text over the CSS background grid. A manual calculation using the alpha-composited intersection of both grid strokes, rather than a flat-paper assumption, gives minimum contrast of 4.75:1 for light muted text, 5.29:1 for light violet text, 8.08:1 for dark muted text and 7.13:1 for dark violet text. Ink text exceeds 12:1. This addresses the reported combinations. It is not a complete WCAG conformance claim.

## Preserved boundaries

No production homepage, existing Pages workflow, native plugin source, release artifact or prior softer design was replaced. The concept stays under `site/retro/` on its own branch. Physical phones, real screen readers and native KeePass compatibility were not tested by this website task. The next product decision is the owner's visual review, not an automatic merge or deployment.
