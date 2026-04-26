# Availability-First Reliability Validation

This file records the validation and packaging proof runs for the availability-first reliability implementation.

## Environment used

- Local .NET SDK install: `C:\Users\simon\.dotnet-sdk\dotnet.exe`
- Framework target validated by build/test outputs: `.NETFramework,Version=v4.8`
- KeePass executable used for PLGX generation/loading checks:
  - `C:\Program Files\KeePass Password Safe 2\KeePass.exe`

## Build and test validation

```powershell
Set-Location C:\Users\simon\Documents\Projects\KeeFetch
C:\Users\simon\.dotnet-sdk\dotnet.exe build KeeFetch.sln --nologo
C:\Users\simon\.dotnet-sdk\dotnet.exe test KeeFetch.sln --nologo
```

- `dotnet build KeeFetch.sln` succeeded.
- `dotnet test KeeFetch.sln` succeeded (`77` tests passed).

## PLGX packaging proof (artifact + manifest + runtime load)

### 1. Artifact proof

- `.plgx` artifact path:  
  `C:\Users\simon\Documents\Projects\KeeFetch_PLGX_Proof\KeeFetch.plgx`
- Artifact size: `43631` bytes
- Artifact timestamp (UTC): `2026-04-11T23:42:39.9208745Z`

### 2. `.plgx.txt` manifest proof

- Manifest info path:  
  `C:\Users\simon\Documents\Projects\KeeFetch_PLGX_Proof\KeeFetch.plgx.txt`
- Manifest size: `1684` bytes
- Manifest timestamp (UTC): `2026-04-11T23:42:40.0173994Z`
- Verified manifest includes newly added redesign files/resources (representative entries):
  - `IconProviders/TwentyIconsProvider.cs`
  - `IconProviders/FaviconeProvider.cs`
  - `IconProviders/DirectSiteProvider.cs`
  - `IconSelection/IconSelector.cs`
  - `IconSelection/ProviderCapabilities.cs`
  - `SettingsForm.resx`

### 3. Portable KeePass load proof

- Artifact was installed into a writable portable KeePass copy (`Plugins\KeeFetch.plgx`) and KeePass was started to trigger plugin load/compile.
- Compiled cache DLL was produced by KeePass at:
  - Path: `C:\Users\simon\AppData\Local\KeePass\PluginCache\gs7wVK0N7Ai0TF6ShlG6\KeeFetch.dll`
  - Size: `125440` bytes
  - Timestamp (UTC): `2026-04-11T23:30:46.2534807Z`

### 4. Cached plugin verification

Confirmed from the compiled cache DLL:

- `TYPE_KeeFetchExt=True`
- `TYPE_SettingsForm=True`
- `TYPE_IconSelector=True`
- `TYPE_TwentyIconsProvider=True`
- `TYPE_FaviconeProvider=True`
- `RESOURCE_SettingsForm=True`
- `SETTINGS_FORM_CONSTRUCTED=True`

## Host-permission note

- Direct installation to `C:\Program Files\KeePass Password Safe 2\Plugins` was **not** validated in this environment due host filesystem permissions (`Access denied` while copying to Program Files).
- Portable KeePass installation and runtime loadability were fully proven as the packaging/loadability acceptance path in this session.
