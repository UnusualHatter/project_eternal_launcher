using System.Windows.Input;
using LauncherTF2.Core;
using LauncherTF2.Services;

namespace LauncherTF2.ViewModels;

public class RpcViewModel : ViewModelBase
{
    private readonly Tf2RichPresenceService _service;

    private string _currentMap = "Main Menu";
    private string _queueStatus = "Idle";
    private bool _isRpcActive;
    private bool _isGameRunning;

    // Settings Bindings
    private bool _autoStartRpc;
    private bool _autoStartWhenGameDetected;
    private bool _pauseWhenGameCloses;

    public string CurrentMap
    {
        get => _currentMap;
        set => SetProperty(ref _currentMap, value);
    }

    public string QueueStatus
    {
        get => _queueStatus;
        set => SetProperty(ref _queueStatus, value);
    }

    public bool IsRpcActive
    {
        get => _isRpcActive;
        set
        {
            if (SetProperty(ref _isRpcActive, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public bool IsGameRunning
    {
        get => _isGameRunning;
        set => SetProperty(ref _isGameRunning, value);
    }

    public bool AutoStartRpc
    {
        get => _autoStartRpc;
        set
        {
            if (SetProperty(ref _autoStartRpc, value))
                _service.AutoStartRpc = value;
        }
    }

    public bool AutoStartWhenGameDetected
    {
        get => _autoStartWhenGameDetected;
        set
        {
            if (SetProperty(ref _autoStartWhenGameDetected, value))
                _service.AutoStartWhenGameDetected = value;
        }
    }

    public bool PauseWhenGameCloses
    {
        get => _pauseWhenGameCloses;
        set
        {
            if (SetProperty(ref _pauseWhenGameCloses, value))
                _service.PauseWhenGameCloses = value;
        }
    }

    public string StatusText => IsRpcActive ? "RPC ACTIVE" : "RPC INACTIVE";
    public string StatusColor => IsRpcActive ? "#00FF00" : "#FF0000"; // Green/Red hex

    public ICommand StartRpcCommand { get; }
    public ICommand StopRpcCommand { get; }

    public RpcViewModel()
    {
        _service = Tf2RichPresenceService.Instance;

        // Defaults requested by user
        _autoStartRpc = true;
        _autoStartWhenGameDetected = true;

        // Sync defaults to service
        _service.AutoStartRpc = _autoStartRpc;
        _service.AutoStartWhenGameDetected = _autoStartWhenGameDetected;

        PauseWhenGameCloses = _service.PauseWhenGameCloses;

        _service.StatusUpdated += Service_StatusUpdated;
        _service.RpcStateChanged += Service_RpcStateChanged;
        QueueStatus = _service.QueueStatus;

        StartRpcCommand = new RelayCommand(o =>
        {
            // Always Fetch latest settings path when starting
            var settings = new SettingsService().GetSettings();
            if (!string.IsNullOrEmpty(settings.SteamPath))
            {
                _service.Tf2Path = settings.SteamPath;
                _service.Start();
            }
        }, o => !IsRpcActive);

        StopRpcCommand = new RelayCommand(o => _service.Stop(), o => IsRpcActive);

        // Initial path set
        var initialSettings = new SettingsService().GetSettings();
        if (!string.IsNullOrEmpty(initialSettings.SteamPath))
        {
            _service.Tf2Path = initialSettings.SteamPath;
            
            // Auto start if enabled
            if (_autoStartRpc)
            {
                _service.Start();
            }
        }
    }

    private void Service_RpcStateChanged(bool active)
    {
        IsRpcActive = active;
        // Force command re-evaluation
        CommandManager.InvalidateRequerySuggested();
    }

    private void Service_StatusUpdated(string status)
    {
        CurrentMap = _service.CurrentMap;
        QueueStatus = _service.QueueStatus;
    }
}
