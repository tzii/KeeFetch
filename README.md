# KeeFetch

[![Build Status](https://github.com/tzii/KeeFetch/workflows/Build%20KeeFetch/badge.svg)](https://github.com/tzii/KeeFetch/actions)
[![GitHub Release](https://img.shields.io/github/v/release/tzii/KeeFetch?include_prereleases)](https://github.com/tzii/KeeFetch/releases)
[![Website](https://img.shields.io/badge/Website-GitHub%20Pages-8150d8)](https://tzii.github.io/KeeFetch/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A fast, smart, and modern favicon downloader plugin for KeePass 2.x.

![KeePass Plugin](https://img.shields.io/badge/KeePass-2.x%20Plugin-blue)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple)

## ✨ Features

- **Concurrent downloads** — Parallel favicon fetching using `SemaphoreSlim` to keep the UI responsive.
- **Availability-first selector engine** — Collects provider candidates, then ranks by trust tier (`Site canonical` → `Strong resolver` → `Synthetic fallback`) so placeholder-prone results cannot outrank stronger real icons.
- **Smart icon detection** — Parses `rel=icon`, `apple-touch-icon`, `rel=manifest` icon entries, and detects SVG-only situations for resolver fallback competition.
- **Study-selected fetch profiles** — `Fast`, `Balanced` (default), `Privacy`, and `Thorough` profiles whose provider chains and timeouts were chosen by a measured provider study; `Custom` exposes every provider and timeout. See [Fetch profiles](#-fetch-profiles).
- **Deduplication** — SHA-256 hashing ensures icons aren't duplicated in your database.
- **Android Support** — Converts `androidapp://` URLs to web domains with 100+ built-in mappings and Play Store scraping.
- **Intelligent URL handling** — Resolves KeePass `{REF:...}` placeholders and auto-prefixes schemes.
- **Modern Standards** — Uses the operating system's TLS policy (TLS 1.2/1.3 on current Windows), the system default proxy configuration, and can optionally accept self-signed certificates for KeeFetch's own requests only.

## 🔒 Privacy

By default, KeeFetch uses third-party favicon services as fallbacks when direct site fetching is insufficient. Domain names from your password entries may be sent to these services to maximize icon availability. Which services are contacted depends on the selected fetch profile (see below); the `Privacy` profile contacts only the site itself. KeeFetch never sends anything other than the host name, and has no telemetry or analytics.

KeeFetch shows a one-time first-run disclosure about this behavior and keeps the availability-first defaults enabled. You can switch to the `Privacy` profile, or disable third-party providers, synthetic fallbacks, or specific resolvers in plugin settings (`Tools` → `KeeFetch` → `Settings...`).

Requests to private or internal hosts (RFC 1918, link-local, loopback, `.local`/`.lan`/`.internal` names, and so on) are never forwarded to third-party services, and redirects that land on such hosts are rejected.

## 🎛 Fetch profiles

| Profile | Provider chain | Total budget | Notes |
|---|---|---|---|
| **Fast** | Direct Site → Google → Twenty Icons | 15 s | Stops at the first strong resolver hit; no synthetic fallbacks. Best for large batches. |
| **Balanced** (default) | Direct Site → Twenty Icons → DuckDuckGo → Google → Yandex → Icon Horse | 45 s | Queries the whole chain before selecting; allows a generated fallback icon when nothing real is found. |
| **Privacy** | Direct Site only | 22 s | No third-party services are contacted. |
| **Thorough** | Direct Site → Yandex | 22 s | Study-selected chain for maximum correct-brand coverage. |
| **Custom** | Any of Direct Site, Twenty Icons, DuckDuckGo, Google, Yandex, Favicone, Icon Horse | configurable | Full manual control over providers, order, and timeouts. |

The managed profiles are generated from the v1.3 provider study (`docs/benchmarks/v1.3-provider-study.md`) and mirrored to the website in `site/data/profiles.json`; CI fails if the two drift apart.

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
# Build the DLL and run tests (set KeePassPath if KeePass is not in the default install folder)
dotnet build KeeFetch.sln -c Release -p:KeePassPath="C:\Program Files\KeePass Password Safe 2"
dotnet test KeeFetch.Tests/KeeFetch.Tests.csproj -c Release

# Repository gates run by CI
./eng/check-version.ps1                 # version.txt == AssemblyInfo (== tag on release)
./eng/check-plgx-manifest.ps1           # KeeFetch.plgx.csproj lists exactly the tracked sources
./eng/export-profile-data.ps1 -Check    # site/data/profiles.json matches the compiled catalog

# Create PLGX (requires KeePass.exe in Path)
KeePass.exe --plgx-create "path\to\KeeFetch"
```

## 📖 Architecture

KeeFetch is designed with an **availability-first ranked selection strategy**. Providers return structured candidates with tier and confidence metadata. The selector then chooses the best surviving candidate, ensuring synthetic fallback providers only win when no stronger site-backed or resolver-backed icon survives.

For a deep dive into the code, see our [Project Structure](CONTRIBUTING.md#project-structure) in the contribution guide.

## 🤝 Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines and the process for submitting pull requests.

## ⚖️ License

Distributed under the MIT License. See `LICENSE` for more information.

## 🙏 Acknowledgments

- [KeePass Password Safe](https://keepass.info/) — The ultimate password manager.
- Inspired by [KeePass-Yet-Another-Favicon-Downloader](https://github.com/navossoc/KeePass-Yet-Another-Favicon-Downloader) — The original favicon downloader plugin that inspired this project.
- [Twenty Icons](https://twenty-icons.com/), [DuckDuckGo](https://duckduckgo.com/), [Google](https://google.com), [Yandex](https://yandex.com), [Favicone](https://favicone.com/), and [Icon Horse](https://icon.horse/) for favicon APIs.
