using Microsoft.Win32;
using System;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

public class SteamDetectionService
{
    private const long SteamId64Base = 76561197960265728;

    public string? GetActiveSteamId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            if (key != null)
            {
                var activeUserValue = key.GetValue("ActiveUser");
                if (activeUserValue is int accountId && accountId != 0)
                {
                    long steamId64 = SteamId64Base + accountId;
                    Logger.LogInfo($"Detected active SteamID: {steamId64}");
                    return steamId64.ToString();
                }
                else
                {
                    Logger.LogDebug("ActiveUser value is null or 0");
                }
            }
            else
            {
                Logger.LogDebug("Steam registry key not found");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error detecting SteamID", ex);
        }

        Logger.LogDebug("Could not detect active SteamID");
        return null;
    }

    public string? GetSteamInstallPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            if (key != null)
            {
                var steamPath = key.GetValue("SteamPath") as string;
                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    Logger.LogInfo($"Detected Steam installation path: {steamPath}");
                    return steamPath;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error detecting Steam installation path", ex);
        }

        Logger.LogDebug("Could not detect Steam installation path");
        return null;
    }

    public bool IsSteamRunning()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("Steam");
            var isRunning = processes.Length > 0;
            
            foreach (var proc in processes)
            {
                proc.Dispose();
            }

            Logger.LogDebug($"Steam running check: {isRunning}");
            return isRunning;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error checking if Steam is running", ex);
            return false;
        }
    }
}
