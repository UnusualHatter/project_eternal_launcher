using LauncherTF2.Models;

namespace LauncherTF2.Services;

/// <summary>
/// Bulk-apply helpers for performance presets. Each method mutates the live
/// <see cref="SettingsModel"/>; the schema-bound wrappers observe those
/// PropertyChanged notifications and refresh the UI in place.
///
/// Presets only touch the cvars they actually own — anything outside their
/// scope (binds, launch options, network, etc.) is left untouched so users
/// can mix and match.
/// </summary>
internal static class PerformancePresets
{
    public static void ApplyMaxFps(SettingsModel m)
    {
        m.Ragdolls = false;
        m.RemoveGibs = true;
        m.RemoveSprays = true;
        m.DisableJiggleBones = true;
        m.DisableFacialAnims = true;
        m.DisableDynamicLights = true;
        m.DisablePyroland = true;
        m.DisableDecals = true;
        m.SoftParticlesOff = true;
        m.Bloom = false;
        m.MotionBlurStrength = 0;
        m.ModelLod = 2;
        m.DetailDistance = 0;
        m.AntiAliasing = 0;
        m.AnisotropicFiltering = 1;
        m.DxLevel = 81;
        m.PerformancePreset = "MaxFps";
    }

    public static void ApplyCompetitive(SettingsModel m)
    {
        m.Ragdolls = false;
        m.RemoveGibs = true;
        m.RemoveSprays = true;
        m.DisableJiggleBones = true;
        m.DisableFacialAnims = true;
        m.DisableDynamicLights = false;
        m.DisablePyroland = true;
        m.DisableDecals = true;
        m.SoftParticlesOff = true;
        m.Bloom = false;
        m.MotionBlurStrength = 0;
        m.ModelLod = 1;
        m.DetailDistance = 600;
        m.AntiAliasing = 0;
        m.AnisotropicFiltering = 4;
        m.DxLevel = 95;
        m.PerformancePreset = "Competitive";
    }

    public static void ApplyBalanced(SettingsModel m)
    {
        m.Ragdolls = true;
        m.RemoveGibs = false;
        m.RemoveSprays = true;
        m.DisableJiggleBones = false;
        m.DisableFacialAnims = false;
        m.DisableDynamicLights = false;
        m.DisablePyroland = false;
        m.DisableDecals = false;
        m.SoftParticlesOff = false;
        m.Bloom = true;
        m.MotionBlurStrength = 0;
        m.ModelLod = 0;
        m.DetailDistance = 1200;
        m.AntiAliasing = 4;
        m.AnisotropicFiltering = 8;
        m.DxLevel = 95;
        m.PerformancePreset = "Balanced";
    }

    public static void ApplyHighQuality(SettingsModel m)
    {
        m.Ragdolls = true;
        m.RemoveGibs = false;
        m.RemoveSprays = false;
        m.DisableJiggleBones = false;
        m.DisableFacialAnims = false;
        m.DisableDynamicLights = false;
        m.DisablePyroland = false;
        m.DisableDecals = false;
        m.SoftParticlesOff = false;
        m.Bloom = true;
        m.MotionBlurStrength = 0.3;
        m.ModelLod = -1;
        m.DetailDistance = 2000;
        m.AntiAliasing = 8;
        m.AnisotropicFiltering = 16;
        m.DxLevel = 95;
        m.PerformancePreset = "HighQuality";
    }
}

/// <summary>
/// Bulk-apply helpers for network presets.
/// References:
///   Casual      — Valve defaults (forgiving).
///   Competitive — tight interp, 66 tick (RGL/UGC).
///   HighPing    — looser interp to compensate for packet loss.
///   LAN         — minimum interp, maximum updaterate.
/// </summary>
internal static class NetworkPresets
{
    public static void ApplyCasual(SettingsModel m)
    {
        m.Interp = 0.0325;
        m.InterpRatio = 2;
        m.UpdateRate = 66;
        m.CmdRate = 66;
        m.Rate = 196608;
        m.Interpolate = true;
        m.SmoothEnabled = true;
        m.PredOptimize = 2;
        m.NetworkPreset = "Casual";
    }

    public static void ApplyCompetitive(SettingsModel m)
    {
        m.Interp = 0.0152;
        m.InterpRatio = 1;
        m.UpdateRate = 66;
        m.CmdRate = 66;
        m.Rate = 196608;
        m.Interpolate = true;
        m.SmoothEnabled = true;
        m.PredOptimize = 2;
        m.NetworkPreset = "Competitive";
    }

    public static void ApplyHighPing(SettingsModel m)
    {
        m.Interp = 0.05;
        m.InterpRatio = 2;
        m.UpdateRate = 66;
        m.CmdRate = 66;
        m.Rate = 65536;
        m.Interpolate = true;
        m.SmoothEnabled = true;
        m.PredOptimize = 2;
        m.NetworkPreset = "HighPing";
    }

    public static void ApplyLan(SettingsModel m)
    {
        m.Interp = 0;
        m.InterpRatio = 1;
        m.UpdateRate = 128;
        m.CmdRate = 128;
        m.Rate = 786432;
        m.Interpolate = true;
        m.SmoothEnabled = false;
        m.PredOptimize = 0;
        m.NetworkPreset = "LAN";
    }
}
