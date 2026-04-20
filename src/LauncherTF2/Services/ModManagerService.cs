using LauncherTF2.Models;
using LauncherTF2.Core;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace LauncherTF2.Services;

/// <summary>
/// Manages locally installed mods, scanning the TF2 custom folder.
/// 
/// Enabled/Disabled state is filesystem-based:
///   - Enabled mods live in: {tf/custom}/
///   - Disabled mods live in: {tf/custom}/disabled/
/// 
/// On first run (no mod_state.json), all mods already in custom/ are treated as enabled.
/// </summary>
public class ModManagerService
{
    private readonly string _modsConfigPath;
    private bool _hasExistingState;

    /// <summary>Resolved path to tf/custom.</summary>
    public string CustomFolderPath { get; private set; }

    /// <summary>Resolved path to tf/custom/disabled.</summary>
    public string DisabledFolderPath => Path.Combine(CustomFolderPath, "disabled");

    public ModManagerService(SettingsService settingsService)
    {
        _modsConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_state.json");
        _hasExistingState = File.Exists(_modsConfigPath);

        CustomFolderPath = ResolveTf2CustomPath(settingsService);
        InitializeDirectories();
    }

    /// <summary>
    /// Resolves the TF2 custom folder path from the shared SettingsService
    /// instead of re-parsing settings.json directly.
    /// </summary>
    private string ResolveTf2CustomPath(SettingsService settingsService)
    {
        try
        {
            var steamPath = settingsService.GetSettings().SteamPath;
            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                var customPath = ResolveCustomFolder(steamPath);
                Logger.LogInfo($"Resolved TF2 custom folder: {customPath}");
                return customPath;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to read settings for TF2 custom path", ex);
        }

        var fallback = Path.Combine(GamePaths.DefaultTf2Path, "custom");
        Logger.LogInfo($"Using default TF2 custom folder: {fallback}");
        return fallback;
    }

    /// <summary>
    /// Handles SteamPath being either ".../tf" or ".../Team Fortress 2".
    /// </summary>
    private static string ResolveCustomFolder(string steamPath)
    {
        var dirName = Path.GetFileName(steamPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (string.Equals(dirName, "tf", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(steamPath, "custom");

        if (Directory.Exists(Path.Combine(steamPath, "tf")))
            return Path.Combine(steamPath, "tf", "custom");

        return Path.Combine(steamPath, "custom");
    }

    private void InitializeDirectories()
    {
        try
        {
            if (!Directory.Exists(CustomFolderPath))
            {
                Directory.CreateDirectory(CustomFolderPath);
                Logger.LogInfo($"Created TF2 custom directory: {CustomFolderPath}");
            }

            if (!Directory.Exists(DisabledFolderPath))
            {
                Directory.CreateDirectory(DisabledFolderPath);
                Logger.LogInfo($"Created disabled mods directory: {DisabledFolderPath}");
            }

            // Mark that state now exists so subsequent runs won't override user choices
            if (!_hasExistingState)
            {
                File.WriteAllText(_modsConfigPath, "{}");
                _hasExistingState = true;
            }

            // Clean stale .cache files left by TF2/Steam on every startup
            CleanCacheFiles();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize mod directories", ex);
        }
    }

    /// <summary>
    /// Recursively deletes all .cache files inside the TF2 custom folder.
    /// TF2 and Steam leave these behind and they can cause issues / clutter.
    /// </summary>
    private void CleanCacheFiles()
    {
        try
        {
            if (!Directory.Exists(CustomFolderPath)) return;

            var cacheFiles = Directory.GetFiles(CustomFolderPath, "*.cache", SearchOption.AllDirectories);
            int deleted = 0;

            foreach (var cacheFile in cacheFiles)
            {
                try
                {
                    File.Delete(cacheFile);
                    deleted++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not delete cache file: {cacheFile}", ex);
                }
            }

            if (deleted > 0)
                Logger.LogInfo($"Cleaned {deleted} .cache file(s) from {CustomFolderPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to clean cache files", ex);
        }
    }

    /// <summary>
    /// Scans both custom/ (enabled) and custom/disabled/ (disabled) folders for mods.
    /// </summary>
    public List<ModModel> GetInstalledMods()
    {
        var mods = new List<ModModel>();

        try
        {
            // --- Scan ENABLED mods (in custom/ directly, excluding the disabled/ subfolder) ---
            foreach (var vpkFile in Directory.GetFiles(CustomFolderPath, "*.vpk", SearchOption.TopDirectoryOnly))
            {
                // Skip numbered chunk files (_000, _001, _002, etc.) — only the _dir.vpk represents the set
                if (IsVpkChunkFile(vpkFile)) continue;

                var mod = CreateModFromVpk(vpkFile);
                if (mod != null)
                {
                    mod.IsEnabled = true;
                    mods.Add(mod);
                }
            }

            foreach (var dir in Directory.GetDirectories(CustomFolderPath))
            {
                var dirName = Path.GetFileName(dir);

                // Skip hidden dirs and the disabled subfolder
                if (dirName.StartsWith(".") ||
                    string.Equals(dirName, "disabled", StringComparison.OrdinalIgnoreCase))
                    continue;

                var mod = CreateModFromFolder(dir);
                if (mod != null)
                {
                    mod.IsEnabled = true;
                    mods.Add(mod);
                }
            }

            // --- Scan DISABLED mods (in custom/disabled/) ---
            if (Directory.Exists(DisabledFolderPath))
            {
                foreach (var vpkFile in Directory.GetFiles(DisabledFolderPath, "*.vpk", SearchOption.TopDirectoryOnly))
                {
                    // Skip numbered chunk files
                    if (IsVpkChunkFile(vpkFile)) continue;

                    var mod = CreateModFromVpk(vpkFile);
                    if (mod != null)
                    {
                        mod.IsEnabled = false;
                        mods.Add(mod);
                    }
                }

                foreach (var dir in Directory.GetDirectories(DisabledFolderPath))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(".")) continue;

                    var mod = CreateModFromFolder(dir);
                    if (mod != null)
                    {
                        mod.IsEnabled = false;
                        mods.Add(mod);
                    }
                }
            }

            Logger.LogInfo($"Found {mods.Count} mods ({mods.Count(m => m.IsEnabled)} enabled, {mods.Count(m => !m.IsEnabled)} disabled)");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to scan for mods", ex);
        }

        return mods;
    }

    /// <summary>
    /// Returns true if this VPK is a numbered data chunk (_000, _001, _002, etc.).
    /// These are part of a multi-file VPK set and should not be shown individually.
    /// </summary>
    private static bool IsVpkChunkFile(string vpkPath)
    {
        var name = Path.GetFileNameWithoutExtension(vpkPath);
        return Regex.IsMatch(name, @"_\d{3}$");
    }

    private ModModel? CreateModFromVpk(string vpkPath)
    {
        try
        {
            var rawName = Path.GetFileNameWithoutExtension(vpkPath);

            // Strip _dir suffix for display (multi-file VPK sets use modname_dir.vpk as index)
            var isMultiFile = rawName.EndsWith("_dir", StringComparison.OrdinalIgnoreCase);
            var displayName = isMultiFile
                ? rawName[..^4]  // remove "_dir"
                : rawName;

            var fileInfo = new FileInfo(vpkPath);

            // For multi-file VPK sets, sum all chunk files for accurate size
            long totalSize = fileInfo.Length;
            if (isMultiFile)
            {
                var baseName = rawName[..^4]; // base without _dir
                var dir = Path.GetDirectoryName(vpkPath) ?? string.Empty;
                totalSize += Directory.GetFiles(dir, $"{baseName}_*.vpk", SearchOption.TopDirectoryOnly)
                    .Where(f => Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"_\d{3}$"))
                    .Sum(f => new FileInfo(f).Length);
            }

            return new ModModel
            {
                Name = displayName,
                Author = "Unknown",
                Description = isMultiFile ? "Multi-file VPK mod" : "VPK mod file",
                Version = "1.0.0",
                ModPath = vpkPath,
                LastModified = fileInfo.LastWriteTime,
                ModType = ModType.Vpk,
                ThumbnailPath = "/Resources/Assets/logo.png",
                SizeBytes = totalSize,
                Categories = new ObservableCollection<string> { "VPK" }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create mod from VPK: {vpkPath}", ex);
            return null;
        }
    }

    private ModModel? CreateModFromFolder(string folderPath)
    {
        try
        {
            var folderName = Path.GetFileName(folderPath);
            var dirInfo = new DirectoryInfo(folderPath);

            // Try to find metadata file
            string author = "Unknown", description = "Folder-based mod", version = "1.0.0";
            var metadataFile = Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLower();
                    return name.Contains("modinfo") || name.Contains("info") || name == "readme";
                });

            if (metadataFile != null)
            {
                try
                {
                    foreach (var line in File.ReadAllLines(metadataFile))
                    {
                        if (line.StartsWith("author", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                            author = line.Split(':', 2)[1].Trim();
                        else if (line.StartsWith("description", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                            description = line.Split(':', 2)[1].Trim();
                        else if (line.StartsWith("version", StringComparison.OrdinalIgnoreCase) && line.Contains(':'))
                            version = line.Split(':', 2)[1].Trim();
                    }
                }
                catch { /* use defaults */ }
            }

            // Thumbnail
            var thumbnail = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly)
                                .FirstOrDefault() ?? "/Resources/Assets/logo.png";

            // Folder size
            long folderSize = 0;
            try { folderSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { }

            return new ModModel
            {
                Name = folderName,
                Author = author,
                Description = description,
                Version = version,
                ModPath = folderPath,
                LastModified = dirInfo.LastWriteTime,
                ModType = ModType.Folder,
                ThumbnailPath = thumbnail,
                SizeBytes = folderSize,
                Categories = new ObservableCollection<string> { "Folder" }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create mod from folder: {folderPath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Toggles a mod between enabled and disabled by physically moving it
    /// between custom/ and custom/disabled/.
    /// For multi-file VPK sets (_dir.vpk + _000.vpk, _001.vpk, ...),
    /// all chunks are moved together.
    /// </summary>
    public void ToggleMod(ModModel mod)
    {
        try
        {
            if (string.IsNullOrEmpty(mod.ModPath))
            {
                Logger.LogWarning("Cannot toggle mod with empty path");
                return;
            }

            var itemName = Path.GetFileName(mod.ModPath);
            var sourceDir = Path.GetDirectoryName(mod.ModPath) ?? string.Empty;
            var targetDir = mod.IsEnabled ? DisabledFolderPath : CustomFolderPath;

            // Collect all files to move (the mod file itself + any VPK chunks)
            var filesToMove = new List<string> { mod.ModPath };

            // If this is a multi-file VPK (_dir.vpk), also move the chunk files
            var nameWithoutExt = Path.GetFileNameWithoutExtension(mod.ModPath);
            if (nameWithoutExt.EndsWith("_dir", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = nameWithoutExt[..^4]; // remove "_dir"
                var chunkFiles = Directory.GetFiles(sourceDir, $"{baseName}_*.vpk", SearchOption.TopDirectoryOnly)
                    .Where(f => IsVpkChunkFile(f));
                filesToMove.AddRange(chunkFiles);
            }

            // Move all collected files
            foreach (var filePath in filesToMove)
            {
                var destPath = Path.Combine(targetDir, Path.GetFileName(filePath));
                MoveItem(filePath, destPath);
            }

            // Update the mod's tracked path (always the _dir.vpk)
            mod.ModPath = Path.Combine(targetDir, itemName);
            mod.IsEnabled = !mod.IsEnabled;

            Logger.LogInfo($"{(mod.IsEnabled ? "Enabled" : "Disabled")} mod: {mod.Name} ({filesToMove.Count} file(s) moved)");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to toggle mod: {mod.Name}", ex);
        }
    }

    private static void MoveItem(string source, string destination)
    {
        // Guard: never operate if source and destination are the same path
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning($"MoveItem: source and destination are the same, skipping. Path: {source}");
            return;
        }

        if (File.Exists(source))
        {
            File.Move(source, destination, true);
        }
        else if (Directory.Exists(source))
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
            Directory.Move(source, destination);
        }
        else
        {
            Logger.LogWarning($"MoveItem: source does not exist: {source}");
        }
    }

    /// <summary>
    /// Installs a mod by copying a file or folder into the TF2 custom directory.
    /// </summary>
    public bool InstallMod(string sourcePath)
    {
        try
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                Logger.LogError($"Source path does not exist: {sourcePath}");
                return false;
            }

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(CustomFolderPath, fileName);

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destPath, true);
                Logger.LogInfo($"Installed mod file: {fileName}");
            }
            else
            {
                if (Directory.Exists(destPath))
                    Directory.Delete(destPath, true);
                CopyDirectory(sourcePath, destPath);
                Logger.LogInfo($"Installed mod folder: {fileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to install mod from: {sourcePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Removes a mod from disk permanently.
    /// </summary>
    public bool RemoveMod(ModModel mod)
    {
        try
        {
            if (string.IsNullOrEmpty(mod.ModPath))
                return false;

            if (File.Exists(mod.ModPath))
                File.Delete(mod.ModPath);
            else if (Directory.Exists(mod.ModPath))
                Directory.Delete(mod.ModPath, true);

            Logger.LogInfo($"Removed mod: {mod.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to remove mod: {mod.Name}", ex);
            return false;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    public void RefreshMods()
    {
        Logger.LogDebug("Refreshed mod state");
    }
}

