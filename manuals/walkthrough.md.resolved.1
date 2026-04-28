# Session Handoff — Project Eternal Launcher

## What Was Done This Session

### 1. Settings Tab Complete Redesign ✅
**All files modified, built, and verified (0 warnings, 0 errors)**

#### Files Changed:
- **`Models/SettingsModel.cs`** — Removed dead API key fields (`BackpackTfApiKey`, `MarketplaceTfApiKey`, `StnTradingApiKey`). Added `NoTextureStream`, `DisableReplay`. Initially added `UseAllCores` and `PrecacheFontChars` but user researched and found these are fake/obsolete TF2 options — **removed them**.
- **`ViewModels/SettingsViewModel.cs`** — Full rewrite. Added: `DisplayMode` radio group (replaces 3 conflicting checkboxes), `BrowseFolderCommand` with `OpenFolderDialog`, path validation with feedback, `SelectedCategory` sidebar nav, fixed `SyncLaunchOptions()` bug (value-flags now properly removed when at default). Removed `-dxlevel` and `-threads` from permanent launch arg sync per user's TF2 research.
- **`Views/SettingsView.xaml`** — Complete rewrite. Now has category sidebar (General, Game, Graphics, Network, Advanced, Launcher, Binds) + scrollable card-based content. Every setting has a description. Written in 3 parts due to token limits.
- **`Views/SettingsView.xaml.cs`** — Added `Category_Click` handler for sidebar scroll-to-section.
- **`Services/AutoexecWriter.cs`** — Removed incorrect `-novid` line (launch args don't belong in autoexec.cfg).

#### User's TF2 Launch Option Research (Important Context):
The user did deep research using mastercomfig and TF2 community sources. Key findings:
- `-useallavailablecores` — **FAKE**, doesn't exist on TF2 (macOS leak)
- `-precachefontchars` — **Unverifiable myth**, removed
- `-threads` — Not recommended by modern guides, can cause harm
- `-dxlevel` — Must be used ONCE then removed, cannot be permanent toggle
- `-no_texture_stream` — **VALID**, added as replacement
- `-noreplay` — **VALID**, kept
- All other flags (`-novid`, `-nojoy`, `-nohltv`, `-softparticlesdefaultoff`, `-no_steam_controller`) confirmed valid

### 2. Previous Session Work (Already Done Before This Session)
- Removed News tab entirely
- Eliminated PricingAggregator backend — pricing logic inlined into launcher
- Updated solution file, start.bat, CLAUDE.md

---

## What Needs To Be Done Next

### Home Tab Rework (Plan Approved, Not Yet Executed)

The implementation plan is at:
`C:\Users\mathe\.gemini\antigravity\brain\1baf3239-d75f-4de7-8650-26f808a6a717\implementation_plan.md`

#### Summary:
Transform the static Home tab splash screen into a live dashboard with:
1. **TF2 Steam News Feed** — changelogs and updates
2. **New TF2 Mods from GameBanana** — with thumbnail images
3. **Tab transition animations** across the launcher
4. **Entrance animations** on Home cards

#### Verified API Endpoints (Both Free, No API Key):

**Steam News:**
```
GET https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=440&count=5&maxlength=300&format=json
```
Response structure: `appnews.newsitems[]` → each has `title`, `contents`, `date` (unix timestamp), `url`, `feedlabel`

**GameBanana New TF2 Mods:**
```
GET https://gamebanana.com/apiv11/Mod/Index?_nPage=1&_nPerpage=8&_csvProperties=_idRow,_sName,_aPreviewMedia,_aSubmitter,_tsDateAdded&_aFilters[Generic_Game]=297&_sSort=Generic_LatestModified
```
Response structure: `_aRecords[]` → each has:
- `_sName` — mod name
- `_aSubmitter._sName` — author
- `_aPreviewMedia._aImages[0]._sBaseUrl` + `/_sFile530` — thumbnail URL
- `_sProfileUrl` — link to mod page
- `_tsDateAdded` — unix timestamp

> [!IMPORTANT]
> Game ID 297 = Team Fortress 2 on GameBanana. The `_aFilters[Generic_Game]=297` parameter is REQUIRED or it returns mods from all games.

#### Files To Create:
1. **`Services/HomeFeedService.cs`** — Fetches Steam news + GameBanana mods. 15-min in-memory cache. Returns empty list on failure.
2. **`Models/NewsItem.cs`** — `Title, Contents, Date, Url, FeedLabel`
3. **`Models/NewModItem.cs`** — `Name, Author, ThumbnailUrl, ProfileUrl, DateAdded`

#### Files To Modify:
1. **`ViewModels/HomeViewModel.cs`** — Add `ObservableCollection<NewsItem>`, `ObservableCollection<NewModItem>`, `IsLoading`, `RefreshCommand`, `OpenUrlCommand`
2. **`Views/HomeView.xaml`** — Full rewrite: news cards + mod thumbnail carousel. Add fade-in/slide-up entrance animations.
3. **`Views/HomeView.xaml.cs`** — Minimal, just InitializeComponent
4. **`Core/ServiceLocator.cs`** — Register `HomeFeedService`
5. **`Views/MainWindow.xaml`** — Add content transition animation on tab switch

#### Design Notes:
- Use the same card style as Settings tab (`CardBrush` background, `CornerRadius="8"`, `Padding="18"`)
- News items should be clickable (open URL in default browser via `Process.Start`)
- Mod thumbnails use the `_sFile530` variant for decent quality
- The existing `GameBananaEnrichmentService` already has an `HttpClient` with browser-like User-Agent — reuse that pattern
- The launcher already has `AccentBrush` (#ff6b00 TF2 orange), `CardBrush` (#27272a), `BackgroundBrush`, `SecondaryTextBrush` defined in `App.xaml`

#### Important Build Note:
The XAML files are large. Due to token limits, write them in parts:
1. Write the file with a `PLACEHOLDER` comment
2. Replace the placeholder with actual content in a second call

---

## Current Build State
```
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug
→ Build succeeded. 0 Warning(s). 0 Error(s).
```

## How To Build & Run
```powershell
cd scripts
./build.ps1    # builds Debug
./start.bat    # launches the exe
```

## Key Architecture Patterns
- **MVVM** with `ViewModelBase` (has `SetProperty` + `OnPropertyChanged`)
- **ServiceLocator** pattern for DI (`ServiceLocator.Settings`, `ServiceLocator.Game`, etc.)
- **RelayCommand** for ICommand binding
- Tab navigation via `MainViewModel.CurrentView` + DataTemplates in `MainWindow.xaml`
- All views are `UserControl`s, main window is borderless with custom chrome
