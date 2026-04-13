using LauncherTF2.Models;
using LauncherTF2.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LauncherTF2.Services;

public class ModManagerService
{
    private readonly string _modsPath;
    private readonly string _modsConfigPath;
    private readonly string _enabledModsPath;
    private readonly HashSet<string> _enabledModPaths = new();
    private ModState? _modState;

    public ModManagerService()
    {
        _modsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Mods");
        _modsConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mod_state.json");
        _enabledModsPath = Path.Combine(_modsPath, ".enabled");

        InitializeDirectories();
        LoadModState();
    }

    private void InitializeDirectories()
    {
        try
        {
            if (!Directory.Exists(_modsPath))
            {
                Directory.CreateDirectory(_modsPath);
                Logger.LogInfo($"Created mods directory: {_modsPath}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize mods directory", ex);
        }
    }

    private void LoadModState()
    {
        try
        {
            if (File.Exists(_modsConfigPath))
            {
                var json = File.ReadAllText(_modsConfigPath);
                _modState = JsonSerializer.Deserialize<ModState>(json);
                
                if (_modState?.EnabledMods != null)
                {
                    foreach (var modPath in _modState.EnabledMods)
                    {
                        _enabledModPaths.Add(modPath);
                    }
                }
                
                Logger.LogInfo($"Loaded mod state with {_enabledModPaths.Count} enabled mods");
            }
            else
            {
                _modState = new ModState { EnabledMods = new List<string>() };
                Logger.LogInfo("Created new mod state");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load mod state", ex);
            _modState = new ModState { EnabledMods = new List<string>() };
        }
    }

    private void SaveModState()
    {
        try
        {
            if (_modState != null)
            {
                _modState.EnabledMods = _enabledModPaths.ToList();
                var json = JsonSerializer.Serialize(_modState, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_modsConfigPath, json);
                Logger.LogDebug("Saved mod state");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save mod state", ex);
        }
    }

    public List<ModModel> GetInstalledMods()
    {
        var mods = new List<ModModel>();

        try
        {
            if (!Directory.Exists(_modsPath))
            {
                Logger.LogWarning($"Mods directory does not exist: {_modsPath}");
                return mods;
            }

            // Scan for VPK files
            var vpkFiles = Directory.GetFiles(_modsPath, "*.vpk", SearchOption.TopDirectoryOnly);
            foreach (var vpkFile in vpkFiles)
            {
                var mod = CreateModFromVpk(vpkFile);
                if (mod != null)
                    mods.Add(mod);
            }

            // Scan for mod folders
            var directories = Directory.GetDirectories(_modsPath);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);
                
                // Skip hidden directories
                if (dirName.StartsWith("."))
                    continue;

                var mod = CreateModFromFolder(dir);
                if (mod != null)
                    mods.Add(mod);
            }

            // Set enabled state based on saved state
            foreach (var mod in mods)
            {
                mod.IsEnabled = _enabledModPaths.Contains(mod.ModPath);
            }

            Logger.LogInfo($"Found {mods.Count} mods in {_modsPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to scan for mods", ex);
        }

        return mods;
    }

    private ModModel? CreateModFromVpk(string vpkPath)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(vpkPath);
            var fileInfo = new FileInfo(vpkPath);

            return new ModModel
            {
                Name = fileName,
                Author = "Unknown",
                Description = "VPK mod file",
                Version = "1.0.0",
                ModPath = vpkPath,
                LastModified = fileInfo.LastWriteTime,
                ModType = ModType.Vpk,
                ThumbnailPath = "/Resources/Assets/logo.png"
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

            // Try to find a modinfo.txt or similar metadata file
            var metadataFile = Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("modinfo") || 
                                   Path.GetFileName(f).ToLower().Contains("info"));

            string author = "Unknown";
            string description = "Folder-based mod";
            string version = "1.0.0";

            if (metadataFile != null)
            {
                try
                {
                    var lines = File.ReadAllLines(metadataFile);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("author", StringComparison.OrdinalIgnoreCase))
                            author = line.Split(':')[1].Trim();
                        else if (line.StartsWith("description", StringComparison.OrdinalIgnoreCase))
                            description = line.Split(':')[1].Trim();
                        else if (line.StartsWith("version", StringComparison.OrdinalIgnoreCase))
                            version = line.Split(':')[1].Trim();
                    }
                }
                catch
                {
                    // Use defaults if metadata parsing fails
                }
            }

            // Look for thumbnail
            var thumbnail = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly)
                .FirstOrDefault() ?? "/Resources/Assets/logo.png";

            return new ModModel
            {
                Name = folderName,
                Author = author,
                Description = description,
                Version = version,
                ModPath = folderPath,
                LastModified = dirInfo.LastWriteTime,
                ModType = ModType.Folder,
                ThumbnailPath = thumbnail
            };
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create mod from folder: {folderPath}", ex);
            return null;
        }
    }

    public void ToggleMod(ModModel mod)
    {
        try
        {
            if (string.IsNullOrEmpty(mod.ModPath))
            {
                Logger.LogWarning("Cannot toggle mod with empty path");
                return;
            }

            if (_enabledModPaths.Contains(mod.ModPath))
            {
                _enabledModPaths.Remove(mod.ModPath);
                mod.IsEnabled = false;
                Logger.LogInfo($"Disabled mod: {mod.Name}");
            }
            else
            {
                _enabledModPaths.Add(mod.ModPath);
                mod.IsEnabled = true;
                Logger.LogInfo($"Enabled mod: {mod.Name}");
            }

            SaveModState();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to toggle mod: {mod.Name}", ex);
        }
    }

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
            var destPath = Path.Combine(_modsPath, fileName);

            if (File.Exists(sourcePath))
            {
                // Copy file
                File.Copy(sourcePath, destPath, true);
                Logger.LogInfo($"Installed mod file: {fileName}");
            }
            else
            {
                // Copy directory
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

    public bool RemoveMod(ModModel mod)
    {
        try
        {
            if (string.IsNullOrEmpty(mod.ModPath))
            {
                Logger.LogWarning("Cannot remove mod with empty path");
                return false;
            }

            if (File.Exists(mod.ModPath))
            {
                File.Delete(mod.ModPath);
                Logger.LogInfo($"Removed mod file: {mod.Name}");
            }
            else if (Directory.Exists(mod.ModPath))
            {
                Directory.Delete(mod.ModPath, true);
                Logger.LogInfo($"Removed mod folder: {mod.Name}");
            }

            _enabledModPaths.Remove(mod.ModPath);
            SaveModState();

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to remove mod: {mod.Name}", ex);
            return false;
        }
    }

    private void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(destination, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    public void RefreshMods()
    {
        LoadModState();
        Logger.LogInfo("Refreshed mod state");
    }
}

public class ModState
{
    [JsonPropertyName("enabled_mods")]
    public List<string> EnabledMods { get; set; } = new();
}
