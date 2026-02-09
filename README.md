# KeeFetch

A fast and smart favicon downloader plugin for KeePass 2.x.

## Features

- **Concurrent downloads** — Uses ThreadPool for parallel favicon fetching without freezing the UI
- **Smart icon detection** — Prioritizes apple-touch-icon, detects favicon-32x32.png, favicon-96x96.png, and modern patterns with `sizes` attribute parsing
- **Robust fallback system** — Direct site → Google → DuckDuckGo → Icon Horse → Yandex
- **Duplicate avoidance** — MD5-hashes icon data to reuse existing custom icons
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

1. Build the project (see below)
2. Copy `KeeFetch.dll` (or `KeeFetch.plgx`) into the KeePass `Plugins` folder
3. Restart KeePass

## Building

### Prerequisites
- Visual Studio 2019+ or MSBuild
- .NET Framework 4.8 SDK
- KeePass 2.x portable ZIP (place `KeePass.exe` in the parent directory of this project, or adjust the `HintPath` in the `.csproj`)

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
- **Right-click a group** → "KeeFetch - Download Favicons" → "Download for this group (recursive)"
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
│   ├── DirectSiteProvider.cs   Primary: HTML parsing, apple-touch-icon, etc.
│   ├── GoogleProvider.cs       Fallback: Google S2 favicons
│   ├── DuckDuckGoProvider.cs   Fallback: DuckDuckGo icons
│   ├── IconHorseProvider.cs    Fallback: icon.horse
│   └── YandexProvider.cs       Fallback: Yandex favicon service
└── Properties/
    └── AssemblyInfo.cs
```

## License

MIT
