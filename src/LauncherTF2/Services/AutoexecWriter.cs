using LauncherTF2.Core;
using LauncherTF2.Models;
using System.IO;
using System.Text;

namespace LauncherTF2.Services;

/// <summary>
/// Generates the launcher-managed section of <c>tf/cfg/autoexec.cfg</c>.
///
/// The writer respects existing user customisations: anything the user already had
/// in <c>autoexec.cfg</c> is preserved verbatim. Only the block bracketed by
/// <see cref="BeginMarker"/> and <see cref="EndMarker"/> is owned by the launcher.
/// The managed block is written at the *bottom* of the file so its values execute
/// last (TF2 evaluates the cfg top-to-bottom, last write wins) — this keeps the
/// settings UI in sync with what the game actually applies.
/// </summary>
public class AutoexecWriter
{
    public const string BeginMarker = "// === ETERNAL LAUNCHER MANAGED BLOCK — do not edit between these markers ===";
    public const string EndMarker = "// === ETERNAL LAUNCHER MANAGED BLOCK END ===";

    public static void WriteToAutoexec(SettingsModel settings, string tfPath)
    {
        var cfgPath = Path.Combine(tfPath, "cfg", "autoexec.cfg");
        var cfgDir = Path.GetDirectoryName(cfgPath);
        if (cfgDir != null && !Directory.Exists(cfgDir))
            Directory.CreateDirectory(cfgDir);

        var userContent = ExtractUserContent(cfgPath);
        var managedBlock = BuildManagedBlock(settings);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(userContent))
        {
            sb.Append(userContent);
            if (!userContent.EndsWith("\n")) sb.AppendLine();
            sb.AppendLine();
        }
        sb.AppendLine(BeginMarker);
        sb.Append(managedBlock);
        sb.AppendLine(EndMarker);

        try
        {
            File.WriteAllText(cfgPath, sb.ToString());
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[AutoexecWriter] Failed to write autoexec.cfg: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the user-owned portion of the cfg — everything outside our markers.
    /// If no markers are present, treats the entire file as user content.
    /// </summary>
    private static string ExtractUserContent(string cfgPath)
    {
        if (!File.Exists(cfgPath)) return string.Empty;

        try
        {
            var lines = File.ReadAllLines(cfgPath);
            var beginIdx = Array.FindIndex(lines, l => l.Trim() == BeginMarker);
            var endIdx = Array.FindIndex(lines, l => l.Trim() == EndMarker);

            if (beginIdx < 0 || endIdx < 0 || endIdx <= beginIdx)
            {
                // No previous launcher block — keep the whole file as user content.
                return string.Join(Environment.NewLine, lines).TrimEnd();
            }

            // Stitch back what's before and after our managed block.
            var before = lines.Take(beginIdx);
            var after = lines.Skip(endIdx + 1);
            return string.Join(Environment.NewLine, before.Concat(after)).TrimEnd();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[AutoexecWriter] Could not read existing autoexec.cfg: {ex.Message}");
            return string.Empty;
        }
    }

    private static string BuildManagedBlock(SettingsModel settings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Auto-generated. Toggles in the Eternal Launcher write to this block;");
        sb.AppendLine("// anything outside the markers is yours and won't be touched.");
        sb.AppendLine();

        sb.AppendLine("// --- Graphics ---");
        sb.AppendLine($"mat_vsync {(settings.VSync ? "1" : "0")}");
        sb.AppendLine($"mat_antialias {settings.AntiAliasing}");
        sb.AppendLine($"mat_forceaniso {settings.AnisotropicFiltering}");
        sb.AppendLine($"mat_disable_bloom {(settings.Bloom ? "0" : "1")}");
        sb.AppendLine($"mat_motion_blur_strength {settings.MotionBlurStrength.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        sb.AppendLine($"r_lod {settings.ModelLod}");
        sb.AppendLine($"cl_ragdoll_physics_enable {(settings.Ragdolls ? "1" : "0")}");
        sb.AppendLine($"cl_detaildist {settings.DetailDistance}");
        sb.AppendLine();

        sb.AppendLine("// --- Gameplay ---");
        sb.AppendLine($"fov_desired {settings.Fov}");
        sb.AppendLine($"viewmodel_fov {settings.ViewmodelFov}");
        sb.AppendLine($"r_drawviewmodel {(settings.DrawViewmodel ? "1" : "0")}");
        sb.AppendLine($"m_rawinput {(settings.RawInput ? "1" : "0")}");
        sb.AppendLine($"sensitivity {settings.MouseSensitivity.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        sb.AppendLine($"cl_autoreload {(settings.AutoReload ? "1" : "0")}");
        sb.AppendLine($"tf_dingalingaling {(settings.HitSound ? "1" : "0")}");
        sb.AppendLine($"hud_combattext {(settings.DamageNumbers ? "1" : "0")}");
        sb.AppendLine();

        sb.AppendLine("// --- Network ---");
        sb.AppendLine($"cl_interp {settings.Interp.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        sb.AppendLine($"cl_interpolate {(settings.Interpolate ? "1" : "0")}");
        sb.AppendLine($"rate {settings.Rate}");
        sb.AppendLine($"cl_cmdrate {settings.CmdRate}");
        sb.AppendLine($"cl_updaterate {settings.UpdateRate}");
        sb.AppendLine($"mat_queue_mode {settings.QueueMode}");

        if (settings.Binds.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// --- Binds ---");
            foreach (var bind in settings.Binds)
            {
                if (!string.IsNullOrWhiteSpace(bind.Key) && !string.IsNullOrWhiteSpace(bind.Command))
                    sb.AppendLine($"bind \"{bind.Key}\" \"{bind.Command}\"");
            }
        }

        return sb.ToString();
    }
}
