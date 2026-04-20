using LauncherTF2.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LauncherTF2.Services;

/// <summary>
/// Centralized launcher for executables shipped under ./native or output root.
/// Includes a lightweight single-flight gate used to prevent duplicate patcher starts.
/// </summary>
public static class NativeExecutableService
{
    private static readonly ConcurrentDictionary<string, int> _singleFlightGate = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryResolveExecutablePath(string executableName, out string executablePath)
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var nativePath = Path.Combine(basePath, "native", executableName);

        if (File.Exists(nativePath))
        {
            executablePath = nativePath;
            return true;
        }

        var rootPath = Path.Combine(basePath, executableName);
        if (File.Exists(rootPath))
        {
            executablePath = rootPath;
            return true;
        }

        executablePath = nativePath;
        return false;
    }

    public static bool TryStartExecutable(string executableName, string context, bool createNoWindow = true)
    {
        if (!TryResolveExecutablePath(executableName, out var executablePath))
        {
            Logger.LogWarning($"{executableName} not found; context: {context}; expected path: {executablePath}");
            return false;
        }

        try
        {
            Logger.LogInfo($"Starting {executableName}: {executablePath} (context: {context})");
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start {executableName} (context: {context})", ex);
            return false;
        }
    }

    public static bool TryStartSingleFlight(string executableName, string gateKey, string context, bool createNoWindow = true)
    {
        if (!_singleFlightGate.TryAdd(gateKey, 1))
        {
            Logger.LogDebug($"Skipped duplicate start of {executableName} (gate: {gateKey}, context: {context})");
            return false;
        }

        try
        {
            return TryStartExecutable(executableName, context, createNoWindow);
        }
        finally
        {
            // Keep the gate for this process lifetime/session until explicitly reset.
        }
    }

    public static void ResetSingleFlight(string gateKey)
    {
        _singleFlightGate.TryRemove(gateKey, out _);
    }
}
