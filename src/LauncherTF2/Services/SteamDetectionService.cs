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
                    Logger.Log($"[SteamDetectionService] Detected active SteamID: {steamId64}");
                    return steamId64.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[SteamDetectionService] Error detecting SteamID: {ex.Message}");
        }

        Logger.Log("[SteamDetectionService] Could not detect active SteamID.");
        return null;
    }
}
