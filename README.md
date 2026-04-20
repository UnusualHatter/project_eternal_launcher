# Project Eternal — TF2 Launcher

A desktop launcher for Team Fortress 2 that handles the stuff Steam doesn't.

Launch TF2 the way you actually want to — with your settings, your mods, and a live view of your inventory prices pulled from multiple stores at once.

---

## What it does

Three things, kept separate so they don't get in each other's way:

**Launch & settings** — Orchestrates TF2 through Steam (including the native patcher flow), persists your settings across sessions, and auto-generates your `autoexec.cfg`. Single-instance with tray support, so it stays out of your way when you don't need it.

**Mod management** — Scans your local mod library, lets you enable/disable/install mods without touching the game folder by hand.

**Inventory** — Pulls per-item pricing from multiple stores and shows them side by side, with filtering and sorting. Direct desktop-to-marketplace calls tend to get throttled by anti-bot layers, so a small local backend handles that instead (more on that below).

---

## Getting started

**You'll need:** Windows 10/11, .NET 8 SDK, and TF2 installed via Steam.

```powershell
git clone ...
./scripts/build.ps1       # builds everything
scripts\start.bat         # starts the aggregator + launcher together
```

If you only want the launcher without the pricing backend:

```bat
scripts\run.bat
```

---

## The pricing aggregator

The inventory tab talks to a local ASP.NET Core backend (`PricingAggregator`) rather than hitting marketplace APIs directly. The reason is practical: marketplace endpoints often block or throttle direct desktop requests. The aggregator centralizes those calls, caches results, adds timeout handling, and hands back a consistent response shape.

Default endpoint: `http://localhost:5204/api/prices`

> The startup script sets `TF2_PRICING_AGGREGATOR_URL` automatically before launching the app.

---

## Project layout

```
project_eternal_launcher/
├─ src/LauncherTF2/        WPF launcher (.NET 8)
├─ PricingAggregator/      local pricing API (.NET 8)
├─ scripts/                build, run, and start helpers
├─ resources/Assets/       launcher images
├─ cfg/                    reference game config files
└─ docs/                   implementation notes and handoffs
```

Build command: `dotnet build project_eternal_launcher-main.sln -c Debug`

> Native patcher binaries are intentionally versioned under `src/LauncherTF2/native/` — don't move them.

---

MIT — see `LICENSE`.
