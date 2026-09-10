# KeeFetch website

Seven static HTML pages with shared styles and scripts. Icons and fonts are served locally. No analytics, third-party runtime assets or browser requests to the sample brands are used. The interactive vault is a labeled illustration, not a product screenshot or live fetch.

Read [PRODUCT.md](PRODUCT.md) for scope and factual constraints, and [DESIGN.md](DESIGN.md) for the icon-studio design decisions.

## Build and check

From the repository root, using Python 3 and Node.js:

```sh
python eng/sync-site-profiles.py --check
python eng/verify-site.py
python eng/test-verify-site.py
node --check site/assets/js/site.js
node --check site/assets/js/theme.js
python site/build.py
python -m http.server 8773 --bind 127.0.0.1 --directory site/dist
```

Open `http://127.0.0.1:8773/`. Stop the server with Ctrl+C. Production output is `site/dist/` and is not committed.

The existing GitHub Pages workflow builds and deploys that directory when its deployment conditions are met on `master`, or when explicitly dispatched. The redesign's Website quality workflow only validates and uploads review artifacts. It does not deploy, publish a plugin, or change release assets.

## Browser and accessibility checks

Test dependencies are development-only. Use Node.js 20 or newer; CI uses Node.js 24. From the repository root:

```sh
python site/build.py
cd eng/site
npm ci --ignore-scripts --no-fund --no-audit
npx playwright install --with-deps
npm test
```

Playwright starts and stops its own loopback HTTP server on port 8765. The suite has 99 cases per engine for Chromium, Firefox and WebKit. The single declared skip is Windows forced-colors emulation in WebKit; Chromium and Firefox execute that case. There are no automatic retries.

The twelve page modes cover seven pages at 320 through 1920 CSS pixels, light/dark themes, touch landscape, disabled JavaScript, reduced motion and 200% text. Interaction tests cover keyboard focus, menu escape, immediate comparison state, table keyboard scrolling, 44-pixel controls, text spacing, failed fonts/scripts/data, denied storage, cross-tab preferences, print and GitHub Pages subdirectories. Axe checks WCAG A/AA rule tags in the selected rendered page and interaction states. A clean scan is not a claim of complete WCAG conformance or screen-reader acceptance.

Results, exact browser versions, full-page screenshots and failure traces are written to `site-qa/` at the repository root. GitHub Actions retains review artifacts for 14 days. Review actual screenshots as well as the machine results.

Website quality also runs `npx --yes impeccable@4.1.0 detect --json site/` before building, so generated output is not counted twice. It saves all source findings for review; an inability to execute the detector fails the job. See DESIGN.md for deliberate design choices. No rule-suppression file is used.

## Generated content and release evidence

Release links, profile summary cards and comparison tables are synchronized from `data/release.json` and `data/profiles.json` by `eng/sync-site-profiles.py`. Preserve the generated block markers. Run the synchronizer without `--check` after changing the inputs.

The stable download remains v1.2.0; v1.3 is a source preview. `previewSourceRef` must be a full immutable commit SHA for the source and study represented by the preview. Generated study links use this ref rather than the potentially older report on `master`. Updating that ref requires rechecking the report, corpus and harness against the copy. It does not change the stable download.

## Progressive enhancement and licensing

Theme and motion controls save device-local preferences when storage is available. Without a saved theme, the site follows the operating system. System reduced motion takes precedence; the comparison still works instantly. Content, navigation, downloads and complete profile comparisons remain available without JavaScript.

Space Grotesk is distributed under SIL OFL 1.1 (`assets/fonts/OFL.txt`). Brand icons come from Simple Icons; their provenance, CC0 license and brand-rights disclaimer are in `assets/brands/`. Existing KeeFetch product icons remain in `assets/icons/`.
