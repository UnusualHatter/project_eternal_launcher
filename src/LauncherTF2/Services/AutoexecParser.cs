using LauncherTF2.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace LauncherTF2.Services;

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

                var match = Regex.Match(cleanLine, @"^([a-zA-Z0-9_]+)\s+""?([^""\s]+)""?");
                if (match.Success)
                {
                    string command = match.Groups[1].Value.ToLower();
                    string valueStr = match.Groups[2].Value;

                    ApplySetting(settings, command, valueStr);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing autoexec: {ex.Message}");
        }
    }

    private void ApplySetting(SettingsModel settings, string command, string value)
    {
        bool ParseBool(string v) => v == "1";
        int ParseInt(string v) => int.TryParse(v, out int i) ? i : 0;
        double ParseDouble(string v) => double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0.0;

        switch (command)
        {
            case "mat_vsync": settings.VSync = ParseBool(value); break;
            case "mat_antialias": settings.AntiAliasing = ParseInt(value); break;
            case "mat_forceaniso": settings.AnisotropicFiltering = ParseInt(value); break;
            case "mat_disable_bloom": settings.Bloom = !ParseBool(value); break;
            case "motion_blur_strength": settings.MotionBlurStrength = ParseDouble(value); break;
            case "r_lod": settings.ModelLod = ParseInt(value); break;
            case "cl_ragdoll_physics_enable": settings.Ragdolls = ParseBool(value); break;
            case "violence_agibs": settings.AlienGibs = ParseBool(value); break;
            case "violence_hgibs": settings.HumanGibs = ParseBool(value); break;
            case "cl_detaildist": settings.DetailDistance = ParseInt(value); break;

            case "fov_desired": settings.Fov = ParseInt(value); break;
            case "viewmodel_fov": settings.ViewmodelFov = ParseInt(value); break;
            case "r_drawviewmodel": settings.DrawViewmodel = ParseBool(value); break;
            case "m_rawinput": settings.RawInput = ParseBool(value); break;
            case "sensitivity": settings.MouseSensitivity = ParseDouble(value); break;
            case "cl_autoreload": settings.AutoReload = ParseBool(value); break;
            case "tf_dingalingaling": settings.HitSound = ParseBool(value); break;
            case "hud_combattext": settings.DamageNumbers = ParseBool(value); break;
            case "tf_killstreak_sheen_brightness": settings.KillstreakGlow = ParseDouble(value); break;

            case "net_graph": settings.NetGraph = ParseBool(value); break;
            case "hud_saytext_time": settings.ChatMessageTime = ParseDouble(value); break;
            case "cl_drawhud": settings.DrawHud = ParseBool(value); break;

            case "cl_interp": settings.Interp = ParseDouble(value); break;
            case "cl_interpolate": settings.Interpolate = ParseBool(value); break;
            case "rate": settings.Rate = ParseInt(value); break;
            case "cl_cmdrate": settings.CmdRate = ParseInt(value); break;
            case "cl_updaterate": settings.UpdateRate = ParseInt(value); break;
            case "mat_queue_mode": settings.QueueMode = ParseInt(value); break;
            case "r_eyes": settings.DisableEyes = !ParseBool(value); break;
            case "r_flex": settings.DisableFlex = !ParseBool(value); break;
        }
    }
}
