# TODO — Next Session

Tracked items for the next development session.

---

## 1 — First-install default settings

Define sensible `SettingsModel` defaults that a brand-new user gets on first launch.

- Resolution / refresh rate are already auto-detected via `DisplayDetectionService` — verify this triggers correctly when `settings.json` is absent.
- Audit every `SettingsModel` field default: DxLevel, network settings (Interp, Rate, etc.), FPS cap, audio.  Make sure the out-of-box experience is safe and playable without requiring any manual configuration.

---

## 2 — "Default" profile (user backup on first run)

After `AutoexecParser` loads the user's existing autoexec on first launch, snapshot the resulting `SettingsModel` into a user profile named **"Default (Your Settings)"** and save it automatically.

- This gives the user a one-click restore point before they apply any built-in profile.
- Hook into the migration banner flow (`IsFirstRunMigration`) that already exists in `ProfileService` / `SettingsViewModel`.
- The profile should be created **once** — skip if a profile with the sentinel name already exists in `profiles/user/`.

---

## 3 — App identity: name + window/tray icon

### 3a — Process name
Confirm whether the desired display name is **"Eternal Launcher"** or the current **"Eternal TF2 Launcher"**. Update `<AssemblyTitle>`, `<Product>`, and `<FileDescription>` in the csproj accordingly.

### 3b — Window icon (title bar + taskbar)
`MainWindow.xaml` currently has no `Icon` property set so the window shows the generic WPF icon.

- Bind `Window.Icon` to `ThemeManagerService.CurrentIconSource` so it automatically updates when the user switches themes.
- The `ThemeManagerService` already resolves the per-theme `.ico` file — just wire it to the window.

### 3c — System tray icon
Verify the `TaskbarIcon` (Hardcodet.NotifyIcon.Wpf) is using the correct `.ico` source from `ThemeManagerService.CurrentIconSource`.

---

## 4 — Installer visual

Make the Inno Setup wizard look closer to the launcher's dark theme.

- Produce a `WizardImageFile` (164 × 314 px) and `WizardSmallImageFile` (55 × 55 px) using the launcher background colour and logo, and reference them in `installer/installer.iss`.
- For a fully custom UI consider WiX Burn bootstrapper, but the Inno Setup bitmaps alone go a long way.

---

## 5 — Full code audit: comments + bugs

- **Remove** all XML doc comments (`<summary>`, `<param>`, `<returns>`) that just restate the identifier name.
- **Remove** inline comments that describe *what* the code does; keep only comments explaining *why* (non-obvious invariants, workarounds, hidden constraints).
- **Rewrite** any remaining comments in a consistent, professional tone.
- **Scan** for `TODO`, `FIXME`, `HACK`, `XXX` markers and resolve them.
- **Bug pass** across all files changed in the profile-system / installer work: `ProfileService.cs`, `AutoexecWriter.cs`, `ProfileManagerViewModel.cs`, `SettingsViewModel.cs`, built-in profile JSONs — look for edge cases, silent failures, and logic errors before the next release.
