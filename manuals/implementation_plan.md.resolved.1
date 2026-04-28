# Home Tab Rework — Implementation Plan

## Goal

Transform the Home tab from a static splash screen into a live dashboard showing:
1. **TF2 Update Feed** — Latest changelogs/news from Steam
2. **Community News** — Steam community announcements
3. **New Mods** — Latest TF2 mods from GameBanana with thumbnails
4. Add **micro-animations** across the entire launcher

## Current State

The Home tab is a static screen with a background image, a "TEAM FORTRESS 2" title, some placeholder cards ("Premium Account", "The Orange Box"), and a "FREE TO PLAY" button. The `HomeViewModel` only has a `PlayCommand`. Nothing loads live data.

---

## Proposed Layout

```
┌─────────────────────────────────────────────────────────┐
│  TF2 Update Feed                            [↻ Refresh] │
│  ┌──────────────────────────────────────────────────┐   │
│  │ 📰 "Summer 2026 Update"          June 15, 2026  │   │
│  │     New weapons, maps, and balance changes...    │   │
│  ├──────────────────────────────────────────────────┤   │
│  │ 📰 "Hotfix Update"               June 10, 2026  │   │
│  │     Fixed crash on cp_dustbowl...                │   │
│  └──────────────────────────────────────────────────┘   │
│                                                         │
│  New TF2 Mods on GameBanana                             │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐      │
│  │ [thumb] │ │ [thumb] │ │ [thumb] │ │ [thumb] │      │
│  │ "OF     │ │ "Outlaw │ │ "Paw-   │ │ "Melta- │      │
│  │ nades"  │ │ from VL"│ │ slinger"│ │ gun"    │      │
│  │ nsxtf2  │ │ Ivon    │ │ Ysachov │ │ Ivon    │      │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘      │
└─────────────────────────────────────────────────────────┘
```

---

## API Endpoints (Verified Working)

### Steam News (no API key required)
```
GET https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=440&count=5&maxlength=300&format=json
```
Response: `appnews.newsitems[]` with `title`, `contents`, `date`, `url`, `feedlabel`

### GameBanana New TF2 Mods (no API key required)
```
GET https://gamebanana.com/apiv11/Mod/Index?_nPage=1&_nPerpage=8&_csvProperties=_idRow,_sName,_aPreviewMedia,_aSubmitter,_tsDateAdded&_aFilters[Generic_Game]=297&_sSort=Generic_LatestModified
```
Response: `_aRecords[]` with `_sName`, `_aSubmitter._sName`, `_aPreviewMedia._aImages[0]`, `_sProfileUrl`

---

## Proposed Changes

### New Service

#### [NEW] `Services/HomeFeedService.cs`
- Fetches Steam news and GameBanana new mods
- 15-minute in-memory cache (no disk persistence needed)
- `GetSteamNewsAsync(count)` → `List<NewsItem>`
- `GetNewModsAsync(count)` → `List<NewModItem>`
- Error handling: returns empty list on failure, doesn't crash

### New Models

#### [NEW] `Models/NewsItem.cs`
```csharp
public class NewsItem { Title, Contents, Date, Url, FeedLabel }
```

#### [NEW] `Models/NewModItem.cs`
```csharp
public class NewModItem { Name, Author, ThumbnailUrl, ProfileUrl, DateAdded }
```

---

### ViewModel

#### [MODIFY] `ViewModels/HomeViewModel.cs`
- Add `ObservableCollection<NewsItem> NewsItems`
- Add `ObservableCollection<NewModItem> NewMods`
- Add `IsLoading` property for loading indicator
- Add `RefreshCommand` to manually reload
- Auto-load on construction via `Task.Run`
- Add `OpenUrlCommand` to open news/mod URLs in browser

---

### UI

#### [MODIFY] `Views/HomeView.xaml`
Complete rewrite:
- Remove static placeholder content
- Top section: scrollable news feed (cards with title, date, preview text)
- Bottom section: horizontal mod carousel with thumbnail images
- Add entrance animations (fade-in + slide-up on load)
- Cards clickable → opens URL in default browser

#### [MODIFY] `Views/HomeView.xaml.cs`
- Minimal — just `InitializeComponent` (logic stays in ViewModel)

---

### Launcher-Wide Animations

#### [MODIFY] `Views/MainWindow.xaml`
- Add content transition animation on tab switch (fade + slight slide)

---

### Service Registration

#### [MODIFY] `Core/ServiceLocator.cs`
- Register `HomeFeedService` as a singleton

---

## Verification Plan

### Automated
- `dotnet build` — 0 warnings, 0 errors

### Manual
- Launch the app
- Verify news items load and show real TF2 update titles
- Verify GameBanana mod thumbnails render
- Click a news item → opens in browser
- Click a mod → opens GameBanana page
- Switch tabs → observe transition animation
- Test with internet disconnected → graceful empty state
