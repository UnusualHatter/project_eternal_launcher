using LauncherTF2.Core;
using System.Collections.ObjectModel;

namespace LauncherTF2.Models;

/// <summary>
/// Default Steam TF2 directory used as fallback across the application.
/// </summary>
public static class GamePaths
{
    public const string DefaultTf2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf";
}

/// <summary>
/// Persistent TF2 settings written to <c>settings.json</c> and applied via
/// Steam launch options (LaunchArgs) and an auto-generated autoexec.cfg.
///
/// Removed in cleanup: -nosound, -nohltv, -no_steam_controller (dangerous/niche),
/// r_eyes / r_flex / cl_drawhud (game-breaking), KillstreakGlow / AlienGibs /
/// HumanGibs / NetGraph / ChatMessageTime / SpectatorMode and the Show* HUD
/// flags (clutter, niche, no useful effect for most users).
/// </summary>
public class SettingsModel : ViewModelBase
{
    private string _steamPath = GamePaths.DefaultTf2Path;
    private string _launchArgs = "+exec autoexec.cfg";

    private bool _skipIntro = true;
    private bool _disableJoystick;
    private bool _highPriority;
    private bool _noTextureStream;
    private bool _disableReplay;
    private bool _softParticlesOff;
    private int _dxLevel = 95;

    private bool _fullscreen = true;
    private bool _windowed;
    private bool _borderless;
    private int _width = 1920;
    private int _height = 1080;
    private int _refreshRate = 60;

    private bool _vSync;
    private int _antiAliasing = 8;
    private int _anisotropicFiltering = 16;
    private bool _bloom = true;
    private double _motionBlurStrength = 0;
    private int _modelLod = 0;
    private bool _ragdolls = true;
    private int _detailDistance = 1200;

    private int _fov = 90;
    private int _viewmodelFov = 70;
    private bool _drawViewmodel = true;
    private bool _rawInput = true;
    private double _mouseSensitivity = 3.0;
    private bool _autoReload = true;
    private bool _hitSound = true;
    private bool _damageNumbers = true;

    private double _interp = 0.0152;
    private bool _interpolate = true;
    private int _rate = 196608;
    private int _cmdRate = 66;
    private int _updateRate = 66;
    private int _queueMode = 2;

    public string SteamPath { get => _steamPath; set => SetProperty(ref _steamPath, value); }
    public string LaunchArgs { get => _launchArgs; set => SetProperty(ref _launchArgs, value); }

    public bool SkipIntro { get => _skipIntro; set => SetProperty(ref _skipIntro, value); }
    public bool DisableJoystick { get => _disableJoystick; set => SetProperty(ref _disableJoystick, value); }
    public bool HighPriority { get => _highPriority; set => SetProperty(ref _highPriority, value); }
    public bool NoTextureStream { get => _noTextureStream; set => SetProperty(ref _noTextureStream, value); }
    public bool DisableReplay { get => _disableReplay; set => SetProperty(ref _disableReplay, value); }
    public bool SoftParticlesOff { get => _softParticlesOff; set => SetProperty(ref _softParticlesOff, value); }
    public int DxLevel { get => _dxLevel; set => SetProperty(ref _dxLevel, value); }

    public bool Fullscreen { get => _fullscreen; set => SetProperty(ref _fullscreen, value); }
    public bool Windowed { get => _windowed; set => SetProperty(ref _windowed, value); }
    public bool Borderless { get => _borderless; set => SetProperty(ref _borderless, value); }
    public int Width { get => _width; set => SetProperty(ref _width, value); }
    public int Height { get => _height; set => SetProperty(ref _height, value); }
    public int RefreshRate { get => _refreshRate; set => SetProperty(ref _refreshRate, value); }

    public bool VSync { get => _vSync; set => SetProperty(ref _vSync, value); }
    public int AntiAliasing { get => _antiAliasing; set => SetProperty(ref _antiAliasing, value); }
    public int AnisotropicFiltering { get => _anisotropicFiltering; set => SetProperty(ref _anisotropicFiltering, value); }
    public bool Bloom { get => _bloom; set => SetProperty(ref _bloom, value); }
    public double MotionBlurStrength { get => _motionBlurStrength; set => SetProperty(ref _motionBlurStrength, value); }
    public int ModelLod { get => _modelLod; set => SetProperty(ref _modelLod, value); }
    public bool Ragdolls { get => _ragdolls; set => SetProperty(ref _ragdolls, value); }
    public int DetailDistance { get => _detailDistance; set => SetProperty(ref _detailDistance, value); }

    public int Fov { get => _fov; set => SetProperty(ref _fov, value); }
    public int ViewmodelFov { get => _viewmodelFov; set => SetProperty(ref _viewmodelFov, value); }
    public bool DrawViewmodel { get => _drawViewmodel; set => SetProperty(ref _drawViewmodel, value); }
    public bool RawInput { get => _rawInput; set => SetProperty(ref _rawInput, value); }
    public double MouseSensitivity { get => _mouseSensitivity; set => SetProperty(ref _mouseSensitivity, value); }
    public bool AutoReload { get => _autoReload; set => SetProperty(ref _autoReload, value); }
    public bool HitSound { get => _hitSound; set => SetProperty(ref _hitSound, value); }
    public bool DamageNumbers { get => _damageNumbers; set => SetProperty(ref _damageNumbers, value); }

    public double Interp { get => _interp; set => SetProperty(ref _interp, value); }
    public bool Interpolate { get => _interpolate; set => SetProperty(ref _interpolate, value); }
    public int Rate { get => _rate; set => SetProperty(ref _rate, value); }
    public int CmdRate { get => _cmdRate; set => SetProperty(ref _cmdRate, value); }
    public int UpdateRate { get => _updateRate; set => SetProperty(ref _updateRate, value); }
    public int QueueMode { get => _queueMode; set => SetProperty(ref _queueMode, value); }

    private ObservableCollection<BindModel> _binds = new();
    public ObservableCollection<BindModel> Binds
    {
        get => _binds;
        set => SetProperty(ref _binds, value);
    }
}
