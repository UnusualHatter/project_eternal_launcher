# AGENTS.md — Project Eternal Launcher

Complete operations manual for AI coding assistants. This is the single source of truth — no other internal doc is needed. Read this before touching any file.

---

## 1. What the Project Is

Project Eternal is a WPF desktop launcher for Team Fortress 2 that owns the parts Steam does not: launch orchestration with native patchers, full TF2 settings management with autoexec generation, mod library management, Steam inventory browsing with live pricing, a profile system for quick settings switching, and a fully themeable shell.

Four active product areas:

1. **Launch orchestration** — self-elevated child process runs the native patchers and starts TF2.
2. **Configuration** — schema-driven settings UI writes a managed block into `autoexec.cfg`.
3. **Mod management** — filesystem-based enable/disable, drag-drop install, archive extraction, GameBanana enrichment.
4. **Inventory + pricing** — reads the Steam backpack, fetches live prices from prices.tf and Steam Market.

There is no `PricingAggregator` project in this branch. Everything runs in the launcher process.

---

## 2. Repository Layout

```
project_eternal_launcher/
├── src/LauncherTF2/               ← the only project that matters
│   ├── App.xaml / App.xaml.cs     ← entry point, mutex, elevation branch
│   ├── app.manifest               ← asInvoker — do not change
│   ├── LauncherTF2.csproj
│   ├── Core/
│   │   ├── ServiceLocator.cs      ← composition root, single source for shared services
│   │   ├── ViewModelBase.cs       ← INotifyPropertyChanged + SetProperty
│   │   ├── RelayCommand.cs        ← ICommand implementation
│   │   ├── Logger.cs              ← bracketed log, size-rotating file
│   │   ├── ScrollAnchor.cs        ← attached property that registers settings sections
│   │   ├── AnimatedScrollHelper.cs
│   │   └── Converters.cs
│   ├── Models/
│   │   ├── SettingsModel.cs       ← all TF2 + launcher settings as INPC properties
│   │   ├── Profile.cs             ← profile snapshot model
│   │   ├── BindModel.cs
│   │   └── Settings/              ← SettingItem subclasses (Toggle/Slider/Choice)
│   ├── Services/
│   │   ├── SettingsService.cs     ← persistence: settings.json + launcher_config.json
│   │   ├── SettingsSchema.cs      ← declarative cvar surface (the schema)
│   │   ├── AutoexecWriter.cs      ← schema → managed block in autoexec.cfg
│   │   ├── AutoexecParser.cs      ← autoexec.cfg → SettingsModel on startup
│   │   ├── ProfileService.cs      ← profile CRUD, apply, detect, import/export
│   │   ├── GameService.cs         ← launch orchestration, patcher coordination
│   │   ├── ModManagerService.cs   ← mod enable/disable/install/remove
│   │   ├── ModInstallationService.cs ← drag-drop + archive extraction
│   │   ├── GameBananaEnrichmentService.cs ← background mod metadata
│   │   ├── HomeFeedService.cs     ← Steam news + GameBanana feed cache
│   │   ├── SteamInventoryService.cs
│   │   ├── InventoryPricingService.cs
│   │   ├── InventoryImageCache.cs
│   │   ├── ThemeManagerService.cs ← live palette swap, logo/icon resolution
│   │   ├── ThemeCatalog.cs        ← all built-in theme definitions
│   │   ├── DisplayDetectionService.cs ← Win32 EnumDisplaySettings
│   │   ├── NativeExecutableService.cs ← runs steam_patcher / pure_patcher
│   │   └── SteamDetectionService.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs       ← tab shells, global commands
│   │   ├── SettingsViewModel.cs   ← settings UI, profile selector, launch args sync
│   │   ├── ProfileManagerViewModel.cs ← profile CRUD modal
│   │   ├── ModsViewModel.cs
│   │   ├── HomeViewModel.cs
│   │   └── Inventory/BackpackViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.xaml        ← WindowChrome — do not change to AllowsTransparency
│   │   ├── SettingsView.xaml      ← schema-driven ItemsControl + fixed sections
│   │   ├── ProfileManagerView.xaml ← compact 760×480 profile modal
│   │   ├── InputDialog.xaml       ← reusable name+description prompt
│   │   ├── MessageDialog.xaml     ← ShowError / ShowConfirm
│   │   └── ...
│   ├── Resources/
│   │   └── Profiles/              ← 4 built-in profile JSONs (EmbeddedResource)
│   │       ├── builtin-competitive.json
│   │       ├── builtin-maxperformance.json
│   │       ├── builtin-maxquality.json
│   │       └── builtin-stability.json
│   └── native/
│       ├── steam_patcher.exe      ← versioned asset, do not replace casually
│       ├── pure_patcher.exe       ← versioned asset, do not replace casually
│       └── presets/               ← source cfg files for built-in profiles
├── resources/Assets/              ← theme logos + icons (PNG / ICO)
├── installer/
│   ├── installer.iss              ← Inno Setup 6 script
│   └── publish/                   ← dotnet publish output (gitignored)
├── scripts/
│   ├── build.ps1
│   ├── publish.ps1                ← dotnet publish wrapper for installer
│   └── start.bat
├── .github/workflows/release.yml  ← tag-triggered CI: build → sign → installer → release
├── TODO.md                        ← next-session task list
└── README.md                      ← user-facing overview
```

---

## 3. Build, Run, Publish

### Debug build and run

```powershell
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
# or
scripts\build.ps1
scripts\start.bat
```

### Release installer

```powershell
# 1. Publish single-file win-x64 (no PDB, framework-dependent)
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1

# 2. Build installer (requires Inno Setup 6 installed)
iscc installer\installer.iss
# → installer\Output\EternalTF2Launcher-Setup-{version}.exe
```

### Notes

- Target: `net8.0-windows`, platform `x64`.
- `src/LauncherTF2/LauncherTF2.csproj` is the correct project path — the solution file also works.
- The manifest is `asInvoker`. Opening the launcher does **not** prompt UAC. UAC only appears when the user clicks "Launch TF2".
- `steam_patcher.exe` and `pure_patcher.exe` are marked `ExcludeFromSingleFile=true` so they stay as loose files next to the exe after publish.

---

## 4. Runtime Architecture

### Elevation model

The manifest declares `asInvoker` for two hard reasons:

1. An elevated WPF window cannot receive OLE drag-drop from Explorer (different integrity levels). The mod-install drop zone requires the launcher to stay unelevated.
2. `AllowsTransparency=True` also breaks WPF drag-drop. The custom title bar therefore uses `WindowChrome` in `MainWindow.xaml` — do not replace it.

When "Launch TF2" is clicked, `GameService.LaunchTF2()`:

1. Calls `Process.Start` on the launcher's own exe path with `Verb="runas"` and `Arguments="--launch-tf2"`.
2. Windows shows exactly one UAC prompt (FileDescription = "Eternal TF2 Launcher" from csproj).
3. The non-elevated UI immediately calls `MinimizeToTray()`.

The **elevated child process**:

1. `App` constructor sees `--launch-tf2` in `GetCommandLineArgs()`, sets `_isElevatedLauncherChild=true`, skips the UI mutex and the auto-clear-logs step.
2. `App.OnStartup` returns early — no `MainWindow` is created.
3. `RunElevatedLaunchAndExit` runs `GameService.RunPatchAndLaunchSequenceAsync()` on a background task, then calls `Current.Shutdown()`.

The UI process fires-and-forgets the helper. The helper exits on its own.

### Game orchestration

`GameService` has two entry points:

| Method | Process | Role |
|---|---|---|
| `LaunchTF2()` | UI process | Spawns the elevated helper, then minimizes to tray |
| `RunPatchAndLaunchSequenceAsync()` | Elevated helper | Runs the full patch + launch sequence |

`OrchestrateSteamPatcherAndLaunch` (inside the helper):

1. Starts `steam_patcher.exe` via `NativeExecutableService`.
2. Waits for Steam to settle (handles the case where the patcher restarts Steam).
3. Launches TF2 via `steam://rungameid/440`.
4. Polls for the `tf_win64` process and waits for a visible window.
5. Starts `pure_patcher.exe`.

### Settings persistence

`SettingsService` owns `settings.json` and `launcher_config.json`, both in `AppDomain.CurrentDomain.BaseDirectory` (next to the exe).

Behaviors:

- Loads on startup; creates defaults if absent.
- On first run (or when still on 1920×1080@60 defaults), calls `DisplayDetectionService.GetPrimaryDisplay()` (`EnumDisplaySettings`) to seed width/height/refresh from the actual primary monitor.
- Saves immediately on any property change via the `PropertyChanged` handler in `SettingsViewModel`.
- After every save, calls `AutoexecWriter.WriteToAutoexec` to regenerate the managed block.
- `launcher_config.json` holds tray behavior, log level, theme ID, and notification prefs — not TF2 settings.

### Settings schema — data-driven UI + autoexec

`SettingsSchema.cs` describes every exposed TF2 cvar as a tree of `SettingCategory → SettingItem`. The same schema drives three separate consumers simultaneously:

| Consumer | How it uses the schema |
|---|---|
| Settings UI | `ItemsControl` + `DataTemplate` per item type (Toggle/Slider/Choice) |
| `AutoexecWriter` | Calls `EmitCvarLines()` on each item; non-default items write to the managed block |
| `ProfileService` | Builds a `Dictionary<string, SettingItem>` index keyed by `PropertyName` for O(1) apply and match |

**`SettingItem` key properties:**

- `PropertyName` — matches a property on `SettingsModel`; used by `GetValue`/`SetValue` (reflection) and by the profile index.
- `Cvar` — TF2 cvar name, shown in tooltips.
- `CustomEmitter` — optional delegate returning the exact cvar lines when the default `cvar 0/1` format is wrong (e.g. `mat_disable_bloom` inverts the bool; `closecaption` emits two lines; null-movement emits an alias block).
- `EmitOnlyWhenOn` — if true, no line is written when the toggle is off.
- `NotCasualCompatible` — UI renders a small badge; the cvar is blocked on sv_pure servers.
- `DependsOn` + `IsEnabledPredicate` — wires a child row to a parent toggle. When the parent changes, child wrappers re-raise `IsEnabled`; the row dims to 0.4 opacity and disables input.

**Adding a new TF2 setting (three steps, no XAML edit required):**

1. Add a property to `SettingsModel` with a safe default. Existing `settings.json` files load fine — missing keys fall through to the default.
2. Append a `ToggleSetting` / `SliderSetting` / `ChoiceSetting` to the correct category in `SettingsSchema.Build`. The UI and `AutoexecWriter` pick it up automatically.
3. If the cvar semantics inverts the toggle (e.g. `mat_disable_bloom 1` means "bloom off"), set `CustomEmitter` and add the mirrored parse case to `AutoexecParser`.

### AutoexecWriter — delta from defaults

`AutoexecWriter.WriteToAutoexec` (called by `SettingsService` after every save):

1. Reads the existing autoexec, extracts user-owned content (everything outside the `=== ETERNAL LAUNCHER MANAGED BLOCK ===` markers).
2. Builds a new managed block by iterating the schema. **Only settings that differ from `new SettingsModel()` defaults are emitted.** This prevents "restore-to-default" noise lines from duplicating the user's own autoexec section.
3. Writes: user content → blank line → begin marker → managed block → end marker.

The managed block never touches the user's section. Applying a profile only affects the managed block.

### AutoexecParser

`AutoexecParser.LoadFromAutoexec` runs once at startup against the **entire** file (managed + user-owned). This lifts an existing hand-crafted autoexec into the UI on first install. It mirrors every `CustomEmitter` inversion, e.g.:

- `mat_disable_bloom 1` → `Bloom = false`
- `cl_jiggle_bone_framerate_cutoff 0` → `DisableJiggleBones = true`
- `cl_first_person_uses_world_model 0` + `r_drawviewmodel 1` → `TransparentViewmodels = true`

### Profile system

`ProfileService` (built once in `ServiceLocator.Initialize`) owns the full profile lifecycle.

**Schema index:** built at construction from `SettingsSchema.Build(new SettingsModel())`. Keyed by `PropertyName` for O(1) lookups. The dummy model is only used for structure — `ApplyProfile` and `ProfileMatches` always operate on the real live model via `SettingItem.GetValue(target)` / `SetValue(target, value)` (reflection).

**Built-in profiles** are embedded JSON resources (`EmbeddedResource` in csproj) in `Resources/Profiles/`. They are never copied to AppData. Each carries:
- `settings` — keyed by `PropertyName`, values are raw model property values (not UI-inverted).
- `launchOptions` — parsed on apply to set `DxLevel`, `SkipIntro`, and `-console` in `LaunchArgs`.

Built-in profiles:

| ID | Name | Key settings |
|---|---|---|
| `builtin-competitive` | Competitive | Interp 0, InterpRatio 1, MatPhong off, NullMovement on, -dxlevel 90 |
| `builtin-maxperformance` | Max Performance | All visual settings minimised, ModelLod 2, Ragdolls off, -dxlevel 81 |
| `builtin-maxquality` | Max Quality | All visual settings maxed, AA 8×, AF 16×, -dxlevel 95 |
| `builtin-stability` | Stability | Interp 0.0152, InterpRatio 2, balanced LOD, -dxlevel 90 |

**User profiles** are stored as individual JSON files in `%APPDATA%\Eternal TF2 Launcher\profiles\user\`, named after the profile ID (`{id}.json`).

**`ApplyProfile` — transactional:**
1. Snapshot the current model state.
2. For each `(key, value)` in `profile.Settings`: skip `null`, skip unknown keys (log warning), call `item.SetValue(target, value)`.
3. Parse and apply `profile.LaunchOptions` if present.
4. On any exception: restore the snapshot, re-throw (caller shows error dialog).

**`ProfileMatches`:** compares each profile setting against the live model using proper type-based comparison — `JsonSerializer.Deserialize(el.GetRawText(), prop.PropertyType)` then `Equals()`. String comparison was a previous bug that caused `0.0 != "0"` mismatches.

**`DetectCurrentProfile` tie-breaking:**
1. User profiles first, ordered by `LastModified` descending.
2. Then built-in profiles, ordered by `Id` alphabetically.
3. Returns `null` (→ UI shows "Custom" red dot) if nothing matches.

**First-run migration:** if `settings.json` exists but `profiles/user/` does not, `IsFirstRunMigration = true` is set and the directory is created. `SettingsViewModel` shows a one-time dismissible banner.

### Theme system

`ThemeManagerService` owns the live palette. All brush resources in `App.xaml` are `SolidColorBrush` or `LinearGradientBrush` instances that are animated in-place when `ApplyTheme` is called — WPF consumers holding `StaticResource` references see the change without a full reload.

`ThemeCatalog.GetBuiltinThemes()` returns all available themes. Adding a theme: append a `ThemeDefinition` to `_themes` and optionally drop logo/icon assets in `resources/Assets/`.

Built-in themes: Eternal Classic, Australium, RED, BLU, Carbon, Midnight, Plasma, **Toxic** (neon green on near-black, replaces the removed Infernal), Synthwave, Minimal.

The active theme ID is persisted in `launcher_config.json` (`SelectedThemeId`).

### Sidebar scroll anchors

`Core/ScrollAnchor.cs` is an attached property. Setting `ScrollAnchor.Name="gameplay"` on any `FrameworkElement` inside a `ScrollViewer` registers it under that ID at `Loaded` time. `SettingsViewModel.ScrollToCategoryRequested` (raised by sidebar clicks) is handled in the `SettingsView` code-behind via `ScrollAnchor.Find`. `ContentScroller.ScrollChanged` walks the registry to update `ActiveSidebarId`, which sidebar entries `DataTrigger`-bind for the active highlight.

### Mod management

`ModManagerService` is purely filesystem-based.

- Enabled mods live in TF2 `tf/custom/`.
- Disabled mods live in `tf/custom/disabled/`.
- VPK files and folders are both supported.
- Multi-file VPK sets (`name_dir.vpk` + `name_000.vpk`, `001.vpk`, …) are treated as one unit. `ToggleMod` and `RemoveMod` move/delete every chunk atomically. `IsVpkChunkFile` only classifies a numbered VPK as a chunk when its `_dir.vpk` companion exists in the same folder — lone files like `awp_dragon_001.vpk` remain their own mod.
- `RemoveMod` permanently deletes from disk.
- Startup removes stale `.cache` files from the mod tree.

`ModInstallationService` handles drag-drop and archive extraction. Supports `.vpk`, `.zip`, `.rar`, `.7z`, `.7zip`; validates output paths against traversal; detects TF2-shaped folders by checking for `materials`, `models`, `sound` subdirectories.

### Home feed

`HomeViewModel` builds a greeting, shows TF2/Steam/autoexec health indicators, loads Steam news + GameBanana new mods, and exposes quick-open actions. `HomeFeedService` caches remote data in memory for 15 minutes and is invalidated on manual refresh.

### Inventory + pricing

`BackpackViewModel` (under `ViewModels/Inventory/`) detects the active Steam user from the registry, loads the TF2 backpack from Steam Community, uses a 10-minute local cache, hydrates item images in the background, and displays pricing from `InventoryPricingService`.

`InventoryPricingService`: calls prices.tf and Steam Market with host-specific throttling, 2-hour disk cache, generates store search URLs even when a price is unavailable, falls back to hardcoded local prices for a few common items when both sources are down.

### GameBanana enrichment

`GameBananaEnrichmentService` runs in the background for every mod. `BuildSearchQuery` strips version suffixes (`_v1_6_2`), year suffixes (`_2024`), packaging tags (`final`, `release`, `fixed`), and converts separators to spaces. Searches DuckDuckGo HTML for GameBanana links, validates against the API, confirms TF2 relevance, downloads thumbnails. Persists positive and negative cache entries for 7 days. Delete `mod_metadata_cache.json` + `mod_thumbnails/` to force a full re-fetch.

---

## 5. File Contracts

### App-directory files (next to the exe)

| File / folder | Owner | Notes |
|---|---|---|
| `settings.json` | `SettingsService` | TF2 settings + launch args |
| `launcher_config.json` | `SettingsService` | Tray, log level, theme ID, notifications |
| `mod_state.json` | `ModManagerService` | Mod state marker |
| `price_cache.json` | `InventoryPricingService` | 2-hour market cache |
| `tf2_inventory_cache.json` | `SteamInventoryService` | 10-minute backpack cache |
| `mod_metadata_cache.json` | `GameBananaEnrichmentService` | 7-day enrichment cache (positive + negative) |
| `mod_thumbnails/` | `GameBananaEnrichmentService` | Downloaded mod thumbnails |
| `app_debug.log` | `Logger` | Size-rotating debug log |
| `crash_log.txt` | App crash handler | Crash dump text |

### AppData files (`%APPDATA%\Eternal TF2 Launcher\`)

| File / folder | Owner | Notes |
|---|---|---|
| `profiles/user/{id}.json` | `ProfileService` | One file per user-created profile |

### TF2 install files

| File / folder | Owner |
|---|---|
| `tf/cfg/autoexec.cfg` | `AutoexecWriter` (managed block only) |
| `tf/custom/` | `ModManagerService` (enabled mods) |
| `tf/custom/disabled/` | `ModManagerService` (disabled mods) |

### Native executables

| File | Notes |
|---|---|
| `src/LauncherTF2/native/steam_patcher.exe` | Versioned asset — do not replace casually |
| `src/LauncherTF2/native/pure_patcher.exe` | Versioned asset — do not replace casually |

Both are marked `ExcludeFromSingleFile=true` and `CopyToPublishDirectory=PreserveNewest` in the csproj so they remain as loose files next to `LauncherTF2.exe` after publish.

---

## 6. Coding Conventions

- **Service access:** always through `ServiceLocator`. Never instantiate shared services manually in a ViewModel.
- **Logging:** bracketed module prefix — `[Game]`, `[Mods]`, `[Profile]`, `[InventoryPricing]`, `[Theme]`, `[AutoexecWriter]`. Output goes to `app_debug.log` next to the exe.
- **Async:** async methods use the `Async` suffix. No `Task.Result` or `.Wait()` on the UI thread.
- **Shared mutable state:** behind `lock` or Interlocked.
- **Comments:** only when the *why* is non-obvious — a hidden constraint, a workaround for a specific platform bug, an invariant that would surprise a reader. Do not describe what the code does; the code does that.
- **app.manifest XML comments:** must be single-line. Multi-line `<!-- … -->` blocks cause the Windows SxS parser to reject the manifest with "Invalid Xml syntax" and the exe fails to start.
- **XAML brushes/colors:** always `{DynamicResource ...}`. Never hardcode a hex color in a `Setter` — the theme system can only swap resources it owns.

---

## 7. Hard Boundaries

### Never change without explicit approval

1. `src/LauncherTF2/native/steam_patcher.exe` and `pure_patcher.exe` — versioned assets.
2. The single-instance mutex in `App.xaml.cs` — the UI process owns it; the elevated helper intentionally bypasses it.
3. The `asInvoker` manifest — changing it breaks drag-drop from Explorer and doubles the UAC prompt count.
4. `WindowChrome` in `MainWindow.xaml` — switching to `AllowsTransparency=True` breaks WPF drag-drop.
5. Direct file-path string literals when a service already owns the path logic (use `GamePaths` / service methods).
6. UI bindings that bypass ViewModels.
7. Any unbounded or silent background loop in the launch path.

### Prefer to keep

- Settings persistence centralised in `SettingsService`.
- Mod enable/disable strictly filesystem-based.
- Launch orchestration fire-and-forget from the UI — the elevated helper owns the rest.
- The managed autoexec block as the only thing the launcher writes to `autoexec.cfg`.

---

## 8. Known Issues and Pending Work

See `TODO.md` for the prioritised task list. Older items still visible in the codebase:

1. `Core/Converters.cs` — several `ConvertBack` methods throw `NotImplementedException`. Safe only where the binding is one-way.
2. Inventory tab degrades to cached/empty results if Steam is not running or not logged in.
3. Network surfaces (Steam news, GameBanana, prices.tf, Steam Market) need graceful degradation beyond the current cache-first strategy.
4. `SharpCompress` has a known moderate-severity vulnerability (NU1902). Upgrade when a trustworthy stable release is available.

---

## 9. Troubleshooting Checklist

| Symptom | First check |
|---|---|
| Settings not saving | `settings.json` loaded? Steam path valid? Check `[Settings]` log entries. |
| "Launch TF2" does nothing | Is the launcher already elevated? Check Task Manager — self-elevation requires an unelevated parent. |
| Patcher not found | Are `steam_patcher.exe` / `pure_patcher.exe` present next to `LauncherTF2.exe` in the output directory? |
| Mod doesn't appear | Check `[Mods] Refresh scan target:` log line — the mod must be a top-level `.vpk` or folder in `custom/`, not inside a subfolder. |
| Mod thumbnails missing | Delete `mod_metadata_cache.json` and `mod_thumbnails/` to force re-enrichment. |
| Inventory empty | Steam running and logged in? `tf2_inventory_cache.json` present and not expired? |
| Theme not applying | Is the brush frozen? `ThemeManagerService.EnsureMutableBrushes()` must run before `MainWindow` resolves `StaticResource` references. |
| Profile always shows "Custom" | Run `ProfileService.DetectCurrentProfile` manually and log the mismatch. Likely a type comparison issue or a setting not in the schema index. |
| autoexec managed block is huge | `AutoexecWriter` should only emit non-default values. If the block is large, the user's autoexec was parsed on startup and lifted non-default values into the model — these are written back on next save. |

---

## 10. Quick-reference: adding things

### New TF2 setting
1. `SettingsModel.cs` — add property with safe default.
2. `SettingsSchema.cs` — append `ToggleSetting` / `SliderSetting` / `ChoiceSetting` to the right category. Done — UI and autoexec follow automatically.
3. If cvar semantics invert: add `CustomEmitter` and mirror it in `AutoexecParser`.

### New built-in profile
1. Create `src/LauncherTF2/Resources/Profiles/{id}.json` with lowercase keys, `"isUserCreated": false`, `"version": 1`, and a `"settings"` dict keyed by `PropertyName`.
2. Add `<EmbeddedResource Include="Resources/Profiles/{id}.json" />` to the csproj (the existing glob already covers `*.json` in that folder).
3. Optionally add `"launchOptions"` for flags like `-dxlevel N`.

### New theme
1. Append a `ThemeDefinition` to `_themes` in `ThemeCatalog.cs`.
2. Drop logo (`logo_{id}.png`) and icon (`logo64_{id}.ico`) in `resources/Assets/` and reference them in the definition.
3. If assets are missing, `ThemeManagerService.ResolveLogo` falls back to the classic logo automatically.

### New service
1. Add to `ServiceLocator.Initialize()`.
2. Expose as a static property on `ServiceLocator`.
3. Inject into ViewModels via the locator, not via constructor parameters.
