# KeeFetch v1.3 Product Polish Design and Delivery Plan

**Date:** 2026-08-11

**Status:** Proposed for user review

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
- Preserve KeePass 2.x, .NET Framework 4.8, C# 5 PLGX, keyboard, DPI, and high-contrast compatibility.

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

## 6. Workstream A: provider evidence

### 6.1 Corpora

Provider research will use two versioned corpora with different purposes.

**Regression corpus**

- Import the supplied `KeeFetch-Test-Package.zip` fixtures into a documented local test-data workflow.
- The package contains 71 synthetic entries, a manifest, a KDBX 3.1 database, and a plaintext XML inspection copy.
- It covers happy paths, normalization, Android mappings, REF resolution, malformed inputs, deduplication, Issue #1 URLs, skip-existing behavior, and concurrency.
- The encrypted database remains a disposable manual-test artifact. No real credentials or personal domains are permitted.

**Public provider corpus**

- Maintain at least 300 public URLs with category labels and stable identifiers.
- Include global high-traffic sites, regional services, small sites, self-hosted software homepages, redirects, deep paths, scheme-less URLs, SVG-only sites, manifests, missing icons, and Android app URLs.
- Avoid collecting credentials, private hosts, personal vault domains, or pages that require authentication.
- Record why each entry exists and the expected outcome class. Do not require a pixel-identical icon because legitimate site artwork changes over time.

### 6.2 Benchmark execution

The existing `eng/benchmark-presets.ps1` becomes a repeatable experiment harness rather than a fixed list of hand-coded profile variants.

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
- Row-level CSV or JSON output plus human-readable summaries.
- Resume/checkpoint behavior so a network interruption does not discard an entire run.

### 6.3 Evaluation metrics

No single success percentage determines the winner. Profile decisions will consider:

- Coverage: proportion returning a usable icon.
- Correctness: proportion matching the intended site or application rather than a generic or wrong brand.
- Quality: dimensions, format, blank/placeholder detection, and synthetic status.
- Latency: median, p95, maximum, and total batch duration.
- Reliability: timeout, error, and run-to-run variance rates.
- Cost: provider calls and wasted fallbacks per successful result.
- Privacy exposure: which third parties receive a domain and how often.

Quality and correctness review uses explicit labels such as correct, acceptable synthetic, generic, wrong brand, blank, and unusable. Ambiguous results remain labeled ambiguous instead of being forced into success or failure.

### 6.4 Profile outcomes

The current Fast, Balanced, and Thorough presets are hypotheses, not compatibility constraints. Research may rename, replace, add, or remove user-facing profiles.

The final catalog should expose three or four clearly differentiated profiles. Candidate user needs are:

- Fast bulk work.
- Recommended everyday use.
- Privacy-leaning retrieval.
- Maximum coverage.

These are not final names or provider chains. They become final only after benchmark review. Custom mode remains available for advanced users.

Legacy saved preset names will map to the closest v1.3 profile. Existing custom provider settings and order will remain custom and will not be silently overwritten.

### 6.5 Provider-test boundaries

Provider code will gain deterministic tests at the parsing, request construction, candidate classification, and selection boundaries. Live endpoint tests will be opt-in because provider availability and network conditions are unstable. The benchmark suite, not required PR CI, detects live-provider drift.

## 7. Workstream B: guided-native plugin UX

### 7.1 Design direction

The selected direction is **guided native**: a familiar WinForms/KeePass appearance with better hierarchy and progressive disclosure. The design avoids web-like decoration that would feel foreign inside KeePass.

The settings window uses four focused pages:

1. **Overview** — profile choice, concise tradeoffs, and the most common behaviors.
2. **Downloads** — URL handling, existing-icon policy, auto-save behavior, icon limits, and batch behavior.
3. **Providers** — provider enablement, order, roles, privacy implications, and synthetic-fallback controls.
4. **Advanced** — timeouts, certificate behavior, diagnostics, reset/migration information, and other uncommon controls.

Overview is sufficient for an ordinary user. Selecting Custom or visiting Providers reveals advanced control without making it the default experience.

### 7.2 Profile selection

Profiles are presented as plain-language choices with:

- Intended use.
- Expected relative speed and coverage.
- Privacy behavior.
- Synthetic-fallback behavior.
- A clear recommended marker where supported by evidence.

The UI will not display unstable benchmark timings as if they were guarantees. It may use measured relative labels and link users to versioned benchmark details.

### 7.3 First run

The current informational disclosure becomes a short guided first-run decision:

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

Retry must operate on an explicit set of failed or not-found entries and must not reprocess successful entries. Cancellation remains distinct from failure.

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

Automated checks will cover:

- Broken internal and external links where practical.
- Missing assets and incorrect release download paths.
- HTML structure and required metadata.
- Profile-data consistency.
- Basic accessibility rules.
- Responsive smoke checks at phone, tablet, and desktop widths.

Manual review will cover readability, keyboard navigation, dark/light appearance if retained, and factual agreement with the final plugin build.

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
- Explicit C# 5 compatibility build.
- PLGX creation and runtime load verification.
- GitHub Pages build/deployment checks.
- No unexpected warnings in release validation.

## 12. Delivery milestones

### Milestone 0: baseline and test-data intake

- Record the current commit, 96-test baseline, v1.2 profile definitions, and existing benchmark results.
- Document the supplied synthetic test package and safe handling rules.
- Define versioned corpus schemas and expected-outcome labels.

**Exit:** baseline is reproducible and test inputs are documented.

### Milestone 1: benchmark foundation

- Refactor the harness around declarative profile definitions.
- Add row-level results, environment metadata, checkpoints, repetitions, cache modes, and richer quality fields.
- Add deterministic provider boundary tests.
- Assemble and review the public corpus.

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

- Establish shared site assets and focused documentation pages.
- Add final screenshots, profile comparisons, privacy, troubleshooting, benchmarks, compatibility, and contribution guidance.
- Add link, consistency, responsive, and accessibility checks.

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
- Legacy preset and Custom settings migrate without silent loss of user choices.
- Settings, first run, progress, completion, cancellation, partial failure, and retry flows pass manual review.
- Critical plugin surfaces pass keyboard, DPI, high-contrast, and clipping checks.
- The release PLGX is created and load-tested in KeePass.
- The website contains all required pages, uses final screenshots, has no known broken primary navigation/download links, and accurately matches the release candidate.
- Privacy statements name the behavior of the final profiles and do not imply transmission of credentials or vault contents.

## 14. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Live provider results vary by time and network | Use repetitions, environment metadata, percentiles, and versioned snapshots; do not make live runs required PR CI. |
| Visual correctness is hard to automate | Use explicit review labels, sampled human review, and deterministic placeholder/format tests. |
| UI redesign breaks PLGX or C# 5 compatibility | Keep WinForms and existing host integration; validate C# 5 and PLGX loading throughout Milestone 3. |
| Renamed profiles surprise existing users | Use stable identifiers, migration aliases, idempotent migration, and clear upgrade notes. |
| Website and plugin descriptions drift | Generate/check website profile data from the shared catalog and gate the release on factual review. |
| Scope expands into a full rewrite | Enforce milestone exits and the non-goals; refactor only boundaries required for evidence, UX, and consistency. |

## 15. Decisions recorded

- All three workstreams belong in v1.3.0.
- Work is evidence-led: provider research, then plugin UX, then website completion.
- The plugin UI direction is guided native.
- The existing profile model is highly flexible and may change based on evidence.
- The primary audience is the everyday KeePass user, followed by privacy-conscious users, power users, and contributors.
- The supplied test package is safe synthetic data and forms the regression corpus baseline.
- The website remains static and dependency-free but expands into focused documentation pages.

## 16. Next step after approval

After this design is reviewed and approved, create a task-level implementation plan with exact file changes, test-first steps, verification commands, and review checkpoints for each milestone. No production implementation begins before that plan is approved.
