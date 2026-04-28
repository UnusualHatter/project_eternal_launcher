# CLAUDE.md — Project Eternal Launcher Operations Manual

## 1. EXECUTABLE COMMANDS

### Build & Compile
```powershell
# Full solution build (Debug)
dotnet build project_eternal_launcher-main.sln -c Debug

# Full solution build (Release)
dotnet build project_eternal_launcher-main.sln -c Release

# Build launcher only
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug

# Publish launcher
dotnet publish src/LauncherTF2/LauncherTF2.csproj -c Release -o ./publish/launcher
```

### Run & Test
```powershell
# Run PowerShell build script
./scripts/build.ps1

# Run launcher
./scripts/start.bat

# Run launcher via dotnet (development)
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
```

### Watch Mode
```powershell
# Launcher: live rebuild on file change
dotnet watch run --project src/LauncherTF2/LauncherTF2.csproj
```

---

## 2. CODEBASE MAP

### Directory Structure
```
project_eternal_launcher/
├─ src/LauncherTF2/                 [WPF Desktop App — .NET 8 x64]
│  ├─ App.xaml(.cs)                 Entry point, single-instance guard, exception handler
│  ├─ Core/                         Infrastructure
│  │  ├─ ServiceLocator.cs          Composition root (all services initialized here)
│  │  ├─ Logger.cs                  10 MB rotating file log → app_debug.log
│  │  ├─ ViewModelBase.cs           MVVM base class
│  │  ├─ RelayCommand.cs            ICommand implementation
│  │  └─ AsyncRelayCommand.cs       Async ICommand wrapper
│  ├─ Models/                       Data structures
│  │  ├─ SettingsModel.cs           Steam path, launch args, etc.
│  │  ├─ LauncherConfig.cs          UI preferences
│  │  ├─ ModModel.cs                Mod enable/disable state
│  │  └─ BindModel.cs               Binding adapters
│  ├─ Services/                     Business logic
│  │  ├─ GameService.cs             Orchestrates TF2 launch (Steam → pure_patcher)
│  │  ├─ SettingsService.cs         Loads/saves settings.json
│  │  ├─ ModManagerService.cs       Scans {tf/custom} → toggles via filesystem
│  │  ├─ SteamDetectionService.cs   Finds Steam/TF2 paths
│  │  ├─ InventoryPricingService.cs Queries prices.tf + Steam Market directly
│  │  ├─ AutoexecWriter.cs          Generates autoexec.cfg
│  │  ├─ AutoexecParser.cs          Reads existing autoexec.cfg
│  │  └─ [others]                   SteamInventoryService, NativeExecutableService, etc.
│  ├─ ViewModels/                   MVVM ViewModel layer
│  │  ├─ MainViewModel.cs           Master orchestrator
│  │  ├─ HomeViewModel.cs           Launch + settings tab
│  │  ├─ ModsViewModel.cs           Mod manager UI
│  │  ├─ InventoryViewModel.cs      Pricing display
│  │  ├─ SettingsViewModel.cs       Config editor

│  ├─ Views/                        XAML UI definitions
│  │  ├─ MainWindow.xaml            Root window + tab control
│  │  ├─ HomeView.xaml              Launch, preset manager
│  │  ├─ ModsView.xaml              Mod list grid
│  │  ├─ InventoryView.xaml         Pricing grid + filters
│  │  ├─ SettingsView.xaml          Config form
│  │  └─ ConfirmDialog.xaml         Reusable confirmation dialog
│  ├─ native/                       **DO NOT MODIFY**
│  │  ├─ steam_patcher.exe          Pre-built native executable (copied to bin/)
│  │  └─ pure_patcher.exe           Pre-built native executable (copied to bin/)
│  ├─ LauncherTF2.csproj            Project file, build config
│  └─ app.manifest                  Admin elevation config
│
├─ resources/Assets/                Images & UI assets (PNG)
├─ cfg/                             Example autoexec.cfg templates
├─ scripts/
│  ├─ build.ps1                     Master build script
│  ├─ start.bat                     Launch launcher
│  └─ run.bat                       Launch launcher only
├─ docs/                            Implementation notes
├─ project_eternal_launcher-main.sln
├─ README.md
└─ LICENSE
```

### Key File Paths (Relative to Repo Root)
- **Launcher Output:** `src/LauncherTF2/bin/Debug/net8.0-windows/LauncherTF2.exe`
- **Native Patchers:** `src/LauncherTF2/native/{steam,pure}_patcher.exe`
- **Launcher Config:** `{AppDir}/settings.json` (persisted)
- **Price Cache:** `{AppDir}/price_cache.json` (2h TTL disk cache)
- **Mod State:** `{AppDir}/mod_state.json` (enable/disable tracking)
- **Logs:** `{AppDir}/app_debug.log` (10 MB rotating)

---

## 3. CODING CONVENTIONS

### Language & Framework
- **C# 12+** — Implicit usings, nullable reference types enabled
- **Target:** .NET 8.0 (Launcher: `-windows` variant)
- **WPF:** MVVM pattern, ObservableCollection, INotifyPropertyChanged
- **ASP.NET Core:** Dependency injection, scoped/transient services

### Naming Conventions
- **Classes:** `PascalCase` (e.g., `GameService`, `InventoryViewModel`)
- **Methods:** `PascalCase` (e.g., `LaunchTF2()`, `GetSettings()`)
- **Private fields:** `_camelCase` (e.g., `_settingsPath`, `_lock`)
- **Properties:** `PascalCase` (e.g., `CustomFolderPath`)
- **Local variables:** `camelCase` (e.g., `finalArgs`, `settings`)
- **Constants:** `UPPER_SNAKE_CASE` (e.g., `MutexName`, `MaxLogFileSize`)

### Async/Threading Patterns
- **Async methods:** Always suffix with `Async` (e.g., `LaunchTF2Async()`)
- **Fire-and-forget:** Use `_ = Task.Run(async () => { ... })` explicitly
- **Thread-safety:** Lock around shared state (e.g., `_lock` in SettingsService)
- **Single-flight gates:** Use `NativeExecutableService.ResetSingleFlight(key)` for one-shot operations

### Memory & Resource Management
- **Streams/Files:** Always wrap in `using` or `using()` declaration
- **Locks:** Acquired/released via `lock (_lock) { ... }` blocks
- **Process spawning:** `Process.Start()` → no explicit cleanup needed (CLR handles)
- **Disposal:** `_mutex?.Dispose()` explicitly in App_Exit

### Logging Style
```csharp
// Format: [Module] Message (brackets required for grepping)
Logger.LogInfo("[GameService] Launch orchestration started");
Logger.LogWarning("[ModManager] Failed to resolve custom folder", ex);
Logger.LogError("[SettingsService] Cannot load settings.json", ex);
```

### Error Handling
- **App-level:** `App_DispatcherUnhandledException` → shows MessageBox + writes `crash_log.txt`
- **Service-level:** Return `bool`/`null` on failure, log the exception
- **User-facing:** Catch, log, show friendly MessageBox

### Service Registration Pattern
```csharp
// In ServiceLocator.Initialize():
Settings = new SettingsService();
Game = new GameService(Settings);  // Dependency passed explicitly
```

### ViewModel Binding Pattern
```csharp
public ObservableCollection<ModModel> Mods { get; } = new();
public ICommand LaunchCommand { get; }

public SomeViewModel()
{
    LaunchCommand = new AsyncRelayCommand(LaunchAsync);
}
```

---

## 4. HARD BOUNDARIES

### ✋ NEVER DO THESE

1. **Native Executables**
   - DO NOT modify `src/LauncherTF2/native/steam_patcher.exe` or `pure_patcher.exe`
   - DO NOT recompile or replace them without explicit approval
   - DO NOT move them outside `native/` folder

2. **Settings & Config Files**
   - DO NOT edit `settings.json` directly; always use `SettingsService`
   - DO NOT parse/modify files outside the designated service (violates separation of concerns)
   - DO NOT store sensitive data in plain text config

3. **External Dependencies**
   - DO NOT add new NuGet packages without justification
   - DO NOT upgrade dependencies without running full build + test
   - DO NOT use undocumented or bleeding-edge package versions

4. **Mutex & Single Instance**
   - DO NOT bypass the `MutexName` guard in `App.xaml.cs`
   - DO NOT allow multiple launcher instances simultaneously

5. **Async & Thread Safety**
   - DO NOT mix `Task.Result` / `.Wait()` with async code (deadlock risk)
   - DO NOT read/write `_currentSettings` without locking
   - DO NOT bypass `_launchOrchestrationInProgress` gate in GameService

6. **Service Layer**
   - DO NOT create duplicate service instances in ViewModels
   - DO NOT access services outside of `ServiceLocator`
   - DO NOT hard-code paths; use `GamePaths` or `SettingsService`

7. **UI & XAML**
   - DO NOT bind directly to services in XAML (use ViewModel intermediary)
   - DO NOT perform long-running operations on the UI thread
   - DO NOT modify MainWindow layout without coordinating tab structure

8. **Logging**
   - DO NOT log sensitive data (Steam paths, API keys, user inventory)
   - DO NOT disable logging; configure via `LauncherConfig.EnableDebugLog`
   - DO NOT write arbitrary files; use Logger only

9. **Version Control**
    - DO NOT commit `bin/`, `obj/`, `.vs/`, `*.user` files
    - DO NOT commit sensitive credentials; use environment variables
    - DO NOT commit local test artifacts or crash logs

### ✅ ALWAYS DO THESE

- Log all major operations with `[ModuleName]` prefix
- Handle exceptions gracefully; show user-friendly messages
- Use `ServiceLocator` for all service access
- Write async methods with `Async` suffix
- Wrap resources in `using` blocks
- Coordinate with `SettingsService` for persistence
- Test build after adding dependencies
- Include XML doc comments on public methods

---

## 5. KNOWN INTEGRATIONS & ENDPOINTS

### External APIs (Direct from Launcher)
- **Launcher → prices.tf:** `https://api2.prices.tf/prices/{sku}`
- **Launcher → Steam Market:** `https://steamcommunity.com/market/priceoverview/...`

### File Paths (Resolved at Runtime)
- **Steam Root:** `HKEY_CURRENT_USER\Software\Valve\Steam\SteamPath`
- **TF2 Custom Folder:** `{SteamPath}/steamapps/common/Team Fortress 2/tf/custom/`
- **Disabled Mods Folder:** `{tf}/custom/disabled/`

### Process & Orchestration
1. **Launch:** User clicks "Launch TF2" → GameService.LaunchTF2()
2. **Steam Patcher:** Runs `steam_patcher.exe` via NativeExecutableService
3. **Steam Protocol:** Invokes `steam://run/440` to start TF2
4. **Pure Patcher Gate:** Single-flight checks prevent duplicate runs
5. **Autoexec:** AutoexecWriter regenerates on each launch

---

## 6. BUILD & DEPLOYMENT

### Build Modes
| Mode    | Command               | Output | Use Case |
|---------|----------------------|--------|----------|
| Debug   | `dotnet build -c Debug` | Symbols + full logs | Development |
| Release | `dotnet build -c Release` | Optimized binary | Production |

### Target Framework
- Launcher: `net8.0-windows` (WPF)
- Platform: x64 only (x86 explicitly not configured)

### Dependencies (Auto-fetched)
**LauncherTF2:**
- Hardcodet.NotifyIcon.Wpf (2.0.1) — Tray icon
- Microsoft.Data.Sqlite (8.0.10) — Settings persistence
- SharpCompress (0.36.0) — Mod archive extraction

---

## 7. TROUBLESHOOTING CHECKLIST FOR CLAUDE

Before making changes:
1. Verify settings.json parses correctly (SettingsService.LoadSettings)
2. Check logs in `app_debug.log` for previous errors
3. Ensure TF2 path exists before launching
4. Confirm no other launcher instance running (Mutex gate)
5. Check `crash_log.txt` if app exits unexpectedly
6. Ensure native patchers exist in `src/LauncherTF2/native/`
