# Local website validation: 2026-09-10

108 checks passed, zero failed. Browser: Chromium 144.0.7559.96. Driver: Playwright 1.57.0. Mode: packaged HTML supplied to a fresh document with `page.set_content`.

The environment rejected HTTP and file navigation. Those restrictions were not bypassed. HTTP-subpath checks, real storage persistence, cross-tab behavior, Firefox, WebKit and axe are not established by this local run. The test script has a separate normal HTTP mode for CI. Native plugin build, tests and physical KeePass validation are outside this website task.

## Tested source SHA-256

```
987d819bbe616160b3e8e47aec39e1f83b61b03d0cd7e563c2cd504dd0cdbb78  index.html
1e1b66194a4c7320c8d251c400b2ed4fb9d0f40408480f926cbfa95dc9130b03  style.css
450e2575ce066fb68df5d9c43845eb6cebd964608d54c03f86408e6310455748  app.js
```

## Coverage

- 88 layout cases: 11 widths (320, 360, 390, 520, 640, 768, 800, 801, 1024, 1440, 1920), 100% and 200% text, light/dark system preference, JS on/off. No horizontal overflow; images load; tested buttons and primary navigation are at least 44 CSS pixels high. No-JS appearance is the light fallback.
- Four asset/script-error cases and one markup/links/ARIA/packaging check.
- Fifteen interaction and degradation cases: icon state and rapid switches, reduced motion, appearance overrides/reset, legacy media listener, storage denial, missing matchMedia, keyboard menu/Escape/anchor focus, keyboard presets, no-JS disclosures, clipboard success handler and denial fallback, long multilingual labels, forced-colors rendering, print, storage-event handler.

Two enlarged-text overflows were found and fixed before this run: a nonwrapping hero phrase and a header that could not wrap. Desktop/light, desktop/dark and mobile screenshots were visually inspected.

A test pass is not a WCAG conformance claim. Automated scans, when run in CI, do not replace real assistive-technology or physical-device testing. The result JSON records whether forced-colors emulation was active and whether storage used a real origin. CI evidence and any follow-up corrections belong to their exact tested commit.

## Cross-browser review

The first HTTP run on implementation commit `660aa7fbc56863f93d652550508ea3d16b6f985b` passed 112 checks in each engine (336 total): Chromium 143.0.7499.4, Firefox 144.0.2 and WebKit 26.0. Twelve axe scans reported zero automatic violations. Reload persistence, cross-tab updates and `/KeeFetch/retro/` asset loading passed. Evidence: https://github.com/tzii/KeeFetch/actions/runs/34521562331

The source hashes and packaged HTML from all three uploaded artifacts matched the local implementation exactly. Axe's incomplete findings were inspected rather than discarded. Two generic containers had accessible names without explicit semantic roles; the workshop is now a named region and the capability strip a named group. The current hash above includes those corrections and has a fresh 108/108 local pass. The HTTP matrix must be rerun on this revision before assigning its predecessor's result to it.

The remaining contrast review items are text drawn over the CSS paper grid. A conservative calculation using both grid strokes at their intersection gives minimum normal-text contrast ratios of 4.75:1 (light muted text), 5.29:1 (light violet text), 8.08:1 (dark muted text), and 7.13:1 (dark violet text). This uses the declared text colors and alpha-composited grid colors over the paper background, not an assumption that the background is flat. It resolves the listed color combinations, not a claim of complete WCAG conformance or assistive-technology testing.
