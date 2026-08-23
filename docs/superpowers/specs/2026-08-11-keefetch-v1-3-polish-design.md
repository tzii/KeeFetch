# KeeFetch v1.3 Product Polish Design and Delivery Plan

**Date:** 2026-08-11

**Status:** Proposed for user review. The benchmark review methodology in section 6.3.1 was superseded on 2026-08-17 by the cold-artifact CENSUS directive (see `docs/handoff-2026-08-17.md`): every unique cold `(fixture_id, artifact_hash)` unit is reviewed exactly once; there is no sampling, no stratification, and no interval estimation anywhere downstream.

**Target release:** v1.3.0

## 1. Purpose

KeeFetch v1.3 will polish the product as a whole rather than treating provider behavior, plugin UX, and the website as separate projects. The release will establish evidence-backed fetch profiles, translate those profiles into a guided native KeePass experience, and make the website the complete and accurate entry point for users.

The work proceeds in this order:

1. Measure providers and profile configurations reproducibly.
2. Build the plugin experience around the validated results.
3. Document the released experience completely on the website.

The release ships only when all three areas tell the same story.

## 2. Product goals

v1.3 must:

- Make the normal path understandable without requiring knowledge of favicon providers.
- Offer configurations that correspond to distinct user needs and are justified by repeatable measurements.
- Preserve advanced provider control and diagnostics without exposing that complexity by default.
- Improve first-run guidance, settings, progress, completion feedback, and error recovery while retaining a native KeePass/Windows feel.
- Give users complete installation, update, usage, privacy, compatibility, and troubleshooting guidance on the website.
- Preserve existing saved settings through an explicit migration path.
- Preserve KeePass 2.x and .NET Framework 4.8 compatibility; compile production plugin sources as C# 5 for the PLGX path; and preserve keyboard, DPI, and high-contrast compatibility.

## 3. Non-goals

v1.3 will not:

- Replace KeePass-native UI with a custom cross-platform framework.
- Add telemetry, analytics, or automatic transmission of benchmark results.
- Run nondeterministic live-provider benchmarks as required pull-request CI checks.
- Expose an in-plugin benchmark console to ordinary users.
- Guarantee that every website has a correct favicon.
- Undertake unrelated architectural rewrites.

## 4. Audience hierarchy

The primary audience is the everyday KeePass user who wants recognizable icons, quick installation, sensible defaults, and confidence that the plugin is safe.

Secondary audiences are:

1. Privacy-conscious users who want to understand and control third-party domain disclosure.
2. Power users who want provider control, profile tradeoffs, diagnostics, and custom ordering.
3. Contributors who need architecture, benchmark methodology, tests, and build guidance.

The plugin and homepage lead with plain-language outcomes. Technical detail remains easy to reach but does not dominate the normal path.

## 5. Product architecture

The release has three ordered workstreams joined by a shared product contract.

```text
Provider evidence -> Guided-native plugin UX -> Complete website
         |                    |                       |
         +------ shared profile definitions ---------+
         +------ shared terminology -----------------+
         +------ versioned evidence snapshots -------+
         +------ release acceptance gates -----------+
```

The provider results determine the user-facing profiles. The profiles determine the UI choices and explanations. The final UI and measured behavior determine the website content.

### 5.1 Shared profile source

Profile behavior and user-facing metadata will be centralized in a focused C# catalog rather than spread across `Configuration`, `SettingsForm`, the benchmark script, tests, and website copy.

The catalog will own:

- Stable profile identifier.
- Display name and short description.
- Intended user need.
- Provider order and enablement.
- Provider and cumulative timeout budgets.
- Third-party and synthetic-fallback policy.
- Migration aliases for legacy preset names.

A repository script will export the catalog to a generated website data file. Automated consistency tests will fail if checked-in website profile data differs from the C# source. The plugin remains independent of website files at runtime.

### 5.2 Language and PLGX compatibility decision

`KeeFetch.csproj` currently defaults to C# 7.3, while CI separately overrides it to C# 5 and the legacy `KeeFetch.plgx.csproj` is used by KeePass during PLGX creation. The current production source builds successfully with `-p:LangVersion=5 -warnaserror`.

v1.3 will make the compatibility requirement explicit:

- Change the production `KeeFetch.csproj` language level from 7.3 to 5 so normal local builds reject syntax that KeePass PLGX compilation may not accept.
- Keep `KeeFetch.Tests.csproj` at C# 7.3; test-only syntax does not enter the PLGX package.
- Keep `KeeFetch.plgx.csproj` as the legacy packaging project and explicitly add every new production source/resource file to it.
- Retain the dedicated CI C# 5 build as a regression gate even after the project default changes.
- Require a generated PLGX to compile and load in a clean KeePass environment before release.

This is a production-source downgrade from the current project default, not a framework downgrade or rewrite.

## 6. Workstream A: provider evidence

### 6.1 Corpora

Provider research will use two versioned corpora with different purposes.

**Regression corpus**

- Import the contents of the supplied `KeeFetch-Test-Package.zip` into `KeeFetch.Tests/Fixtures/Regression/`; do not commit the outer archive.
- The package contains 71 synthetic entries, a manifest, a KDBX 3.1 database, and a plaintext XML inspection copy.
- It covers happy paths, normalization, Android mappings, REF resolution, malformed inputs, deduplication, Issue #1 URLs, skip-existing behavior, and concurrency.
- The encrypted database remains a disposable manual-test artifact. No real credentials or personal domains are permitted.
- Version the manifest CSV, README, KDBX, and inspection XML together so fixture IDs and manual-test data cannot drift.

**Public provider corpus**

- Maintain at least 300 public URLs in `KeeFetch.Tests/Fixtures/ProviderCorpus/v1/public-sites.csv`.
- Include global high-traffic sites, regional services, small sites, self-hosted software homepages, redirects, deep paths, scheme-less URLs, SVG-only sites, manifests, missing icons, and Android app URLs.
- Avoid collecting credentials, private hosts, personal vault domains, or pages that require authentication.
- Record why each entry exists and the expected outcome class. Do not require a pixel-identical icon because legitimate site artwork changes over time.

Both manifest types use stable UTF-8 CSV schemas. The public corpus columns are `fixture_id`, `category`, `input_url`, `expected_class`, `expected_host`, `review_required`, and `notes`. The regression manifest retains its existing fields and gains a stable `fixture_id`.

`expected_class` is restricted to `usable-site-icon`, `usable-icon`, `graceful-not-found`, `graceful-reject`, `android-map`, `resolve-reference`, `skip-existing`, `deduplicate`, or `concurrency-success`. `category` values come from a checked vocabulary file stored beside the corpus rather than free-form spelling. Schema validation rejects duplicate fixture IDs, unknown classes/categories, missing URLs where a URL is required, and private/authenticated targets before any benchmark or manual database test.

### 6.2 Benchmark execution

The existing `eng/benchmark-presets.ps1` becomes a repeatable experiment harness rather than a fixed list of hand-coded profile variants. This work is split so data-model changes can be tested before long live runs:

1. **Harness and schema:** declarative experiment input, row-level output, metadata, validation, checkpointing, and tests using deterministic/fake results.
2. **Corpus and live execution:** versioned public fixtures, repeated network runs, visual review, aggregation, and profile comparison reports.

Each benchmark record will include:

- Run identifier, timestamp, KeeFetch commit, corpus version, network context, concurrency, and cache mode.
- URL fixture identifier and category.
- Selected provider, trust tier, synthetic status, candidate metadata, and miss reason.
- Per-provider calls, elapsed time, candidate count, outcome, and errors.
- Total elapsed time and cache/coalescing behavior.
- Image type, dimensions, byte size, and validation result when available.

Experiments will support:

- Cold-cache and warm-cache runs.
- Fixed concurrency levels matching real bulk use.
- Multiple repetitions to expose provider and network variance.
- Declarative provider combinations and timeout budgets.
- Append-only `results.ndjson` for row-level checkpoint/resume, followed by deterministic `rows.csv`, `summary.csv`, and `run.json` metadata exports when a run is finalized.
- Resume/checkpoint behavior so a network interruption does not discard an entire run.

Declarative experiments live under `eng/benchmark/experiments/`. Each JSON definition names a corpus version, profile/provider configuration, repetition count, concurrency, cache mode, timeout budgets, and output directory. Generated result directories remain ignored by Git; reviewed summary reports and the exact experiment definitions used for a profile decision are committed.

### 6.3 Evaluation metrics

No single success percentage determines the winner. Profile decisions will consider:

- Coverage: proportion returning a usable icon.
- Correctness: proportion matching the intended site or application rather than a generic or wrong brand.
- Quality: dimensions, format, blank/placeholder detection, and synthetic status.
- Latency: median, p95, maximum, and total batch duration.
- Reliability: timeout, error, and run-to-run variance rates.
- Cost: provider calls and wasted fallbacks per successful result.
- Privacy exposure: which third parties receive a domain and how often.

#### 6.3.1 Result-labeling protocol

Machine signals and human judgments remain separate:

- `tier` uses the exact `IconTier` values `SiteCanonical`, `StrongResolved`, `SyntheticFallback`, or `Rejected`.
- `is_synthetic`, `placeholder_suspected`, and `blank_suspected` copy the structured candidate/result flags. They are heuristics, not proof of correctness.
- `machine_outcome` records success, not-found, invalid-image, timeout, provider-error, or harness-error.
- `review_label` is one of `correct`, `acceptable-synthetic`, `generic`, `wrong-brand`, `blank`, `unusable`, `ambiguous`, or `not-reviewed`.
- Human review records reviewer, review timestamp, optional notes, and the result artifact hash so labels remain traceable when live artwork changes.

Human review is a CENSUS over the measured cold cells, not a sample (superseding the original stratified-sample design): every unique cold `(fixture_id, artifact_hash)` unit is reviewed exactly once, and the label propagates to every occurrence of that exact artifact across repetitions and candidates. The review queue is regenerated from the evidence and must equal the census exactly - a missing unit or a fabricated key fails closed. `ambiguous` is reported separately and excluded from correctness numerators and denominators. If unresolved ambiguous results could reverse a ranking, expand the review to resolve them or conservatively count them as failures for the decision; the selector replays every winner rule under ambiguity-as-failure and ambiguity-as-usable and rejects any selection whose winner is not stable across both scenarios. Label rates are exact proportions of the reviewed census population; no interval estimates are reported.

### 6.4 Profile outcomes

The current Fast, Balanced, and Thorough presets are hypotheses, not compatibility constraints. Research may rename, replace, add, or remove user-facing profiles.

The catalog reserves four managed profile IDs plus Custom. v1.3 will expose three or four managed profiles based on evidence; a reserved profile that does not demonstrate a distinct benefit remains hidden rather than receiving an arbitrary provider chain. Display names and provider chains remain evidence-driven:

- `bulk-fast` — fast bulk work.
- `everyday` — recommended everyday use.
- `privacy` — privacy-leaning retrieval.
- `max-coverage` — maximum coverage.
- `custom` — user-managed provider settings.

These are not final names or provider chains. They become final only after benchmark review. Custom mode remains available for advanced users.

#### 6.4.1 Configuration migration matrix

| Stored v1.2 value | v1.3 profile ID | Migration behavior |
| --- | --- | --- |
| `Fast` | `bulk-fast` | Adopt the final measured bulk profile. |
| `Balanced` | `everyday` | Adopt the final measured recommended profile. |
| `Thorough` | `max-coverage` | Adopt the final measured maximum-coverage profile. |
| `Custom` | `custom` | Preserve provider toggles, timeouts, fallback flags, and order. |
| Missing value on a new install | `everyday` | Use the v1.3 recommended default. |
| Unknown non-empty value | `custom` | Preserve existing provider settings and avoid guessing. |

Add a string `FetchProfileId` configuration key and a migration schema version. When `FetchProfileId` is absent, migration reads the legacy `FetchPresetMode` string, applies the table once, writes the new ID and schema version, and leaves the legacy key intact for rollback. Repeated loads produce the same result.

Provider-order normalization retains the current `GetProviderOrderList()` guarantees: trim and normalize known names, remove duplicates case-insensitively while preserving first occurrence, then append any missing known providers in default order. Migration tests cover duplicates, unknown names, empty values, and all legacy preset values.

### 6.5 Provider-test boundaries

Provider code will gain deterministic tests at the parsing, request construction, candidate classification, and selection boundaries. Live endpoint tests will be opt-in because provider availability and network conditions are unstable. The benchmark suite, not required PR CI, detects live-provider drift.

## 7. Workstream B: guided-native plugin UX

### 7.1 Design direction

The selected direction is **guided native**: a familiar WinForms/KeePass appearance with better hierarchy and progressive disclosure. The design avoids web-like decoration that would feel foreign inside KeePass.

The settings experience remains one modal, resizable `SettingsForm` so Save and Cancel are atomic. It uses a standard WinForms `TabControl`, not separate settings forms or custom-drawn sidebar navigation. Each `TabPage` hosts a focused `UserControl` so page behavior and layout can be tested independently:

1. **Overview** — profile choice, concise tradeoffs, and the most common behaviors.
2. **Downloads** — URL handling, existing-icon policy, auto-save behavior, icon limits, and batch behavior.
3. **Providers** — provider enablement, order, roles, privacy implications, and synthetic-fallback controls.
4. **Advanced** — timeouts, certificate behavior, diagnostics, reset/migration information, and other uncommon controls.

Overview is sufficient for an ordinary user. Selecting Custom or visiting Providers reveals advanced control without making it the default experience.

The form owns a working `SettingsDraft` copied from `Configuration`. Page controls update the draft only. **Save** validates and commits the draft; **Cancel** or the window close button discards it. Shared Save, Cancel, and restore-defaults actions sit outside the `TabControl`.

Native control choices are intentional:

- Overview uses radio-button or selectable-panel profile choices with standard labels; it does not depend on custom painting.
- Downloads and Advanced use `TableLayoutPanel`, labeled checkboxes, and bounded numeric controls.
- Providers uses a checked provider list plus standard Up, Down, and Reset buttons; drag-and-drop is not required.
- Validation uses an `ErrorProvider` and an accessible summary at the top of the active page.
- Layout uses `AutoScaleMode.Font`, system colors, and anchored/docked containers rather than fixed pixel positioning for growing content.

### 7.2 Profile selection

Profiles are presented as plain-language choices with:

- Intended use.
- Expected relative speed and coverage.
- Privacy behavior.
- Synthetic-fallback behavior.
- A clear recommended marker where supported by evidence.

The UI will not display unstable benchmark timings as if they were guarantees. It may use measured relative labels and link users to versioned benchmark details.

### 7.3 First run

The current informational disclosure becomes a dedicated modal `FirstRunForm` shown before the first download:

- Explain what KeeFetch does.
- Explain that third-party providers may receive entry domains.
- Present the recommended profile and its privacy behavior.
- Offer a privacy-leaning choice when one exists.
- Link to provider details and the website privacy page.
- Persist the choice only after explicit confirmation.

The dialog must not imply that KeeFetch sends credentials, usernames, passwords, or complete database contents.

### 7.4 Progress experience

Bulk downloads continue to use KeePass-native status infrastructure. Improvements include:

- A stable task title and meaningful current action.
- Completed/total counts and clear cancellation state.
- Calm provider-level detail without rapidly flickering technical messages.
- Responsive cancellation and an explicit distinction between cancelling and cancelled.
- No modal interruption for recoverable per-entry failures.

### 7.5 Completion and recovery

Replace the dense final message box with a native summary dialog containing:

- Updated, skipped, not found, failed, and cancelled counts.
- Elapsed time and selected profile.
- A concise explanation of partial success.
- Actions to close, open diagnostics, copy a summary, or retry eligible failures.
- Paths to log and CSV exports without forcing users to find them manually.

The dialog is a dedicated modal `CompletionForm` using a `TableLayoutPanel` for counts and standard buttons for Close, Open diagnostics, Copy summary, and Retry eligible. Open diagnostics launches the existing file with the Windows shell; if that fails, KeeFetch copies or displays the exact path without failing the completed run.

`FaviconDialog` will return a structured batch result containing immutable per-entry outcomes and diagnostic paths. The retry set includes only entries with `NotFound` or recoverable provider/network errors. It excludes successful, skipped, invalid-input, and cancelled entries. Retry starts a new batch using the current profile, does not reapply successful icons, and preserves the original result in diagnostics. Cancellation remains distinct from failure.

### 7.6 Accessibility and layout

All plugin surfaces must support:

- Logical tab order and keyboard-only operation.
- Mnemonics where appropriate.
- Windows DPI scaling without clipping at common scale factors.
- High-contrast themes and system colors.
- Text that does not rely only on color or icons.
- Screen-reader-friendly labels and control relationships where WinForms permits.
- Native focus indicators and resizable layouts where content can grow.

Tests will assert critical layout relationships, but visual/manual review remains required because coordinate-only tests cannot prove usability.

The required manual UI matrix is:

| Dimension | Cases |
| --- | --- |
| DPI scaling | 100%, 125%, 150%, and 200% |
| Theme | Normal Windows colors and Windows High Contrast |
| Keyboard | Forward/reverse tab traversal, tab-page switching, mnemonics, Space, Enter, Escape, and default/cancel buttons |
| Content stress | Long provider/profile descriptions, long diagnostics paths, maximum numeric values, and empty/error states |
| Host flow | First install, v1.2 upgrade, Custom migration, cancellation, partial failure, retry, and successful completion |

Automated layout tests cover containment, overlap, tab order, accessible names, profile/page state, and Save/Cancel draft semantics. Manual review covers actual clipping, focus visibility, screen-reader announcements, high contrast, and the complete DPI matrix.

## 8. Workstream C: complete website

### 8.1 Structure

Retain a dependency-free static site deployed by GitHub Pages. Move from a single marketing page toward a small documentation site with shared CSS, JavaScript, navigation, and visual language.

Required information architecture:

- **Home:** value proposition, current release, primary download, compatibility, key capabilities, and trust signals.
- **Getting started:** installation, first run, single entry, group, and database-wide usage.
- **Profiles and providers:** profile comparison, provider roles, measured tradeoffs, Custom mode, and benchmark links.
- **Privacy and security:** exactly what data can leave the machine, when, to whom, what never leaves, certificate behavior, logs, and telemetry statement.
- **Troubleshooting:** plugin loading, PLGX compatibility, missing icons, slow batches, proxy/TLS issues, diagnostics, and bug-report checklist.
- **Benchmarks:** corpus description, methodology, environment, limitations, versioned results, and reproduction commands.
- **Contributing:** build, test, PLGX packaging, project architecture, provider contribution contract, and links to issues/pull requests.

The homepage remains concise. Detailed material belongs on focused pages rather than being hidden in an excessively long landing page.

### 8.2 Content requirements

The site must include:

- Accurate v1.3 screenshots or short recordings from the final build.
- Install, update, uninstall, and rollback instructions.
- Supported KeePass, Windows, and .NET requirements.
- Release verification guidance and direct links to GitHub release assets.
- A profile comparison that is generated or checked against the shared catalog.
- Clear disclosure of each enabled provider's privacy role.
- A searchable or clearly navigable troubleshooting/FAQ path.
- Links for reporting bugs, requesting features, viewing releases, and contributing.
- Accessible semantic HTML, focus states, reduced-motion handling, alt text, and mobile layouts.

Version-specific claims must not be hard-coded in multiple unrelated locations. Release labels and profile data should come from a small checked data source or be covered by consistency checks.

### 8.3 Website quality gates

Required CI checks run without downloading site-specific packages or contacting external websites:

- `eng/verify-site.py`, using only the Python standard library, parses every HTML file and checks internal links, fragment IDs, local assets, required page titles/descriptions/canonical metadata, image alt text, primary navigation, and profile-data consistency.
- External links are checked for valid HTTPS syntax in offline CI; reachability is an optional network-enabled release check because external availability must not make ordinary CI flaky.
- `eng/smoke-site.ps1` serves `site/` locally and uses the Microsoft Edge installation already present on the Windows CI image in headless mode. It renders the required pages at 390×844, 768×1024, and 1440×1000, fails on navigation/load errors, and verifies that non-empty screenshots are produced. Pixel-perfect snapshot comparison is intentionally excluded.
- If the pinned CI image no longer provides Edge at its documented path, the build fails with an explicit prerequisite error rather than downloading a browser.

Together those scripts cover:

- Broken internal links and fragments.
- Missing assets and incorrect release download paths.
- HTML structure and required metadata.
- Profile-data consistency.
- Basic accessibility rules.
- Responsive smoke checks at phone, tablet, and desktop widths.

Manual review will cover readability, keyboard navigation, dark/light appearance if retained, and factual agreement with the final plugin build.

### 8.4 Website delivery split

Website work is divided into two bounded implementation packages:

1. **Foundation and content:** extract shared CSS/JavaScript, create the seven-page information architecture, add generated profile data, and write release-accurate content with temporary text-only visual slots.
2. **Verification and final media:** add offline checking scripts and CI steps, capture final v1.3 screenshots/recordings, run the responsive/accessibility matrix, and perform release-link verification.

The second package begins only after the plugin UX is stable enough to produce final media. This prevents screenshots and profile copy from being revised repeatedly during implementation.

## 9. Data flow

### 9.1 Research to profile catalog

```text
Versioned corpora
    -> benchmark configurations
    -> row-level observations
    -> aggregated evidence report
    -> reviewed profile decision
    -> shared C# profile catalog
```

Benchmark output never changes production defaults automatically. A human-reviewed profile decision updates the catalog and records the evidence used.

### 9.2 Catalog to product surfaces

```text
Shared C# profile catalog
    -> downloader configuration
    -> settings and first-run copy
    -> configuration migration aliases
    -> generated website profile data
    -> consistency tests
```

### 9.3 Download run to user feedback

```text
Selected entries + profile
    -> provider pipeline
    -> per-entry structured result
    -> progress aggregation
    -> icon updates
    -> completion summary + diagnostics + retry set
```

UI surfaces consume structured results rather than parsing human-readable diagnostic strings.

## 10. Error handling

### 10.1 Benchmark failures

- Record timeout, network error, invalid response, invalid image, and harness failure separately.
- Preserve completed row results when a run stops.
- Mark incomplete runs as incomplete; never publish them as comparable full results.
- Keep environment and corpus metadata with every result set.

### 10.2 Plugin failures

- A single provider or entry failure must not terminate a batch.
- Fatal setup failures receive a concise user message and a diagnostic reference.
- Partial success receives a summary with actionable next steps.
- Retry is bounded to eligible entries and respects cancellation.
- Settings are validated before saving; cancelling leaves the prior configuration unchanged.
- Legacy-setting migration is idempotent and covered by tests.

### 10.3 Website failures

- Release links point to GitHub's stable latest-release asset routes where appropriate.
- Missing optional visual assets must not hide installation or privacy information.
- JavaScript enhancements are progressive; core content and navigation work without JavaScript.

## 11. Testing strategy

### 11.1 Automated plugin tests

- Profile catalog definitions, invariants, and legacy migration.
- Configuration persistence and cancellation behavior.
- Provider request construction and response parsing with deterministic fixtures.
- Candidate validation, ranking, placeholder rejection, and trust tiers.
- Timeout budgets, cancellation, caching, negative caching, and coalescing.
- Structured run aggregation and retry-set selection.
- Settings-page state transitions and critical layout/accessibility properties.
- Completion-summary counts and diagnostic export content.
- Existing 96 tests remain green or are intentionally updated with documented reasons.

### 11.2 Live and manual validation

- Repeatable provider benchmark runs on the public corpus.
- Manual regression runs using the supplied 71-entry KDBX package.
- Single-entry, group, and whole-database flows in KeePass.
- First-run, upgrade, Custom-mode, cancellation, partial-failure, and retry flows.
- DPI, keyboard, high-contrast, and long-localized-text checks.
- Release DLL and PLGX loading proof in a clean KeePass environment.

### 11.3 Build and compatibility gates

- Debug and Release builds.
- All unit/integration tests.
- `KeeFetch.csproj` defaults to `LangVersion` 5 and builds with warnings treated as errors in the compatibility job.
- `KeeFetch.Tests.csproj` may remain on C# 7.3 because test binaries are not packaged into PLGX.
- `KeeFetch.plgx.csproj` explicitly includes every new production source and resource.
- PLGX creation, manifest inspection, and runtime load verification.
- GitHub Pages build/deployment checks.
- No unexpected warnings in release validation.

## 12. Delivery milestones

The task-level work will be written as five bounded implementation plans rather than one repository-wide plan. Each plan has its own test-first steps and review checkpoint:

1. Benchmark harness and corpus foundation.
2. Provider study, profile decision, shared catalog, and migration.
3. Guided-native plugin UX and structured batch results.
4. Website foundation, content, verification, and final media.
5. Cross-surface release validation.

### Milestone 0: baseline and test-data intake

- Record the current commit, 96-test baseline, v1.2 profile definitions, and existing benchmark results.
- Document the supplied synthetic test package and safe handling rules.
- Define versioned corpus schemas and expected-outcome labels.

**Exit:** baseline is reproducible and test inputs are documented.

### Milestone 1: benchmark foundation

**Milestone 1A — harness and schemas**

- Define corpus, experiment, row-result, review-label, and summary schemas.
- Refactor the harness around declarative experiment definitions.
- Add row-level results, environment metadata, checkpoints, repetitions, cache modes, and deterministic fake-result tests.

**Milestone 1B — corpora and live-run support**

- Import and validate the 71-entry regression fixtures.
- Assemble and review the 300-entry public corpus.
- Add deterministic provider boundary tests and live-run aggregation/reporting.

**Exit:** provider combinations can be compared reproducibly without editing harness code.

### Milestone 2: profile research and decision

- Run controlled provider and timeout experiments.
- Review ambiguous or incorrect visual results.
- Select three or four differentiated user profiles.
- Publish a versioned evidence report and migration mapping.

**Exit:** every v1.3 profile has a distinct audience, measured justification, final provider chain, and privacy statement.

### Milestone 3: guided-native plugin UX

- Introduce the shared profile catalog and legacy migration.
- Implement the four-page settings experience.
- Improve first run, progress, completion, diagnostics access, and retry.
- Complete accessibility, layout, and behavioral tests.

**Exit:** ordinary users can choose and use a profile without provider knowledge, while advanced controls remain available.

### Milestone 4: website completion

**Milestone 4A — foundation and content**

- Establish shared site assets and seven focused pages.
- Add generated profile data, profile comparisons, privacy, troubleshooting, benchmarks, compatibility, and contribution guidance.

**Milestone 4B — verification and final media**

- Add standard-library link/metadata/profile checks and local Edge responsive smoke checks.
- Capture final screenshots/recordings after plugin UX freeze.
- Run accessibility, release-link, and cross-surface factual review.

**Exit:** a new user can discover, install, understand, troubleshoot, and verify KeeFetch using the website alone.

### Milestone 5: release validation

- Run the full automated suite and manual KeePass matrix.
- Produce and load-test release DLL and PLGX artifacts.
- Verify every website instruction and download against the release candidate.
- Update changelog, version metadata, and release notes.

**Exit:** provider claims are measured, plugin behavior is polished, website content is accurate, and release artifacts are proven loadable.

## 13. Acceptance criteria

v1.3 is ready when:

- The shared profile catalog is the authoritative production source and website profile data passes consistency checks.
- Final profiles are supported by a committed methodology and versioned result summary.
- The 71-entry synthetic regression package completes without crashes or hangs and matches its expected behavior classes.
- The public benchmark corpus contains at least 300 labeled public fixtures.
- Existing and new automated tests pass, including the C# 5 compatibility build.
- Normal production builds use C# 5; no production source relies on C# 6+ syntax.
- Legacy preset and Custom settings migrate without silent loss of user choices.
- Migration implements the documented legacy-value matrix and preserves provider-order normalization/deduplication.
- Settings, first run, progress, completion, cancellation, partial failure, and retry flows pass manual review.
- Critical plugin surfaces pass keyboard, DPI, high-contrast, and clipping checks.
- The release PLGX is created and load-tested in KeePass.
- The website contains all required pages, uses final screenshots, has no known broken primary navigation/download links, and accurately matches the release candidate.
- Offline CI passes the standard-library site verifier and local Edge viewport smoke tests without downloading site-specific tools.
- Privacy statements name the behavior of the final profiles and do not imply transmission of credentials or vault contents.

## 14. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Live provider results vary by time and network | Use repetitions, environment metadata, percentiles, and versioned snapshots; do not make live runs required PR CI. |
| Visual correctness is hard to automate | Use explicit review labels, complete-census human review of every unique cold artifact, and deterministic placeholder/format tests. |
| UI redesign breaks PLGX or C# 5 compatibility | Keep WinForms and existing host integration; validate C# 5 and PLGX loading throughout Milestone 3. |
| Renamed profiles surprise existing users | Use stable identifiers, migration aliases, idempotent migration, and clear upgrade notes. |
| Website and plugin descriptions drift | Generate/check website profile data from the shared catalog and gate the release on factual review. |
| Scope expands into a full rewrite | Enforce milestone exits and the non-goals; refactor only boundaries required for evidence, UX, and consistency. |

## 15. Decisions recorded

- All three workstreams belong in v1.3.0.
- Work is evidence-led: provider research, then plugin UX, then website completion.
- The plugin UI direction is guided native.
- Guided-native settings use one modal resizable form with a standard `TabControl` and four testable page controls; separate settings forms and custom-drawn navigation are excluded.
- The existing profile model is highly flexible and may change based on evidence.
- Stable v1.3 profile IDs are `bulk-fast`, `everyday`, `privacy`, `max-coverage`, and `custom`; legacy values migrate through the documented matrix.
- The primary audience is the everyday KeePass user, followed by privacy-conscious users, power users, and contributors.
- The supplied test package is safe synthetic data and forms the regression corpus baseline.
- The website remains static and dependency-free but expands into focused documentation pages.
- Production plugin sources move from the SDK project's C# 7.3 default to C# 5; test-only code may remain C# 7.3.
- Required website CI is offline: Python standard-library semantic checks plus local system-Edge viewport smoke renders.

## 16. Next step after approval

After this design is reviewed and approved, create a task-level implementation plan with exact file changes, test-first steps, verification commands, and review checkpoints for each milestone. No production implementation begins before that plan is approved.
