# KeeFetch website

## Product and audience

KeeFetch is a favicon downloader plugin for KeePass 2.x. This website helps a KeePass user understand the benefit, get the real stable plugin, install it, and make informed choices about requests and diagnostics. The secondary audience is a contributor inspecting source and benchmark evidence.

The homepage explains the benefit through a plainly labeled local illustration. The other six pages are guides and references. Neither is a recreation of the actual KeePass application.

## Non-negotiable facts

- Preserve the stable download fields in `data/release.json` and the generated release markers. The stable download is v1.2.0 at this branch's starting point. v1.3 is a source preview, not a published download.
- Profile order, provider chain, timeout budgets and disclosure text come from the existing checked catalog. Budgets are not measured speed claims.
- The Privacy profile does not mean offline or same-origin-only. Preserve the linked-host, redirect and diagnostic caveats.
- The sample vault is an illustration. Do not label it as a screenshot or imply its transition timing measures the plugin.
- Do not change the plugin, release artifacts, benchmark evidence or deployment target as part of this redesign.

## Essential tasks

Download stable PLGX; check requirements; install, update or roll back; understand provider behavior; find diagnostics and security reporting; inspect study limitations.

## Runtime boundary

Seven static HTML pages, shared CSS and two small scripts. Local licensed assets. No analytics, external runtime assets, new account, service worker or application framework. All essential content, release links and the generated comparison work without JavaScript. Theme and motion are optional local preferences.
