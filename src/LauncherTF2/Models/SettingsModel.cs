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

public class SettingsModel : ViewModelBase
{
    private string _steamPath = GamePaths.DefaultTf2Path;
    private string _launchArgs = "+exec w/config.cfg +exec autoexec.cfg";
    private string _steamApiKey = string.Empty;

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
    private bool _noTextureStream;
    private bool _disableReplay;

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

    // Uses SetProperty from ViewModelBase — eliminates duplicated INPC boilerplate
    public string SteamPath { get => _steamPath; set => SetProperty(ref _steamPath, value); }
    public string LaunchArgs { get => _launchArgs; set => SetProperty(ref _launchArgs, value); }
    public string SteamApiKey { get => _steamApiKey; set => SetProperty(ref _steamApiKey, value); }

    public bool SkipIntro { get => _skipIntro; set => SetProperty(ref _skipIntro, value); }
    public bool DisableJoystick { get => _disableJoystick; set => SetProperty(ref _disableJoystick, value); }
    public bool HighPriority { get => _highPriority; set => SetProperty(ref _highPriority, value); }
    public int Threads { get => _threads; set => SetProperty(ref _threads, value); }
    public int DxLevel { get => _dxLevel; set => SetProperty(ref _dxLevel, value); }

    public bool Fullscreen { get => _fullscreen; set => SetProperty(ref _fullscreen, value); }
    public bool Windowed { get => _windowed; set => SetProperty(ref _windowed, value); }
    public bool Borderless { get => _borderless; set => SetProperty(ref _borderless, value); }
    public int Width { get => _width; set => SetProperty(ref _width, value); }
    public int Height { get => _height; set => SetProperty(ref _height, value); }
    public int RefreshRate { get => _refreshRate; set => SetProperty(ref _refreshRate, value); }

    public bool DisableSound { get => _disableSound; set => SetProperty(ref _disableSound, value); }
    public bool DisableHltv { get => _disableHltv; set => SetProperty(ref _disableHltv, value); }
    public bool SoftParticlesOff { get => _softParticlesOff; set => SetProperty(ref _softParticlesOff, value); }
    public bool DisableSteamController { get => _disableSteamController; set => SetProperty(ref _disableSteamController, value); }
    public bool NoTextureStream { get => _noTextureStream; set => SetProperty(ref _noTextureStream, value); }
    public bool DisableReplay { get => _disableReplay; set => SetProperty(ref _disableReplay, value); }

    public bool VSync { get => _vSync; set => SetProperty(ref _vSync, value); }
    public int AntiAliasing { get => _antiAliasing; set => SetProperty(ref _antiAliasing, value); }
    public int AnisotropicFiltering { get => _anisotropicFiltering; set => SetProperty(ref _anisotropicFiltering, value); }
    public bool Bloom { get => _bloom; set => SetProperty(ref _bloom, value); }
    public double MotionBlurStrength { get => _motionBlurStrength; set => SetProperty(ref _motionBlurStrength, value); }
    public int ModelLod { get => _modelLod; set => SetProperty(ref _modelLod, value); }
    public bool Ragdolls { get => _ragdolls; set => SetProperty(ref _ragdolls, value); }
    public bool AlienGibs { get => _alienGibs; set => SetProperty(ref _alienGibs, value); }
    public bool HumanGibs { get => _humanGibs; set => SetProperty(ref _humanGibs, value); }
    public int DetailDistance { get => _detailDistance; set => SetProperty(ref _detailDistance, value); }

    public int Fov { get => _fov; set => SetProperty(ref _fov, value); }
    public int ViewmodelFov { get => _viewmodelFov; set => SetProperty(ref _viewmodelFov, value); }
    public bool DrawViewmodel { get => _drawViewmodel; set => SetProperty(ref _drawViewmodel, value); }
    public bool RawInput { get => _rawInput; set => SetProperty(ref _rawInput, value); }
    public double MouseSensitivity { get => _mouseSensitivity; set => SetProperty(ref _mouseSensitivity, value); }
    public bool AutoReload { get => _autoReload; set => SetProperty(ref _autoReload, value); }
    public bool HitSound { get => _hitSound; set => SetProperty(ref _hitSound, value); }
    public bool DamageNumbers { get => _damageNumbers; set => SetProperty(ref _damageNumbers, value); }
    public double KillstreakGlow { get => _killstreakGlow; set => SetProperty(ref _killstreakGlow, value); }

    public bool NetGraph { get => _netGraph; set => SetProperty(ref _netGraph, value); }
    public double ChatMessageTime { get => _chatMessageTime; set => SetProperty(ref _chatMessageTime, value); }
    public bool DrawHud { get => _drawHud; set => SetProperty(ref _drawHud, value); }

    public double Interp { get => _interp; set => SetProperty(ref _interp, value); }
    public bool Interpolate { get => _interpolate; set => SetProperty(ref _interpolate, value); }
    public int Rate { get => _rate; set => SetProperty(ref _rate, value); }
    public int CmdRate { get => _cmdRate; set => SetProperty(ref _cmdRate, value); }
    public int UpdateRate { get => _updateRate; set => SetProperty(ref _updateRate, value); }
    public int QueueMode { get => _queueMode; set => SetProperty(ref _queueMode, value); }
    public bool DisableEyes { get => _disableEyes; set => SetProperty(ref _disableEyes, value); }
    public bool DisableFlex { get => _disableFlex; set => SetProperty(ref _disableFlex, value); }

    private bool _showBackpackRarities = true;
    private bool _showIngameNotifications = true;
    private bool _showPluginMessages = true;
    private bool _showHelp = true;
    private bool _scoreboardPingText = true;
    private int _spectatorMode = 4;
    private bool _newImpactEffects = true;
    private bool _drawTracersFirstPerson = true;
    private bool _colorblindAssist;

    public bool ShowBackpackRarities { get => _showBackpackRarities; set => SetProperty(ref _showBackpackRarities, value); }
    public bool ShowIngameNotifications { get => _showIngameNotifications; set => SetProperty(ref _showIngameNotifications, value); }
    public bool ShowPluginMessages { get => _showPluginMessages; set => SetProperty(ref _showPluginMessages, value); }
    public bool ShowHelp { get => _showHelp; set => SetProperty(ref _showHelp, value); }
    public bool ScoreboardPingText { get => _scoreboardPingText; set => SetProperty(ref _scoreboardPingText, value); }
    public int SpectatorMode { get => _spectatorMode; set => SetProperty(ref _spectatorMode, value); }
    public bool NewImpactEffects { get => _newImpactEffects; set => SetProperty(ref _newImpactEffects, value); }
    public bool DrawTracersFirstPerson { get => _drawTracersFirstPerson; set => SetProperty(ref _drawTracersFirstPerson, value); }
    public bool ColorblindAssist { get => _colorblindAssist; set => SetProperty(ref _colorblindAssist, value); }

    private ObservableCollection<BindModel> _binds = new();
    public ObservableCollection<BindModel> Binds
    {
        get => _binds;
        set => SetProperty(ref _binds, value);
    }
}
