# CLAUDE.md — Project Eternal Launcher Operations Manual

This file is the internal working manual for the launcher branch currently in the workspace. It should describe the code as it exists now, not the earlier architecture notes that mention a separate pricing backend or a fully-elevated launcher.

## 1. What Exists Today

Project Eternal is a WPF launcher for Team Fortress 2 with four active product areas:

1. Launch orchestration (via a single self-elevated child process).
2. Configuration and config-file generation.
3. Mod library management and enrichment.
4. Steam inventory browsing with direct pricing lookups.

The current codebase keeps those responsibilities in the launcher process itself. There is no `PricingAggregator` project in this branch.

### Current entry points

- `src/LauncherTF2/App.xaml.cs` — initializes the app, owns the single-instance mutex, branches between **UI mode** and **elevated helper mode** based on the `--launch-tf2` command-line flag.
- `src/LauncherTF2/Core/ServiceLocator.cs` — wires the shared services.
- `src/LauncherTF2/ViewModels/MainViewModel.cs` — owns the tab shells and global commands.
- `src/LauncherTF2/Services/GameService.cs` — drives the launch pipeline; in the UI process it spawns the elevated helper, in the helper it runs the actual orchestration.

## 2. Build, Run, and Verify

### Recommended commands

```powershell
dotnet build project_eternal_launcher-main.sln -c Debug
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
```

Or:

```powershell
scripts\build.ps1
scripts\start.bat
```

### Notes

- The launcher targets `net8.0-windows`.
- Use the `src/LauncherTF2/LauncherTF2.csproj` path, not the older root-level path that still appears in some notes.
- The launcher manifest is `asInvoker` — opening the launcher does **not** prompt UAC. UAC only appears when the user clicks "Launch TF2".

## 3. Real Runtime Architecture

### Elevation model

The launcher's `app.manifest` declares `asInvoker`. This is intentional:

- An elevated WPF window cannot receive OLE drag-drop from Explorer (different integrity levels), so the mod-install drop zone needs the launcher to stay unelevated.
- Customising the title bar uses `WindowChrome` (in `Views/MainWindow.xaml`) instead of `AllowsTransparency=True`. WPF drag-drop is also broken on transparent windows, so this is the second reason to keep things this way.

When the user clicks "Launch TF2", `GameService.LaunchTF2()`:

1. Calls `Process.Start` on the launcher's own executable path with `Verb="runas"` and `Arguments="--launch-tf2"`.
2. Windows shows one UAC prompt with the FileDescription from the csproj (`Eternal TF2 Launcher`).
3. The non-elevated UI immediately minimizes to tray.

The elevated child:

1. `App` constructor detects `--launch-tf2` in `Environment.GetCommandLineArgs()`, sets `_isElevatedLauncherChild=true`, skips the UI mutex and the auto-clear-logs step (the UI process owns those concerns).
2. `App.OnStartup` returns early without creating `MainWindow`.
3. `RunElevatedLaunchAndExit` runs `GameService.RunPatchAndLaunchSequenceAsync()` on a background task, then calls `Current.Shutdown()` when it completes.

Splitting the launch flow this way lets us keep the UI unelevated while still giving the patchers admin rights, behind a **single** UAC prompt per game launch.

### Game orchestration

`GameService` exposes two entry points:

- `LaunchTF2()` — UI-side. Spawns the elevated helper via `TrySpawnElevatedHelper()`, then `MinimizeToTray()`.
- `RunPatchAndLaunchSequenceAsync()` — helper-side. Reads settings, resets the `pure_patcher` single-flight gate, runs `OrchestrateSteamPatcherAndLaunch()`.

`OrchestrateSteamPatcherAndLaunch` (unchanged logic, just relocated):

- Starts `steam_patcher.exe` through `NativeExecutableService`.
- Waits for Steam to settle (handles the case where the patcher restarts Steam).
- Launches TF2 via `steam://rungameid/440`.
- Polls for the `tf_win64` process and waits for a visible window.
- Starts `pure_patcher.exe` once the game is ready.

The UI process has nothing to wait on after spawning the helper — the helper is fire-and-forget from the UI's perspective and exits on its own when TF2 is patched.

### Configuration management

`SettingsService` is the persistence layer for TF2 settings.

Behavior:

- Loads `settings.json` from the app base directory.
- Creates defaults if the file is missing.
- Saves immediately when settings change.
- Writes `autoexec.cfg` through `AutoexecWriter` after successful save.
- Also stores launcher-specific preferences in `launcher_config.json`.

`SettingsViewModel` works with a `SettingsModel` instance and binds bind-editing, display mode, and validation state.

### Mod management

`ModManagerService` is filesystem-based.

Behavior:

- Enabled mods live in TF2 `custom/`.
- Disabled mods live in `custom/disabled/`.
- VPK files and folders are both supported.
- Multi-file VPK sets (`name_dir.vpk` + `name_000.vpk`, `name_001.vpk`, …) are treated as one mod. `ToggleMod()` and `RemoveMod()` move/delete every chunk together so no orphans are left behind. `IsVpkChunkFile()` only treats a numbered VPK as a chunk when its `_dir.vpk` companion exists in the same folder — lone files like `awp_dragon_001.vpk` still register as their own mod.
- `RemoveMod()` deletes the mod from disk permanently.
- `InstallMod()` copies a file or folder into `custom/`.
- Startup removes `.cache` files from the mod tree.
- `RefreshMods()` logs the resolved `CustomFolderPath` and top-level VPK/folder counts on every scan — useful when a mod is "missing" because the user put it in the wrong directory.

`ModInstallationService` handles drag-and-drop and archive extraction.

Behavior:

- Supports folder drops directly.
- Supports `.vpk`, `.zip`, `.rar`, `.7z`, and `.7zip`.
- Validates archive output paths to prevent traversal.
- Detects TF2-shaped folders by searching for known subdirectories such as `materials`, `models`, and `sound`.

`ModsViewModel` wraps that service with filtering, toggle commands, background GameBanana enrichment, and reload cancellation. The view uses both grid and list templates; both honour the `ThumbnailImage` (frozen `BitmapImage` produced by enrichment) when `IsEnriched=True`, and fall back to `ThumbnailPath` otherwise.

### Home feed

`HomeViewModel` is the landing page controller.

Behavior:

- Builds a greeting from the time of day and Windows username.
- Shows quick health indicators for TF2, Steam, and autoexec state.
- Loads Steam news and GameBanana new mods.
- Invalidates the feed cache on manual refresh.
- Exposes quick actions for opening the TF2 folder, mods folder, autoexec, and settings backup.

`HomeFeedService` fetches the remote data and caches it in memory for 15 minutes.

### Inventory and pricing

The inventory tab is driven by `BackpackViewModel` under `ViewModels/Inventory/`.

Behavior:

- Detects the active Steam user from the local Steam registry state.
- Loads the TF2 backpack from Steam Community.
- Uses a 10-minute local inventory cache and falls back to it when rate limited.
- Hydrates item images in the background.
- Displays pricing in the selected item detail panel.
- Fetches store pricing directly in-process through `InventoryPricingService`.

`InventoryPricingService` behavior:

- Calls prices.tf and Steam Market directly.
- Uses host-specific throttling and a 2-hour disk cache.
- Produces store search URLs even when a price is unavailable.
- Falls back to local prices for a few common TF2 items when both sources are unavailable.

### Enrichment and caches

`GameBananaEnrichmentService` does background metadata resolution for mods.

Behavior:

- `BuildSearchQuery()` cleans the raw mod filename before searching: strips trailing version (`_v1_6_2`) and year (`_2024`) suffixes, drops packaging tags (`final`, `release`, `fixed`, `patch`, `update`), and converts separators to spaces.
- Searches DuckDuckGo HTML results for GameBanana links matching the cleaned query.
- Validates candidates against the GameBanana API.
- Confirms the mod is TF2-related before applying metadata.
- Downloads thumbnails into a local cache.
- Persists positive and negative cache entries for 7 days — so failed matches from before the search-quality improvement stay cached. Delete `mod_metadata_cache.json` and `mod_thumbnails/` to force a re-fetch.

`InventoryImageCache` and the mod thumbnail cache exist to avoid re-downloading assets on every view refresh.

## 4. File Contracts

### App-directory files

- `settings.json` — TF2 settings and launch arguments.
- `launcher_config.json` — tray, logging, and UI preferences.
- `mod_state.json` — mod state marker file.
- `price_cache.json` — market cache.
- `tf2_inventory_cache.json` — cached backpack data.
- `mod_metadata_cache.json` — GameBanana metadata cache.
- `mod_thumbnails/` — local thumbnail image cache.
- `app_debug.log` — rotating log file.
- `crash_log.txt` — crash dump text.

### TF2 install files

- `tf/cfg/autoexec.cfg` — generated by `AutoexecWriter`.
- `tf/custom/` — enabled mods.
- `tf/custom/disabled/` — disabled mods.

### Native executables

- `src/LauncherTF2/native/steam_patcher.exe`
- `src/LauncherTF2/native/pure_patcher.exe`

These are versioned assets and should not be replaced casually.

## 5. Coding Conventions That Matter Here

- Prefer `ServiceLocator` for all shared service access.
- Keep logging bracketed, e.g. `[Game]`, `[Mods]`, `[InventoryPricing]`.
- Continue using async methods with `Async` suffix.
- Keep shared mutable state behind `lock` or interlocked gates.
- Do not put blocking waits on the UI thread.
- Use `ApplyPatch` / dedicated edit tools for changes in this workspace; do not hand-edit with shell writes.
- XML comments inside `app.manifest` must be single-line — multi-line `<!-- ... \n ... -->` blocks make the Windows SxS parser reject the manifest with "Invalid Xml syntax" and the exe fails to start with a side-by-side error.

## 6. Hard Boundaries

### Do not change

1. Native patchers under `src/LauncherTF2/native/` without explicit approval.
2. The single-instance guard in `App.xaml.cs` (UI-mode only — the elevated helper intentionally bypasses it).
3. The `asInvoker` manifest. Re-elevating the UI process breaks drag-drop from Explorer.
4. The `WindowChrome` setup in `MainWindow.xaml`. Replacing it with `AllowsTransparency=True` also breaks drag-drop.
5. Direct file-path assumptions when a service already owns the path logic.
6. UI bindings that bypass view models.
7. Silent or unbounded background loops in the launch path.

### Prefer to keep

- Settings persistence centralized in `SettingsService`.
- Mod enable/disable behavior filesystem-based.
- Launch orchestration fire-and-forget from the UI's perspective — the elevated helper owns the rest.

## 7. Current Pending Work

These are the concrete open items visible in the codebase right now:

1. `Core/Converters.cs` still contains `ConvertBack` methods that throw `NotImplementedException`. They are safe only where the binding is one-way.
2. The inventory tab still depends on Steam Community login state for the full experience. If Steam is not running or logged in, the view degrades to cached or empty results.
3. Network-driven surfaces such as Steam news, GameBanana enrichment, prices.tf, and Steam Market need graceful failure behavior. The current design is cache-first and should stay that way.
4. The `SharpCompress` 0.36.0 dependency has a known moderate-severity vulnerability (NU1902). Upgrade when there's a stable release we trust.

## 8. Troubleshooting Checklist

When something regresses, check these first:

1. Confirm `settings.json` loads and the Steam path points at the TF2 install.
2. Confirm `tf_win64.exe` exists under the configured TF2 path.
3. Check `app_debug.log` for `[Game]`, `[Mods]`, `[Home]`, and `[InventoryPricing]` entries.
4. Make sure Steam is running and logged in before testing inventory behavior.
5. Make sure native patcher binaries are present in the output directory after build.
6. If a mod doesn't appear after copying it to `custom/`, check the `[Mods] Refresh scan target:` line — it logs the resolved path and counts. The mod must be a top-level `.vpk` or folder (not inside a subfolder, not under `disabled/`).
7. If mod thumbnails don't appear, delete `mod_metadata_cache.json` and `mod_thumbnails/` to force re-enrichment.
8. If "Launch TF2" doesn't prompt UAC, the launcher might already be elevated — check Task Manager. Self-elevation requires the parent to be unelevated.

## 9. Suggested Next Edits

If you are continuing the cleanup pass, the next sensible tasks are:

1. Replace or remove the placeholder `ConvertBack` implementations in `Core/Converters.cs`.
2. Decide whether `RefreshMods()` should remain a diagnostic-only hook or become a meaningful rescan trigger.
3. Add a UI button to clear the GameBanana enrichment cache (currently a manual file deletion).
4. Bump `SharpCompress` past the known vulnerability when a trustworthy version is available.
