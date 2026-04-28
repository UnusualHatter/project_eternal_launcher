using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
using System.IO;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly AutoexecParser _autoexecParser;
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

    public string[] LogLevels { get; } = ["Debug", "Info", "Warning", "Error"];
    public string[] DisplayModes { get; } = ["Fullscreen", "Windowed", "Borderless Windowed"];
    public string[] Categories { get; } = ["General", "Game", "Graphics", "Network", "Advanced", "Launcher", "Binds"];

    public ICommand ResetCommand { get; }
    public ICommand AddBindCommand { get; }
    public ICommand RemoveBindCommand { get; }
    public ICommand StartListeningCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand SelectCategoryCommand { get; }

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

        if (string.IsNullOrEmpty(_currentSettings.SteamPath) || _currentSettings.SteamPath == @"C:\Program Files (x86)\Steam")
        {
            _currentSettings.SteamPath = GamePaths.DefaultTf2Path;
        }

        if (string.IsNullOrEmpty(_currentSettings.LaunchArgs))
        {
            _currentSettings.LaunchArgs = "+exec w/config.cfg +exec autoexec.cfg";
        }

        _autoexecParser.LoadFromAutoexec(_currentSettings, _currentSettings.SteamPath);

        LoadLauncherSettings();
        SyncDisplayModeFromModel();
        ValidateTf2Path(_currentSettings.SteamPath);

        _currentSettings.PropertyChanged += CurrentSettings_PropertyChanged;
        _currentSettings.Binds.CollectionChanged += (s, e) => _settingsService.SaveSettings(_currentSettings);

        ResetCommand = new RelayCommand(o => Reset());
        AddBindCommand = new RelayCommand(o => AddBind());
        RemoveBindCommand = new RelayCommand(o => RemoveBind(o));
        StartListeningCommand = new RelayCommand(o => StartListening(o));
        BrowseFolderCommand = new RelayCommand(o => BrowseFolder());
        SelectCategoryCommand = new RelayCommand(o => { if (o is string cat) SelectedCategory = cat; });
    }

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
    /// Rebuilds the LaunchArgs string from the current toggle/value states.
    /// Fixed: now properly removes flags when disabled instead of leaving orphaned args.
    /// </summary>
    private void SyncLaunchOptions()
    {
        var args = _currentSettings.LaunchArgs ?? "";
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

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
                    // Update existing value
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
            else
            {
                // Remove flag and its value when disabled
                if (index != -1)
                {
                    if (index + 1 < parts.Count && !parts[index + 1].StartsWith('-') && !parts[index + 1].StartsWith('+'))
                        parts.RemoveAt(index + 1);
                    parts.RemoveAt(index);
                }
            }
        }

        SetFlag("-novid", _currentSettings.SkipIntro);
        SetFlag("-nojoy", _currentSettings.DisableJoystick);
        SetFlag("-high", _currentSettings.HighPriority);
        SetFlag("-full", _currentSettings.Fullscreen);
        SetFlag("-windowed", _currentSettings.Windowed);
        SetFlag("-noborder", _currentSettings.Borderless);
        SetFlag("-nosound", _currentSettings.DisableSound);
        SetFlag("-nohltv", _currentSettings.DisableHltv);
        SetFlag("-softparticlesdefaultoff", _currentSettings.SoftParticlesOff);
        SetFlag("-no_steam_controller", _currentSettings.DisableSteamController);
        SetFlag("-no_texture_stream", _currentSettings.NoTextureStream);
        SetFlag("-noreplay", _currentSettings.DisableReplay);

        // Value flags — resolution always included, others only when non-default
        SetValueFlag("-w", _currentSettings.Width.ToString(), true);
        SetValueFlag("-h", _currentSettings.Height.ToString(), true);
        SetValueFlag("-freq", _currentSettings.RefreshRate.ToString(), _currentSettings.RefreshRate != 60);

        _currentSettings.LaunchArgs = string.Join(" ", parts);
    }

    #endregion

    private void Reset()
    {
        _currentSettings.PropertyChanged -= CurrentSettings_PropertyChanged;

        _settingsService.ResetSettings();
        CurrentSettings = _settingsService.GetSettings();

        SyncLaunchOptions();
        SyncDisplayModeFromModel();
        OnPropertyChanged(nameof(DisplayMode));
        ValidateTf2Path(_currentSettings.SteamPath);

        _currentSettings.PropertyChanged += CurrentSettings_PropertyChanged;
        _currentSettings.Binds.CollectionChanged += (s, e) => _settingsService.SaveSettings(_currentSettings);
    }
}
