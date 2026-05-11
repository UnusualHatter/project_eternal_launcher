# AGENTS.md

This file is a pointer for AI coding assistants working on Project Eternal.

The authoritative documentation lives in two places:

- **[README.md](README.md)** — user-facing overview: what the launcher does, how to build it, runtime files, and external boundaries.
- **[CLAUDE.md](CLAUDE.md)** — internal operations manual: architecture, elevation model, file contracts, coding conventions, hard boundaries, and troubleshooting.

Both files are kept in sync with the actual code. If anything here contradicts them, the README and CLAUDE.md win.

## Quick facts for new agents

- **Language / framework:** C# 12 on `.NET 8.0-windows`, WPF.
- **Entry points:** `src/LauncherTF2/App.xaml.cs` (branches on `--launch-tf2` flag) and `src/LauncherTF2/Views/MainWindow.xaml`.
- **Shared services:** wired through `src/LauncherTF2/Core/ServiceLocator.cs`. Prefer it over manual instantiation.
- **Elevation:** the launcher runs `asInvoker`. The Launch TF2 button self-elevates a single child process of the same exe (`--launch-tf2`) to run the patchers. Do not change the manifest to `requireAdministrator` — it breaks drag-drop and the single-prompt UAC flow.
- **Window styling:** `WindowChrome` (in `MainWindow.xaml`). Do not switch back to `AllowsTransparency=True` — WPF doesn't support drag-drop on transparent windows.
- **Mods:** filesystem-based under `tf/custom/` (enabled) and `tf/custom/disabled/` (disabled). Multi-file VPK sets are handled as a unit by `ModManagerService` — see `IsVpkChunkFile`, `ToggleMod`, and `RemoveMod`.
- **Logging:** bracketed module prefix (`[Game]`, `[Mods]`, `[InventoryPricing]`, …). Goes to `app_debug.log` next to the exe with size-based rotation.
- **Caches that age out:** `mod_metadata_cache.json` and `mod_thumbnails/` (7 days, includes negative entries), `price_cache.json` (2 hours), `tf2_inventory_cache.json` (10 minutes), in-memory feed cache (15 minutes).
- **Settings schema:** TF2 cvar surface is declarative in `Services/SettingsSchema.cs` (one `SettingItem` per cvar). Adding a setting = one model property + one schema entry. Both the UI (data-templated `ItemsControl`) and `AutoexecWriter` consume the same schema. `AutoexecParser` mirrors every `CustomEmitter` so an existing user autoexec round-trips into the UI on first run. See CLAUDE.md for full details.

## Build

```powershell
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug
# or
scripts\build.ps1
```

## Run

```powershell
scripts\start.bat
# or
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
```

For anything beyond this card — architecture decisions, hard "don't change this" rules, troubleshooting — go read [CLAUDE.md](CLAUDE.md).
