using LauncherTF2.Models;
using LauncherTF2.Core;
using System.IO;
using System.Text.Json;

namespace LauncherTF2.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private SettingsModel _currentSettings = new();
    private readonly object _lock = new();

    public SettingsService()
    {
        _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        LoadSettings();
    }

    public SettingsModel GetSettings()
    {
        lock (_lock)
        {
            return _currentSettings;
        }
    }

    public bool SaveSettings(SettingsModel settings)
    {
        lock (_lock)
        {
            try
            {
                _currentSettings = settings ?? throw new ArgumentNullException(nameof(settings));
                return SaveSettingsCore();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save settings", ex);
                return false;
            }
        }
    }

    public bool ResetSettings()
    {
        lock (_lock)
        {
            try
            {
                _currentSettings = new SettingsModel();
                return SaveSettingsCore();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to reset settings", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Internal save — must be called while _lock is already held.
    /// Extracted to prevent the re-entrant lock anti-pattern.
    /// </summary>
    private bool SaveSettingsCore()
    {
        ValidateSettings(_currentSettings);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(_currentSettings, options);
        File.WriteAllText(_settingsPath, json);

        Logger.LogInfo("Settings saved successfully");

        if (!string.IsNullOrWhiteSpace(_currentSettings.SteamPath) && Directory.Exists(_currentSettings.SteamPath))
        {
            try
            {
                AutoexecWriter.WriteToAutoexec(_currentSettings, _currentSettings.SteamPath);
                Logger.LogDebug("Autoexec updated successfully");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to write autoexec file", ex);
            }
        }
        else
        {
            Logger.LogWarning($"Steam path not valid or doesn't exist: {_currentSettings.SteamPath}");
        }

        return true;
    }

    private void LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string fileContent = File.ReadAllText(_settingsPath);

                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        var loadedSettings = JsonSerializer.Deserialize<SettingsModel>(fileContent, options);

                        if (loadedSettings != null)
                        {
                            _currentSettings = loadedSettings;
                            ValidateSettings(_currentSettings);
                            Logger.LogInfo("Settings loaded successfully");
                        }
                        else
                        {
                            Logger.LogWarning("Deserialized settings were null, using defaults");
                            _currentSettings = new SettingsModel();
                        }
                    }
                    else
                    {
                        Logger.LogWarning("Settings file is empty, using defaults");
                        _currentSettings = new SettingsModel();
                    }
                }
                else
                {
                    Logger.LogInfo("Settings file not found, creating with defaults");
                    _currentSettings = new SettingsModel();
                    SaveSettingsCore();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load settings, using defaults", ex);
                _currentSettings = new SettingsModel();
            }
        }
    }

    private void ValidateSettings(SettingsModel settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SteamPath))
        {
            Logger.LogWarning("Steam path is empty, using default");
            settings.SteamPath = GamePaths.DefaultTf2Path;
        }

        // Clamp numeric ranges to valid bounds
        if (settings.Threads < 1) settings.Threads = 1;
        if (settings.Threads > 32) settings.Threads = 32;

        if (settings.Fov < 75) settings.Fov = 75;
        if (settings.Fov > 130) settings.Fov = 130;

        if (settings.ViewmodelFov < 50) settings.ViewmodelFov = 50;
        if (settings.ViewmodelFov > 90) settings.ViewmodelFov = 90;

        if (settings.MouseSensitivity < 0.1) settings.MouseSensitivity = 0.1;
        if (settings.MouseSensitivity > 30) settings.MouseSensitivity = 30;

        if (settings.Width < 640) settings.Width = 640;
        if (settings.Width > 7680) settings.Width = 7680;

        if (settings.Height < 480) settings.Height = 480;
        if (settings.Height > 4320) settings.Height = 4320;

        if (settings.Rate < 10000) settings.Rate = 10000;
        if (settings.Rate > 786432) settings.Rate = 786432;

        if (settings.CmdRate < 10) settings.CmdRate = 10;
        if (settings.CmdRate > 128) settings.CmdRate = 128;

        if (settings.UpdateRate < 10) settings.UpdateRate = 10;
        if (settings.UpdateRate > 128) settings.UpdateRate = 128;

        if (settings.Interp < 0) settings.Interp = 0;
        if (settings.Interp > 0.5) settings.Interp = 0.5;
    }

    public bool BackupSettings()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Logger.LogWarning("Settings file doesn't exist, nothing to backup");
                    return false;
                }

                var backupPath = Path.Combine(
                    Path.GetDirectoryName(_settingsPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    $"settings_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                );

                File.Copy(_settingsPath, backupPath);
                Logger.LogInfo($"Settings backed up to: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to backup settings", ex);
                return false;
            }
        }
    }

    public bool RestoreSettings(string backupPath)
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    Logger.LogError($"Backup file not found: {backupPath}");
                    return false;
                }

                var fileContent = File.ReadAllText(backupPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var restoredSettings = JsonSerializer.Deserialize<SettingsModel>(fileContent, options);

                if (restoredSettings != null)
                {
                    ValidateSettings(restoredSettings);
                    _currentSettings = restoredSettings;
                    SaveSettingsCore();
                    Logger.LogInfo($"Settings restored from: {backupPath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to restore settings from: {backupPath}", ex);
                return false;
            }
        }
    }

    // ─────────── Launcher-specific configuration (separate from TF2 settings) ───────────

    private static readonly string LauncherConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");

    public LauncherConfig GetLauncherConfig()
    {
        try
        {
            if (File.Exists(LauncherConfigPath))
            {
                var json = File.ReadAllText(LauncherConfigPath);
                return JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load launcher config", ex);
        }
        return new LauncherConfig();
    }

    public void SaveLauncherConfig(LauncherConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LauncherConfigPath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save launcher config", ex);
        }
    }
}
