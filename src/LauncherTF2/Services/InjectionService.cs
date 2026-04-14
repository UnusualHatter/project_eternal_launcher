using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

/// <summary>
/// Serviço de monitoramento de runtime para mods de otimização.
///
/// Importante: este serviço não realiza injeção remota de DLL em processos de terceiros.
/// Ele apenas monitora o estado do jogo e valida a disponibilidade de uma DLL auxiliar local.
/// </summary>
public class InjectionService : IDisposable
{
    private static InjectionService? _instance;
    public static InjectionService Instance => _instance ??= new InjectionService();

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private readonly string[] _dllCandidates;
    private string? _resolvedDllPath;
    private bool _wasGameRunning;
    private bool _disposed;

    public InjectionService()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _dllCandidates =
        [
            Path.Combine(basePath, "Resources", "Injections", "pure_patcher.dll"),
            Path.Combine(basePath, "Resources", "Injections", "casual_fix.dll"),
            Path.Combine(basePath, "pure_patcher.dll"),
            Path.Combine(basePath, "casual_fix.dll")
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
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;

        Logger.LogInfo("Runtime monitoring stopped");
    }

    private async Task MonitorLoop(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Starting runtime monitoring loop");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                MonitorGameState();

                await Task.Delay(2000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            Logger.LogInfo("Runtime monitoring loop cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error in runtime monitoring loop", ex);
        }
    }

    /// <summary>
    /// Detecta transições de estado do jogo (abriu/fechou) e registra contexto de runtime.
    /// </summary>
    private void MonitorGameState()
    {
        var isRunning = IsGameRunning();

        if (isRunning && !_wasGameRunning)
        {
            if (!string.IsNullOrWhiteSpace(_resolvedDllPath))
            {
                Logger.LogInfo($"hl2 detected. Optimization runtime available at: {_resolvedDllPath}");
            }
            else
            {
                Logger.LogWarning("hl2 detected, but no optimization runtime DLL was found.");
            }
        }

        if (!isRunning && _wasGameRunning)
        {
            Logger.LogInfo("hl2 closed. Runtime session finalized.");
        }

        _wasGameRunning = isRunning;
    }

    /// <summary>
    /// Verifica se o processo do jogo está ativo e libera recursos dos objetos Process.
    /// </summary>
    private static bool IsGameRunning()
    {
        var processes = Process.GetProcessesByName("hl2");
        var isRunning = processes.Length > 0;

        foreach (var process in processes)
        {
            process.Dispose();
        }

        return isRunning;
    }

    /// <summary>
    /// Resolve a DLL auxiliar disponível no ambiente atual.
    /// </summary>
    private string? ResolveRuntimeDllPath()
    {
        foreach (var path in _dllCandidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Registra status da DLL auxiliar e hash SHA-256 para rastreabilidade.
    /// </summary>
    private static void LogDllStatus(string? dllPath)
    {
        if (string.IsNullOrWhiteSpace(dllPath))
        {
            Logger.LogWarning("No optimization runtime DLL found. Monitoring will continue without runtime binding.");
            return;
        }

        var hash = ComputeSha256(dllPath);
        Logger.LogInfo($"Runtime DLL found: {dllPath}");
        Logger.LogDebug($"Runtime DLL SHA-256: {hash}");
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
