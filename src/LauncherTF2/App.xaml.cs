using System.Windows;
using LauncherTF2.Core;
using LauncherTF2.Models;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace LauncherTF2;

public partial class App : Application
{
    private const string MutexName = "ProjectEternalLauncher_Mutex";
    private Mutex? _mutex;

    public App()
    {
        // Single-instance guard — prevents multiple launchers from running
        _mutex = new Mutex(false, MutexName);

        if (!_mutex.WaitOne(0, false))
        {
            MessageBox.Show(
                "Project Eternal Launcher já está em execução.\n\nPor favor, feche a instância atual antes de abrir outra.",
                "Launcher Já em Execução",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            _mutex.Dispose();
            _mutex = null;
            Current.Shutdown();
            return;
        }

        // Apply launcher config (auto-clear logs, log level) before the logger writes anything
        var launcherConfig = TryLoadLauncherConfig();
        TryAutoClearLogs(launcherConfig);

        Logger.Initialize(ResolveStartupLogLevel(launcherConfig));
        ServiceLocator.Initialize();

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        Exit += App_Exit;

        Logger.LogInfo("[App] Startup complete");
    }

    /// <summary>
    /// Global exception handler — logs the crash, shows a user-friendly dialog,
    /// and writes a crash_log.txt for post-mortem analysis.
    /// </summary>
    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogError("[App] Unhandled exception caught", e.Exception);

        string errorMsg = $"An unhandled exception occurred: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
        MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        // Write a standalone crash log for debugging outside the normal log rotation
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

        // Swallow recoverable exceptions so the app stays alive — fatal ones
        // (OOM, stack overflow, etc.) pass through and terminate the process
        e.Handled = !IsFatalException(e.Exception);
    }

    // These exceptions mean the runtime is in an unrecoverable state
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
            // Logger is not initialized yet — fall through to defaults
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
            // Best-effort: logger not up yet, nothing to surface this through
        }
    }

    private static LogLevel ResolveStartupLogLevel(LauncherConfig? cfg)
    {
        if (cfg == null) return LogLevel.Info;
        if (cfg.EnableDebugLog) return LogLevel.Debug;
        return Enum.TryParse<LogLevel>(cfg.LogLevel, ignoreCase: true, out var lvl) ? lvl : LogLevel.Info;
    }
}
