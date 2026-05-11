using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Models.Settings;
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
///
/// Block layout is data-driven: <see cref="SettingsSchema"/> defines the
/// categories and items, and each <see cref="SettingItem.EmitCvarLines"/> call
/// produces zero or more cvar lines. Items that return nothing don't get a
/// row, so the cfg never gains junk lines for unused features.
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

        foreach (var cat in SettingsSchema.Build(settings))
        {
            // Build the lines first so we can skip the whole header when a category emits nothing.
            var lines = cat.Items
                .SelectMany(item => item.EmitCvarLines())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            if (lines.Count == 0) continue;

            sb.AppendLine($"// --- {cat.AutoexecLabel} ---");
            foreach (var line in lines) sb.AppendLine(line);
            sb.AppendLine();
        }

        if (settings.Binds.Count > 0)
        {
            sb.AppendLine("// --- Binds ---");
            foreach (var bind in settings.Binds)
            {
                if (!string.IsNullOrWhiteSpace(bind.Key) && !string.IsNullOrWhiteSpace(bind.Command))
                    sb.AppendLine($"bind \"{bind.Key}\" \"{bind.Command}\"");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
