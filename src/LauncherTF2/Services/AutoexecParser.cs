using LauncherTF2.Core;
using LauncherTF2.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace LauncherTF2.Services;

/// <summary>
/// Reads <c>tf/cfg/autoexec.cfg</c> on startup and applies any recognised cvar
/// values back onto <see cref="SettingsModel"/>. Runs against *the entire*
/// file — including the user's own pre-existing content outside our managed
/// markers — so installing the launcher onto a TF2 install that already has
/// a custom autoexec automatically lifts those values into the UI on first
/// run.
///
/// Unknown cvars are ignored; the goal is graceful absorption, not strict
/// validation. Cvars that the schema's CustomEmitter writes (e.g. the
/// inverse <c>mat_disable_bloom</c>) parse back through the same inversion
/// here so a round-trip stays stable.
/// </summary>
public class AutoexecParser
{
    public void LoadFromAutoexec(SettingsModel settings, string tfPath)
    {
        string cfgPath = Path.Combine(tfPath, "cfg", "autoexec.cfg");
        if (!File.Exists(cfgPath)) return;

        try
        {
            string[] lines = File.ReadAllLines(cfgPath);
            foreach (string line in lines)
            {
                string cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine) || cleanLine.StartsWith("//")) continue;

                var match = Regex.Match(cleanLine, @"^([a-zA-Z0-9_]+)\s+""?(-?[^""\s]+)""?");
                if (!match.Success) continue;

                string command = match.Groups[1].Value.ToLowerInvariant();
                string valueStr = match.Groups[2].Value;
                ApplySetting(settings, command, valueStr);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[AutoexecParser] Failed to read autoexec.cfg: {ex.Message}");
        }
    }

    private static void ApplySetting(SettingsModel settings, string command, string value)
    {
        bool ParseBool(string v) => v == "1";
        int  ParseInt(string v) => int.TryParse(v, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int i) ? i : 0;
        double ParseDouble(string v) => double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0.0;

        switch (command)
        {
            // ── Graphics / Advanced ──
            case "mat_vsync": settings.VSync = ParseBool(value); break;
            case "mat_antialias": settings.AntiAliasing = ParseInt(value); break;
            case "mat_forceaniso": settings.AnisotropicFiltering = ParseInt(value); break;
            case "mat_disable_bloom": settings.Bloom = !ParseBool(value); break;
            case "mat_motion_blur_strength":
            case "motion_blur_strength": settings.MotionBlurStrength = ParseDouble(value); break;
            case "mat_queue_mode": settings.QueueMode = ParseInt(value); break;
            case "mat_dxlevel": settings.DxLevel = ParseInt(value); break;
            case "r_lod": settings.ModelLod = ParseInt(value); break;
            case "cl_ragdoll_physics_enable": settings.Ragdolls = ParseBool(value); break;
            case "cl_detaildist": settings.DetailDistance = ParseInt(value); break;

            // ── Performance removers (model field is "remove/disable" — invert the cvar) ──
            case "cl_burninggibs": settings.RemoveGibs = !ParseBool(value); break;
            case "cl_playerspraydisable": settings.RemoveSprays = ParseBool(value); break;
            case "cl_jiggle_bone_framerate_cutoff": settings.DisableJiggleBones = (ParseInt(value) == 0); break;
            case "r_eyemove": settings.DisableFacialAnims = !ParseBool(value); break;
            case "r_dynamic": settings.DisableDynamicLights = !ParseBool(value); break;
            case "tf_pyrovision_override": settings.DisablePyroland = (ParseInt(value) == -1); break;
            case "r_decals": settings.DisableDecals = (ParseInt(value) == 0); break;

            // ── Gameplay ──
            case "fov_desired": settings.Fov = ParseInt(value); break;
            case "viewmodel_fov": settings.ViewmodelFov = ParseInt(value); break;
            case "r_drawviewmodel": settings.DrawViewmodel = ParseBool(value); break;
            case "m_rawinput": settings.RawInput = ParseBool(value); break;
            case "sensitivity": settings.MouseSensitivity = ParseDouble(value); break;
            case "cl_autoreload": settings.AutoReload = ParseBool(value); break;
            case "tf_dingalingaling": settings.HitSound = ParseBool(value); break;
            case "tf_dingalingaling_lasthit": settings.KillSound = ParseBool(value); break;
            case "hud_combattext": settings.DamageNumbers = ParseBool(value); break;
            case "hud_fastswitch": settings.HudFastSwitch = ParseBool(value); break;
            case "tf_use_min_viewmodels": settings.MinViewmodels = ParseBool(value); break;
            case "cl_autorezoom": settings.AutoRezoom = ParseBool(value); break;
            case "closecaption": settings.ClosedCaptions = ParseBool(value); break;
            case "m_customaccel": settings.CustomAccel = ParseInt(value); break;

            // ── Competitive ──
            case "hud_medicautocallers": settings.MedicAutocall = ParseBool(value); break;
            case "hud_medicautocallersthreshold": settings.MedicAutocallThreshold = ParseInt(value); break;
            case "fps_max": settings.FpsMax = ParseInt(value); break;
            case "fps_max_menu": settings.FpsMaxMenu = ParseInt(value); break;

            // ── Performance: visual quality cvars ──
            case "mat_specular": settings.MatSpecular = ParseBool(value); break;
            case "mat_phong": settings.MatPhong = ParseBool(value); break;
            case "mat_bumpmap": settings.MatBumpmap = ParseBool(value); break;
            case "cl_flipviewmodels": settings.FlipViewmodels = ParseBool(value); break;

            // ── Audio ──
            case "tf_dingalingaling_volume": settings.HitsoundVolume = ParseDouble(value); break;
            case "voice_scale": settings.VoiceScale = ParseDouble(value); break;
            case "tf_dingalingaling_pitch_min": settings.HitsoundPitchMin = ParseInt(value); break;
            case "tf_dingalingaling_pitch_max": settings.HitsoundPitchMax = ParseInt(value); break;
            case "snd_mix_async": settings.AsyncSound = ParseBool(value); break;

            // ── Viewmodel offsets ──
            case "tf_viewmodels_offset_override_x": settings.ViewmodelOffsetX = ParseDouble(value); break;
            case "tf_viewmodels_offset_override_y": settings.ViewmodelOffsetY = ParseDouble(value); break;
            case "tf_viewmodels_offset_override_z": settings.ViewmodelOffsetZ = ParseDouble(value); break;

            // ── Network ──
            case "cl_interp": settings.Interp = ParseDouble(value); break;
            case "cl_interp_ratio": settings.InterpRatio = ParseInt(value); break;
            case "cl_interpolate": settings.Interpolate = ParseBool(value); break;
            case "rate": settings.Rate = ParseInt(value); break;
            case "cl_cmdrate": settings.CmdRate = ParseInt(value); break;
            case "cl_updaterate": settings.UpdateRate = ParseInt(value); break;
            case "cl_smooth": settings.SmoothEnabled = ParseBool(value); break;
            case "cl_pred_optimize": settings.PredOptimize = ParseInt(value); break; // legacy — same rationale as fps_max

            // Anything else — ignored. Likely a user customisation outside our
            // schema or a deprecated launcher field; either way the user's
            // verbatim line stays in autoexec.cfg outside our markers.
        }
    }
}
