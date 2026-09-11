# KeeFetch documentation

Start with the [user guide](https://tzii.github.io/KeeFetch/getting-started.html), [privacy details](https://tzii.github.io/KeeFetch/privacy.html) or [contributor guide](../CONTRIBUTING.md).

## Release and engineering records

- [v1.3.0 release notes](releases/v1.3.0.md)
- [Published release verification](validation/2026-09-11-v1.3-publication.md)
- [Release validation and accepted packages](validation/v1.3-release-validation.md)
- [Native-host acceptance](validation/2026-09-10-v1.3-manual-host-results.md)
- [Provider study and scoring limitations](benchmarks/v1.3-provider-study.md)
- [Repository cleanup and archive record](validation/2026-09-11-repository-cleanup.md)

Dated plans and validation reports describe the state at the time they were written. The [latest release](https://github.com/tzii/KeeFetch/releases/latest) and current user guide describe the shipped product.

## Files

| File | Description |
|------|-------------|
| `assets/keefetch-banner.svg` | Original, code-authored README banner matching the official website; no external assets or scripts |
| `../site/assets/media/*.png` | Unedited v1.3 first-run, settings and completion captures shared with the website |
| `usage-single.gif` | Demo of single entry right-click favicon fetch |
| `usage-group.gif` | Demo of bulk group processing with progress dialog |
| `usage-android.gif` | Demo of Android `androidapp://` URL mapping |
| `usage-maintenance.png` | Screenshot of database-wide maintenance via Tools menu |
| `availability-first-reliability-implementation.md` | File-by-file implementation summary for the reliability redesign |
| `availability-first-reliability-validation.md` | Build/test/packaging validation commands and outcomes |
| `availability-first-reliability-regression.md` | Regression corpus and test coverage summary |

## Recording Guidelines

The three GIFs and the maintenance image above are historical. The README keeps them in an expandable section until new recordings are available. Current screenshots live in `site/assets/media/` so the README and website use the same files.

If updating or re-recording demos:

1. **Theme:** Use standard Windows theme (works for both dark/light mode users)
2. **Tool:** Use [ScreenToGif](https://www.screentogif.com/) or similar
3. **Specs:** 800px width, <2MB per GIF, 30fps
4. **Cursor:** Highlight clicks with cursor effects
