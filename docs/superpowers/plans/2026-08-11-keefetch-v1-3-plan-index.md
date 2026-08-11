# KeeFetch v1.3 Implementation Plan Index

The approved v1.3 design is implemented through five bounded plans. Execute them in order; each plan ends in a reviewable, testable checkpoint and the next plan assumes the previous plan is complete.

1. [Benchmark foundation](2026-08-11-keefetch-v1-3-01-benchmark-foundation.md)
2. [Profile study, catalog, and migration](2026-08-11-keefetch-v1-3-02-profile-catalog-migration.md)
3. [Guided-native plugin UX](2026-08-11-keefetch-v1-3-03-guided-native-ux.md)
4. [Website completion](2026-08-11-keefetch-v1-3-04-website-completion.md)
5. [Release validation](2026-08-11-keefetch-v1-3-05-release-validation.md)

Implementation must occur in a dedicated `codex/` worktree or feature branch, not directly on `master`. Do not start a later plan while an earlier plan's exit criteria are unmet.
