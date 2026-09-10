# KeeFetch website

A static, dependency-free website. HTML, styles, scripts, icons and fonts are
served locally. No analytics, third-party runtime assets or browser network calls
to the sample brands are used. The vault animation is an illustration, not a
product screenshot or live fetch.

From the repository root:

```powershell
python -m http.server 8773 --bind 127.0.0.1 --directory site
python eng/sync-site-profiles.py --check
python eng/verify-site.py
python eng/test-verify-site.py
node --check site/assets/js/site.js
node --check site/assets/js/theme.js
python site/build.py
```

The static build is emitted into `site/dist/`. The GitHub Pages workflow validates
and builds the website, then deploys that directory when website changes reach
`master` (or the workflow is dispatched manually). The public website is
[tzii.github.io/KeeFetch](https://tzii.github.io/KeeFetch/).

Release links, profile summary cards and comparison tables are synchronized from
`data/release.json` and `data/profiles.json` by `eng/sync-site-profiles.py`.
Preserve the generated block markers. Run the synchronizer without `--check`
after changing those inputs. v1.3 is stable; its published assets and checksums are linked throughout the site.

The theme and motion controls save device-local preferences when storage is
available. System reduced motion takes precedence. Content, primary navigation,
downloads and profile comparisons remain available without JavaScript.

Space Grotesk is distributed under SIL OFL 1.1 (`assets/fonts/OFL.txt`). Brand
icons come from Simple Icons; their provenance, CC0 license and brand-rights
disclaimer are included in `assets/brands/`. The existing KeeFetch product icons
remain in `assets/icons/`.
