# KeeFetch current state

Status: WORKING
Agent: Codex

Focus: Tidy the repository's PRs and branches and refresh the README with a code-authored banner.
Next: Finish PR20 CI for the independently validated SDK refresh, merge it and prune its completed branch.
Pointer: artifacts/repository-cleanup-20260911
As-of: 2026-09-11 Europe/Rome

Notes:
- GitHub confirms v1.3.0 is published and master is 4ebc7a8. Release packages and the official website remain accepted.
- Audit found six open PRs: four superseded website designs and two dependency updates. Seventeen remote branches were recorded before cleanup.
- All Git refs are saved in a verified all-branches-before.bundle (SHA-256 b0b2bbd1133aaa144da0eb7c980ce07a9995e483b7a8e07fd84070740050c7c7). Root working changes and site-next.css are also backed up in the audit directory.
- PR19 merged as 522353d; PRs #11/#17 are merged too. Final PR CI, post-merge build and Pages deployment passed. README and app-icon banner are live and verified on GitHub.
- Active source checkout: artifacts/repository-cleanup-20260911/worktree, branch codex/test-sdk-refresh, preparing the safe update to PR20. Dependabot's immediate rescan proposed SDK 18.10.0 and incompatible Coverlet 10.0.0. SDK 18.10.0 passes the warning-free solution build and all 237 tests with real coverage; Coverlet stays at 6.0.4. All four published incompatible collector versions are explicitly excluded, with future versions still eligible.
- Four superseded website PRs are closed, 14 old remote and 10 old local branch names are removed, and auto-delete-after-merge is enabled. All histories remain recoverable; historical worktree files remain intact.
- Root is now on current master. Its old local website experiment and handoff files are preserved as stash 3621c96d6d5ef93b0abcbfb279e54ef46fdd478d, a permanent local archive ref, and recovery-with-local-edits.bundle (SHA-256 1f9380dd03300f3351e9555751f7d811ccf64f2a6aa739e4cb7b71aa3bdbd435).
- README has current screenshots, an expandable historical-GIF section, installation/privacy guidance and a code-authored SVG banner. The owner requested the actual app icon in the banner; it is embedded byte-for-byte from Assets/Icons/keefetch-app.png and the updated layout passes desktop/mobile light/dark checks.
