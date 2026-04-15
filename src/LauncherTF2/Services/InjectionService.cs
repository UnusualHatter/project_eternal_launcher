using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

/// <summary>
/// Monitors the TF2 process lifecycle and injects pure_patcher.dll
/// once the game reaches its main menu (window handle becomes available).
/// </summary>
public class InjectionService : IDisposable
{
    private static InjectionService? _instance;
    public static InjectionService Instance => _instance ??= new InjectionService();

    private const string TF2ProcessName = "tf_win64";

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private readonly string[] _dllCandidates;
    private string? _resolvedDllPath;
    private bool _injectedThisSession;
    private bool _wasGameRunning;
    private bool _disposed;

    // Seconds to wait after MainWindowHandle is detected before injecting,
    // giving the engine time to finish loading shaders and assets
    private const int PostWindowDelaySeconds = 8;
    private const int PollIntervalMs = 2000;

    public InjectionService()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _dllCandidates =
        [
            Path.Combine(basePath, "native", "pure_patcher.dll"),
            Path.Combine(basePath, "pure_patcher.dll")
        ];
    }

    public void StartMonitoring()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            Logger.LogWarning("Runtime monitoring is already running");
            return;
        }

        _resolvedDllPath = ResolveRuntimeDllPath();
        LogDllStatus(_resolvedDllPath);

        _cancellationTokenSource = new CancellationTokenSource();
        _wasGameRunning = false;
        _injectedThisSession = false;

        _monitoringTask = Task.Run(() => MonitorLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        Logger.LogInfo("Runtime monitoring started");
    }

    public void StopMonitoring()
    {
        if (_cancellationTokenSource == null)
            return;

        _cancellationTokenSource.Cancel();

        try
        {
            _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            // Expected on cancellation
        }

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        Logger.LogInfo("Runtime monitoring stopped");
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        Logger.LogDebug("Starting runtime monitoring loop");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await MonitorGameState(ct);
                await Task.Delay(PollIntervalMs, ct);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("Runtime monitoring loop cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error in runtime monitoring loop", ex);
        }
    }

    /// <summary>
    /// Tracks TF2 process state transitions and triggers injection
    /// when the game window becomes available for the first time.
    /// </summary>
    private async Task MonitorGameState(CancellationToken ct)
    {
        var gameProcess = FindGameProcess();
        var isRunning = gameProcess != null;

        if (isRunning && !_wasGameRunning)
        {
            Logger.LogInfo($"{TF2ProcessName} process detected, waiting for main window...");
        }

        // Game just closed — reset injection flag for next session
        if (!isRunning && _wasGameRunning)
        {
            Logger.LogInfo($"{TF2ProcessName} closed. Runtime session finalized.");
            _injectedThisSession = false;
        }

        _wasGameRunning = isRunning;

        // Only attempt injection once per game session
        if (gameProcess == null || _injectedThisSession)
        {
            gameProcess?.Dispose();
            return;
        }

        try
        {
            // Wait for the engine to create its main window (menu loaded)
            if (!HasMainWindow(gameProcess))
            {
                return;
            }

            Logger.LogInfo($"Main window detected (handle: 0x{gameProcess.MainWindowHandle:X}). " +
                           $"Waiting {PostWindowDelaySeconds}s for engine initialization...");

            await Task.Delay(TimeSpan.FromSeconds(PostWindowDelaySeconds), ct);

            // Re-check the process is still alive after the delay
            gameProcess.Refresh();
            if (gameProcess.HasExited)
            {
                Logger.LogWarning($"{TF2ProcessName} exited during post-window delay, skipping injection");
                return;
            }

            await AttemptInjection(gameProcess);
        }
        finally
        {
            gameProcess.Dispose();
        }
    }

    /// <summary>
    /// Calls NativeInjector to inject the resolved DLL into the target process.
    /// </summary>
    private async Task AttemptInjection(Process target)
    {
        if (string.IsNullOrWhiteSpace(_resolvedDllPath))
        {
            Logger.LogWarning("No injection DLL found, skipping injection");
            _injectedThisSession = true;
            return;
        }

        var absoluteDllPath = Path.GetFullPath(_resolvedDllPath);
        Logger.LogInfo($"Injecting {Path.GetFileName(absoluteDllPath)} into {TF2ProcessName} (PID: {target.Id})...");

        try
        {
            int result = await NativeInjector.InjectAsync(target, absoluteDllPath);
            string message = NativeInjector.TranslateReturnCode(result);

            if (result == 0)
            {
                Logger.LogInfo($"Injection succeeded — {Path.GetFileName(absoluteDllPath)} loaded into {TF2ProcessName}");
            }
            else
            {
                Logger.LogError($"Injection failed: {message} (code: {result})");
            }
        }
        catch (PlatformNotSupportedException ex)
        {
            Logger.LogError("Injection aborted: launcher is not running as x64", ex);
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error during injection", ex);
        }

        // Mark as injected regardless of outcome to avoid spam retries
        _injectedThisSession = true;
    }

    private static Process? FindGameProcess()
    {
        var processes = Process.GetProcessesByName(TF2ProcessName);
        if (processes.Length == 0)
            return null;

        var target = processes[0];
        for (int i = 1; i < processes.Length; i++)
            processes[i].Dispose();

        return target;
    }

    /// <summary>
    /// Checks whether the process has created its main window,
    /// indicating the game has finished initial loading.
    /// </summary>
    private static bool HasMainWindow(Process process)
    {
        try
        {
            process.Refresh();
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    private string? ResolveRuntimeDllPath()
    {
        foreach (var path in _dllCandidates)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static void LogDllStatus(string? dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            Logger.LogWarning("No injection DLL found. Monitoring will continue but injection will be skipped.");
            return;
        }

        var hash = ComputeSha256(dllPath);
        Logger.LogInfo($"Injection DLL resolved: {dllPath}");
        Logger.LogDebug($"DLL SHA-256: {hash}");
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopMonitoring();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~InjectionService()
    {
        Dispose();
    }
}
