# AGENTS.md — Project Eternal Launcher: Complete Operational Specification

## LAUNCHER CAPABILITIES & ARCHITECTURE

### Overview
Project Eternal is a three-layer launcher for Team Fortress 2 that operates independently from Steam's launcher:

1. **Game Orchestration** — Manages TF2 startup, patchers, and process lifecycle
2. **Configuration Management** — Persists settings and auto-generates game configs
3. **Content Management** — Mod installation/toggling and live inventory pricing

---

## LAYER 1: GAME ORCHESTRATION

### What It Does
The launcher acts as a orchestration middleman between the user and Steam/TF2. It automates the full launch pipeline:

**User clicks "Launch TF2"**
↓
**GameService.LaunchTF2()** → Interlocked gate prevents duplicate launches
↓
**NativeExecutableService** spawns `steam_patcher.exe` in background
↓
**GameService** invokes Steam protocol: `steam://run/440` (TF2 app ID)
↓
**GameService** monitors TF2 process lifecycle in background task
↓
**When TF2 loaded + ready** → triggers `pure_patcher.exe` (single-flight gate)
↓
**Launcher minimizes to tray**, stays running until TF2 exits
↓
**On TF2 exit** → Launcher can optionally close or stay in tray

### Key Components

#### GameService (`Services/GameService.cs`)
```
Public Interface:
  LaunchTF2() → bool (returns true even if async task fails, UI stays responsive)
  
Behind the Scenes:
  • Calls SettingsService.GetSettings() → retrieves Steam path + launch args
  • Validates Steam path format (expected: ".../steamapps/common/Team Fortress 2")
  • Resets pure_patcher single-flight gate (NativeExecutableService.ResetSingleFlight)
  • Spawns steam_patcher.exe via NativeExecutableService.RunNativeAsync()
  • Waits 3-5 seconds for patcher to complete
  • Invokes steam://run/440 protocol (Steam handles auto-launch if not running)
  • Creates background Task to monitor TF2 process:
    - Loops until TF2.exe exits
    - Polls every 500ms
    - On "process ready" event → fires pure_patcher exactly once
  • Returns immediately; UI thread unblocked
```

#### NativeExecutableService
```
Responsibilities:
  • Locates steam_patcher.exe and pure_patcher.exe in bin/Debug/net8.0-windows/
  • Spawns them with safe Process.Start() calls
  • Never waits for native executable completion (fire-and-forget)
  • Implements single-flight gate for pure_patcher via static dictionary
  • Cleans up Process objects properly
  
Entry Points:
  RunNativeAsync(exeName, args) → Task<bool>
  ResetSingleFlight(key) → void (allows gate to fire once more)
```

### Why This Design?
- **Steam Still Owns Launch:** We don't replace Steam's logic; we hook into it
- **Patcher Flow Automated:** Removes manual user steps (previously: user had to run patcher .exe before launching)
- **Non-Blocking:** MainWindow stays responsive during 30+ second TF2 startup
- **Single Launch Gate:** Prevents user from mashing "Launch" button multiple times

---

## LAYER 2: CONFIGURATION MANAGEMENT

### What It Does
Manages all persistent settings and auto-generated game configs. Handles three separate concerns:

1. **Launcher Settings** (UI preferences + Steam path)
2. **Game Settings** (autoexec.cfg generation)
3. **Mod State** (which mods are enabled/disabled)

### Key Components

#### SettingsService (`Services/SettingsService.cs`)
```
Responsibilities:
  • Loads settings.json at startup (or creates default if missing)
  • Thread-safe read/write access via _lock
  • Persists to disk whenever settings change
  • Validates Steam path on save
  
Data Model (SettingsModel):
  {
    "SteamPath": "C:/Games/Steam/steamapps/common/Team Fortress 2",
    "LaunchArgs": "-novid -useallavailablecores",
    "EnableDebugLog": false,
    "LogLevel": "Info",
    "AutoClearLogs": true,
    "MinimizeToTrayOnLaunch": true,
    "CloseToTray": true,
    "ShowNotifications": true
  }
  
Usage Pattern:
  SettingsService settings = ServiceLocator.Settings;
  var model = settings.GetSettings();        // locked read
  model.LaunchArgs = "-novid";
  settings.SaveSettings(model);              // locked write + disk flush
```

#### AutoexecWriter (`Services/AutoexecWriter.cs`)
```
Responsibilities:
  • Generates autoexec.cfg from user's launch args
  • Writes to {TF2}/cfg/autoexec.cfg
  • Reads existing autoexec.cfg to preserve custom user lines
  • Merges launcher-generated args with user's hand-crafted sections
  
Typical Flow:
  1. User sets launch args in SettingsView (e.g., "-novid -useallavailablecores")
  2. On "Save Settings" → SettingsService.SaveSettings()
  3. GameService.LaunchTF2() calls AutoexecWriter.GenerateAutoexec()
  4. Writes complete config to {tf}/cfg/autoexec.cfg
  5. TF2 loads cfg on startup
```

#### AutoexecParser (`Services/AutoexecParser.cs`)
```
Responsibilities:
  • Reads existing autoexec.cfg
  • Extracts user-custom lines (anything not launcher-generated)
  • Preserves user customizations during regeneration
  
Why Needed:
  • User may hand-edit autoexec.cfg between sessions
  • We must not destroy their tweaks
  • Parser identifies our own generated lines and leaves rest alone
```

### Settings Persistence Strategy

```
Disk Layout:
  {AppDirectory}/
    ├─ settings.json          ← SettingsService reads/writes
    ├─ mod_state.json         ← ModManagerService tracks mod enable/disable
    └─ app_debug.log          ← Logger, rotated at 10 MB
  
Steam Game Directory:
    └─ tf/cfg/
        └─ autoexec.cfg       ← AutoexecWriter generates
```

### Why This Design?
- **Separation of Concerns:** Launcher config ≠ Game config ≠ Mod state
- **Non-Destructive Updates:** AutoexecParser preserves user hand-edits
- **Fast Startup:** Settings cached in memory; no disk I/O on every read
- **Atomic Writes:** Lock ensures no partial/corrupt settings.json

---

## LAYER 3: CONTENT MANAGEMENT

### 3A. MOD MANAGEMENT

#### What It Does
Scans the TF2 mod folder and lets users enable/disable mods without manually moving files.

**User perspective:**
```
Mod Manager shows:
  ☑ CustomHUD_v2.0
  ☑ Transparent Viewmodels
  ☐ Custom Crosshairs (disabled)
  ☑ HitsoundPacks
  
Click checkbox → instantly enables/disables the mod
```

#### Technical Implementation

**ModManagerService** (`Services/ModManagerService.cs`)
```
Core Concept:
  • ENABLED mods live in: {TF2}/tf/custom/
  • DISABLED mods live in: {TF2}/tf/custom/disabled/
  
Scan Algorithm:
  1. Recursively scan {tf}/custom/ → folders = enabled mods
  2. Recursively scan {tf}/custom/disabled/ → folders = disabled mods
  3. Check mod_state.json for per-session state
  4. If mod_state.json missing → treat all in custom/ as enabled
  5. Return ObservableCollection<ModModel> to UI
  
Enable/Disable Operation:
  DisableMod(modName):
    1. Move {custom}/modName/ → {custom}/disabled/modName/
    2. Update mod_state.json
  
  EnableMod(modName):
    1. Move {custom}/disabled/modName/ → {custom}/modName/
    2. Update mod_state.json
```

**ModModel** (data structure)
```csharp
public class ModModel : INotifyPropertyChanged
{
    public string Name { get; set; }              // folder name
    public bool IsEnabled { get; set; }           // UI binding
    public string? Description { get; set; }      // metadata
    public DateTime DateAdded { get; set; }
}
```

#### ModsViewModel (`ViewModels/ModsViewModel.cs`)
```
Responsibilities:
  • Binds ModManagerService.Mods to UI grid
  • Command handlers:
    ToggleModCommand(ModModel) → calls ModManagerService.EnableMod/DisableMod
    InstallModCommand() → file picker → extracts archive → moves to {custom}/
    UninstallModCommand(ModModel) → deletes mod folder
  • Shows progress during install/extraction
```

#### ModsView.xaml
```xaml
<DataGrid ItemsSource="{Binding Mods, UpdateSourceTrigger=PropertyChanged}">
  <DataGridCheckBoxColumn Binding="{Binding IsEnabled}">
    <!-- Double-click or checkbox click → triggers toggle command -->
  </DataGridCheckBoxColumn>
  <DataGridTextColumn Binding="{Binding Name}" />
  <DataGridTextColumn Binding="{Binding DateAdded}" />
</DataGrid>
```

---

### 3B. INVENTORY PRICING

#### What It Does
Displays per-item pricing from multiple marketplaces side-by-side. User can filter, sort, and check value before trades.

**Data flow:**
```
User opens Inventory Tab
↓
InventoryViewModel requests prices from InventoryPricingService
↓
InventoryPricingService calls: http://localhost:5204/api/prices
↓
PricingAggregator backend queries:
  • prices.tf API
  • Steam Community Market
  • [Future: other sources]
↓
Results merged, cached, returned to InventoryView
↓
Grid displays:
  Item Name | prices.tf | Steam Market | Last Updated
```

#### InventoryPricingService (`Services/InventoryPricingService.cs`)
```
Responsibilities:
  • Calls PricingAggregator REST API
  • Timeout handling (default 5 seconds)
  • Caches results (configurable TTL in appsettings.json)
  • Converts JSON → ItemPriceResult objects
  
Usage:
  var results = await service.GetPricesAsync(itemName, skuFilter, cancellationToken);
  // Returns: List<ItemPriceResult>
```

#### PricingAggregator Backend (`PricingAggregator/`)
```
Architecture:
  PricesController
    ↓
  PricingAggregatorService (orchestrates sources, merges results)
    ↓
  IPricingSource implementations:
    • PricesTfSource    (queries prices.tf API)
    • SteamMarketSource (queries Steam Community Market)
  
Endpoint:
  GET http://localhost:5204/api/prices?itemName=Strange%20Knife&sku=Unusual

Response Shape:
  {
    "itemName": "Strange Knife",
    "sku": "Unusual",
    "prices": [
      {
        "storeName": "prices.tf",
        "price": 150.50,
        "currency": "keys",
        "lastUpdated": "2025-04-27T14:30:00Z"
      },
      {
        "storeName": "Steam Market",
        "price": 45.67,
        "currency": "USD",
        "lastUpdated": "2025-04-27T14:30:05Z"
      }
    ]
  }
```

#### InventoryViewModel & InventoryView
```xaml
<!-- Filters: rarity, type, value range -->
<ComboBox ItemsSource="{Binding Rarities}" SelectedItem="{Binding SelectedRarity}" />
<Slider Minimum="0" Maximum="1000" Value="{Binding MaxPriceFilter}" />

<!-- Grid with pricing data -->
<DataGrid ItemsSource="{Binding FilteredItems}">
  <DataGridTextColumn Header="Name" Binding="{Binding ItemName}" />
  <DataGridTextColumn Header="prices.tf" Binding="{Binding PricesTfPrice}" />
  <DataGridTextColumn Header="Steam" Binding="{Binding SteamMarketPrice}" />
</DataGrid>
```

#### Why This Design?
- **Backend Isolation:** Desktop → local API prevents direct marketplace API throttling
- **Caching:** Backend caches results; multiple UI refreshes don't hammer upstream APIs
- **Extensible:** Adding new pricing source = implement IPricingSource + register in DI
- **Cross-Platform:** Backend can run on any OS; launcher uses HTTP

---

## LAYER 4: SUPPORTING INFRASTRUCTURE

### Logging System (`Core/Logger.cs`)
```
Features:
  • Automatic file rotation (10 MB limit)
  • Keeps last 10 archived logs (.log.0, .log.1, etc.)
  • Thread-safe writes via lock
  • Configurable minimum log level
  
Usage:
  Logger.LogInfo("[GameService] TF2 launched successfully");
  Logger.LogError("[SettingsService] Failed to parse settings.json", ex);
  
Output Files:
  {AppDir}/app_debug.log             (current log, ~10 MB)
  {AppDir}/logs/app_debug.log.0      (previous session)
  {AppDir}/crash_log.txt             (last crash stack trace)
```

### Tray Icon (`Hardcodet.NotifyIcon.Wpf`)
```
Features:
  • Single-instance guard (prevents duplicate windows)
  • Minimize to tray
  • Right-click context menu: [Restore, Settings, Exit]
  • Notification bubbles on events (if enabled)
  
Implementation:
  MainWindow.xaml includes:
    <tb:TaskbarIcon
      IconSource="{resource ...logo.ico}"
      TrayMouseDoubleClick="{Binding RestoreCommand}" />
```

### Steam Detection (`Services/SteamDetectionService.cs`)
```
Algorithm:
  1. Check registry key: HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath
  2. If not found, scan: C:/Program Files (x86)/Steam
  3. Once Steam found, look for: {SteamPath}/steamapps/common/Team Fortress 2
  4. Validates folder structure (expects: tf/ subfolder)
  
Returns:
  (steamPath, tf2Path) or (null, null) if not found
```

---

## COMPLETE LAUNCH SEQUENCE

### User initiates launch via HomeView "Launch TF2" button:

```
1. HomeViewModel.LaunchCommand executes
2. MainViewModel.LaunchTF2() called
3. GameService.LaunchTF2() invoked
   ├─ SettingsService.GetSettings() → validate paths
   ├─ NativeExecutableService.RunNativeAsync("steam_patcher.exe")
   ├─ Process.Start("steam://run/440")
   ├─ Task.Run(async () => {
   │    while (TF2.exe running) {
   │      if (process_ready) {
   │        NativeExecutableService.RunNativeAsync("pure_patcher.exe")
   │      }
   │      await Task.Delay(500)
   │    }
   │  }) ← fires in background
   └─ return true immediately
4. SettingsView.LaunchArgs saved to settings.json
5. AutoexecWriter.GenerateAutoexec() writes cfg/autoexec.cfg
6. UI minimizes to tray (if MinimizeToTrayOnLaunch=true)
7. User plays TF2...
8. When TF2.exe exits:
   ├─ Launcher background task completes
   ├─ Optional: Close launcher or show notification
   └─ User can relaunch anytime
```

---

## PERSISTENCE & STATE MANAGEMENT

### On Startup (App.xaml.cs → ServiceLocator.Initialize())
```
1. Logger.Initialize(LogLevel.Info)
2. ServiceLocator.Initialize() creates:
   ├─ SettingsService → loads settings.json
   ├─ SteamDetectionService → detects Steam/TF2 paths
   ├─ GameService(SettingsService)
   ├─ ModManagerService(SettingsService) → scans mods
   ├─ InventoryPricingService
   └─ GameBananaEnrichmentService → pulls mod metadata
3. MainViewModel initialized with all services
4. MainWindow displayed
```

### On Shutdown (App_Exit)
```
1. Dispose mutex
2. Flush any pending logs
3. Save mod_state.json if modified
4. Clear temporary resources
```

---

## INTEGRATION POINTS & DEPENDENCIES

### Runtime Environment Variables
- `TF2_PRICING_AGGREGATOR_URL` — set by start.bat (default: http://localhost:5204/api/prices)
- `.NET 8.0` — Required (Launcher: Windows variant; Aggregator: cross-platform)

### External APIs (via PricingAggregator)
- **prices.tf** — HTTP GET → JSON pricing
- **Steam Community Market** — Screen scraping via HtmlAgilityPack (if used)

### File System Contracts
- **settings.json** — Must exist in app directory; auto-created if missing
- **tf/custom/** — Scanned recursively; enables/disables via move operations
- **cfg/autoexec.cfg** — Generated on each launch; user edits preserved
- **app_debug.log** — Rotated automatically

### Registry (Windows only)
- `HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath` — Used by SteamDetectionService

---

## EXTENSION POINTS FOR AI AGENTS

### Safe Modifications:
1. **Add new PricingSource** → implement IPricingSource, register in PricingAggregator/Program.cs
2. **Add new ViewModel/View** → follow MVVM pattern, inject via ServiceLocator
3. **Add new native wrapper** → use NativeExecutableService pattern
4. **Add new Logger targets** → extend Logger.cs (file, console, network)
5. **Extend ModModel** → add new properties, update UI binding

### Prohibited Modifications:
1. Removing single-instance guard
2. Modifying native patcher .exe files
3. Bypassing ServiceLocator pattern
4. Hard-coding file paths
5. Adding blocking waits in UI thread

---

## QUICK REFERENCE TABLE

| Component | Language | Purpose | Entry Point |
|-----------|----------|---------|-------------|
| Launcher | C# / WPF | Desktop UI + orchestration | src/LauncherTF2/App.xaml.cs |
| Aggregator | C# / ASP.NET | Pricing API | PricingAggregator/Program.cs |
| GameService | C# | Steam launch orchestration | GameService.LaunchTF2() |
| ModManager | C# | Mod enable/disable | ModManagerService.ScanMods() |
| Logger | C# | File-based logging | Logger.LogInfo/Error/Warning |
| AutoexecWriter | C# | Config generation | AutoexecWriter.GenerateAutoexec() |
| SettingsService | C# | Persistence | SettingsService.GetSettings() |

