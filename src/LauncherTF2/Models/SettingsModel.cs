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
    private int _dxLevel = 90;

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
    private int _interpRatio = 2;
    private bool _smoothEnabled = true;
    private int _predOptimize = 2;
    private string? _networkPreset = "Casual";

    // ─── Gameplay extensions (new, default to current TF2 defaults) ───
    private bool _hudFastSwitch = true;
    private bool _minViewmodels;
    private bool _killSound = true;
    private bool _autoRezoom = true;
    private bool _closedCaptions;
    private int _customAccel; // 0 = off

    // ─── Competitive ───
    private bool _medicAutocall = true;
    private int _medicAutocallThreshold = 75;
    private int _fpsMax = 300;
    private bool _nullMovement;
    private bool _transparentViewmodels;

    // ─── Performance toggles + preset id ───
    private bool _removeGibs;
    private bool _removeSprays = true;
    private bool _disableJiggleBones;
    private bool _disableFacialAnims;
    private bool _disableDynamicLights;
    private bool _disablePyroland;
    private bool _disableDecals;
    private string? _performancePreset = "Balanced";

    // ─── FPS cap + visual quality (Performance) ───
    private int _fpsMaxMenu = 60;
    private bool _matSpecular = true;
    private bool _matPhong = true;
    private bool _matBumpmap = true;

    // ─── Viewmodel extras ───
    private bool _flipViewmodels;

    // ─── Viewmodel offsets ───
    private double _viewmodelOffsetX;
    private double _viewmodelOffsetY;
    private double _viewmodelOffsetZ;
    private bool _viewmodelBobOff;
    private bool _viewmodelSwayOff;

    // ─── Audio ───
    private double _hitsoundVolume = 0.7;
    private double _voiceScale = 1.0;
    private int _hitsoundPitchMin = 100;
    private int _hitsoundPitchMax = 100;
    private bool _asyncSound = true;

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
    public int InterpRatio { get => _interpRatio; set => SetProperty(ref _interpRatio, value); }
    public bool SmoothEnabled { get => _smoothEnabled; set => SetProperty(ref _smoothEnabled, value); }
    public int PredOptimize { get => _predOptimize; set => SetProperty(ref _predOptimize, value); }
    public string? NetworkPreset { get => _networkPreset; set => SetProperty(ref _networkPreset, value); }

    // ─── Gameplay extensions ───
    public bool HudFastSwitch { get => _hudFastSwitch; set => SetProperty(ref _hudFastSwitch, value); }
    public bool MinViewmodels { get => _minViewmodels; set => SetProperty(ref _minViewmodels, value); }
    public bool KillSound { get => _killSound; set => SetProperty(ref _killSound, value); }
    public bool AutoRezoom { get => _autoRezoom; set => SetProperty(ref _autoRezoom, value); }
    public bool ClosedCaptions { get => _closedCaptions; set => SetProperty(ref _closedCaptions, value); }
    public int CustomAccel { get => _customAccel; set => SetProperty(ref _customAccel, value); }

    // ─── Competitive ───
    public bool MedicAutocall { get => _medicAutocall; set => SetProperty(ref _medicAutocall, value); }
    public int MedicAutocallThreshold { get => _medicAutocallThreshold; set => SetProperty(ref _medicAutocallThreshold, value); }
    public int FpsMax { get => _fpsMax; set => SetProperty(ref _fpsMax, value); }
    public bool NullMovement { get => _nullMovement; set => SetProperty(ref _nullMovement, value); }
    public bool TransparentViewmodels { get => _transparentViewmodels; set => SetProperty(ref _transparentViewmodels, value); }

    // ─── Performance ───
    public bool RemoveGibs { get => _removeGibs; set => SetProperty(ref _removeGibs, value); }
    public bool RemoveSprays { get => _removeSprays; set => SetProperty(ref _removeSprays, value); }
    public bool DisableJiggleBones { get => _disableJiggleBones; set => SetProperty(ref _disableJiggleBones, value); }
    public bool DisableFacialAnims { get => _disableFacialAnims; set => SetProperty(ref _disableFacialAnims, value); }
    public bool DisableDynamicLights { get => _disableDynamicLights; set => SetProperty(ref _disableDynamicLights, value); }
    public bool DisablePyroland { get => _disablePyroland; set => SetProperty(ref _disablePyroland, value); }
    public bool DisableDecals { get => _disableDecals; set => SetProperty(ref _disableDecals, value); }
    public string? PerformancePreset { get => _performancePreset; set => SetProperty(ref _performancePreset, value); }
    public int FpsMaxMenu { get => _fpsMaxMenu; set => SetProperty(ref _fpsMaxMenu, value); }
    public bool MatSpecular { get => _matSpecular; set => SetProperty(ref _matSpecular, value); }
    public bool MatPhong { get => _matPhong; set => SetProperty(ref _matPhong, value); }
    public bool MatBumpmap { get => _matBumpmap; set => SetProperty(ref _matBumpmap, value); }
    public bool FlipViewmodels { get => _flipViewmodels; set => SetProperty(ref _flipViewmodels, value); }

    // ─── Viewmodel offsets ───
    public double ViewmodelOffsetX { get => _viewmodelOffsetX; set => SetProperty(ref _viewmodelOffsetX, value); }
    public double ViewmodelOffsetY { get => _viewmodelOffsetY; set => SetProperty(ref _viewmodelOffsetY, value); }
    public double ViewmodelOffsetZ { get => _viewmodelOffsetZ; set => SetProperty(ref _viewmodelOffsetZ, value); }
    public bool ViewmodelBobOff { get => _viewmodelBobOff; set => SetProperty(ref _viewmodelBobOff, value); }
    public bool ViewmodelSwayOff { get => _viewmodelSwayOff; set => SetProperty(ref _viewmodelSwayOff, value); }

    // ─── Audio ───
    public double HitsoundVolume { get => _hitsoundVolume; set => SetProperty(ref _hitsoundVolume, value); }
    public double VoiceScale { get => _voiceScale; set => SetProperty(ref _voiceScale, value); }
    public int HitsoundPitchMin { get => _hitsoundPitchMin; set => SetProperty(ref _hitsoundPitchMin, value); }
    public int HitsoundPitchMax { get => _hitsoundPitchMax; set => SetProperty(ref _hitsoundPitchMax, value); }
    public bool AsyncSound { get => _asyncSound; set => SetProperty(ref _asyncSound, value); }

    private ObservableCollection<BindModel> _binds = new();
    public ObservableCollection<BindModel> Binds
    {
        get => _binds;
        set => SetProperty(ref _binds, value);
    }
}
