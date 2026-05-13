# Eternal TF2 Launcher

A Windows launcher for Team Fortress 2 that handles the things Steam doesn't: apply optimised settings, manage mods, browse your inventory, and launch the game with the right patchers — all from one place.

---

## What it does

**Settings** — Every TF2 cvar the launcher exposes is in the Settings tab, organised by category (Gameplay, Competitive, Performance, Audio, Viewmodels, Network, Advanced). Changes write a managed block into your `autoexec.cfg` automatically. If you already have a hand-rolled autoexec, the launcher reads it on first launch and pulls your existing values into the UI without touching anything else in the file.

**Profiles** — Save, load, and share complete settings snapshots. Four built-in presets (Competitive, Max Performance, Max Quality, Stability) apply settings and the correct DirectX level in one click. You can save your current setup as a user profile and export it as a JSON file.

**Mods** — Drag a VPK, folder, ZIP, RAR, or 7z onto the mod list to install it. Mods can be enabled and disabled without deleting them. Multi-file VPK sets (the `_dir.vpk + _000.vpk` kind) are handled as a single unit. GameBanana metadata and thumbnails are fetched in the background.

**Inventory** — Browse your TF2 backpack and see current market prices from prices.tf and the Steam Market in the item detail panel. Results are cached locally so the tab stays usable when you're rate-limited.

**Home feed** — Latest TF2 Steam news and new GameBanana mods on the landing page.

**Themes** — 10 built-in themes (Eternal Classic, Australium, RED, BLU, Carbon, Midnight, Plasma, Toxic, Synthwave, Minimal). The palette, logo, and window icon update live when you switch.

---

## Requirements

- Windows 10 version 2004 or later (64-bit)
- .NET 8 Desktop Runtime (x64) — download at https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe
- Team Fortress 2 installed via Steam

---

## Install

Download `EternalTF2Launcher-Setup-{version}.exe` from the Releases page and run it. The installer does not require admin rights. It installs to `%LocalAppData%\Programs\Eternal TF2 Launcher` and creates a Start Menu shortcut.

---

## Build from source

Requires .NET 8 SDK.

```powershell
dotnet build src/LauncherTF2/LauncherTF2.csproj -c Debug
dotnet run --project src/LauncherTF2/LauncherTF2.csproj
```

Or use the included scripts:

```powershell
scripts\build.ps1
scripts\start.bat
```

---

## How the game launch works

Clicking "Launch TF2" shows one UAC prompt. The launcher stays unelevated in the tray while an elevated child process runs `steam_patcher.exe`, starts TF2 through Steam, waits for the game window to appear, and then runs `pure_patcher.exe`. The child exits on its own when that sequence finishes.

---

## Settings and autoexec

The launcher writes only to its own marked section in `autoexec.cfg`:

```
// === ETERNAL LAUNCHER MANAGED BLOCK — do not edit between these markers ===
...
// === ETERNAL LAUNCHER MANAGED BLOCK END ===
```

Everything outside those markers is yours and is never touched. Only settings that differ from the launcher's built-in defaults are written — if a cvar matches the default, it is omitted so the block stays minimal and does not fight with your own config.

DirectX level is applied as a Steam launch option (`-dxlevel N`), not as `mat_dxlevel` in autoexec, because writing `mat_dxlevel` corrupts `video.txt` on the next start.

---

## Mods

Enabled mods sit in `tf/custom/`. Disabled mods move to `tf/custom/disabled/`. Toggling a mod moves its files between the two folders. Removing a mod deletes it from disk permanently.

---

## Files the launcher creates

All of these are next to the exe (or in AppData for profiles) and are not part of the repository:

- `settings.json` — your TF2 settings and launch args
- `launcher_config.json` — theme, tray behaviour, log level
- `%AppData%\Eternal TF2 Launcher\profiles\user\` — saved profiles
- `price_cache.json` — market prices (2-hour cache)
- `tf2_inventory_cache.json` — backpack data (10-minute cache)
- `mod_metadata_cache.json` and `mod_thumbnails/` — GameBanana enrichment (7-day cache)
- `app_debug.log` — debug log

---

## License

MIT — see `LICENSE`.
