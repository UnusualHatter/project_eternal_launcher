namespace LauncherTF2.Models;

/// <summary>
/// Launcher-specific configuration (log level, tray behavior, etc.)
/// stored separately from TF2 game settings.
/// </summary>
public class LauncherConfig
{
    public bool EnableDebugLog { get; set; }
    public string? LogLevel { get; set; }
    public bool AutoClearLogs { get; set; }
    public bool MinimizeToTrayOnLaunch { get; set; }
    public bool CloseToTray { get; set; }
    public bool ShowNotifications { get; set; }
}
