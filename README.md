# KeeFetch

[![Build Status](https://github.com/tzii/KeeFetch/workflows/Build%20KeeFetch/badge.svg)](https://github.com/tzii/KeeFetch/actions)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/tzii/KeeFetch)](https://github.com/tzii/KeeFetch/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A fast and smart favicon downloader plugin for KeePass 2.x.

![KeePass Plugin](https://img.shields.io/badge/KeePass-2.x%20Plugin-blue)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple)

## Features

- **Concurrent downloads** — Uses `SemaphoreSlim` for parallel favicon fetching without freezing the UI
- **Smart icon detection** — Prioritizes apple-touch-icon, detects favicon-32x32.png, favicon-96x96.png, and modern patterns with `sizes` attribute parsing
- **Robust fallback system** — Direct site → Google → DuckDuckGo → Icon Horse → Yandex
- **Duplicate avoidance** — SHA-256-hashes icon data (truncated to 128 bits) to reuse existing custom icons
- **Auto-prefix URLs** — Automatically adds `https://` to entries without a scheme
- **Title field fallback** — Uses the Title field if URL is empty
- **Skip existing icons** — Optionally skip entries that already have custom icons
- **Configurable icon size** — Scales down to configurable max (default 128×128 px)
- **Icon name prefix** — Configurable prefix (default: `kpif-`) for custom icon names
- **Android URL support** — Converts `androidapp://` URLs to web domains with 100+ app mappings, Google Play Store fallback
- **Placeholder resolution** — Resolves KeePass `{REF:...}` placeholders in URL fields
- **Self-signed certificate support** — Optional bypass for internal servers
- **Configurable timeout** — 5–60 seconds
- **Auto-save** — Optionally save database after downloading
- **Ghost modification fix** — Database only marked modified when icons actually change
- **Proxy support** — Respects KeePass proxy settings
- **Modern TLS** — Supports TLS 1.2/1.3 on .NET 4.8

## Installation

### Quick Install (Recommended)

1. Download `KeeFetch.plgx` from the [latest release](https://github.com/tzii/KeeFetch/releases/latest)
2. Copy the file into your KeePass `Plugins` folder:
   - **Portable**: `KeePass/Plugins/`
   - **Installed**: `%ProgramFiles(x86)%/KeePass Password Safe 2/Plugins/` or `%ProgramFiles%/KeePass Password Safe 2/Plugins/`
3. Restart KeePass

### Plugin File Types

| File | Size | Description |
|------|------|-------------|
| `KeeFetch.plgx` | ~28KB | **Recommended** - Self-contained plugin archive |
| `KeeFetch.dll` | ~28KB | Raw assembly file, requires .NET Framework 4.8 |

**Note:** Use `.plgx` unless you have specific reasons to use the DLL directly.

## Building from Source

### Prerequisites

- Windows (for PLGX creation)
- Visual Studio 2019+ or MSBuild
- .NET Framework 4.8 SDK
- KeePass 2.x (for PLGX creation)

### Build Commands

```powershell
# Build DLL
msbuild KeeFetch.csproj /p:Configuration=Release

# Build PLGX (requires KeePass.exe)
KeePass.exe --plgx-create "path\to\KeeFetch"
```

### Automated Builds

CI builds are triggered on every push and release. See [GitHub Actions](https://github.com/tzii/KeeFetch/actions) for build history.

## Usage

### Downloading Favicons

| Method | Location | Description |
|--------|----------|-------------|
| **Single Entry** | Right-click entry → "KeeFetch - Download Favicons" | Download icon for one entry |
| **Group (Recursive)** | Right-click group → "KeeFetch - Download Favicons" | Download icons for all entries in group and subgroups |
| **All Entries** | Tools → "KeeFetch" → "Download All Favicons" | Download icons for entire database |
| **Settings** | Tools → "KeeFetch" → "Settings..." | Configure plugin options |

### Configuration Options

- **Maximum icon size** — Scale icons to specific size (32-512 px, default: 128)
- **Icon name prefix** — Prefix for custom icon names (default: `kpif-`)
- **Timeout** — Download timeout per provider (5-60 seconds)
- **Skip existing icons** — Skip entries that already have custom icons
- **Auto-save database** — Automatically save after downloading icons
- **Accept self-signed certificates** — Allow HTTPS with invalid certificates
- **Enable/disable providers** — Toggle individual icon sources

## Project Structure

```
KeeFetch/
├── KeeFetchExt.cs              Main plugin entry point
├── FaviconDownloader.cs        Core download orchestrator with fallback chain
├── FaviconDialog.cs            Concurrent download UI with progress
├── Configuration.cs            Plugin settings (persisted via KeePass config)
├── SettingsForm.cs             Settings dialog
├── AndroidAppMapper.cs         androidapp:// URL → web domain mapper
├── Util.cs                     Image resize, hashing, URL helpers
├── IconProviders/
│   ├── IIconProvider.cs        Provider interface
│   ├── IconProviderBase.cs     Shared HTTP download logic
│   ├── DirectSiteProvider.cs   Primary: HTML parsing, apple-touch-icon, etc.
│   ├── GoogleProvider.cs       Fallback: Google S2 favicons
│   ├── DuckDuckGoProvider.cs   Fallback: DuckDuckGo icons
│   ├── IconHorseProvider.cs    Fallback: icon.horse
│   └── YandexProvider.cs       Fallback: Yandex favicon service
└── Properties/
    └── AssemblyInfo.cs
```

## Architecture

The download pipeline follows a **provider chain** pattern:

1. **DirectSiteProvider** fetches the actual website, parses HTML for `<link rel="icon">` and `<link rel="apple-touch-icon">` tags, and downloads the best candidate icon ordered by size and priority.
2. If the direct approach fails, **fallback providers** (Google S2, DuckDuckGo, Icon Horse, Yandex) are tried in order, each with reduced timeouts and a cumulative time budget.
3. For `androidapp://` URLs, the package name is mapped to a web domain via a 100+ entry lookup table, with Google Play Store icon scraping as a last resort.
4. Downloaded icons are hashed (SHA-256, truncated to 128 bits for `PwUuid`) to deduplicate against existing custom icons in the database.

Concurrency is managed with a `SemaphoreSlim` (max 8 parallel downloads) and progress is reported via KeePass's built-in `IStatusLogger`.

## Changelog

### v1.0.0 (2025-02-11)

**Initial stable release**

- Concurrent favicon downloading with provider fallback chain (Direct → Google → DuckDuckGo → Icon Horse → Yandex)
- Support for 100+ Android app URL mappings (`androidapp://` → web domain)
- Configurable settings with persistent storage
- Full test coverage (55 unit tests)
- Automated CI/CD with GitHub Actions
- GitHub Releases integration

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

See [PLAN.md](PLAN.md) for the development roadmap.

## Support

- **Issues**: [GitHub Issues](https://github.com/tzii/KeeFetch/issues)
- **Releases**: [GitHub Releases](https://github.com/tzii/KeeFetch/releases)
- **Discussions**: [GitHub Discussions](https://github.com/tzii/KeeFetch/discussions)

## Acknowledgments

- [KeePass Password Safe](https://keepass.info/) — The best password manager
- Icon providers: Google S2, DuckDuckGo, Icon Horse, Yandex

## License

MIT License — see [LICENSE](LICENSE) file for details
