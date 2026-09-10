# KeeFetch: retro refinement

A working, isolated redesign of the published homepage. Open `index.html` in a browser, or serve `site` and visit `/retro/`. The original `site/index.html` and the softer design in PR #12 are not replaced.

## Direction

Keep the cream grid, black section bars, violet accents, squared panels and strong wordmark. Use a quieter background, fewer competing boxes, readable body text, and a clear path from explanation to download. The custom wordmark and vault illustration are SVG, not a raster mockup. Body typography uses system fonts; there are no font downloads or runtime dependencies.

The icon comparison, appearance switch, mobile navigation, preset guide, disclosures and checksum controls are functional. The four entries are examples, not real database contents. The comparison never fetches icons or contacts providers.

## Content boundaries

The stable download, checksum, install paths and v1.2 preset description were checked against the release and its tagged README on 2026-09-10:

- https://github.com/tzii/KeeFetch/releases/tag/v1.2.0
- https://github.com/tzii/KeeFetch/blob/v1.2.0/README.md

The page removes the original unlabelled provider timings and does not present a fixed provider chain as the execution policy. Network/privacy copy is explicit about domain sharing, IP visibility and website-linked icon hosts. No native compatibility or v1.3 release claim follows from these website checks.

## Run and package

From the repository root:

```sh
python -m http.server 8000 --directory site
# Open http://localhost:8000/retro/
python site/retro/package_preview.py KeeFetch-retro-preview.html
```

The packaged file includes the complete homepage, images, CSS and JavaScript. External documentation and release links still need a connection. A file viewer may disable scripts; open it in a browser to use the controls. Saved preferences and clipboard permissions depend on the browser and origin.

## Checks

```sh
python -m pip install playwright==1.57.0
python -m playwright install --with-deps chromium firefox webkit
npm install --no-save --ignore-scripts axe-core@4.10.3
python site/retro/test_browser.py --browser chromium --axe node_modules/axe-core/axe.min.js --output qa/chromium
# Repeat with --browser firefox and --browser webkit.
node --check site/retro/app.js
```

The HTTP suite serves the files under `/KeeFetch/retro/`. A separate `--inline` mode tests the packaged file in environments where browser navigation is restricted. It does not establish real HTTP routing or persistent-origin behavior.

Local baseline: Chromium 144.0.7559.96, Playwright 1.57.0, packaged-file mode, **108 passed / 0 failed**. Eleven widths (320 through 1920), normal and 200% text, both system appearance preferences, JavaScript on/off, keyboard interactions, storage denial, reduced motion, long labels, forced colors and print were exercised. With JavaScript off the page uses its light fallback. Source identities are in `QA.md`.

No local axe, Firefox, WebKit, physical phone or screen-reader run was completed. Clipboard success uses a test double; denial selects the checksum for manual copying. The local storage check exercises the event handler, not persistence. The read-only CI workflow adds the three real browser engines, HTTP subpath, persistence and axe checks. Its results must be read before claiming those gates passed.

## Design references

Impeccable's documentation informed the review, not a claimed CLI execution:

- https://impeccable.style/docs/improve-design/
- https://impeccable.style/docs/typeset/
- https://impeccable.style/docs/adapt/
- https://impeccable.style/docs/harden/
- https://impeccable.style/docs/polish/

## Assets

The KeeFetch mark is reused from the project. GitHub, Figma, Notion and Spotify glyphs are the existing Simple Icons assets from the earlier website branch. Their CC0 license and trademark disclaimer are included in `brands/`. Brand marks identify example entries only and do not imply endorsement. All new source follows the repository MIT license.

This is a design preview, not a deployment. Nothing here authorizes changing the production Pages target, merging a PR, tagging or publishing a plugin release.
