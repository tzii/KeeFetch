# KeeFetch v1.3 Website Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the current single marketing page into a complete, dependency-free seven-page KeeFetch site whose profile claims, downloads, privacy guidance, and screenshots match the final plugin.

**Architecture:** Keep plain static HTML/CSS/JavaScript and GitHub Pages deployment. Extract shared assets, generate profile cards from checked `site/data/profiles.json`, verify semantics and links with Python's standard library, and run offline viewport smoke renders using the system Microsoft Edge already present on the Windows CI image.

**Tech Stack:** HTML5, CSS, small progressive JavaScript, JSON, Python 3 standard library, PowerShell 5.1, headless Microsoft Edge, GitHub Pages.

---

## File map

- Rewrite `site/index.html` — concise homepage and primary download.
- Create `site/getting-started.html` — install, update, uninstall, and usage.
- Create `site/profiles.html` — generated profile comparison and provider roles.
- Create `site/privacy.html` — exact disclosure and controls.
- Create `site/troubleshooting.html` — diagnosis and bug-report path.
- Create `site/benchmarks.html` — methodology, limitations, and evidence links.
- Create `site/contributing.html` — build/test/provider contribution guidance.
- Create `site/assets/css/site.css` — shared responsive visual system.
- Create `site/assets/js/site.js` — navigation/theme/profile enhancement only.
- Retain `site/assets/icons/` and add final v1.3 media under `site/assets/media/`.
- Use generated `site/data/profiles.json` from Plan 2.
- Create `eng/verify-site.py` — offline semantic/link/profile verifier.
- Create `eng/smoke-site.ps1` — local server plus Edge viewport renders.
- Create `eng/test-verify-site.py` — standard-library unit tests that load the hyphenated verifier path with `importlib.util`.
- Create `eng/sync-site-profiles.py` — deterministic no-JavaScript fallback-table generator.
- Modify `.github/workflows/build.yml` — offline verifier and viewport smoke.
- Modify `.github/workflows/pages.yml` — keep static deployment and include every site path.

### Task 1: Extract a shared static-site foundation

**Files:**
- Create: `site/assets/css/site.css`
- Create: `site/assets/js/site.js`
- Modify: `site/index.html`

- [ ] **Step 1: Preserve a baseline screenshot and content inventory**

Open the current site locally and capture phone and desktop screenshots outside the committed site directory. Record current sections, release links, theme behavior, and icon assets in the implementation notes. This is regression evidence, not final media.

- [ ] **Step 2: Extract shared CSS without changing content**

Move the existing `<style>` rules to `site/assets/css/site.css`, preserving custom properties and responsive breakpoints. Replace inline CSS with:

```html
<link rel="stylesheet" href="assets/css/site.css">
```

Keep critical semantic HTML and visible content unchanged in this step.

- [ ] **Step 3: Extract progressive JavaScript**

Move theme/navigation behavior to `site/assets/js/site.js` and load it with:

```html
<script src="assets/js/site.js" defer></script>
```

The script must guard every query result before use, retain usable navigation with JavaScript disabled, and respect `prefers-reduced-motion`.

- [ ] **Step 4: Verify the unchanged page locally**

```powershell
python -m http.server 8123 --directory site
```

Open `http://localhost:8123/`, compare against baseline at phone/desktop widths, then stop the server. Expected: no missing styles/scripts/assets and no content change.

- [ ] **Step 5: Commit**

```powershell
git add site/index.html site/assets/css/site.css site/assets/js/site.js
git commit -m "refactor: extract shared website assets"
```

### Task 2: Create the seven-page information architecture

**Files:**
- Rewrite: `site/index.html`
- Create: `site/getting-started.html`
- Create: `site/profiles.html`
- Create: `site/privacy.html`
- Create: `site/troubleshooting.html`
- Create: `site/benchmarks.html`
- Create: `site/contributing.html`

- [ ] **Step 1: Define the shared navigation contract**

Every page must contain the same primary links in this order: Home, Getting started, Profiles, Privacy, Troubleshooting, Benchmarks, Contributing, GitHub. Mark the current page with `aria-current="page"`. Include Skip to content as the first focusable link and a shared footer with current release, license, security policy, and repository links.

- [ ] **Step 2: Rewrite the homepage around ordinary users**

Keep the primary PLGX download above the fold. Include value proposition, KeePass/.NET compatibility, three-step install summary, measured profile overview sourced from JSON, privacy trust statement, final UX preview slot, and links into detailed pages. Remove duplicate technical explanations from Home.

- [ ] **Step 3: Write Getting Started**

Include exact sections: Requirements, Install, First run, Single entry, Group, Entire database, Update, Uninstall, Roll back, and Verify release. Commands/paths must match the final PLGX name and KeePass plugin directory behavior.

- [ ] **Step 4: Write Profiles and Providers**

Include a `<div data-profile-list>` generated from `profiles.json`, a no-JavaScript fallback table, provider roles, Custom behavior, privacy/synthetic definitions, and a direct link to the v1.3 evidence report.

- [ ] **Step 5: Write Privacy and Security**

State exactly: KeeFetch reads entry URL/title data locally; Direct Site contacts the target origin; enabled third-party providers may receive the domain; credentials, usernames, passwords, and full database contents are not sent; no telemetry/analytics exist. Document certificate override risk, diagnostic files, profile behavior, and links to `SECURITY.md`.

- [ ] **Step 6: Write Troubleshooting, Benchmarks, and Contributing**

Troubleshooting covers PLGX loading, missing icons, slow batches, proxy/TLS, cancellation/retry, diagnostics paths, and the bug-report checklist. Benchmarks covers corpus versions, metrics, review labels, environment variance, limitations, reproduction command, and evidence links. Contributing covers clone, restore, C# 5 production boundary, tests, provider contract, PLGX packaging, and PR links.

- [ ] **Step 7: Perform a factual cross-check**

Search all pages for `v1.2`, old Fast/Balanced/Thorough descriptions, old completion MessageBox instructions, and obsolete screenshot captions. Retain historical version text only when explicitly labeled historical.

- [ ] **Step 8: Commit**

```powershell
git add site/*.html
git commit -m "docs: add complete KeeFetch website structure"
```

### Task 3: Render profile data consistently and progressively

**Files:**
- Modify: `site/assets/js/site.js`
- Modify: `site/profiles.html`
- Modify: `site/index.html`
- Use: `site/data/profiles.json`
- Create: `eng/sync-site-profiles.py`

- [ ] **Step 1: Implement deterministic fallback-table generation**

Place `<!-- PROFILE_FALLBACK_START -->` and `<!-- PROFILE_FALLBACK_END -->` markers in both pages. Implement `eng/sync-site-profiles.py` with Python's standard `json`, `html`, and `pathlib` modules. It reads `site/data/profiles.json`, renders visible profiles in catalog order, and replaces only the marker contents. Each row includes display name, intended use, provider display names, relative speed/coverage, third-party behavior, synthetic fallback, and evidence link. The default/recommended profile has visible text, not color-only styling.

Support `--check`: render in memory and exit 1 with the mismatched page names instead of writing. Run:

```powershell
python eng/sync-site-profiles.py
python eng/sync-site-profiles.py --check
```

Expected: the second command exits 0 without changing files.

- [ ] **Step 2: Add progressive JSON rendering**

Implement:

```javascript
async function loadProfiles() {
  const targets = document.querySelectorAll('[data-profile-list]');
  if (!targets.length) return;
  const response = await fetch('data/profiles.json', { cache: 'no-store' });
  if (!response.ok) throw new Error(`Profile data HTTP ${response.status}`);
  const profiles = await response.json();
  for (const target of targets) target.replaceChildren(...profiles.filter(p => p.isVisible).map(renderProfile));
}
```

`renderProfile` uses `document.createElement` and `textContent`; do not inject profile strings with `innerHTML`. On error, leave the checked fallback visible.

- [ ] **Step 3: Verify catalog consistency**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1 -Check
python eng/sync-site-profiles.py --check
```

Expected: exit 0.

- [ ] **Step 4: Commit**

```powershell
git add site/index.html site/profiles.html site/assets/js/site.js site/data/profiles.json eng/sync-site-profiles.py
git commit -m "feat: render website profiles from the shared catalog"
```

### Task 4: Implement the offline semantic and link verifier

**Files:**
- Create: `eng/verify-site.py`
- Create: `eng/test-verify-site.py`

- [ ] **Step 1: Write failing standard-library tests**

Use `tempfile.TemporaryDirectory` to create small valid/invalid sites. Cover missing title, missing description/canonical, duplicate ID, broken local link, broken fragment, missing image alt, invalid external non-HTTPS URL, inconsistent nav order, missing local asset, and mismatched profile IDs.

Run:

At the top of `eng/test-verify-site.py`, load `verify-site.py` explicitly:

```python
import importlib.util
from pathlib import Path

target = Path(__file__).with_name("verify-site.py")
spec = importlib.util.spec_from_file_location("verify_site", target)
verify_site_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify_site_module)
```

End the file with `unittest.main()`, then run:

```powershell
python eng/test-verify-site.py -v
```

Expected: import failure because `verify-site.py` does not exist.

- [ ] **Step 2: Implement a dependency-free parser**

Create an `HTMLParser` subclass that records page title, meta/link metadata, IDs, anchors, image sources/alts, script/stylesheet sources, navigation labels, and `aria-current`. Resolve local paths with `pathlib.Path` and URLs with `urllib.parse`.

The CLI contract is:

```python
def verify_site(site_root: Path) -> list[str]:
    """Return deterministic human-readable errors; an empty list means success."""

if __name__ == "__main__":
    root = Path(sys.argv[1] if len(sys.argv) > 1 else "site").resolve()
    errors = verify_site(root)
    for error in errors:
        print(error)
    raise SystemExit(1 if errors else 0)
```

Use only Python's standard library. Do not request external URLs; validate that external links use HTTPS except explicitly permitted `mailto:` links.

- [ ] **Step 3: Run unit tests and the real-site verifier**

```powershell
python eng/test-verify-site.py -v
python eng/verify-site.py site
```

Expected: all unit tests pass and real-site verification exits 0.

- [ ] **Step 4: Commit**

```powershell
git add eng/verify-site.py eng/test-verify-site.py
git commit -m "test: add offline website verifier"
```

### Task 5: Add offline Edge viewport smoke rendering

**Files:**
- Create: `eng/smoke-site.ps1`
- Modify: `.github/workflows/build.yml`

- [ ] **Step 1: Implement deterministic browser discovery**

Check these paths in order and fail with `Microsoft Edge not found; offline site smoke cannot run.` if none exists:

```powershell
$candidates = @(
  'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
  'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
)
```

- [ ] **Step 2: Serve the site locally and render every page**

Start `python -m http.server` with `-WindowStyle Hidden`, choose an OS-assigned free loopback port, and wrap cleanup in `try/finally`. For each of seven pages and each viewport `390,844`, `768,1024`, `1440,1000`, construct the Edge argument list from concrete `$width`, `$height`, `$screenshotPath`, and `$pageUrl` variables:

```powershell
$arguments = @(
  '--headless', '--disable-gpu', '--hide-scrollbars',
  "--window-size=$width,$height",
  "--screenshot=$screenshotPath",
  $pageUrl
)
& $edge @arguments
if ($LASTEXITCODE -ne 0) { throw "Edge render failed for $pageUrl at ${width}x${height}." }
```

Fail when Edge exits nonzero, a screenshot is absent/zero bytes, or the HTTP request for a page is not 200. Store screenshots under a temporary directory, not `site/`.

- [ ] **Step 3: Run locally**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/smoke-site.ps1
```

Expected: 21 non-empty renders and exit 0.

- [ ] **Step 4: Add offline CI steps**

After profile export consistency, add:

```yaml
- name: Verify static website
  shell: pwsh
  run: |
    python eng/test-verify-site.py -v
    python eng/sync-site-profiles.py --check
    python eng/verify-site.py site
    powershell -NoProfile -ExecutionPolicy Bypass -File eng/smoke-site.ps1
```

- [ ] **Step 5: Commit**

```powershell
git add eng/smoke-site.ps1 .github/workflows/build.yml
git commit -m "ci: add offline responsive website smoke tests"
```

### Task 6: Capture final v1.3 media after UX freeze

**Files:**
- Create: `site/assets/media/settings-overview.png`
- Create: `site/assets/media/settings-providers.png`
- Create: `site/assets/media/first-run.png`
- Create: `site/assets/media/completion-summary.png`
- Create or replace: `docs/usage-single.gif`
- Create or replace: `docs/usage-group.gif`
- Modify: relevant `site/*.html`

- [ ] **Step 1: Prepare a clean capture environment**

Use the release-candidate PLGX in a disposable KeePass profile with the synthetic KDBX. Capture at 100% DPI with default Windows theme, no personal databases, usernames, file paths, or desktop notifications visible.

- [ ] **Step 2: Capture the required states**

Capture Settings Overview, Settings Providers/Custom, First Run privacy decision, Completion partial-success summary, single-entry fetch, and group fetch. Use PNG for still states and optimized GIF or short animated WebP only where motion explains a workflow.

- [ ] **Step 3: Optimize and validate**

Keep text readable at rendered site width, remove metadata that contains local paths, add meaningful alt text/captions, and confirm no image contradicts final profile names or controls.

- [ ] **Step 4: Run website gates and commit**

```powershell
python eng/verify-site.py site
powershell -NoProfile -ExecutionPolicy Bypass -File eng/smoke-site.ps1
git add site docs/usage-single.gif docs/usage-group.gif
git commit -m "docs: add final v1.3 plugin media"
```

### Task 7: Complete factual, accessibility, and deployment review

**Files:**
- Create: `docs/validation/v1.3-website-matrix.md`
- Modify: `.github/workflows/pages.yml` only if deployment fails to include the complete static tree

- [ ] **Step 1: Run the full offline gate**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/export-profile-data.ps1 -Check
python eng/sync-site-profiles.py --check
python eng/test-verify-site.py -v
python eng/verify-site.py site
powershell -NoProfile -ExecutionPolicy Bypass -File eng/smoke-site.ps1
```

Expected: all commands exit 0.

- [ ] **Step 2: Perform manual accessibility review**

At phone/tablet/desktop widths, record keyboard navigation, skip link, focus visibility, headings, landmarks, image alternatives, zoom to 200%, reduced motion, light/dark appearance if retained, and content with JavaScript disabled.

- [ ] **Step 3: Perform network-enabled release-link review**

Verify the latest-release page and `KeeFetch.plgx` asset paths in a release-candidate environment. Record HTTP status and final destination. Do not put external reachability in required PR CI.

- [ ] **Step 4: Verify Pages deployment locally and after merge**

Confirm `pages.yml` uploads the entire `site` directory. After an authorized deployment, repeat primary navigation/download smoke on the published URL and record results.

- [ ] **Step 5: Commit validation evidence**

```powershell
git add docs/validation/v1.3-website-matrix.md .github/workflows/pages.yml
git commit -m "test: validate complete v1.3 website"
```
