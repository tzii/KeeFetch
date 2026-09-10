# KeeFetch website design

## Direction and source

The owner-selected `KeeFetch-little-machine-source.zip` replaces the earlier
PR12 visual direction. Preserve its warm squared panels, fine paper grid,
violet mechanical illustration and angular KeeFetch wordmark. The machine is
an explicitly labeled local illustration. The four native-window PNGs are real,
unaltered KeePass captures and open at full size from the homepage and guide.

The seven-page site shares one header, footer, theme preference and navigation.
The six guide pages use a quieter heading scale and readable line lengths.
Hosting remains GitHub Pages under `/KeeFetch/`.

## Typography and composition

Use system sans-serif fonts, with a monospace face for small labels and code.
Body text starts at 16px; controls and secondary copy stay readable at 14px or
larger. `assets/css/site.css` supplies the selected design, `project.css` supplies
the shared documentation and screenshot layouts, and `motion.css` owns animated
scene details. Light paper is #f4f1e9, ink #201b25 and accent #6b35c2. Dark paper
is #18151d, ink #f5f0f9 and accent #c4a3fa. Use the defined tokens in both themes.

The homepage pairs the large wordmark with the icon workshop, then covers
features, workflow, generated profiles, privacy, actual screenshots, stable
installation and contribution links. The four captures form a two-column
gallery on desktop and a single column on mobile. Do not alter screenshot
pixels, crop away outcomes or present the six-entry demo as a benchmark.

## Interaction and fallback

The 4.9-second animation runs once when visible. Run machine replays it;
Pause/Resume keeps its exact pose; Motion off and system reduced motion settle
immediately. One frame loop owns all mechanical parts. Hidden/offscreen/print
states stop unnecessary work. Before/With KeeFetch is also usable without
motion. The four profile selectors change only the website preview.

No JavaScript is required for content, links, screenshots or profile details.
Theme follows the system until overridden; Use system theme clears that choice.
Denied storage falls back to memory. Navigation collapses at 1152px, with a
matching JavaScript media query. Escape closes it and restores trigger focus.
Keep keyboard targets at least 44px high, and preserve forced-color outlines.

## Product boundaries and checks

`data/release.json` owns stable URLs and the verified v1.3 PLGX checksum.
`data/profiles-v1.3.json` is the checksum-bound candidate export and owns the four stable v1.3 profile policies. Both the
release banner and profile/gallery descriptions identify the published release.
Keep detailed study limitations on benchmarks.html and privacy caveats on
privacy.html. Use local SVGs and retain their Simple Icons license files.

Run the static gates, `eng/test-site-browser.py` and `eng/test-site-machine.py`
against the actual built site over HTTP under `/KeeFetch/`. CI covers Chromium,
Firefox and WebKit. Inspect real screenshots in light/dark, phone/desktop,
200% text and no-JavaScript modes. Retain axe incomplete findings as well as
violations; automated passes are not full WCAG or physical-device certification.

The independent website publication keeps `data/profiles.json` tied to the compiled branch catalog. `data/profiles-v1.3.json` is the accepted 9f0b43f export; its source commit and hash are recorded in release.json. The website retains this frozen release snapshot, which matches the compiled 1.3 catalog, so publishing documentation does not change the plugin or weaken its export check. Update the snapshot deliberately with the next accepted candidate.
