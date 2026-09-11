# Repository cleanup — 2026-09-11

The owner requested cleanup of six open PRs and seventeen remote branches, plus a refreshed README and a banner made without image generation. The starting published revision was `4ebc7a813ed0fa12a83a658101c1cc15538a8fb2`, after the v1.3.0 release and stable website cutover.

## Disposition

- Design drafts [#5](https://github.com/tzii/KeeFetch/pull/5), [#6](https://github.com/tzii/KeeFetch/pull/6), [#14](https://github.com/tzii/KeeFetch/pull/14) and [#15](https://github.com/tzii/KeeFetch/pull/15) were closed as superseded by the owner-selected website in #16 and #18. Their distinct designs and test tooling were archived, not applied to the published site.
- Fourteen remote branches were deleted after ancestry checks or explicit superseded-design disposition. Deletion used expected-head leases and an atomic push, so concurrent branch changes would stop cleanup.
- Test-tooling PR #11 and Actions PR #17 are incorporated by merge commits into the housekeeping branch. The MSTest upgrade additionally replaces the obsolete `DataTestMethod` attribute with `TestMethod`, preserving all five migration data rows. No test is removed or suppressed.
- Automatic deletion of merged PR branches is enabled to prevent another backlog of completed branches.

### Archived branches with unique history

| Branch | Retained head | Reason |
| --- | --- | --- |
| `agent/site-visual-polish` | `1ea1f2eed4dbccdc9038ec18a7fe59b55b8cf7a5` | Superseded design, PR #5 |
| `agent/site-icon-foundry-redesign` | `5ab2d27f9712ff08515d9a963be8fc03ad3d07d7` | Superseded design, PR #6 |
| `codex/site-icon-studio-2026-09-10` | `5f2d94d95b5347696bf0b0c82f13c6b43c7c429b` | Superseded design and its QA, PR #14 |
| `codex/site-retro-refined-2026-09-10` | `8345ae93fa29207f5e3f880822bdbaec2e79aa3b` | Superseded design and its QA, PR #15 |
| `codex/v1-3-release-gap-audit` | `c2ea5c60c1bf7ba908d5579180ec7cc343fb3976` | Historical pre-release assessment, superseded by published release evidence |

The other nine removed branches are ancestors of the published master: the official, playful and stable website branches; guided UX, profile foundation and selection; availability-first reliability, pre-public cleanup and hardening.

## Recovery and boundaries

Before cleanup, every local and remote Git ref was captured in `artifacts/repository-cleanup-20260911/refs-before.txt` and a complete, independently verified Git bundle:

`artifacts/repository-cleanup-20260911/all-branches-before.bundle`

SHA-256: `b0b2bbd1133aaa144da0eb7c980ce07a9995e483b7a8e07fd84070740050c7c7`.

These are local, gitignored recovery files, not downloadable release assets. Removed remote heads also remain under local `refs/archive/repository-cleanup-20260911/`. A branch can be recovered with `git switch -c recovered-design refs/archive/repository-cleanup-20260911/<old-branch>`, or by fetching the corresponding original ref from the bundle into a fresh checkout. Closed PRs retain the public discussion and diffs.

Unrelated root changes to `site/index.html` and the untracked `site/assets/css/site-next.css` were left intact and copied into the recovery directory. Release evidence and existing worktree directories were retained. Public v1.3.0 binaries, tag, website content and profile data were not changed by the README/dependency work.

## README and validation

The README leads with installation and actual v1.3 screenshots, keeps the old GIFs in a labelled expandable section, and retains provider/scoring/privacy limitations. The banner is an original, static, self-contained SVG using the official site's palette and path wordmark; it contains no generated raster art, third-party logos, external fonts, scripts or network requests. The documentation index now points to release and study records.

Local verification: Release solution build and C# 5 production build both pass with `-warnaserror`; all **237 MSTests** pass with zero skipped; benchmark harness, profile export `-Check`, version, PLGX manifest and release-workflow safeguards pass. GitHub CI on the combined PR is required before merge. Browser/layout evidence and final PR/run identifiers are retained in the local recovery directory.

Compatibility references for the dependency review: [MSTEST0044 migration](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0044), [checkout v7](https://github.com/actions/checkout/releases/tag/v7.0.0), [upload-artifact v7](https://github.com/actions/upload-artifact/releases/tag/v7.0.0), [setup-python v7](https://github.com/actions/setup-python/releases/tag/v7.0.0). Existing workflows do not use the removed `pip-install` input or fork checkout under `pull_request_target`; artifact archive behavior is unchanged.
