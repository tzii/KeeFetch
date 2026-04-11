# Availability-First Reliability Implementation

This document describes the reliability redesign implemented from the KeeFetch availability-first plan/spec.

## Product posture preserved

- Third-party fallbacks remain enabled by default.
- Synthetic fallbacks remain allowed by default.
- Selection is now smarter: synthetic/placeholder-prone results cannot outrank stronger direct/resolved candidates.

## Engine redesign

KeeFetch moved from `first valid image wins` to a **candidate collection + tiered selector** model:

1. Providers emit structured `IconCandidate` objects.
2. Candidates are classified/ranked by tier and confidence.
3. Selector applies rejection rules (blank/synthetic containment) before final selection.
4. Result includes diagnostics metadata, attempted providers, and rejected candidates.

### New selection contracts

- `IconSelection/IconTier.cs`
- `IconSelection/IconCandidate.cs`
- `IconSelection/IconSelectionResult.cs`
- `IconSelection/IconRequest.cs`
- `IconSelection/ProviderCapabilities.cs`
- `IconSelection/IconSelector.cs`

## Provider integrations and ordering

Default provider order is now:

1. Direct Site
2. Twenty Icons
3. DuckDuckGo
4. Google
5. Yandex
6. Favicone
7. Icon Horse

Implemented providers:

- Added `IconProviders/TwentyIconsProvider.cs` (Tier 2 strong resolver).
- Added `IconProviders/FaviconeProvider.cs` (Tier 3 synthetic-capable fallback).
- Updated all provider classes to capabilities + candidate contract.
- Demoted Icon Horse to explicit last-resort synthetic behavior.

## Direct-site improvements

`IconProviders/DirectSiteProvider.cs` now includes:

- `rel=icon` + `shortcut icon`
- `apple-touch-icon` + `apple-touch-icon-precomposed`
- `rel=manifest` discovery
- manifest icon parsing with relative URL resolution
- SVG detection/signaling for selector competition
- `og:image` as weaker backup candidate (deprioritized)

## Downloader/orchestration changes

`FaviconDownloader.cs` now performs:

- collect -> rank -> select
- provider-level concurrency caps
- bounded retry handling for transient failures and rate-limit style responses
- temporary in-run provider cooldown when repeated failures occur
- cache identity keyed by normalized origin (`scheme://host:port`)

Android path was aligned to selection flow:

- `AndroidAppMapper.cs` now emits a structured Google Play candidate (`Tier 1`) via `FetchGooglePlayIconCandidateAsync`.

## Settings/config updates

`Configuration.cs` and settings UI now include:

- `AllowSyntheticFallbacks` (default `true`)
- per-provider enable flags
- provider order persistence (`ProviderOrder`)
- first-run disclosure marker (`HasSeenFirstRunDisclosure`)

`SettingsForm.cs` / `SettingsForm.Designer.cs` expose all of the above controls.

`KeeFetchExt.cs` shows the one-time first-run disclosure.

## Diagnostics improvements

`FaviconDialog.cs` now logs/outputs:

- selected provider
- selected tier
- synthetic selection flag
- attempted providers
- rejected-candidate reasons

Completion summary now breaks down:

- direct-site successes
- third-party resolved successes
- synthetic fallback successes
- not found
- errors

## Documentation updates

- Root `README.md` updated for the new architecture and provider chain.
- `KeeFetch.plgx.csproj` updated to include new source files for PLGX compilation.
