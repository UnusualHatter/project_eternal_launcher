namespace LauncherTF2.Models;

/// <summary>
/// Launcher-specific configuration (log level, tray behavior, theming, etc.)
/// stored separately from TF2 game settings.
///
/// First-run defaults: MinimizeToTrayOnLaunch, CloseToTray, and ShowNotifications
/// are true so new installs feel polished out of the box. Existing users with a
/// launcher_config.json on disk keep whatever they set previously — JSON properties
/// that exist in the file override these C# defaults during deserialisation.
/// </summary>
public class LauncherConfig
{
    public bool EnableDebugLog { get; set; }
    public string? LogLevel { get; set; }
    public bool AutoClearLogs { get; set; }
    public bool MinimizeToTrayOnLaunch { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;

    // — Personalization —
    /// <summary>Theme ID picked in the Personalization section. Empty / unknown ID falls back to the default theme.</summary>
    public string? SelectedThemeId { get; set; }
}
