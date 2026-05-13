using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Models.Settings;
using LauncherTF2.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;

namespace LauncherTF2.ViewModels;

/// <summary>
/// One sidebar nav entry. Combines schema-driven categories with fixed
/// sections (General, Launcher, Personalization, Binds). The
/// <see cref="Id"/> is the stable anchor used by both the scroll-to-section
/// logic and the IsActive highlight.
/// </summary>
public sealed class SidebarEntry : ViewModelBase
{
    private bool _isActive;
    public string Id { get; init; } = "";   
    public string Title { get; init; } = "";
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
}

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly AutoexecParser _autoexecParser;
    private readonly ProfileService _profileService;
    private SettingsModel _currentSettings;

    // Launcher configuration properties
    private bool _enableDebugLog = false;
    private string _currentLogLevel = "Info";
    private bool _autoClearLogs = false;
    private bool _minimizeToTrayOnLaunch = true;
    private bool _closeToTray = true;
    private bool _showNotifications = true;

    private string _selectedCategory = "General";
    private string _displayMode = "Fullscreen";
    private string _pathValidationMessage = "";
    private bool _isPathValid;

    public SettingsModel CurrentSettings
    {
        get => _currentSettings;
        set => SetProperty(ref _currentSettings, value);
    }

    #region Launcher Config Properties

    public bool EnableDebugLog
    {
        get => _enableDebugLog;
        set
        {
            if (SetProperty(ref _enableDebugLog, value))
            {
                Logger.MinimumLogLevel = value ? LogLevel.Debug : LogLevel.Info;
                SaveLauncherSettings();
            }
        }
    }

    public string CurrentLogLevel
    {
        get => _currentLogLevel;
        set
        {
            if (SetProperty(ref _currentLogLevel, value) && Enum.TryParse<LogLevel>(value, out var logLevel))
            {
                Logger.MinimumLogLevel = logLevel;
                SaveLauncherSettings();
            }
        }
    }

    public bool AutoClearLogs
    {
        get => _autoClearLogs;
        set
        {
            if (SetProperty(ref _autoClearLogs, value))
                SaveLauncherSettings();
        }
    }

    public bool MinimizeToTrayOnLaunch
    {
        get => _minimizeToTrayOnLaunch;
        set
        {
            if (SetProperty(ref _minimizeToTrayOnLaunch, value))
                SaveLauncherSettings();
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (SetProperty(ref _closeToTray, value))
                SaveLauncherSettings();
        }
    }

    public bool ShowNotifications
    {
        get => _showNotifications;
        set
        {
            if (SetProperty(ref _showNotifications, value))
                SaveLauncherSettings();
        }
    }

    #endregion

    #region UI Navigation Properties

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    /// <summary>
    /// Unified display mode replacing 3 separate conflicting checkboxes.
    /// Maps to/from Fullscreen, Windowed, and Borderless booleans on the model.
    /// </summary>
    public string DisplayMode
    {
        get => _displayMode;
        set
        {
            if (SetProperty(ref _displayMode, value))
                ApplyDisplayMode(value);
        }
    }

    public string PathValidationMessage
    {
        get => _pathValidationMessage;
        set => SetProperty(ref _pathValidationMessage, value);
    }

    public bool IsPathValid
    {
        get => _isPathValid;
        set => SetProperty(ref _isPathValid, value);
    }

    #endregion

    #region Profiles

    private Profile? _currentProfile;
    private Profile? _selectedProfile;

    /// <summary>The profile that exactly matches the current settings state (or null if "Custom").</summary>
    public Profile? CurrentProfile
    {
        get => _currentProfile;
        private set => SetProperty(ref _currentProfile, value);
    }

    /// <summary>The profile currently selected in the dropdown.</summary>
    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public ObservableCollection<Profile> AvailableProfiles { get; } = new();

    public ICommand ApplyProfileCommand { get; }
    public ICommand SaveAsProfileCommand { get; }
    public ICommand OpenProfileManagerCommand { get; }
    public ICommand DismissMigrationBannerCommand { get; }

    private bool _showProfileMigrationBanner;
    public bool ShowProfileMigrationBanner
    {
        get => _showProfileMigrationBanner;
        set => SetProperty(ref _showProfileMigrationBanner, value);
    }

    #endregion

    public string[] LogLevels { get; } = ["Debug", "Info", "Warning", "Error"];
    public string[] DisplayModes { get; } = ["Fullscreen", "Windowed", "Borderless Windowed"];

    /// <summary>
    /// Three-way DirectX picker shown in General → Launch behavior. The selected
    /// option is written to <see cref="SettingsModel.DxLevel"/> and surfaces as
    /// the <c>-dxlevel N</c> launch argument (see <see cref="SyncLaunchOptions"/>).
    /// Source's <c>mat_dxlevel</c> autoexec cvar is intentionally NOT used — it
    /// corrupts video.txt on the next launch.
    /// </summary>
    public IReadOnlyList<ChoiceOption> DxLevelOptions { get; } =
    [
        new ChoiceOption("DirectX 8.1 — Low-end PCs / laptops", 81,
            "Highest FPS. Disables skins / war paints and may cause minor visual bugs (grayed-out level badges)."),
        new ChoiceOption("DirectX 9.0 — Recommended", 90,
            "Good balance for better graphics while maintaining decent performance."),
        new ChoiceOption("DirectX 9.5 — Best Graphics", 95,
            "For modern computers. Very stable, full quality, supports skins / war paints + all modern visuals."),
    ];

    /// <summary>Two-way bound by the General → DirectX combo. Mirrors <see cref="SettingsModel.DxLevel"/>.</summary>
    public ChoiceOption? DxLevelSelected
    {
        get => DxLevelOptions.FirstOrDefault(o => Equals(o.Value, _currentSettings.DxLevel))
               ?? DxLevelOptions.FirstOrDefault(o => Equals(o.Value, 90));
        set
        {
            if (value == null) return;
            var asInt = Convert.ToInt32(value.Value);
            if (_currentSettings.DxLevel == asInt) return;
            _currentSettings.DxLevel = asInt;
            OnPropertyChanged();
        }
    }

    // — Personalization bindings (proxied from ThemeManagerService) —

    /// <summary>The live theme service — bound directly by the personalization UI.</summary>
    public ThemeManagerService Theme => ServiceLocator.Theme;

    /// <summary>
    /// Schema-driven categories rendered into the dynamic ItemsControl. Built
    /// once from <see cref="SettingsSchema"/> and never replaced — wrappers
    /// inside it observe SettingsModel.PropertyChanged so external mutations
    /// (Reset, preset apply) keep the UI in sync.
    /// </summary>
    public ObservableCollection<SettingCategory> SchemaCategories { get; } = new();

    /// <summary>All sidebar nav entries (schema cats + fixed pages), in display order.</summary>
    public ObservableCollection<SidebarEntry> SidebarEntries { get; } = new();

    /// <summary>The currently highlighted sidebar entry, driven by the scroll position.</summary>
    public string? ActiveSidebarId
    {
        get => _activeSidebarId;
        set
        {
            if (SetProperty(ref _activeSidebarId, value))
            {
                foreach (var e in SidebarEntries)
                    e.IsActive = string.Equals(e.Id, value, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
    private string? _activeSidebarId;

    /// <summary>Raised when the user clicks a sidebar entry; the view animates the scroll to the matching anchor.</summary>
    public event EventHandler<string>? ScrollToCategoryRequested;

    public ICommand ResetCommand { get; }
    public ICommand AddBindCommand { get; }
    public ICommand RemoveBindCommand { get; }
    public ICommand StartListeningCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand NavigateCategoryCommand { get; }
    public ICommand SelectThemeCommand { get; }

    private BindModel? _listeningBind;

    public string[] AvailableKeys { get; } =
    [
        "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
        "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "kp_ins", "kp_end", "kp_downarrow", "kp_pgdn", "kp_leftarrow", "kp_5", "kp_rightarrow",
        "kp_home", "kp_uparrow", "kp_pgup", "kp_slash", "kp_multiply", "kp_minus", "kp_plus", "kp_enter", "kp_del",
        "escape", "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12",
        "mouse1", "mouse2", "mouse3", "mouse4", "mouse5", "mwheelup", "mwheeldown",
        "space", "backspace", "tab", "shift", "rshift", "ctrl", "rctrl", "alt", "ralt",
        "ins", "del", "home", "end", "pgup", "pgdn", "uparrow", "downarrow", "leftarrow", "rightarrow",
        "semicolon", "'"
    ];

    public SettingsViewModel()
    {
        _settingsService = ServiceLocator.Settings;
        _autoexecParser = new AutoexecParser();

        _currentSettings = _settingsService.GetSettings();

        if (string.IsNullOrEmpty(_currentSettings.SteamPath))
        {
            _currentSettings.SteamPath = GamePaths.DefaultTf2Path;
        }

        if (string.IsNullOrEmpty(_currentSettings.LaunchArgs))
        {
            _currentSettings.LaunchArgs = "+exec autoexec.cfg";
        }

        _autoexecParser.LoadFromAutoexec(_currentSettings, _currentSettings.SteamPath);

        _profileService = ServiceLocator.Profile;
        _profileService.LoadAllProfiles();
        RefreshProfiles();

        ShowProfileMigrationBanner = _profileService.IsFirstRunMigration;

        LoadLauncherSettings();
        SyncDisplayModeFromModel();
        ValidateTf2Path(_currentSettings.SteamPath);

        _currentSettings.PropertyChanged += CurrentSettings_PropertyChanged;
        _currentSettings.Binds.CollectionChanged += (s, e) => _settingsService.SaveSettings(_currentSettings);

        ResetCommand = new RelayCommand(o =>
        {
            if (Views.MessageDialog.ShowConfirm(
                "Reset All Settings",
                "This will restore all settings to their default values.\n\nAre you sure?",
                "Reset"))
            {
                Reset();
            }
        });
        AddBindCommand = new RelayCommand(o => AddBind());
        RemoveBindCommand = new RelayCommand(o => RemoveBind(o));
        StartListeningCommand = new RelayCommand(o => StartListening(o));
        BrowseFolderCommand = new RelayCommand(o => BrowseFolder());
        NavigateCategoryCommand = new RelayCommand(o =>
        {
            if (o is SidebarEntry e) RequestScroll(e.Id);
            else if (o is string id)  RequestScroll(id);
        });

        SelectThemeCommand = new RelayCommand(o =>
        {
            if (o is ThemeDefinition td) Theme.ApplyTheme(td.Id);
            else if (o is string id) Theme.ApplyTheme(id);
        });

        ApplyProfileCommand = new RelayCommand(o => ApplySelectedProfile(), o => SelectedProfile != null && SelectedProfile != CurrentProfile);
        SaveAsProfileCommand = new RelayCommand(o => SaveCurrentAsProfile());
        OpenProfileManagerCommand = new RelayCommand(o => OpenProfileManager());
        DismissMigrationBannerCommand = new RelayCommand(o => ShowProfileMigrationBanner = false);

        BuildSchemaAndSidebar();
    }

    /// <summary>
    /// Builds the schema-driven categories and the unified sidebar list. The
    /// fixed entries (General/Launcher/Personalization/Binds) are appended so
    /// the sidebar can scroll to them too via the same anchor mechanism.
    /// </summary>
    private void BuildSchemaAndSidebar()
    {
        SchemaCategories.Clear();
        foreach (var c in SettingsSchema.Build(_currentSettings))
            SchemaCategories.Add(c);

        SidebarEntries.Clear();
        SidebarEntries.Add(new SidebarEntry { Id = "general", Title = "General" });
        foreach (var c in SchemaCategories)
            SidebarEntries.Add(new SidebarEntry { Id = c.Id, Title = c.Title });
        SidebarEntries.Add(new SidebarEntry { Id = "launcher", Title = "Launcher" });
        SidebarEntries.Add(new SidebarEntry { Id = "personalization", Title = "Personalization" });
        SidebarEntries.Add(new SidebarEntry { Id = "binds", Title = "Binds" });

        ActiveSidebarId = "general";
    }

    private void RequestScroll(string anchorId)
    {
        ActiveSidebarId = anchorId;
        ScrollToCategoryRequested?.Invoke(this, anchorId);
    }

    /// <summary>Called by the view when the scroll position changes — updates the highlighted sidebar entry.</summary>
    public void SyncActiveFromScroll(string anchorId) => ActiveSidebarId = anchorId;

    #region Display Mode

    private void SyncDisplayModeFromModel()
    {
        if (_currentSettings.Fullscreen)
            _displayMode = "Fullscreen";
        else if (_currentSettings.Windowed && _currentSettings.Borderless)
            _displayMode = "Borderless Windowed";
        else if (_currentSettings.Windowed)
            _displayMode = "Windowed";
        else
            _displayMode = "Fullscreen";
    }

    private void ApplyDisplayMode(string mode)
    {
        _isHandlingConflict = true;
        try
        {
            switch (mode)
            {
                case "Fullscreen":
                    _currentSettings.Fullscreen = true;
                    _currentSettings.Windowed = false;
                    _currentSettings.Borderless = false;
                    break;
                case "Windowed":
                    _currentSettings.Fullscreen = false;
                    _currentSettings.Windowed = true;
                    _currentSettings.Borderless = false;
                    break;
                case "Borderless Windowed":
                    _currentSettings.Fullscreen = false;
                    _currentSettings.Windowed = true;
                    _currentSettings.Borderless = true;
                    break;
            }
            SyncLaunchOptions();
            _settingsService.SaveSettings(_currentSettings);
        }
        finally
        {
            _isHandlingConflict = false;
        }
    }

    #endregion

    #region Path Validation

    private void ValidateTf2Path(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            IsPathValid = false;
            PathValidationMessage = "Path is empty";
            return;
        }

        var cfgDir = Path.Combine(path, "cfg");
        if (Directory.Exists(cfgDir))
        {
            IsPathValid = true;
            PathValidationMessage = "✓ Valid TF2 path detected";
        }
        else
        {
            IsPathValid = false;
            PathValidationMessage = "⚠ Could not find tf/cfg folder at this path";
        }
    }

    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select your TF2 'tf' folder",
            InitialDirectory = Directory.Exists(_currentSettings.SteamPath)
                ? _currentSettings.SteamPath
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        if (dialog.ShowDialog() == true)
        {
            _currentSettings.SteamPath = dialog.FolderName;
            ValidateTf2Path(dialog.FolderName);
        }
    }

    #endregion

    #region Launcher Config Persistence

    private void LoadLauncherSettings()
    {
        var config = _settingsService.GetLauncherConfig();
        _enableDebugLog = config.EnableDebugLog;
        _currentLogLevel = config.LogLevel ?? "Info";
        _autoClearLogs = config.AutoClearLogs;
        _minimizeToTrayOnLaunch = config.MinimizeToTrayOnLaunch;
        _closeToTray = config.CloseToTray;
        _showNotifications = config.ShowNotifications;

        if (Enum.TryParse<LogLevel>(_currentLogLevel, out var logLevel))
        {
            Logger.MinimumLogLevel = logLevel;
        }
    }

    private void SaveLauncherSettings()
    {
        _settingsService.SaveLauncherConfig(new LauncherConfig
        {
            EnableDebugLog = _enableDebugLog,
            LogLevel = _currentLogLevel,
            AutoClearLogs = _autoClearLogs,
            MinimizeToTrayOnLaunch = _minimizeToTrayOnLaunch,
            CloseToTray = _closeToTray,
            ShowNotifications = _showNotifications
        });
    }

    #endregion

    #region Bind Listening

    private void StartListening(object? parameter)
    {
        if (parameter is BindModel bind)
        {
            if (_listeningBind != null)
                _listeningBind.IsListening = false;

            _listeningBind = bind;
            _listeningBind.IsListening = true;
        }
    }

    public void HandleKeyPress(Key key)
    {
        if (_listeningBind != null)
        {
            if (key == Key.Escape)
            {
                _listeningBind.IsListening = false;
                _listeningBind = null;
                return;
            }

            string keyName = MapKeyToSource(key);
            if (keyName != null)
            {
                _listeningBind.Key = keyName;
            }

            _listeningBind.IsListening = false;
            _listeningBind = null;
            _settingsService.SaveSettings(_currentSettings);
        }
    }

    private static string MapKeyToSource(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return key.ToString().ToLower();
        if (key >= Key.D0 && key <= Key.D9) return key.ToString().TrimStart('D');
        if (key >= Key.F1 && key <= Key.F12) return key.ToString().ToLower();

        return key switch
        {
            Key.NumPad0 => "kp_ins",
            Key.NumPad1 => "kp_end",
            Key.NumPad2 => "kp_downarrow",
            Key.NumPad3 => "kp_pgdn",
            Key.NumPad4 => "kp_leftarrow",
            Key.NumPad5 => "kp_5",
            Key.NumPad6 => "kp_rightarrow",
            Key.NumPad7 => "kp_home",
            Key.NumPad8 => "kp_uparrow",
            Key.NumPad9 => "kp_pgup",
            Key.Multiply => "kp_multiply",
            Key.Add => "kp_plus",
            Key.Subtract => "kp_minus",
            Key.Divide => "kp_slash",
            Key.Decimal => "kp_del",
            Key.Return => "enter",

            Key.Escape => "escape",
            Key.Space => "space",
            Key.Back => "backspace",
            Key.Tab => "tab",
            Key.LeftShift => "shift",
            Key.RightShift => "rshift",
            Key.LeftCtrl => "ctrl",
            Key.RightCtrl => "rctrl",
            Key.LeftAlt => "alt",
            Key.RightAlt => "ralt",
            Key.Insert => "ins",
            Key.Delete => "del",
            Key.Home => "home",
            Key.End => "end",
            Key.PageUp => "pgup",
            Key.PageDown => "pgdn",
            Key.Up => "uparrow",
            Key.Down => "downarrow",
            Key.Left => "leftarrow",
            Key.Right => "rightarrow",
            Key.OemSemicolon => "semicolon",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",

            _ => key.ToString().ToLower()
        };
    }

    public bool HandleMousePress(MouseButton button)
    {
        if (_listeningBind != null)
        {
            string? mouseName = MapMouseToSource(button);
            if (mouseName != null)
            {
                _listeningBind.Key = mouseName;
            }

            _listeningBind.IsListening = false;
            _listeningBind = null;
            _settingsService.SaveSettings(_currentSettings);
            return true;
        }
        return false;
    }

    private static string? MapMouseToSource(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "mouse1",
            MouseButton.Right => "mouse2",
            MouseButton.Middle => "mouse3",
            MouseButton.XButton1 => "mouse4",
            MouseButton.XButton2 => "mouse5",
            _ => null
        };
    }

    #endregion

    #region Binds CRUD

    private void AddBind()
    {
        CurrentSettings.Binds.Add(new BindModel { Name = "New Bind", Key = "x", Command = "say hello" });
    }

    private void RemoveBind(object? parameter)
    {
        if (parameter is BindModel bind)
        {
            CurrentSettings.Binds.Remove(bind);
            _settingsService.SaveSettings(_currentSettings);
        }
    }

    #endregion

    #region Property Change + Launch Arg Sync

    private bool _isHandlingConflict;

    private void CurrentSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isHandlingConflict) return;

        if (e.PropertyName == nameof(SettingsModel.SteamPath))
        {
            ValidateTf2Path(_currentSettings.SteamPath);
        }

        // Sync display mode UI when model booleans change externally
        if (e.PropertyName is nameof(SettingsModel.Fullscreen) or nameof(SettingsModel.Windowed) or nameof(SettingsModel.Borderless))
        {
            SyncDisplayModeFromModel();
            OnPropertyChanged(nameof(DisplayMode));
        }

        // Mirror DxLevel back to the combo when a preset / reset changes it.
        if (e.PropertyName == nameof(SettingsModel.DxLevel))
            OnPropertyChanged(nameof(DxLevelSelected));

        if (e.PropertyName != nameof(SettingsModel.LaunchArgs))
        {
            try
            {
                _isHandlingConflict = true;
                SyncLaunchOptions();
            }
            finally
            {
                _isHandlingConflict = false;
            }
        }
        _settingsService.SaveSettings(_currentSettings);
    }

    /// <summary>
    /// Flags that the launcher used to write but no longer supports — when we
    /// rebuild LaunchArgs we strip these so the string can't become stale.
    /// </summary>
    private static readonly string[] ObsoleteFlags =
    {
        "-full",                 // never a real TF2 flag — TF2 is fullscreen by default
        "-nosound",              // dangerous: silences the game
        "-nohltv",               // niche
        "-no_steam_controller",  // niche, can break controller users
        "-threads"               // deprecated in TF2 since 2017+
    };

    /// <summary>
    /// Rebuilds <see cref="SettingsModel.LaunchArgs"/> from the current toggle states.
    /// Custom user flags (anything we don't manage ourselves) are preserved.
    /// </summary>
    private void SyncLaunchOptions()
    {
        var parts = (_currentSettings.LaunchArgs ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Strip obsolete flags + their values (if the next token isn't itself a flag).
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            if (Array.IndexOf(ObsoleteFlags, parts[i]) < 0) continue;
            if (i + 1 < parts.Count && !parts[i + 1].StartsWith('-') && !parts[i + 1].StartsWith('+'))
                parts.RemoveAt(i + 1);
            parts.RemoveAt(i);
        }

        void SetFlag(string flag, bool enable)
        {
            if (enable)
            {
                if (!parts.Contains(flag)) parts.Add(flag);
            }
            else
            {
                parts.Remove(flag);
            }
        }

        void SetValueFlag(string flag, string value, bool include)
        {
            int index = parts.IndexOf(flag);
            if (include)
            {
                if (index != -1)
                {
                    if (index + 1 < parts.Count)
                        parts[index + 1] = value;
                    else
                        parts.Add(value);
                }
                else
                {
                    parts.Add(flag);
                    parts.Add(value);
                }
            }
            else if (index != -1)
            {
                if (index + 1 < parts.Count && !parts[index + 1].StartsWith('-') && !parts[index + 1].StartsWith('+'))
                    parts.RemoveAt(index + 1);
                parts.RemoveAt(index);
            }
        }

        // Display mode: TF2 defaults to fullscreen, so we only ever add the
        // -windowed / -noborder pair when explicitly requested.
        SetFlag("-windowed", _currentSettings.Windowed || _currentSettings.Borderless);
        SetFlag("-noborder", _currentSettings.Borderless);

        SetFlag("-novid", _currentSettings.SkipIntro);
        SetFlag("-nojoy", _currentSettings.DisableJoystick);
        SetFlag("-high", _currentSettings.HighPriority);
        SetFlag("-softparticlesdefaultoff", _currentSettings.SoftParticlesOff);
        SetFlag("-no_texture_stream", _currentSettings.NoTextureStream);
        SetFlag("-noreplay", _currentSettings.DisableReplay);

        SetValueFlag("-w", _currentSettings.Width.ToString(), true);
        SetValueFlag("-h", _currentSettings.Height.ToString(), true);
        SetValueFlag("-freq", _currentSettings.RefreshRate.ToString(), _currentSettings.RefreshRate != 60);
        // -dxlevel applies once at startup and writes the value into video.txt.
        // We always include it so the picker in General → Launch behavior is the
        // single source of truth. Cleared automatically if the user later edits
        // the launch options textbox and removes it manually.
        SetValueFlag("-dxlevel", _currentSettings.DxLevel.ToString(), true);

        // Always make sure +exec autoexec.cfg is present so our generated cfg runs.
        if (!parts.Any(p => p.Equals("+exec", StringComparison.OrdinalIgnoreCase)
                            && parts.IndexOf(p) + 1 < parts.Count
                            && parts[parts.IndexOf(p) + 1].Equals("autoexec.cfg", StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add("+exec");
            parts.Add("autoexec.cfg");
        }

        _currentSettings.LaunchArgs = string.Join(" ", parts);
    }

    #endregion

    /// <summary>
    /// Hard-resets every launcher-managed setting back to the SettingsModel defaults,
    /// rebuilds LaunchArgs from those defaults (stripping any previously-added flags),
    /// regenerates autoexec.cfg's managed block, and persists everything to disk.
    /// </summary>
    private void Reset()
    {
        // Detach during the swap so the per-property handler doesn't fire 30+ saves.
        _currentSettings.PropertyChanged -= CurrentSettings_PropertyChanged;

        _settingsService.ResetSettings();           // installs a fresh SettingsModel + JSON write
        CurrentSettings = _settingsService.GetSettings();

        SyncDisplayModeFromModel();
        OnPropertyChanged(nameof(DisplayMode));
        ValidateTf2Path(_currentSettings.SteamPath);

        // Rebuild LaunchArgs from the new defaults so resolution / +exec autoexec is back.
        _isHandlingConflict = true;
        try { SyncLaunchOptions(); } finally { _isHandlingConflict = false; }

        // Commit the rebuilt LaunchArgs + force autoexec regeneration.
        _settingsService.SaveSettings(_currentSettings);

        _currentSettings.PropertyChanged += CurrentSettings_PropertyChanged;
        _currentSettings.Binds.CollectionChanged += (s, e) => _settingsService.SaveSettings(_currentSettings);

        // Schema wrappers held references to the OLD model — rebuild them
        // against the new instance so toggles/sliders stay in sync.
        BuildSchemaAndSidebar();

        RefreshProfiles();

        Logger.LogInfo("[Settings] Reset all — defaults restored and persisted");
    }

    #region Profile Logic

    public void RefreshProfiles()
    {
        var prevSelected = SelectedProfile;

        AvailableProfiles.Clear();
        foreach (var p in _profileService.GetBuiltInProfiles()) AvailableProfiles.Add(p);
        foreach (var p in _profileService.GetUserProfiles()) AvailableProfiles.Add(p);

        CurrentProfile = _profileService.DetectCurrentProfile(_currentSettings);

        if (CurrentProfile != null)
        {
            // Always select the live-matched profile so the combo reflects reality.
            SelectedProfile = AvailableProfiles.FirstOrDefault(p => p.Id == CurrentProfile.Id) ?? CurrentProfile;
        }
        else if (prevSelected != null && AvailableProfiles.Any(p => p.Id == prevSelected.Id))
        {
            // Custom state — keep the combo on whatever was last selected so it doesn't jump to blank.
            SelectedProfile = AvailableProfiles.First(p => p.Id == prevSelected.Id);
        }
        // else: first run with no matching profile — leave SelectedProfile as-is (null).
    }

    private void ApplySelectedProfile()
    {
        if (SelectedProfile == null) return;
        
        try
        {
            _profileService.ApplyProfile(SelectedProfile, _currentSettings);
            _settingsService.SaveSettings(_currentSettings);
            RefreshProfiles();
        }
        catch (Exception ex)
        {
            // The service already rolled back the model on failure.
            // In a full app, we would show a toast/dialog here.
            Logger.LogError($"Failed to apply profile: {ex.Message}");
        }
    }

    private void SaveCurrentAsProfile()
    {
        var dialog = new Views.InputDialog("Save Current Settings", "Profile Name:", "My Profile", true)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true) return;

        var p = _profileService.CreateUserProfile(dialog.InputText, dialog.DescriptionText, _currentSettings);
        RefreshProfiles();
        SelectedProfile = p;
    }

    private void OpenProfileManager()
    {
        var vm = new ProfileManagerViewModel(_profileService, _currentSettings);
        var win = new Views.ProfileManagerView(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.ShowDialog();
        
        // Profiles might have been created/deleted/applied
        RefreshProfiles();
        // Since ApplyProfile might have been called, save just in case
        _settingsService.SaveSettings(_currentSettings);
    }

    #endregion
}



