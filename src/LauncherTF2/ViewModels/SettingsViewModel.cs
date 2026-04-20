using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
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

    public SettingsModel CurrentSettings
    {
        get => _currentSettings;
        set => SetProperty(ref _currentSettings, value);
    }

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
            {
                SaveLauncherSettings();
            }
        }
    }

    public bool MinimizeToTrayOnLaunch
    {
        get => _minimizeToTrayOnLaunch;
        set
        {
            if (SetProperty(ref _minimizeToTrayOnLaunch, value))
            {
                SaveLauncherSettings();
            }
        }
    }

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (SetProperty(ref _closeToTray, value))
            {
                SaveLauncherSettings();
            }
        }
    }

    public bool ShowNotifications
    {
        get => _showNotifications;
        set
        {
            if (SetProperty(ref _showNotifications, value))
            {
                SaveLauncherSettings();
            }
        }
    }

    public string[] LogLevels { get; } = { "Debug", "Info", "Warning", "Error" };

    public ICommand ResetCommand { get; }
    public ICommand AddBindCommand { get; }
    public ICommand RemoveBindCommand { get; }
    public ICommand StartListeningCommand { get; }

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

        _currentSettings.PropertyChanged += CurrentSettings_PropertyChanged;
        _currentSettings.Binds.CollectionChanged += (s, e) => _settingsService.SaveSettings(_currentSettings);

        ResetCommand = new RelayCommand(o => Reset());
        AddBindCommand = new RelayCommand(o => AddBind());
        RemoveBindCommand = new RelayCommand(o => RemoveBind(o));
        StartListeningCommand = new RelayCommand(o => StartListening(o));
    }

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

    private bool _isHandlingConflict;

    private void CurrentSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isHandlingConflict) return;

        if (e.PropertyName != nameof(SettingsModel.LaunchArgs))
        {
            try
            {
                _isHandlingConflict = true;
                HandleWindowModeConflicts(e.PropertyName);

                SyncLaunchOptions();
            }
            finally
            {
                _isHandlingConflict = false;
            }
        }
        _settingsService.SaveSettings(_currentSettings);
    }

    private void HandleWindowModeConflicts(string? changedProperty)
    {
        if (changedProperty == nameof(SettingsModel.Fullscreen) && _currentSettings.Fullscreen)
        {
            _currentSettings.Windowed = false;
            _currentSettings.Borderless = false;
        }
        else if ((changedProperty == nameof(SettingsModel.Windowed) && _currentSettings.Windowed) ||
                 (changedProperty == nameof(SettingsModel.Borderless) && _currentSettings.Borderless))
        {
            _currentSettings.Fullscreen = false;
        }
    }

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

        void SetValue(string flag, string value)
        {
            int index = parts.IndexOf(flag);
            if (index != -1)
            {
                if (index + 1 < parts.Count)
                {
                    parts[index + 1] = value;
                }
                else
                {
                    parts.Add(value);
                }
            }
            else
            {
                parts.Add(flag);
                parts.Add(value);
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

        SetValue("-threads", _currentSettings.Threads.ToString());
        SetValue("-dxlevel", _currentSettings.DxLevel.ToString());
        SetValue("-w", _currentSettings.Width.ToString());
        SetValue("-h", _currentSettings.Height.ToString());
        SetValue("-freq", _currentSettings.RefreshRate.ToString());

        _currentSettings.LaunchArgs = string.Join(" ", parts);
    }

    private void Reset()
    {
        _currentSettings.PropertyChanged -= CurrentSettings_PropertyChanged;

        _settingsService.ResetSettings();
        CurrentSettings = _settingsService.GetSettings();

        SyncLaunchOptions();

        _currentSettings.PropertyChanged += CurrentSettings_PropertyChanged;
        _currentSettings.Binds.CollectionChanged += (s, e) => _settingsService.SaveSettings(_currentSettings);
    }
}
