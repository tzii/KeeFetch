# Local website validation: 2026-09-10

108 checks passed, zero failed. Browser: Chromium 144.0.7559.96. Driver: Playwright 1.57.0. Mode: packaged HTML supplied to a fresh document with `page.set_content`.

The environment rejected HTTP and file navigation. Those restrictions were not bypassed. HTTP-subpath checks, real storage persistence, cross-tab behavior, Firefox, WebKit and axe are not established by this local run. The test script has a separate normal HTTP mode for CI. Native plugin build, tests and physical KeePass validation are outside this website task.

## Tested source SHA-256

```
5e84ad1ac4a28d94abbbe01c7e91497234ed794b4b0cadf66c441f3d50e8237d  index.html
1e1b66194a4c7320c8d251c400b2ed4fb9d0f40408480f926cbfa95dc9130b03  style.css
450e2575ce066fb68df5d9c43845eb6cebd964608d54c03f86408e6310455748  app.js
```

## Coverage

- 88 layout cases: 11 widths (320, 360, 390, 520, 640, 768, 800, 801, 1024, 1440, 1920), 100% and 200% text, light/dark system preference, JS on/off. No horizontal overflow; images load; tested buttons and primary navigation are at least 44 CSS pixels high. No-JS appearance is the light fallback.
- Four asset/script-error cases and one markup/links/ARIA/packaging check.
- Fifteen interaction and degradation cases: icon state and rapid switches, reduced motion, appearance overrides/reset, legacy media listener, storage denial, missing matchMedia, keyboard menu/Escape/anchor focus, keyboard presets, no-JS disclosures, clipboard success handler and denial fallback, long multilingual labels, forced-colors rendering, print, storage-event handler.

Two enlarged-text overflows were found and fixed before this run: a nonwrapping hero phrase and a header that could not wrap. Desktop/light, desktop/dark and mobile screenshots were visually inspected.

A test pass is not a WCAG conformance claim. Automated scans, when run in CI, do not replace real assistive-technology or physical-device testing. The result JSON records whether forced-colors emulation was active and whether storage used a real origin. CI evidence and any follow-up corrections belong to their exact tested commit.
