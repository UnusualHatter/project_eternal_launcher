namespace LauncherTF2.Models;

public class SettingsModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _steamPath = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf";
    private string _launchArgs = "+exec w/config.cfg +exec autoexec.cfg";

    private bool _skipIntro;
    private bool _disableJoystick;
    private bool _highPriority;
    private int _threads = 4;
    private int _dxLevel = 95;

    private bool _fullscreen = true;
    private bool _windowed;
    private bool _borderless;
    private int _width = 1920;
    private int _height = 1080;
    private int _refreshRate = 60;

    private bool _disableSound;
    private bool _disableHltv;
    private bool _softParticlesOff;
    private bool _disableSteamController;

    private bool _vSync;
    private int _antiAliasing = 8;
    private int _anisotropicFiltering = 16;
    private bool _bloom = true;
    private double _motionBlurStrength = 0;
    private int _modelLod = 0;
    private bool _ragdolls = true;
    private bool _alienGibs = true;
    private bool _humanGibs = true;
    private int _detailDistance = 1200;

    private int _fov = 90;
    private int _viewmodelFov = 70;
    private bool _drawViewmodel = true;
    private bool _rawInput = true;
    private double _mouseSensitivity = 3.0;
    private bool _autoReload = true;
    private bool _hitSound = true;
    private bool _damageNumbers = true;
    private double _killstreakGlow = 1.0;

    private bool _netGraph;
    private double _chatMessageTime = 12.0;
    private bool _drawHud = true;

    private double _interp = 0.0152;
    private bool _interpolate = true;
    private int _rate = 196608;
    private int _cmdRate = 66;
    private int _updateRate = 66;
    private int _queueMode = -1;
    private bool _disableEyes;
    private bool _disableFlex;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    public string SteamPath { get => _steamPath; set { if (_steamPath != value) { _steamPath = value; OnPropertyChanged(); } } }
    public string LaunchArgs { get => _launchArgs; set { if (_launchArgs != value) { _launchArgs = value; OnPropertyChanged(); } } }

    public bool SkipIntro { get => _skipIntro; set { if (_skipIntro != value) { _skipIntro = value; OnPropertyChanged(); } } }
    public bool DisableJoystick { get => _disableJoystick; set { if (_disableJoystick != value) { _disableJoystick = value; OnPropertyChanged(); } } }
    public bool HighPriority { get => _highPriority; set { if (_highPriority != value) { _highPriority = value; OnPropertyChanged(); } } }
    public int Threads { get => _threads; set { if (_threads != value) { _threads = value; OnPropertyChanged(); } } }
    public int DxLevel { get => _dxLevel; set { if (_dxLevel != value) { _dxLevel = value; OnPropertyChanged(); } } }

    public bool Fullscreen { get => _fullscreen; set { if (_fullscreen != value) { _fullscreen = value; OnPropertyChanged(); } } }
    public bool Windowed { get => _windowed; set { if (_windowed != value) { _windowed = value; OnPropertyChanged(); } } }
    public bool Borderless { get => _borderless; set { if (_borderless != value) { _borderless = value; OnPropertyChanged(); } } }
    public int Width { get => _width; set { if (_width != value) { _width = value; OnPropertyChanged(); } } }
    public int Height { get => _height; set { if (_height != value) { _height = value; OnPropertyChanged(); } } }
    public int RefreshRate { get => _refreshRate; set { if (_refreshRate != value) { _refreshRate = value; OnPropertyChanged(); } } }

    public bool DisableSound { get => _disableSound; set { if (_disableSound != value) { _disableSound = value; OnPropertyChanged(); } } }
    public bool DisableHltv { get => _disableHltv; set { if (_disableHltv != value) { _disableHltv = value; OnPropertyChanged(); } } }
    public bool SoftParticlesOff { get => _softParticlesOff; set { if (_softParticlesOff != value) { _softParticlesOff = value; OnPropertyChanged(); } } }
    public bool DisableSteamController { get => _disableSteamController; set { if (_disableSteamController != value) { _disableSteamController = value; OnPropertyChanged(); } } }

    public bool VSync { get => _vSync; set { if (_vSync != value) { _vSync = value; OnPropertyChanged(); } } }
    public int AntiAliasing { get => _antiAliasing; set { if (_antiAliasing != value) { _antiAliasing = value; OnPropertyChanged(); } } }
    public int AnisotropicFiltering { get => _anisotropicFiltering; set { if (_anisotropicFiltering != value) { _anisotropicFiltering = value; OnPropertyChanged(); } } }
    public bool Bloom { get => _bloom; set { if (_bloom != value) { _bloom = value; OnPropertyChanged(); } } }
    public double MotionBlurStrength { get => _motionBlurStrength; set { if (_motionBlurStrength != value) { _motionBlurStrength = value; OnPropertyChanged(); } } }
    public int ModelLod { get => _modelLod; set { if (_modelLod != value) { _modelLod = value; OnPropertyChanged(); } } }
    public bool Ragdolls { get => _ragdolls; set { if (_ragdolls != value) { _ragdolls = value; OnPropertyChanged(); } } }
    public bool AlienGibs { get => _alienGibs; set { if (_alienGibs != value) { _alienGibs = value; OnPropertyChanged(); } } }
    public bool HumanGibs { get => _humanGibs; set { if (_humanGibs != value) { _humanGibs = value; OnPropertyChanged(); } } }
    public int DetailDistance { get => _detailDistance; set { if (_detailDistance != value) { _detailDistance = value; OnPropertyChanged(); } } }

    public int Fov { get => _fov; set { if (_fov != value) { _fov = value; OnPropertyChanged(); } } }
    public int ViewmodelFov { get => _viewmodelFov; set { if (_viewmodelFov != value) { _viewmodelFov = value; OnPropertyChanged(); } } }
    public bool DrawViewmodel { get => _drawViewmodel; set { if (_drawViewmodel != value) { _drawViewmodel = value; OnPropertyChanged(); } } }
    public bool RawInput { get => _rawInput; set { if (_rawInput != value) { _rawInput = value; OnPropertyChanged(); } } }
    public double MouseSensitivity { get => _mouseSensitivity; set { if (_mouseSensitivity != value) { _mouseSensitivity = value; OnPropertyChanged(); } } }
    public bool AutoReload { get => _autoReload; set { if (_autoReload != value) { _autoReload = value; OnPropertyChanged(); } } }
    public bool HitSound { get => _hitSound; set { if (_hitSound != value) { _hitSound = value; OnPropertyChanged(); } } }
    public bool DamageNumbers { get => _damageNumbers; set { if (_damageNumbers != value) { _damageNumbers = value; OnPropertyChanged(); } } }
    public double KillstreakGlow { get => _killstreakGlow; set { if (_killstreakGlow != value) { _killstreakGlow = value; OnPropertyChanged(); } } }

    public bool NetGraph { get => _netGraph; set { if (_netGraph != value) { _netGraph = value; OnPropertyChanged(); } } }
    public double ChatMessageTime { get => _chatMessageTime; set { if (_chatMessageTime != value) { _chatMessageTime = value; OnPropertyChanged(); } } }
    public bool DrawHud { get => _drawHud; set { if (_drawHud != value) { _drawHud = value; OnPropertyChanged(); } } }

    public double Interp { get => _interp; set { if (_interp != value) { _interp = value; OnPropertyChanged(); } } }
    public bool Interpolate { get => _interpolate; set { if (_interpolate != value) { _interpolate = value; OnPropertyChanged(); } } }
    public int Rate { get => _rate; set { if (_rate != value) { _rate = value; OnPropertyChanged(); } } }
    public int CmdRate { get => _cmdRate; set { if (_cmdRate != value) { _cmdRate = value; OnPropertyChanged(); } } }
    public int UpdateRate { get => _updateRate; set { if (_updateRate != value) { _updateRate = value; OnPropertyChanged(); } } }
    public int QueueMode { get => _queueMode; set { if (_queueMode != value) { _queueMode = value; OnPropertyChanged(); } } }
    public bool DisableEyes { get => _disableEyes; set { if (_disableEyes != value) { _disableEyes = value; OnPropertyChanged(); } } }
    public bool DisableFlex { get => _disableFlex; set { if (_disableFlex != value) { _disableFlex = value; OnPropertyChanged(); } } }

    private bool _showBackpackRarities = true;
    private bool _showIngameNotifications = true;
    private bool _showPluginMessages = true;
    private bool _showHelp = true;
    private bool _scoreboardPingText = true;
    private int _spectatorMode = 4;
    private bool _newImpactEffects = true;
    private bool _drawTracersFirstPerson = true;
    private bool _colorblindAssist;

    public bool ShowBackpackRarities { get => _showBackpackRarities; set { if (_showBackpackRarities != value) { _showBackpackRarities = value; OnPropertyChanged(); } } }
    public bool ShowIngameNotifications { get => _showIngameNotifications; set { if (_showIngameNotifications != value) { _showIngameNotifications = value; OnPropertyChanged(); } } }
    public bool ShowPluginMessages { get => _showPluginMessages; set { if (_showPluginMessages != value) { _showPluginMessages = value; OnPropertyChanged(); } } }
    public bool ShowHelp { get => _showHelp; set { if (_showHelp != value) { _showHelp = value; OnPropertyChanged(); } } }
    public bool ScoreboardPingText { get => _scoreboardPingText; set { if (_scoreboardPingText != value) { _scoreboardPingText = value; OnPropertyChanged(); } } }
    public int SpectatorMode { get => _spectatorMode; set { if (_spectatorMode != value) { _spectatorMode = value; OnPropertyChanged(); } } }
    public bool NewImpactEffects { get => _newImpactEffects; set { if (_newImpactEffects != value) { _newImpactEffects = value; OnPropertyChanged(); } } }
    public bool DrawTracersFirstPerson { get => _drawTracersFirstPerson; set { if (_drawTracersFirstPerson != value) { _drawTracersFirstPerson = value; OnPropertyChanged(); } } }
    public bool ColorblindAssist { get => _colorblindAssist; set { if (_colorblindAssist != value) { _colorblindAssist = value; OnPropertyChanged(); } } }

    private System.Collections.ObjectModel.ObservableCollection<BindModel> _binds = new();
    public System.Collections.ObjectModel.ObservableCollection<BindModel> Binds
    {
        get => _binds;
        set
        {
            if (_binds != value)
            {
                _binds = value;
                OnPropertyChanged();
            }
        }
    }
}
