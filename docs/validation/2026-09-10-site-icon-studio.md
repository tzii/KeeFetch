# Icon-studio website review

Date: 2026-09-10. Scope: draft PR #14, `codex/site-icon-studio-2026-09-10`, targeting `codex/v1-3-guided-native-ux`.

## Result

The implemented website and test tooling at `b4400e1b472eacb50267e5a3bdb383f845a4a69c` passed the complete [Website quality PR run 34503178647](https://github.com/tzii/KeeFetch/actions/runs/34503178647). This note and the handoff are documentation-only additions after that execution. Check the PR's latest checks before merging.

| Check | Observed result |
| --- | --- |
| Seven-page source verifier, generated content and build | Pass |
| Offline verifier regression tests | 18 passed |
| Chromium 153.0.8010.12 | 99 passed, zero failed/flaky |
| Firefox 155.0 | 99 passed, zero failed/flaky |
| WebKit 26.6 | 98 passed, one declared skip, zero failed/flaky |
| C# 5 Release build with warnings as errors | Pass |
| Release test build with warnings as errors | Pass |
| Full .NET Framework 4.8 MSTest suite | 237 passed, zero failed/skipped |
| Profile export consistency and benchmark harness self-tests | Pass |
| Version, PLGX manifest, release-workflow safeguards and whitespace checks | Pass |

The browser suite contains 297 declared engine cases: 296 executed and passed. WebKit does not emulate Windows forced colors, so that single case is explicitly skipped there and executed in Chromium and Firefox. Tests use no automatic retries. Axe found no violations for the selected WCAG A/AA rule tags in the executed audit states.

The native result is preserved as `keefetch-plugin-compatibility` artifact 10162803674, including its 237-test TRX. This is automated build/integration evidence, not native KeePass UI acceptance.

## Coverage and visual inspection

All seven pages run through twelve modes: 320, 375, 768, 1024, 1440 and 1920 CSS-pixel widths, light/dark, touch landscape, JavaScript disabled, reduced motion, and 200% text on phone and desktop. Additional cases exercise increased text spacing, keyboard skip/focus/menu behavior, reversible comparison, keyboard table scrolling, control and symbol bounds, font/script/data failures, denied storage, cross-tab preference changes, print and `/KeeFetch/` deployment paths.

Page tests reject unexpected external runtime requests, page errors, failed assets and document overflow. Optional data uses text insertion rather than HTML injection. Failing it leaves the complete generated comparison readable.

Manually inspected Chromium captures include the full light homepage, desktop dark hero, mobile dark layout, enlarged-text header/hero, installation guide and profile guide. Screenshots came from production output, not design mockups. The reviewed browser artifacts from run 34502796654 are Chromium 10162596978, Firefox 10162613115 and WebKit 10162617045; the page and browser-test sources are unchanged in the subsequent complete green run. That earlier run's Windows driver failed and is not represented as an all-green run.

## Defects found and repaired

Initial browser rounds exposed low-contrast footer lettering, inaccessible overflowing code blocks, an invalid label on the optional empty profile container, and document overflow at enlarged text sizes. Later text-spacing checks caught a footer text overflow. Manual review caught toolbar symbols growing outside fixed buttons at 200% text; their containment is now an explicit assertion.

The reduced-motion setting previously disabled the demonstration. It now disables only animation. System light-theme detection, denied-storage behavior, no-script navigation, optional-data fallback, and exact preview evidence links were also corrected.

The initial Firefox no-JavaScript test waited on a page-owned font promise that cannot settle in that mode. The test now avoids that wait only when page JavaScript is disabled; it still executes navigation, rendering and asset assertions with JavaScript actually disabled.

The first new Windows integration driver leaked its StrictMode into legacy check scripts. Existing checks now run in isolated PowerShell processes, as they do in the original build workflow. No legacy gate was weakened and no plugin code was changed.

## Impeccable review

Read and applied the craft-floor, audit, adapt, harden, clarify, delight and polish guidance. CI actually runs the pinned Impeccable 4.1.0 source detector before generating `site/dist`; it retains every finding, accepts the documented findings exit status for review, and fails when the scanner cannot execute. There is no suppression file.

The reviewed source scan contains 17 advisories, not zero:

- Eight font advisories identify the existing local Space Grotesk declaration and uses. Retaining the licensed project typeface is a deliberate continuity decision, recorded in `site/DESIGN.md`.
- Three kicker and one cadence advisory concern the compact product/section labels and short homepage copy. They were reviewed in the rendered layout and retained; they are not assertions of performance or third-party approval.
- Three padding advisories concern compact inner/inline or print rules. Rendered controls retain their 44-pixel target size, and text/reflow tests pass.
- The small-text advisory interprets `.88em` as approximately 0.88 pixels. It is a relative code-font size, not subpixel text.
- The contrast advisory combines the print-only muted token with the dark studio background. Those are not the rendered screen-state colors; the studio is hidden in print. Rendered light/dark contrast audits pass.

Source heuristics and automated accessibility scans are evidence, not proof of universal design quality or complete WCAG conformance.

## Preserved boundaries

Stable downloads remain v1.2.0. v1.3 is explicitly a source preview. The profile catalog and budget values are unchanged; budgets are not measured speed claims. The demo is explicitly an illustration with no requests to sample websites.

The preview study, corpus and harness links are pinned to `19a0c224ca4dbf7042d22c3497e7599f61937c0b`, the source represented by the preview. Their previously linked report on master was older. The source ref is validated and generated links have regression tests.

Concurrent release-acceptance documentation at `a93ed76` was integrated into this work branch through #13. This did not modify master or the release branch. Frozen candidate packages, provider study/selection, runtime source and the published site remain untouched.

Not performed here: physical iOS/Android/Safari/Edge testing, screen-reader acceptance, native KeePass UI testing, live download execution, performance benchmarking or deployment. The separate owner-reported host acceptance and four remaining genuine release screenshots retain their own provenance and requirements.

## Reproduce and review

Use `site/README.md` for the offline and three-engine commands. The read-only Windows driver is `eng/site/verify-plugin-compatibility.ps1`; run it from PowerShell with `-KeePassPath 'C:\Program Files\KeePass Password Safe 2'` after installing the repository's .NET build dependencies and KeePass 2.60.

Review the latest PR checks, screenshots and code before deciding whether to merge #14 into its preparation branch. The website workflow uploads evidence for 14 days. It does not publish or deploy.
