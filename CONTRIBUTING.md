# Contributing to KeeFetch

Thank you for your interest in contributing to KeeFetch!

## How to Contribute

1. **Fork the Repository**: Create your own fork of the project.
2. **Clone the Fork**: `git clone https://github.com/YOUR_USERNAME/KeeFetch.git`
3. **Create a Branch**: `git checkout -b feature/your-feature-name`
4. **Make Changes**: Implement your feature or fix.
5. **Add Tests**: Ensure your changes are covered by unit tests in `KeeFetch.Tests`.
6. **Run Tests**: Use `dotnet test` to verify everything is working.
7. **Commit Changes**: `git commit -m 'feat: add amazing feature'`
8. **Push to GitHub**: `git push origin feature/your-feature-name`
9. **Open a Pull Request**: Submit your PR for review.

## Coding Standards

- Follow existing code style (see `.editorconfig`).
- Use XML documentation for public and internal members.
- Keep methods small and focused.
- Avoid external dependencies unless absolutely necessary.
- **All code must be C# 5 compatible** — the PLGX is compiled by KeePass using `CSharpCodeProvider` (legacy `csc.exe`). This means: no string interpolation, no expression-bodied members, no null-conditional operators, no pattern matching.

## Development Environment

- Visual Studio 2022 or VS Code with C# Dev Kit.
- .NET 8 SDK (required for SDK-style project support).
- .NET Framework 4.8 targeting pack (the plugin targets .NET Framework 4.8).
- KeePass 2.x installed for testing and PLGX creation.

## Building the PLGX

The PLGX is built using the `KeeFetch.plgx.csproj` file which is a legacy-style project file required by KeePass. The main `KeeFetch.csproj` is an SDK-style project used for modern development and testing.

## Project Structure

```
KeeFetch/
├── KeeFetchExt.cs              # Plugin entry point — registers menus, handles click events
├── FaviconDownloader.cs        # Orchestrates the provider chain with caching and timeouts
├── FaviconDialog.cs            # Progress dialog — concurrent downloads with SemaphoreSlim
├── Configuration.cs            # Plugin settings stored in KeePass custom config
├── SettingsForm.cs             # Settings UI (WinForms)
├── SettingsForm.Designer.cs    # WinForms designer-generated code
├── Logger.cs                   # Thread-safe in-memory log with level filtering
├── Util.cs                     # URL parsing, image resizing, hashing, proxy helpers
├── AndroidAppMapper.cs         # Maps androidapp:// URLs to web domains + Play Store scraping
├── IconProviders/
│   ├── IIconProvider.cs        # Interface — GetIconAsync(host, size, timeout, proxy, token)
│   ├── IconProviderBase.cs     # Abstract base — shared HTTP download + validation logic
│   ├── DirectSiteProvider.cs   # Primary — parses HTML for <link rel="icon">, apple-touch-icon
│   ├── GoogleProvider.cs       # Fallback — google.com/s2/favicons API
│   ├── DuckDuckGoProvider.cs   # Fallback — icons.duckduckgo.com API
│   ├── IconHorseProvider.cs    # Fallback — icon.horse API
│   └── YandexProvider.cs       # Fallback — favicon.yandex.net API
├── KeeFetch.Tests/
│   ├── UtilTests.cs            # Tests for URL parsing, hashing, image validation
│   ├── AndroidAppMapperTests.cs# Tests for Android URL mapping and package guessing
│   ├── DirectSiteProviderTests.cs # Tests for HTML icon link parsing
│   ├── LoggerTests.cs          # Tests for logging, limits, and filtering
│   └── ConfigurationTests.cs   # Tests for config properties and clamping
├── Properties/
│   └── AssemblyInfo.cs         # Assembly metadata and InternalsVisibleTo
├── KeeFetch.csproj             # SDK-style project (development & testing)
├── KeeFetch.plgx.csproj        # Legacy-style project (PLGX creation only)
├── docs/                       # README demo GIFs and screenshots
└── .github/workflows/
    └── build.yml               # CI: build DLL, run tests, create PLGX, publish releases
```

### Architecture Overview

The plugin follows a **provider-based fallback chain**:

1. **`DirectSiteProvider`** — Fetches the site directly, parses `<head>` for icon links, prioritizes `apple-touch-icon` and large PNGs.
2. **`GoogleProvider`** → **`DuckDuckGoProvider`** → **`IconHorseProvider`** → **`YandexProvider`** — Third-party fallbacks tried in order if the direct attempt fails.

`FaviconDownloader` orchestrates this chain with cumulative timeouts and per-host caching. `FaviconDialog` runs downloads concurrently (up to 8 parallel via `SemaphoreSlim`) and marshals database writes to the UI thread.
