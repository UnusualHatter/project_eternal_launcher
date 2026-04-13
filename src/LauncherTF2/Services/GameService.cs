using System.Diagnostics;
using System.Windows;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

public class GameService
{
    private readonly SettingsService _settingsService;

    public GameService()
    {
        _settingsService = new SettingsService();
    }

    public bool LaunchTF2()
    {
        try
        {
            Logger.LogInfo("Attempting to launch TF2");

            var settings = _settingsService.GetSettings();
            if (settings == null)
            {
                Logger.LogError("Settings are null, cannot launch game");
                return false;
            }

            var args = settings.LaunchArgs ?? "";

            // Validate Steam path
            if (string.IsNullOrWhiteSpace(settings.SteamPath) || !Directory.Exists(settings.SteamPath))
            {
                Logger.LogWarning($"Steam path is invalid or doesn't exist: {settings.SteamPath}");
                // Continue anyway, Steam URL launch might still work
            }

            // Start DLL Injection background monitoring
            try
            {
                InjectionService.Instance.StartMonitoring();
                Logger.LogInfo("Injection monitoring started");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to start injection monitoring", ex);
                // Continue anyway, injection is optional
            }

            // Launch the game via Steam
            var steamUrl = $"steam://rungameid/440//{args}";
            Logger.LogInfo($"Launching TF2 with URL: {steamUrl}");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamUrl,
                    UseShellExecute = true
                });
                Logger.LogInfo("TF2 launch command sent successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to launch TF2 via Steam", ex);
                return false;
            }

            // Minimize to Tray
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.Hide();
                        Logger.LogInfo("Launcher minimized to tray");
                    }
                    else
                    {
                        Logger.LogWarning("Main window is null, cannot minimize to tray");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to minimize to tray", ex);
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error launching TF2", ex);
            return false;
        }
    }

    public bool ValidateGameInstallation()
    {
        try
        {
            var settings = _settingsService.GetSettings();
            if (settings == null || string.IsNullOrWhiteSpace(settings.SteamPath))
            {
                Logger.LogWarning("Cannot validate game installation: settings or Steam path is null");
                return false;
            }

            var tf2Path = settings.SteamPath;
            
            if (!Directory.Exists(tf2Path))
            {
                Logger.LogWarning($"TF2 directory does not exist: {tf2Path}");
                return false;
            }

            var hl2Exe = Path.Combine(tf2Path, "hl2.exe");
            if (!File.Exists(hl2Exe))
            {
                Logger.LogWarning($"hl2.exe not found in: {tf2Path}");
                return false;
            }

            Logger.LogInfo("Game installation validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error validating game installation", ex);
            return false;
        }
    }

    public bool IsGameRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("hl2");
            var isRunning = processes.Length > 0;
            
            foreach (var proc in processes)
            {
                proc.Dispose();
            }

            Logger.LogDebug($"Game running check: {isRunning}");
            return isRunning;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error checking if game is running", ex);
            return false;
        }
    }
}
