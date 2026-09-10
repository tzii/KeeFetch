# KeeFetch website product brief

This brief covers the website, not the native KeePass interface.

## Purpose and audience

Help KeePass 2.x users understand what KeeFetch does, download the right release,
install it, and make an informed choice about provider requests. Readers include
first-time plugin users and people maintaining large or privacy-sensitive vaults.
The homepage should explain the benefit immediately. Documentation should answer
specific installation, configuration and troubleshooting questions without a
JavaScript dependency.

The product is a favicon downloader inside KeePass. Familiar website icons make
entries easier to recognize. The website does not read a vault or fetch icons from
sample domains. Its before/after interaction is an explicitly labeled illustration.

## Product truth and boundaries

- The stable download is v1.2.0. The described v1.3.0 features are a source preview.
  `data/release.json` and the synchronizer own generated release links and labels.
- `data/profiles.json` owns managed profile facts. Do not tune time budgets,
  providers, names or evidence as a design change.
- Resolver services can receive entry domains. Direct Site can follow asset links
  and redirects to other hosts. Privacy is not a promise of network isolation.
  Diagnostic files may contain titles and URLs.
- Keep genuine product screenshots separate from illustrations. No fabricated
  host-validation evidence, performance claims, testimonials or usage statistics.
- KeePass 2.x and .NET Framework 4.8 are the documented requirements. KeePassXC is
  not this plugin's host. Native release validation remains a separate gate.
- Destination: GitHub Pages, under `/KeeFetch/`. A design branch is not a deployment.
  No hosted editor, analytics, remote font service or runtime package dependency.

## Content and sources

Preserve all seven pages: overview, getting started, profiles, privacy,
troubleshooting, benchmarks and contributing. Installation, update, uninstall,
rollback and checksum caveats must remain findable.

This redesign starts from `19a0c224ca4dbf7042d22c3497e7599f61937c0b` on
`codex/v1-3-guided-native-ux`. Its completed 19-candidate study differs from the
older study on `master`. Evidence links use that immutable source snapshot via
`EVIDENCE_REF` in `eng/sync-site-profiles.py`. Updating this reference requires
checking the replacement evidence, not merely changing a branch name.

## Success and review boundaries

Downloads, documentation, navigation and comparisons work without JavaScript.
With JavaScript, controls work by keyboard and tolerate blocked storage, missing
optional browser APIs and reduced-motion preferences. Normal browsing sends no
requests to third-party asset hosts or illustrated brands.

Automated browser checks and screenshots do not establish physical-device,
screen-reader or native KeePass compatibility. Record which checks ran and keep
those remaining acceptance checks explicit.
