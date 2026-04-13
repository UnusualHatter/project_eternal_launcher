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
        // Check if another instance is already running
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        
        if (!createdNew)
        {
            MessageBox.Show(
                "Project Eternal Launcher já está em execução.\n\nPor favor, feche a instância atual antes de abrir outra.",
                "Launcher Já em Execução",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            
            Current.Shutdown();
            return;
        }

        // Initialize Logger
        Logger.Initialize(LogLevel.Info);
        
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        Exit += App_Exit;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogError("Unhandled exception in dispatcher", e.Exception);
        
        string errorMsg = $"An unhandled exception occurred: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
        MessageBox.Show(errorMsg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        try
        {
            var crashLogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
            System.IO.File.WriteAllText(crashLogPath, $"{DateTime.Now}: {errorMsg}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to write crash log", ex);
        }

        e.Handled = true;
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        Logger.LogInfo("Application shutting down");
        
        // Release the mutex
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
