# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
