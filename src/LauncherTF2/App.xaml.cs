using System.Windows;
using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace LauncherTF2;

public partial class App : Application
{
    /// <summary>
    /// Command-line flag the launcher passes to itself when self-elevating for
    /// the game launch flow. The elevated child detects this flag and runs the
    /// patchers + TF2 orchestration without showing the UI.
    /// </summary>
    public const string LaunchTf2Flag = "--launch-tf2";

    private const string UiMutexName = "ProjectEternalLauncher_Mutex";
    private Mutex? _mutex;
    private bool _isElevatedLauncherChild;

    public App()
    {
        var args = Environment.GetCommandLineArgs();
        _isElevatedLauncherChild = args.Any(a => string.Equals(a, LaunchTf2Flag, StringComparison.OrdinalIgnoreCase));

        // The elevated helper child skips the UI mutex (the UI instance owns it)
        // and runs the game launch flow as a one-shot. No single-instance guard
        // because the user might restart the UI between launches.
        if (!_isElevatedLauncherChild)
        {
            _mutex = new Mutex(false, UiMutexName);
            if (!_mutex.WaitOne(0, false))
            {
                Views.MessageDialog.ShowError(
                    "Launcher Já em Execução",
                    "Project Eternal Launcher já está em execução.\n\nPor favor, feche a instância atual antes de abrir outra."
                );

                _mutex.Dispose();
                _mutex = null;
                Current.Shutdown();
                return;
            }
        }

        var launcherConfig = TryLoadLauncherConfig();
        if (!_isElevatedLauncherChild)
            TryAutoClearLogs(launcherConfig);

        Logger.Initialize(ResolveStartupLogLevel(launcherConfig));

        ServiceLocator.Initialize();

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        Exit += App_Exit;

        if (_isElevatedLauncherChild)
        {
            Logger.LogInfo("[App] Elevated helper child started — running game launch orchestration");
            RunElevatedLaunchAndExit();
            return;
        }

        Logger.LogInfo("[App] Startup complete");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Elevated child runs without a UI; nothing to show.
        if (_isElevatedLauncherChild) return;

        // Restore the user's saved theme before the window paints — this avoids
        // a one-frame flash of the default Eternal palette on first render.
        ServiceLocator.Theme.LoadFromConfig();

        // Normal UI startup — StartupUri was removed from App.xaml so we create
        // the window manually after services are initialized.
        var mainWindow = new Views.MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// Elevated-helper entry. Runs the full patcher + TF2 launch sequence in the
    /// background, then shuts the helper process down when the orchestration
    /// completes (or fails).
    /// </summary>
    private void RunElevatedLaunchAndExit()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ServiceLocator.Game.RunPatchAndLaunchSequenceAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("[App] Elevated launch orchestration crashed", ex);
            }
            finally
            {
                Logger.LogInfo("[App] Elevated helper exiting");
                _ = Dispatcher.BeginInvoke(() => Current?.Shutdown());
            }
        });
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogError("[App] Unhandled exception caught", e.Exception);

        // The elevated helper has no UI to surface errors through; just log and exit.
        if (_isElevatedLauncherChild)
        {
            e.Handled = !IsFatalException(e.Exception);
            return;
        }

        string errorMsg = $"An unhandled exception occurred: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
        Views.MessageDialog.ShowError("Error", errorMsg);

        try
        {
            var crashLogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            System.IO.File.WriteAllText(crashLogPath, $"{DateTime.Now}: {errorMsg}");
            Logger.LogInfo($"[App] Crash log written to: {crashLogPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError("[App] Failed to write crash log", ex);
        }

        e.Handled = !IsFatalException(e.Exception);
    }

    private static bool IsFatalException(Exception ex)
    {
        return ex is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or System.Runtime.InteropServices.SEHException;
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        Logger.LogInfo("[App] Shutting down — releasing resources");
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }

    private static LauncherConfig? TryLoadLauncherConfig()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void TryAutoClearLogs(LauncherConfig? cfg)
    {
        if (cfg?.AutoClearLogs != true) return;
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logFile = Path.Combine(baseDir, "app_debug.log");
            var archiveDir = Path.Combine(baseDir, "logs");
            if (File.Exists(logFile)) File.Delete(logFile);
            if (Directory.Exists(archiveDir)) Directory.Delete(archiveDir, recursive: true);
        }
        catch
        {
            // Best-effort: logger not up yet
        }
    }

    private static LogLevel ResolveStartupLogLevel(LauncherConfig? cfg)
    {
        if (cfg == null) return LogLevel.Info;
        if (cfg.EnableDebugLog) return LogLevel.Debug;
        return Enum.TryParse<LogLevel>(cfg.LogLevel, ignoreCase: true, out var lvl) ? lvl : LogLevel.Info;
    }
}
