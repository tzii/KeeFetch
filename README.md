# KeeFetch

A fast and smart favicon downloader plugin for KeePass 2.x.

## Features

- **Concurrent downloads** — Uses ThreadPool for parallel favicon fetching without freezing the UI
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

1. Download `KeeFetch.dll` or `KeeFetch.plgx` from the [latest release](https://github.com/tzii/KeeFetch/releases)
2. Copy it into the KeePass `Plugins` folder
3. Restart KeePass

## Building

### Prerequisites
- Visual Studio 2019+ or MSBuild
- .NET Framework 4.8 SDK
- KeePass 2.x (install or portable ZIP)

### Build DLL
```
msbuild KeeFetch.csproj /p:Configuration=Release
```

### Build PLGX
```
KeePass.exe --plgx-create "path\to\KeeFetch"
```

## Usage

- **Right-click entries** → "KeeFetch - Download Favicons"
- **Right-click a group** → "KeeFetch - Download Favicons" (recursive, includes subgroups)
- **Tools menu** → "KeeFetch" → "Download All Favicons"
- **Tools menu** → "KeeFetch" → "Settings..."

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

## Contributing

See [PLAN.md](PLAN.md) for the current refactor roadmap.

## License

MIT
