# KeeFetch

[![Build Status](https://github.com/tzii/KeeFetch/workflows/Build%20KeeFetch/badge.svg)](https://github.com/tzii/KeeFetch/actions)
[![GitHub Release](https://img.shields.io/github/v/release/tzii/KeeFetch?include_prereleases)](https://github.com/tzii/KeeFetch/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A fast, smart, and modern favicon downloader plugin for KeePass 2.x.

![KeePass Plugin](https://img.shields.io/badge/KeePass-2.x%20Plugin-blue)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple)

## ✨ Features

- **Concurrent downloads** — Parallel favicon fetching using `SemaphoreSlim` to keep the UI responsive.
- **Smart icon detection** — Prioritizes `apple-touch-icon`, parses modern `sizes` attributes, and detects high-resolution candidates.
- **Robust fallback chain** — Direct site → Google → DuckDuckGo → Icon Horse → Yandex.
- **Deduplication** — SHA-256 hashing ensures icons aren't duplicated in your database.
- **Android Support** — Converts `androidapp://` URLs to web domains with 100+ built-in mappings and Play Store scraping.
- **Intelligent URL handling** — Resolves KeePass `{REF:...}` placeholders and auto-prefixes schemes.
- **Modern Standards** — Supports TLS 1.3, respects KeePass proxy settings, and handles self-signed certificates.

## 🚀 Installation

### Quick Install (Recommended)

1. Download `KeeFetch.plgx` from the [latest release](https://github.com/tzii/KeeFetch/releases/latest).
2. Copy the file into your KeePass `Plugins` folder:
   - **Portable**: `KeePass/Plugins/`
   - **Installed**: `%ProgramFiles%/KeePass Password Safe 2/Plugins/`
3. Restart KeePass.

## 🛠 Usage & Demo

### 1. Simple One-Click Fetch

Right-click any entry and select **KeeFetch - Download Favicons**. The plugin will instantly search for the best icon, prioritizing high-resolution sources like `apple-touch-icon` and large PNGs.

![Single Entry Demo](docs/usage-single.gif)
*Right-click any entry to instantly fetch its favicon*

### 2. Bulk Group Processing

Process entire groups (including all subgroups) in one go. KeeFetch uses a concurrent engine with `SemaphoreSlim` for up to 8 parallel downloads, so fetching 100+ icons only takes seconds.

![Group Download Demo](docs/usage-group.gif)
*Process entire groups with concurrent downloads*

### 3. Android App Support

KeeFetch uniquely handles `androidapp://` URLs. It maps package names (like `com.instagram.android`) to official web domains using a built-in database of 100+ app mappings, with Google Play Store fallback.

![Android Mapping Demo](docs/usage-android.gif)
*Automatic androidapp:// URL to web domain mapping*

### 4. Database-wide Maintenance

Keep your entire database up to date via the Tools menu. Perfect for cleaning up missing icons in large, existing databases.

![Database Maintenance](docs/usage-maintenance.png)
*Update all entries across your entire database*

**Menu Path:** `Tools` → `KeeFetch` → `Download All Favicons`

> **💡 Tip:** Configure KeeFetch to skip entries that already have custom icons in **Settings** (`Tools` → `KeeFetch` → `Settings...`).

## 🏗 Building from Source

KeeFetch uses an SDK-style project for development and a legacy-style project for PLGX compatibility.

### Prerequisites
- Visual Studio 2022 or .NET 8 SDK
- .NET Framework 4.8 Targeting Pack
- KeePass 2.x (installed for PLGX creation)

### Build Commands
```powershell
# Build the DLL and run tests
dotnet build
dotnet test

# Create PLGX (requires KeePass.exe in Path)
KeePass.exe --plgx-create "path\to\KeeFetch"
```

## 📖 Architecture

KeeFetch is designed with a **provider-based fallback strategy**. It first attempts to parse the website directly to find the highest quality icon (looking for `apple-touch-icon` or large PNGs). If that fails, it cycles through multiple specialized favicon services until an icon is found or all sources are exhausted.

For a deep dive into the code, see our [Project Structure](CONTRIBUTING.md#project-structure) in the contribution guide.

## 🤝 Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines and the process for submitting pull requests.

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for more information.

## 🙏 Acknowledgments

- [KeePass Password Safe](https://keepass.info/) — The ultimate password manager.
- Inspired by [KeePass-Yet-Another-Favicon-Downloader](https://github.com/navossoc/KeePass-Yet-Another-Favicon-Downloader) — The original favicon downloader plugin that inspired this project.
- [Icon Horse](https://icon.horse/), [DuckDuckGo](https://duckduckgo.com/), [Google](https://google.com), and [Yandex](https://yandex.com) for their favicon APIs.
