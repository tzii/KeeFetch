# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Study-selected managed fetch profiles (`Fast`, `Balanced`, `Privacy`, `Thorough`) chosen by the v1.3 provider study; see `docs/benchmarks/v1.3-provider-study.md`
- Benchmark harness, regression corpus, and profile catalog tooling under `eng/`
- CI gates: PLGX manifest consistency (`eng/check-plgx-manifest.ps1`), version/tag consistency (`eng/check-version.ps1`), and `SHA256SUMS.txt` release asset

### Changed
- Balanced profile now runs Direct Site → Twenty Icons → DuckDuckGo → Google → Yandex → Icon Horse with synthetic fallbacks allowed (supersedes the 1.2.0 Direct Site/Google/Favicone default); Favicone remains available for Custom configurations
- Fast profile now runs Direct Site → Google → Twenty Icons; Thorough runs Direct Site → Yandex
- Existing `Fast`/`Balanced`/`Thorough` preset settings migrate to the matching managed profile; `Custom` is preserved

### Security
- Self-signed certificate acceptance now applies only to KeeFetch's own HTTP client instead of the process-wide `ServicePointManager` callback shared with KeePass and other plugins
- KeeFetch's own HTTP client now negotiates only TLS 1.2/1.3 and no longer assigns `ServicePointManager.SecurityProtocol`; the previous code set TLS 1.0/1.1/1.2/1.3 for the whole KeePass process
- Private-address detection now covers `0.0.0.0/8`, carrier-grade NAT (`100.64.0.0/10`), documentation/benchmark ranges, multicast, IPv4-mapped IPv6, and NAT64 addresses, and resolver providers now reject redirects that land on a private host
- GitHub Actions workflow runs with a read-only token except for release asset upload; the release action is pinned by commit SHA

### Fixed
- Bare IPv6 literals are no longer misclassified as intranet hostnames

## [1.2.0] - 2026-04-26

### Added
- Availability-first favicon selection with structured provider candidates, trust tiers, confidence scoring, and rejection diagnostics
- Fetch presets for Fast, Balanced, Thorough, and Custom workflows
- New Twenty Icons and Favicone providers
- Per-provider enablement, preset-managed provider order, and synthetic fallback controls
- First-run disclosure for third-party favicon service usage
- Per-entry diagnostics log and CSV export with provider timings and miss reasons
- In-run coalescing and negative caching for repeated origins during bulk downloads
- Regression coverage for provider selection, presets, diagnostics, and settings layout

### Changed
- Balanced preset now defaults to Direct Site, Google, and Favicone for a speed/coverage tradeoff validated on a 300-entry KeePass test database
- Direct-site fetching now parses manifest icons, apple touch icons, SVG-only cases, and Open Graph image fallbacks
- Direct-site candidate URLs are canonicalized before download to avoid duplicate equivalent fetches
- Settings dialog layout now shows all providers clearly, explains preset-managed providers, and avoids clipped controls at normal Windows DPI
- KeeFetch menu subcommands now include icons

### Fixed
- Prevented synthetic or placeholder-prone providers from outranking stronger direct-site or resolver-backed candidates
- Fixed overlapping provider order buttons and clipped settings text in the settings dialog
- Preserved provider/tier/synthetic metadata on cache hits for accurate completion summaries

## [1.1.1] - 2026-02-13

### Changed
- Removed unused `IWebProxy` parameter from `IIconProvider.GetIconAsync()` and all implementations — the parameter was accepted but never applied to HttpClient
- Consolidated multiple static HttpClient instances into a single `SharedHttp` static class

## [1.1.0] - 2026-02-11

### Added
- Comprehensive XML documentation comments for all public/internal APIs
- SDK-style project format for KeeFetch.csproj
- CancellationToken support throughout the codebase for responsive cancellation
- Thread-safe certificate callback handling with reference counting
- CHANGELOG.md to track version history

### Changed
- Migrated FaviconDialog from Thread-based to async/await pattern
- Replaced CountdownEvent with SemaphoreSlim for better concurrency control
- Improved error handling with structured logging via Logger.cs
- Refactored icon providers to use IconProviderBase for DRY code

### Fixed
- Fixed potential race condition in certificate validation callback setup
- Fixed UI thread marshaling issues during concurrent downloads
- Improved timeout handling for fallback providers

## [1.0.0] - 2025-01-01

### Added
- Initial release of KeeFetch plugin for KeePass
- Favicon download for password entries from multiple sources:
  - Direct site favicon.ico and HTML link tags
  - Google favicon service
  - DuckDuckGo favicon service
  - Icon Horse service
  - Yandex favicon service
- Android app URL support (androidapp://) with Google Play Store icon fetching
- Configuration options:
  - Auto-prefix URLs with http:// or https://
  - Use title field as fallback for domain guessing
  - Skip entries with existing icons
  - Auto-save database after download
  - Allow self-signed certificates
  - Toggle third-party fallback providers
  - Configurable icon size and timeout
- Progress dialog with cancellation support
- Error logging to file
- Settings dialog for configuration
