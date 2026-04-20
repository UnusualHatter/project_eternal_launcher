using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

/// <summary>
/// Orchestrates TF2 game launch: runs the Steam patcher, starts TF2 via the
/// Steam protocol, waits for the process, and triggers pure_patcher when ready.
/// </summary>
public class GameService
{
    private readonly SettingsService _settingsService;
    private int _launchOrchestrationInProgress;
    private const string PurePatcherGateKey = "pure_patcher_session";

    public GameService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <summary>
    /// Entry point for launching TF2. Starts the full orchestration pipeline
    /// in the background and minimizes the launcher to the system tray.
    /// </summary>
    public bool LaunchTF2()
    {
        try
        {
            Logger.LogInfo("[Game] Launch requested");

            // Prevent multiple simultaneous launch attempts
            if (Interlocked.CompareExchange(ref _launchOrchestrationInProgress, 1, 0) != 0)
            {
                Logger.LogWarning("[Game] Launch ignored — previous orchestration still running");
                return true;
            }

            var settings = _settingsService.GetSettings();
            if (settings == null)
            {
                Logger.LogError("[Game] Cannot launch — settings are null");
                return false;
            }

            var finalArgs = (settings.LaunchArgs ?? string.Empty).Trim();

            if (!IsSteamPathValid(settings.SteamPath))
                Logger.LogWarning($"[Game] Steam path looks invalid: {settings.SteamPath}");

            // Allow pure_patcher to run exactly once this session
            NativeExecutableService.ResetSingleFlight(PurePatcherGateKey);

            // The orchestration runs entirely in the background so the UI stays responsive
            _ = Task.Run(async () =>
            {
                try
                {
                    await OrchestrateSteamPatcherAndLaunch(finalArgs);
                }
                finally
                {
                    Interlocked.Exchange(ref _launchOrchestrationInProgress, 0);
                }
            });

            // Hide the launcher while the game is running
            MinimizeToTray();

            return true;
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _launchOrchestrationInProgress, 0);
            Logger.LogError("[Game] Unexpected error during launch", ex);
            return false;
        }
    }

    /// <summary>
    /// Full orchestration pipeline:
    ///   1. Run steam_patcher.exe (patches Steam client)
    ///   2. Wait for Steam to restart (if it does)
    ///   3. Launch TF2 via steam:// protocol
    ///   4. Wait for tf_win64 process and trigger pure_patcher
    /// </summary>
    private async Task OrchestrateSteamPatcherAndLaunch(string finalArgs)
    {
        try
        {
            // Step 1: Run the Steam patcher
            Logger.LogInfo("[Game] Starting steam_patcher.exe");
            _ = NativeExecutableService.TryStartExecutable("steam_patcher.exe", "Game launch orchestration");

            bool steamWasRunning = IsProcessRunning("steam");

            if (steamWasRunning)
            {
                // The patcher may restart Steam — wait for it to close
                Logger.LogDebug("[Game] Steam is running — waiting for potential restart...");
                await WaitForProcessAsync("steam", waitForExit: true, timeoutSeconds: 6);
            }

            // Step 2: Make sure Steam is actually running before launching TF2
            if (!IsProcessRunning("steam"))
            {
                Logger.LogInfo("[Game] Waiting for Steam process to start...");
                await WaitForProcessAsync("steam", waitForExit: false, timeoutSeconds: 60);

                Logger.LogDebug("[Game] Steam process found — waiting for window initialization...");
                await WaitForProcessWindowAsync("steam", timeoutSeconds: 15);

                // Brief delay for Steam IPC readiness
                await Task.Delay(2000);
            }
            else
            {
                Logger.LogDebug("[Game] Steam already running — skipping wait");
            }

            // Step 3: Launch TF2 through Steam
            TryLaunchThroughSteam(finalArgs);

            // Step 4: Wait for TF2 and trigger pure_patcher
            await WaitForTf2AndLaunchPurePatcher();
        }
        catch (Exception ex)
        {
            Logger.LogError("[Game] Orchestration pipeline failed", ex);
        }
    }

    /// <summary>
    /// Waits for the TF2 process to appear, then triggers pure_patcher.exe
    /// to apply runtime patches while the game is loading.
    /// </summary>
    private async Task WaitForTf2AndLaunchPurePatcher()
    {
        try
        {
            Logger.LogInfo("[Game] Waiting for tf_win64 process (timeout: 120s)...");
            await WaitForProcessAsync("tf_win64", waitForExit: false, timeoutSeconds: 120);

            if (!IsProcessRunning("tf_win64"))
            {
                Logger.LogWarning("[Game] TF2 not detected after 120s — skipping pure_patcher");
                return;
            }

            Logger.LogInfo("[Game] TF2 process detected — waiting for window...");
            await WaitForProcessWindowAsync("tf_win64", timeoutSeconds: 120);

            Logger.LogInfo("[Game] TF2 window ready — launching pure_patcher.exe");
            _ = NativeExecutableService.TryStartSingleFlight(
                "pure_patcher.exe",
                PurePatcherGateKey,
                "TF2 process ready");
        }
        catch (Exception ex)
        {
            Logger.LogError("[Game] Error during post-launch patching", ex);
        }
    }

    /// <summary>
    /// Verifies that the TF2 installation exists at the configured Steam path.
    /// </summary>
    public bool ValidateGameInstallation()
    {
        try
        {
            var settings = _settingsService.GetSettings();
            if (settings == null || string.IsNullOrWhiteSpace(settings.SteamPath))
            {
                Logger.LogWarning("[Game] Cannot validate — Steam path not configured");
                return false;
            }

            var tf2Path = settings.SteamPath;

            if (!Directory.Exists(tf2Path))
            {
                Logger.LogWarning($"[Game] TF2 directory missing: {tf2Path}");
                return false;
            }

            var tf2Exe = Path.Combine(tf2Path, "tf_win64.exe");
            if (!File.Exists(tf2Exe))
            {
                Logger.LogWarning($"[Game] tf_win64.exe not found in: {tf2Path}");
                return false;
            }

            Logger.LogInfo("[Game] Installation validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("[Game] Validation failed", ex);
            return false;
        }
    }

    public bool IsGameRunning()
    {
        try
        {
            return IsProcessRunning("tf_win64");
        }
        catch (Exception ex)
        {
            Logger.LogError("[Game] Error checking game process", ex);
            return false;
        }
    }

    // Launches TF2 via the Steam protocol URL
    private bool TryLaunchThroughSteam(string args)
    {
        var encoded = string.IsNullOrWhiteSpace(args) ? string.Empty : Uri.EscapeDataString(args);
        var steamUrl = string.IsNullOrWhiteSpace(encoded)
            ? "steam://rungameid/440"
            : $"steam://rungameid/440//{encoded}";

        Logger.LogInfo($"[Game] Launching TF2 — URL: {steamUrl}");

        Process.Start(new ProcessStartInfo
        {
            FileName = steamUrl,
            UseShellExecute = true
        });

        Logger.LogInfo("[Game] Launch command sent to Steam");
        return true;
    }

    // Hides the launcher window to the system tray
    private void MinimizeToTray()
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow == null)
        {
            Logger.LogWarning("[Game] Cannot minimize — MainWindow not found");
            return;
        }

        mainWindow.Hide();
        Logger.LogInfo("[Game] Launcher minimized to system tray");
    }

    // Process utility helpers

    private static bool IsProcessRunning(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        bool running = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return running;
    }

    // Polls until a process starts or exits (depending on waitForExit flag)
    private static async Task WaitForProcessAsync(string processName, bool waitForExit, int timeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var isRunning = IsProcessRunning(processName);
            if (waitForExit && !isRunning) return;
            if (!waitForExit && isRunning) return;
            await Task.Delay(500);
        }
    }

    // Polls until the process has a visible main window (fully initialized)
    private static async Task WaitForProcessWindowAsync(string processName, int timeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var procs = Process.GetProcessesByName(processName);
            bool hasWindow = false;
            foreach (var p in procs)
            {
                p.Refresh();
                if (p.MainWindowHandle != IntPtr.Zero)
                    hasWindow = true;
                p.Dispose();
            }

            if (hasWindow) return;
            await Task.Delay(500);
        }
    }

    private static bool IsSteamPathValid(string? steamPath)
    {
        return !string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath);
    }
}
