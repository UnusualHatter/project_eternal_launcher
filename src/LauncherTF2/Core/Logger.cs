using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LauncherTF2.Core;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class Logger
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_debug.log");
    private static readonly string LogArchivePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private static readonly object _lock = new();
    private static readonly long MaxLogFileSize = 10 * 1024 * 1024; // 10 MB
    private static LogLevel _minimumLogLevel = LogLevel.Info;

    public static LogLevel MinimumLogLevel
    {
        get => _minimumLogLevel;
        set => _minimumLogLevel = value;
    }

    public static void LogDebug(string message) => Log(LogLevel.Debug, message);
    public static void LogInfo(string message) => Log(LogLevel.Info, message);
    public static void LogWarning(string message) => Log(LogLevel.Warning, message);
    /// <summary>
    /// Registra aviso com exceção associada para preservar stack trace em cenários recuperáveis.
    /// </summary>
    public static void LogWarning(string message, Exception? exception) => Log(LogLevel.Warning, message, exception);
    public static void LogError(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

    public static void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level < _minimumLogLevel)
            return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = new StringBuilder();
            logEntry.Append($"[{timestamp}] [{level}] {message}");

            if (exception != null)
            {
                logEntry.AppendLine();
                logEntry.Append($"Exception: {exception.Message}");
                logEntry.AppendLine();
                logEntry.Append(exception.StackTrace);
            }

            logEntry.AppendLine();

            lock (_lock)
            {
                CheckAndRotateLog();
                File.AppendAllText(LogPath, logEntry.ToString());
            }

            // Also output to debug for development
            System.Diagnostics.Debug.WriteLine(logEntry.ToString());
        }
        catch (Exception ex)
        {
            // Last resort - try to write to event log or console
            try
            {
                System.Diagnostics.Debug.WriteLine($"Logger failed: {ex.Message}");
                Console.WriteLine($"Logger failed: {ex.Message}");
            }
            catch
            {
                // If even this fails, we can't do anything
            }
        }
    }

    public static async Task LogAsync(LogLevel level, string message, Exception? exception = null)
    {
        if (level < _minimumLogLevel)
            return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = new StringBuilder();
            logEntry.Append($"[{timestamp}] [{level}] {message}");

            if (exception != null)
            {
                logEntry.AppendLine();
                logEntry.Append($"Exception: {exception.Message}");
                logEntry.AppendLine();
                logEntry.Append(exception.StackTrace);
            }

            logEntry.AppendLine();

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    CheckAndRotateLog();
                    File.AppendAllText(LogPath, logEntry.ToString());
                }
            }).ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine(logEntry.ToString());
        }
        catch (Exception ex)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Logger failed: {ex.Message}");
                Console.WriteLine($"Logger failed: {ex.Message}");
            }
            catch
            {
            }
        }
    }

    private static void CheckAndRotateLog()
    {
        try
        {
            if (!File.Exists(LogPath))
                return;

            var fileInfo = new FileInfo(LogPath);
            if (fileInfo.Length >= MaxLogFileSize)
            {
                if (!Directory.Exists(LogArchivePath))
                    Directory.CreateDirectory(LogArchivePath);

                var archiveName = $"app_debug_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                var archivePath = Path.Combine(LogArchivePath, archiveName);
                File.Move(LogPath, archivePath);

                // Keep only last 10 log files
                var logFiles = Directory.GetFiles(LogArchivePath, "app_debug_*.log")
                    .OrderByDescending(f => f)
                    .Skip(10);

                foreach (var oldLog in logFiles)
                {
                    try
                    {
                        File.Delete(oldLog);
                    }
                    catch
                    {
                        // Ignore deletion errors
                    }
                }
            }
        }
        catch
        {
            // Ignore rotation errors
        }
    }

    public static void Initialize(LogLevel minimumLevel = LogLevel.Info)
    {
        _minimumLogLevel = minimumLevel;
        LogInfo("Logger initialized");
    }
}
