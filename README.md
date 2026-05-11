# Project Eternal — TF2 Launcher

Project Eternal is a Windows desktop launcher for Team Fortress 2 that owns the parts Steam does not: launch orchestration, settings persistence, mod management, live community feeds, and direct inventory pricing.

The current branch is not a thin wrapper around Steam. It includes a real WPF UI, background services, local caches, filesystem-based mod enable/disable, and a launch pipeline that patches Steam and TF2 at runtime.

## What it does today

The launcher currently ships these working pieces:

- Home dashboard with live Steam news and fresh GameBanana TF2 mods.
- Launch orchestration that runs `steam_patcher.exe`, starts TF2 through the Steam protocol, watches the TF2 process, and fires `pure_patcher.exe` once the game is ready.
- Settings management backed by `settings.json` and `launcher_config.json`, with validation, backup, autoexec generation, and bind editing.
- Mod library management with filesystem-based enable/disable, drag-and-drop installation, archive support, multi-file VPK set handling, and GameBanana metadata enrichment.
- Inventory browser that reads the Steam Community inventory directly, caches results locally, and shows per-item market price snapshots in the detail panel.
- Tray behavior, single-instance startup, and structured logging.

## Current architecture

The launcher is split into three practical layers:

1. Game orchestration — `GameService` handles the launch flow and process monitoring.
2. Configuration management — `SettingsService`, `AutoexecWriter`, and `AutoexecParser` persist user state and generate TF2 config.
3. Content management — `ModManagerService`, `ModInstallationService`, `GameBananaEnrichmentService`, `HomeFeedService`, `SteamInventoryService`, and the inventory view models manage mods, feeds, and pricing.

The inventory stack lives in-process. There is no separate `PricingAggregator` backend in this branch; pricing comes from direct Steam Community and prices.tf requests with local caching and fallback URLs.

### Elevation model

The launcher itself runs **unelevated** (`asInvoker`) so drag-and-drop from Explorer works and Windows doesn't gate the UI behind UAC at every start. When the user clicks **Launch TF2**, `GameService` spawns a single elevated child process of this same executable (with the `--launch-tf2` flag). That child runs both `steam_patcher.exe` and `pure_patcher.exe` plus the TF2 process monitoring, all under a single UAC prompt named "Eternal TF2 Launcher".

The custom title bar and rounded corners are handled by `WindowChrome` instead of `AllowsTransparency=True` (the latter breaks WPF drag-and-drop).

## Main folders

```text
project_eternal_launcher/
├─ src/LauncherTF2/        WPF launcher (.NET 8, Windows)
├─ cfg/                    Example TF2 config files
├─ resources/Assets/       Launcher images and icons
├─ scripts/                Build and launch helpers
├─ manuals/                Implementation notes and walkthroughs
└─ project_eternal_launcher-main.sln
```

## Runtime files

The launcher writes its own state next to the executable:

- `settings.json` — TF2 settings and launch options.
- `launcher_config.json` — UI preferences such as tray and logging behavior.
- `mod_state.json` — mod enable/disable tracking.
- `price_cache.json` — cached market results.
- `tf2_inventory_cache.json` — cached Steam inventory data.
- `mod_metadata_cache.json` and `mod_thumbnails/` — GameBanana enrichment cache (7-day TTL, includes negative entries).
- `app_debug.log` — rotating launcher log.
- `crash_log.txt` — crash capture when the app fails hard.

TF2 config is written under the game install path, usually `tf/cfg/autoexec.cfg`.

## Build and run

From the repository root:

```powershell
dotnet build project_eternal_launcher-main.sln -c Debug
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
```

Or use the convenience scripts:

```powershell
scripts\build.ps1
scripts\start.bat
```

The launcher is a Windows-only WPF app targeting .NET 8.

## Launcher flow

1. `HomeViewModel` or the global sidebar play command calls `GameService.LaunchTF2()`.
2. `GameService` re-launches this same exe with the `--launch-tf2` argument under the `runas` verb. Windows shows a single UAC prompt titled "Eternal TF2 Launcher".
3. The non-elevated UI minimizes to tray (when enabled).
4. The elevated child detects the flag in `App.OnStartup`, skips the window, and runs the orchestration:
    - Resets the single-flight gate for `pure_patcher.exe`.
    - Starts `steam_patcher.exe` in the background.
    - Launches TF2 through `steam://rungameid/440`.
    - Polls `tf_win64` and waits for the window to exist.
    - Starts `pure_patcher.exe` once the game is ready.
    - Exits.

## Mod workflow

Mods are managed by moving files and folders inside the TF2 `custom` tree.

- Enabled mods live in `tf/custom/`.
- Disabled mods live in `tf/custom/disabled/`.
- VPK files and folder-based mods are both supported.
- Multi-file VPK sets (`name_dir.vpk` + `name_000.vpk`, `name_001.vpk`, …) are treated as one mod: toggling moves every chunk together, removing deletes every chunk.
- ZIP, RAR, and 7z archives can be dropped into the mod view for installation.
- Archive extraction validates paths to block zip-slip style traversal.

GameBanana enrichment runs in the background and updates author and thumbnail data when a matching mod is found. The search query is normalised before being sent to DuckDuckGo: trailing version suffixes (`_v1_6_2`), year suffixes (`_2024`), generic packaging tags (`final`, `release`), and separator characters are stripped so the match has a fair chance of landing.

## Inventory workflow

The inventory tab uses the Steam Community inventory directly, not a separate API service.

- Steam login/session state is detected from local Steam data.
- Inventory responses are cached locally and reused when the user is rate limited.
- Item prices are fetched on demand in the detail panel.
- The view supports search, filter chips, and sorting.
- Fallback search URLs are generated when a store price is unavailable.

## Current limitations and pending work

- Some converter methods in `Core/Converters.cs` still throw `NotImplementedException` in `ConvertBack`; they are only safe for one-way binding today.
- The launcher still depends on Steam and logged-in Steam Community state for the inventory tab and some enrichment paths.
- External feeds can fail or return empty results; the UI is expected to fall back to cache or empty lists instead of blocking.
- GameBanana enrichment caches both positive and negative results for 7 days. If a mod was scanned before a search-quality improvement, you have to delete `mod_metadata_cache.json` (and optionally `mod_thumbnails/`) to force a re-fetch.

## Working on the project

- Keep `src/LauncherTF2/native/steam_patcher.exe` and `pure_patcher.exe` versioned as-is.
- Use `ServiceLocator` for shared service access.
- Prefer the existing logging style with `[Module]` prefixes.
- Avoid reintroducing a separate pricing backend unless the codebase is being restructured for it end to end.
- Keep the `asInvoker` manifest. If the launcher ever needs admin again, drag-drop from Explorer will break — the proper escape hatch is the existing `--launch-tf2` self-elevation pattern, not the manifest.

MIT — see `LICENSE`.
