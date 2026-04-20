using System.Windows;
using LauncherTF2.Core;
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

        // Boot sequence: logger → services → error handlers
        Logger.Initialize(LogLevel.Info);
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
}
