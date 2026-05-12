# PRD — Unified Profile System for Eternal Launcher

## Executive Summary

Redesign the preset system from cfg-based fragments + mastercomfig integration to a unified **Profile** system (similar to OBS/VSCode/Blender) that:

- Stores snapshots of SettingsModel state as JSON
- Applies profiles by directly mutating SettingsModel properties
- Integrates with existing schema/UI/persistence pipeline
- Supports user-created profiles alongside built-in profiles
- Detects existing user autoexec and builds a "Current Profile" view
- Survives user manual autoexec edits

---

## Current State

### Existing Systems (TO REMOVE)

- `PerformancePresets.cs` — Apply*MaxFps/Competitive/Balanced/HighQuality
- `NetworkPresets.cs` — Apply*Casual/Competitive/HighPing/Lan
- Preset chips in Settings UI (clicking them calls preset funcs)
- Any mastercomfig integration code
- Any logic that loads/execs external cfg fragments

### Existing Systems (KEEP + INTEGRATE)

- `SettingsModel` — data model, PropertyChanged notifications
- `SettingsSchema.cs` — declarative cvar surface, getters/setters
- `AutoexecWriter` — serializes SettingsModel → autoexec.cfg managed block
- `AutoexecParser` — parses entire autoexec → rehydrates SettingsModel
- `SettingsService` — persistence (settings.json, launcher_config.json)
- Schema-driven UI (ItemsControl + DataTemplates)
- Property bindings (SettingsModel ↔ UI)

---

## Goals

### Primary Goals

1. **Unified system** — one way to manage groups of settings (no PerformancePresets + NetworkPresets distinction)
2. **User-created profiles** — save/rename/delete/export/import user profiles
3. **Built-in profiles** — handcrafted launcher profiles (Competitive, Balanced, Max FPS, etc)
4. **Autoexec-aware** — detect and represent existing user autoexec as "Current Profile"
5. **Schema-integrated** — profiles apply via schema setters, not cfg exec chains

### Non-Goals

- Mastercomfig imports (too heavyweight, not our design)
- External cfg modules or includes
- Preset chip system (profile selection in UI replaces this)
- Multi-step preset application ("apply this, then that")
- Cross-game profiles

---

## REMOVE

Delete completely:

- `src/LauncherTF2/Services/SettingsPresets.cs`
- Any PresetButton/chip UI logic in SettingsView
- Any "Apply Preset" commands or triggers
- PresetSetting in SettingsSchema (replaced by profile selection)
- Any mastercomfig detection/import code
- Any preset= markers in autoexec

---

## PROFILE ARCHITECTURE

### Profile Model

```csharp
public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    
    // User-created profiles: true. Built-in profiles: false.
    public bool IsUserCreated { get; set; }
    
    // Dictionary of SettingsModel property names → values to apply
    // Only includes settings that differ from defaults or are explicitly set.
    public Dictionary<string, object?> Settings { get; set; } = new();
    
    // Timestamp when this profile was created/edited
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    
    // Version number for future schema migration
    public int Version { get; set; } = 1;
}
```

### Storage

Profiles stored as individual JSON files:

```
AppData/profiles/
├── builtin/
│   ├── competitive-graphics.json
│   ├── balanced-graphics.json
│   ├── maxfps-graphics.json
│   ├── competitive-network.json
│   └── ...
└── user/
    ├── mycompetitive.json
    ├── streaming-setup.json
    └── ...
```

Built-in profiles bundled with launcher, read-only (versioned in git, not overwritten by update).

User profiles persisted in AppData, fully editable.

---

## PROFILE TYPES

### Built-in Profiles (Handcrafted)

**Graphics:**
- `competitive-graphics` — max clarity, minimum effects
- `balanced-graphics` — good balance of quality + perf
- `maxfps-graphics` — all optimizations, minimum eye candy
- `highquality-graphics` — enable all visual features, recommend powerful PC
- `cinematic-graphics` — maximum quality (demo/video mode)

**Network:**
- `competitive-network` — low interp, high rate/cmdrate
- `casual-network` — standard community pub settings
- `lan-network` — optimized for local/LAN play
- `highping-network` — adjusts for high-latency connections

**Audio:**
- `competitive-audio` — maximum clarity, minimal immersion
- `immersive-audio` — full mix for atmosphere

**Viewmodels:**
- `minimal-viewmodels` — off-screen or minimal
- `vanilla-viewmodels` — standard TF2
- `competitive-viewmodels` — optimized for comp play

Each profile is a `.json` file containing only the settings it changes.

### User Profiles

Users can:
- Create blank profile from scratch
- Save current settings as profile
- Duplicate existing profile
- Rename profile
- Delete profile
- Export profile (JSON file download)
- Import profile (user selects JSON file)

---

## AUTOEXEC DETECTION & PROFILE REPRESENTATION

### Current Profile Detection

On startup (in `SettingsViewModel` ctor):

1. Load settings.json (default launcher settings)
2. Run `AutoexecParser.LoadFromAutoexec()` (reads user's autoexec.cfg, applies values to SettingsModel)
3. Compare resulting SettingsModel state against:
   - Default SettingsModel (fresh instance)
   - Available profiles (built-in + user)
4. Determine "current profile":
   - If matches a profile exactly → show that profile name in UI
   - If partially matches → show "Custom" with indicator
   - If unmodified from defaults → show "Default"

### User Autoexec as "Profile"

The AutoexecParser already does the heavy lifting:
- Parses entire autoexec (managed block + user-owned)
- Extracts cvar values
- Applies via SettingsModel setters (with inversions like mat_disable_bloom)

**New behavior:**

- Run parser on each startup (already does this)
- If user has hand-edited autoexec outside the managed block → those values stay loaded
- When user applies a profile → launcher rewrites managed block with profile values, but preserves user-owned content
- If user modifies UI → launcher updates managed block, user content unaffected

**Example:**

User has autoexec.cfg:
```
// === ETERNAL LAUNCHER MANAGED BLOCK ===
...
// === END ETERNAL LAUNCHER MANAGED BLOCK ===

// ─── User's custom stuff ───
exec my_custom.cfg
echo User cfg loaded
```

User applies "Competitive Graphics" profile:
- Launcher writes new managed block (Competitive Graphics settings)
- `exec my_custom.cfg` and echo line SURVIVE (outside managed block)

If user later hand-edits `// User's custom stuff` section:
- Next launcher restart parses it
- Values apply to SettingsModel
- If they conflict with a profile, show "Custom" in UI

---

## PROFILE TYPES IN UI

### Profile Selection UI

New component in Settings tab (top of General section or new "Profiles" section):

- **Dropdown:** "Current Profile: [Competitive Graphics ▼]"
  - Options: Default, all built-in profiles, all user profiles, Custom (if mismatch)
  - "Manage Profiles..." button → opens profile manager modal

- **Apply button** (next to dropdown)
  - Clicking applies selected profile
  - Shows confirmation if profile overwrites user settings
  - After apply: model updates → schema re-evaluates → UI reflects → autoexec regenerated

### Profile Manager Modal

- **Built-in profiles section (read-only)**
  - List: name, description, category
  - Click → preview which settings change
  - Apply button

- **User profiles section**
  - List: name, creation date, last modified
  - Buttons per profile: Rename, Duplicate, Delete, Export
  - "New Profile" button
  - "Import Profile" button

- **Save Current as Profile**
  - Button: "Save Current Settings as Profile"
  - Prompts for name + description
  - Creates new user profile with current SettingsModel state

---

## APPLICATION FLOW

### Applying a Profile

1. User selects profile from dropdown or clicks "Apply"
2. `ProfileService.ApplyProfile(profile)`
3. For each entry in profile.Settings:
   - Lookup property on SettingsModel
   - Call setter (via schema delegate or reflection)
   - Model PropertyChanged fires
4. Schema wrappers re-evaluate (subscribed to PropertyChanged)
5. UI updates (bindings refresh)
6. `SettingsService.SaveSettings()` writes settings.json + autoexec.cfg
7. Profile name displayed as "Current Profile"

### Creating/Saving a Profile

1. User clicks "Save Current Settings as Profile"
2. Modal: name + description fields
3. "Save" button:
   - Create Profile instance
   - Snapshot current SettingsModel state
   - Compare against defaults → only include changed values
   - Save to `profiles/user/{name}.json`
   - Refresh profile list

### Importing a Profile

1. User clicks "Import Profile"
2. File picker → select .json
3. Parse as Profile, validate format
4. If already exists by ID → ask "Replace?" or "Import as Duplicate"
5. Copy to `profiles/user/{filename}.json`
6. Refresh profile list

---

## SERVICES & ARCHITECTURE

### New: ProfileService

```csharp
public class ProfileService
{
    // Load all profiles (built-in + user)
    public IReadOnlyList<Profile> GetAllProfiles();
    public IReadOnlyList<Profile> GetBuiltInProfiles();
    public IReadOnlyList<Profile> GetUserProfiles();
    
    // Apply a profile to SettingsModel
    public void ApplyProfile(Profile profile, SettingsModel target);
    
    // Detect which profile the current SettingsModel matches
    public Profile? DetectCurrentProfile(SettingsModel current);
    public bool ProfileMatches(Profile profile, SettingsModel current);
    
    // User profile CRUD
    public Profile CreateUserProfile(string name, string? description, SettingsModel snapshot);
    public void RenameUserProfile(string profileId, string newName);
    public void DeleteUserProfile(string profileId);
    public void ExportUserProfile(string profileId, string destinationPath);
    public Profile ImportUserProfile(string sourcePath);
    
    // Utility
    public void LoadAllProfiles(); // call on startup
}
```

### Integration Points

- **SettingsViewModel ctor** — call `ProfileService.LoadAllProfiles()` + `DetectCurrentProfile()`
- **SettingsViewModel** — add `CurrentProfile` property (bound to UI dropdown)
- **SettingsViewModel** — add `ApplyProfileCommand`
- **SettingsViewModel** — add `SaveAsProfileCommand`, `OpenProfileManagerCommand`
- **SettingsView** — add profile dropdown + apply button (General section top)
- **New ProfileManagerView** — modal for CRUD + import/export

### Schema Integration

ProfileService uses schema information:

```csharp
// When applying profile, iterate SettingsSchema.Build(target)
// to find the setter for each setting name
foreach (var category in SettingsSchema.Build(target))
{
    foreach (var item in category.Items)
    {
        if (profile.Settings.TryGetValue(item.PropertyName, out var value))
        {
            item.SetValue(target, value);  // use schema setter
        }
    }
}
```

This ensures:
- All CustomEmitters are respected (inversions stay correct)
- Dependencies (DependsOn) are honored
- Type conversions are consistent

---

## STORAGE STRUCTURE

### Built-in Profiles Location

Embedded in launcher:
```
src/LauncherTF2/Resources/Profiles/
├── competitive-graphics.json
├── balanced-graphics.json
├── maxfps-graphics.json
├── highquality-graphics.json
├── cinematic-graphics.json
├── competitive-network.json
├── casual-network.json
├── lan-network.json
├── highping-network.json
├── competitive-audio.json
├── immersive-audio.json
├── minimal-viewmodels.json
├── vanilla-viewmodels.json
└── competitive-viewmodels.json
```

Built-in profiles copied to AppData on first run (for display + reference).

### User Profiles Location

```
%APPDATA%/Eternal TF2 Launcher/profiles/user/
```

Each user profile is its own `.json` file, named after the profile.

### Example Profile JSON

```json
{
  "version": 1,
  "id": "comp-graphics-001",
  "name": "Competitive Graphics",
  "description": "Optimized for competitive play with maximum clarity.",
  "category": "graphics",
  "isUserCreated": false,
  "lastModified": "2025-05-12T10:30:00Z",
  "settings": {
    "Bloom": false,
    "MotionBlur": false,
    "MotionBlurStrength": 0.0,
    "VSync": false,
    "AnisotropicFiltering": 1,
    "Ragdolls": false,
    "DisableJiggleBones": true,
    "DisableDynamicLights": true,
    "DisableDecals": true,
    "DetailDistance": 1,
    "ModelLod": 2
  }
}
```

---

## BACKWARDS COMPATIBILITY

### Migration from Old Presets

On first launch after update:

1. Detect if user ever used old PerformancePresets/NetworkPresets
2. If last-used preset was "Competitive", auto-select "competitive-graphics" + "competitive-network"
3. If custom autoexec exists, build "Custom" as pseudo-profile for display
4. Do NOT auto-apply profiles — let user choose

### Settings.json Schema

Current `settings.json` structure unchanged. Profiles stored separately.

If user downgrades launcher → old version ignores `profiles/` folder, works with settings.json as before.

---

## VALIDATION & ERROR HANDLING

### Profile Validation

- Profile.Id must be unique
- Profile.Settings dict keys must be valid SettingsModel property names
- Profile.Settings values must be correct type or deserializable
- Built-in profiles must exist in resources
- User profiles must be readable/writable

### Invalid Profile Handling

- If profile.json is corrupt → log warning, skip it, continue
- If import fails → show error dialog, don't crash
- If apply fails (bad values) → log, show toast, revert to previous settings

---

## TESTING STRATEGY

### Unit Tests

- ProfileService.ApplyProfile — verify each setting applies correctly
- ProfileService.DetectCurrentProfile — verify matching logic
- Profile serialization/deserialization (JSON roundtrip)
- Import/export validation

### Integration Tests

- Apply profile → settings.json updates → autoexec.cfg regenerates correctly
- User manual autoexec edit → preserved when applying new profile
- Built-in profiles load on startup
- User profile CRUD works (create/rename/delete)

### Manual QA

- Apply each built-in profile, verify UI updates
- Save current settings as profile, apply it, verify identical
- Import/export user profile roundtrip
- Hand-edit autoexec, restart, verify values loaded
- Apply profile → hand-edit autoexec section outside managed block → apply different profile → verify hand-edits survived

---

## IMPLEMENTATION PHASES

### Phase 1: Core Infrastructure

1. Create Profile model + ProfileService
2. Build profile JSON loader (built-in resources)
3. Implement ApplyProfile + DetectCurrentProfile
4. Add ProfileService to ServiceLocator
5. Write unit tests

### Phase 2: UI Integration

1. Add profile dropdown + apply button to SettingsView
2. Create ProfileManagerView modal
3. Wire commands (ApplyProfileCommand, SaveAsProfileCommand, etc)
4. Add CurrentProfile property to SettingsViewModel
5. Test UI flow

### Phase 3: Import/Export

1. Implement ExportUserProfile
2. Implement ImportUserProfile
3. Add file pickers to ProfileManagerView
4. Test import/export roundtrip

### Phase 4: Built-in Profile Content

1. Handcraft each built-in profile JSON
2. Verify each profile applies correctly
3. Balance between competition/quality for "Balanced"
4. Document each profile's purpose

### Phase 5: Polish & Migration

1. Detect old preset usage, auto-suggest replacement
2. Migrate user-created presets (if any) to new format
3. Update launcher docs
4. Remove old PerformancePresets/NetworkPresets code
5. Full QA pass

---

## DOCUMENTATION

- Update SETTINGS_TAB_ARCHITECTURE.md with profile system
- Profile JSON schema (in code comments)
- User guide (help text in ProfileManagerView)
- Built-in profile descriptions (in each .json Description field)

---

## References

- Existing: AutoexecParser, AutoexecWriter, SettingsSchema, SettingsModel
- New: ProfileService, ProfileManagerView, Profile model
- Removal: PerformancePresets, NetworkPresets, old preset UI
