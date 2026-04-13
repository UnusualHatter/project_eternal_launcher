using LauncherTF2.Core;

namespace LauncherTF2.Models;

public class ModModel : ViewModelBase
{
    private string _name = string.Empty;
    private string _author = "Unknown";
    private string _description = string.Empty;
    private string _version = "1.0.0";
    private bool _isEnabled;
    private string _thumbnailPath = "/Resources/Assets/logo.png";
    private string _modPath = string.Empty;
    private DateTime _lastModified;
    private ModType _modType = ModType.Unknown;
    private string? _loadError;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Author
    {
        get => _author;
        set => SetProperty(ref _author, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string ThumbnailPath
    {
        get => _thumbnailPath;
        set => SetProperty(ref _thumbnailPath, value);
    }

    public string ModPath
    {
        get => _modPath;
        set => SetProperty(ref _modPath, value);
    }

    public DateTime LastModified
    {
        get => _lastModified;
        set => SetProperty(ref _lastModified, value);
    }

    public ModType ModType
    {
        get => _modType;
        set => SetProperty(ref _modType, value);
    }

    public string? LoadError
    {
        get => _loadError;
        set => SetProperty(ref _loadError, value);
    }

    public bool HasError => !string.IsNullOrEmpty(_loadError);
}

public enum ModType
{
    Unknown,
    Vpk,
    Folder,
    Plugin,
    Custom
}
