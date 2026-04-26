# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
