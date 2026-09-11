# Repository cleanup — 2026-09-11

The owner requested cleanup of six open PRs and seventeen remote branches, plus a refreshed README and a banner made without image generation. The starting published revision was `4ebc7a813ed0fa12a83a658101c1cc15538a8fb2`, after the v1.3.0 release and stable website cutover.

## Disposition

- Design drafts [#5](https://github.com/tzii/KeeFetch/pull/5), [#6](https://github.com/tzii/KeeFetch/pull/6), [#14](https://github.com/tzii/KeeFetch/pull/14) and [#15](https://github.com/tzii/KeeFetch/pull/15) were closed as superseded by the owner-selected website in #16 and #18. Their distinct designs and test tooling were archived, not applied to the published site.
- Fourteen remote branches were deleted after ancestry checks or explicit superseded-design disposition. Deletion used expected-head leases and an atomic push, so concurrent branch changes would stop cleanup.
- Test-tooling PR #11 and Actions PR #17 are incorporated by merge commits into the housekeeping branch. The MSTest upgrade additionally replaces the obsolete `DataTestMethod` attribute with `TestMethod`, preserving all five migration data rows. No test is removed or suppressed. Coverlet 10.0.1 restored successfully but failed real coverage collection: its package contains only net8/net9/net10 build imports, none selected by this net48 project. The collector is updated from 6.0.0 to compatible 6.0.4 instead. Dependabot ignores only 10.0.1; future versions remain reviewable.
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

Unrelated root changes to `site/index.html`, the untracked `site/assets/css/site-next.css` and local handoff files were preserved in stash `3621c96d6d5ef93b0abcbfb279e54ef46fdd478d` and the recovery directory. Their Git blob hashes were checked against the originals. A permanent local ref, `refs/archive/repository-cleanup-20260911/root-working-copy`, also retains the stash, including its untracked-files parent. Use `git stash branch recovered-local-site 3621c96d6d5ef93b0abcbfb279e54ef46fdd478d` from a clean checkout to restore the experiment on its original base.

The main checkout was moved to current master. Ten old local branch names were archived and removed; their worktree directories and local edits remain intact at detached revisions, with extra patches/untracked-file copies in `worktree-backups/`. The additional `recovery-with-local-edits.bundle` includes the stash and all archive refs (SHA-256 `1f9380dd03300f3351e9555751f7d811ccf64f2a6aa739e4cb7b71aa3bdbd435`). Release evidence, public v1.3.0 binaries, tag, website content and profile data were not changed by the README/dependency work.

## README and validation

The README leads with installation and actual v1.3 screenshots, keeps the old GIFs in a labelled expandable section, and retains provider/scoring/privacy limitations. The banner is an original, static, self-contained SVG using the official site's palette and path wordmark. At the owner's request, it embeds the existing KeeFetch app icon byte-for-byte beside the wordmark. The layout and machine illustration were written as SVG; no image generation was used. The file has no third-party logos, external fonts, scripts or network requests. The documentation index now points to release and study records.

Local verification: Release solution build and C# 5 production build both pass with `-warnaserror`; all **237 MSTests** pass with zero skipped; benchmark harness, profile export `-Check`, version, PLGX manifest and release-workflow safeguards pass. A real coverage run also passes all 237 tests and reports 4,041 covered KeeFetch lines out of 5,335. CI now requires exactly one coverage report containing executed KeeFetch code, so a missing collector cannot silently pass. GitHub CI on the combined PR is required before merge. Browser/layout evidence and final PR/run identifiers are retained in the local recovery directory.

Compatibility references for the dependency review: [MSTEST0044 migration](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0044), [checkout v7](https://github.com/actions/checkout/releases/tag/v7.0.0), [upload-artifact v7](https://github.com/actions/upload-artifact/releases/tag/v7.0.0), [setup-python v7](https://github.com/actions/setup-python/releases/tag/v7.0.0). Existing workflows do not use the removed `pip-install` input or fork checkout under `pull_request_target`; artifact archive behavior is unchanged.
