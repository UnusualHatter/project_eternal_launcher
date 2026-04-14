using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
using LauncherTF2.Views;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

/// <summary>
/// ViewModel for the Mod Library tab — manages the local mod collection,
/// filtering, toggling, removing, and installing mods via drag & drop.
/// </summary>
public class ModsViewModel : ViewModelBase
{
    private readonly ModManagerService _modService;
    private readonly GameBananaEnrichmentService _enrichmentService;
    private ObservableCollection<ModModel> _allMods;
    private ICollectionView _filteredModsView;
    private string _searchQuery = string.Empty;
    private string _currentFilter = "All";
    private bool _isGridView = true;
    private bool _isDropPanelExpanded = true;
    private bool _isInstalling;
    private string _statusMessage = string.Empty;

    public ObservableCollection<ModModel> AllMods
    {
        get => _allMods;
        set => SetProperty(ref _allMods, value);
    }

    public ICollectionView FilteredModsView
    {
        get => _filteredModsView;
        set => SetProperty(ref _filteredModsView, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                FilteredModsView?.Refresh();
            }
        }
    }

    public string CurrentFilter
    {
        get => _currentFilter;
        set
        {
            if (SetProperty(ref _currentFilter, value))
            {
                FilteredModsView?.Refresh();
            }
        }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    public bool IsDropPanelExpanded
    {
        get => _isDropPanelExpanded;
        set => SetProperty(ref _isDropPanelExpanded, value);
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set => SetProperty(ref _isInstalling, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Computed stats
    public int TotalModsCount => _allMods?.Count ?? 0;
    public int EnabledCount => _allMods?.Count(m => m.IsEnabled) ?? 0;
    public int DisabledCount => TotalModsCount - EnabledCount;

    public string CustomFolderPath => _modService.CustomFolderPath;

    // Commands
    public ICommand ToggleViewModeCommand { get; }
    public ICommand ToggleModActivationCommand { get; }
    public ICommand RemoveModCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenModsFolderCommand { get; }
    public ICommand ChangeFilterCommand { get; }
    public ICommand ToggleDropPanelCommand { get; }

    public ModsViewModel()
    {
        _modService = new ModManagerService();
        _enrichmentService = new GameBananaEnrichmentService();
        _allMods = new ObservableCollection<ModModel>();
        _filteredModsView = CollectionViewSource.GetDefaultView(_allMods);
        _filteredModsView.Filter = FilterMod;

        ToggleViewModeCommand = new RelayCommand(_ => IsGridView = !IsGridView);
        ChangeFilterCommand = new RelayCommand(o => CurrentFilter = o?.ToString() ?? "All");
        ToggleDropPanelCommand = new RelayCommand(_ => IsDropPanelExpanded = !IsDropPanelExpanded);

        ToggleModActivationCommand = new RelayCommand(o =>
        {
            if (o is ModModel mod)
            {
                _modService.ToggleMod(mod);
                RefreshStats();
            }
        });

        RemoveModCommand = new RelayCommand(o =>
        {
            if (o is ModModel mod)
            {
                var confirmed = ConfirmDialog.Show(
                    "Remove Mod",
                    $"Are you sure you want to permanently remove \"{mod.Name}\"?\nThis will delete the mod files from your disk.");

                if (confirmed)
                {
                    var success = _modService.RemoveMod(mod);
                    if (success)
                    {
                        _allMods.Remove(mod);
                        RefreshStats();
                        StatusMessage = $"Removed: {mod.Name}";
                    }
                    else
                    {
                        StatusMessage = $"Failed to remove: {mod.Name}";
                    }
                }
            }
        });

        RefreshCommand = new RelayCommand(_ => LoadMods());

        OpenModsFolderCommand = new RelayCommand(_ =>
        {
            try
            {
                var path = _modService.CustomFolderPath;
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                else
                {
                    StatusMessage = "TF2 custom folder not found. Check your Steam path in Settings.";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open mods folder", ex);
                StatusMessage = "Could not open mods folder.";
            }
        });

        // Initialize mod list
        LoadMods();
    }

    /// <summary>
    /// Loads / refreshes the installed mods list from the TF2 custom folder.
    /// </summary>
    public void LoadMods()
    {
        _allMods.Clear();
        _modService.RefreshMods();

        foreach (var mod in _modService.GetInstalledMods()
                     .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allMods.Add(mod);
        }

        FilteredModsView?.Refresh();
        RefreshStats();

        StatusMessage = _allMods.Count > 0
            ? $"Loaded {_allMods.Count} mods from {_modService.CustomFolderPath}"
            : "No mods installed.";

        // Enrich in background — cards update progressively as metadata arrives
        // Fire-and-forget with explicit exception logging so errors are never silently swallowed
        var modsToEnrich = _allMods.ToList();
        Task.Run(() => EnrichModsAsync(modsToEnrich)).ContinueWith(t =>
        {
            if (t.Exception != null)
                Logger.LogError("EnrichModsAsync crashed", t.Exception.Flatten());
        }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Enriches each mod with GameBanana metadata (thumbnail + author) in the background.
    /// Runs concurrently with max 3 simultaneous requests.
    /// Uses Dispatcher.BeginInvoke for fire-and-forget UI updates (no deadlock risk).
    /// </summary>
    private async Task EnrichModsAsync(List<ModModel> mods)
    {
        if (mods.Count == 0) return;

        Logger.LogInfo($"Starting GameBanana enrichment for {mods.Count} mods");

        var dispatcher = System.Windows.Application.Current.Dispatcher;
        var semaphore = new System.Threading.SemaphoreSlim(3);
        int enriched = 0;

        var tasks = mods.Select(async mod =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                string author = mod.Author;
                string thumb = mod.ThumbnailPath;
                bool gotEnriched = false;

                // All heavy work on thread pool
                var snapshot = new ModModel
                {
                    Name = mod.Name,
                    Author = mod.Author,
                    ThumbnailPath = mod.ThumbnailPath
                };

                await _enrichmentService.EnrichModAsync(snapshot).ConfigureAwait(false);

                author = snapshot.Author;
                thumb = snapshot.ThumbnailPath;
                gotEnriched = snapshot.IsEnriched;

                if (gotEnriched)
                    System.Threading.Interlocked.Increment(ref enriched);

                // Convert local path to BitmapImage on thread pool (IO already done, just decode)
                System.Windows.Media.Imaging.BitmapImage? bitmap = null;
                if (gotEnriched && !string.IsNullOrEmpty(thumb) && Path.IsPathRooted(thumb) && File.Exists(thumb))
                {
                    try
                    {
                        // Must be created on UI thread or as a frozen image
                        var uri = new Uri(thumb);
                        bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = uri;
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Make it cross-thread-safe
                    }
                    catch (Exception bex)
                    {
                        Logger.LogWarning($"Failed to build BitmapImage for '{mod.Name}': {bex.Message}");
                        bitmap = null;
                    }
                }

                // Fire-and-forget UI update — no await, no deadlock risk
                var capturedBitmap = bitmap;
                var capturedAuthor = author;
                var capturedEnriched = gotEnriched;
                dispatcher.BeginInvoke(() =>
                {
                    mod.Author = capturedAuthor;
                    mod.IsEnriched = capturedEnriched;
                    if (capturedBitmap != null)
                        mod.ThumbnailImage = capturedBitmap;
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Enrichment failed for '{mod.Name}': {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList(); // Materialize so all tasks start immediately

        await Task.WhenAll(tasks).ConfigureAwait(false);
        Logger.LogInfo($"GameBanana enrichment complete: {enriched}/{mods.Count} mods enriched");
    }

    /// <summary>
    /// Handles files/folders dropped onto the install panel.
    /// Supports .zip, .rar, .7z archives and direct folders/VPK files.
    /// </summary>
    public async Task HandleDropAsync(string[] paths)
    {
        if (IsInstalling || paths == null || paths.Length == 0)
            return;

        IsInstalling = true;
        int successCount = 0;
        int failCount = 0;

        try
        {
            foreach (var path in paths)
            {
                try
                {
                    StatusMessage = $"Installing: {Path.GetFileName(path)}...";

                    if (Directory.Exists(path))
                    {
                        // Direct folder — copy to custom
                        if (_modService.InstallMod(path))
                            successCount++;
                        else
                            failCount++;
                    }
                    else if (File.Exists(path))
                    {
                        var ext = Path.GetExtension(path).ToLowerInvariant();

                        if (ext == ".vpk")
                        {
                            // Direct VPK — copy to custom
                            if (_modService.InstallMod(path))
                                successCount++;
                            else
                                failCount++;
                        }
                        else if (ext == ".zip")
                        {
                            // ZIP — extract using built-in support
                            await Task.Run(() => ExtractZip(path));
                            successCount++;
                        }
                        else if (ext is ".rar" or ".7z" or ".7zip")
                        {
                            // RAR / 7Z — extract using SharpCompress
                            await ExtractArchiveAsync(path);
                            successCount++;
                        }
                        else
                        {
                            Logger.LogWarning($"Unsupported file type: {ext}");
                            failCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to install dropped item: {path}", ex);
                    failCount++;
                }
            }

            // Refresh the library after installing
            LoadMods();

            if (failCount == 0)
                StatusMessage = $"Installed {successCount} mod(s) successfully!";
            else
                StatusMessage = $"Installed {successCount}, failed {failCount}.";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Extracts a .zip archive into the TF2 custom folder.
    /// </summary>
    private void ExtractZip(string zipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tf2mod_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir, true);
            InstallExtractedContents(tempDir, Path.GetFileNameWithoutExtension(zipPath));
        }
        finally
        {
            TryCleanup(tempDir);
        }
    }

    /// <summary>
    /// Extracts .rar / .7z archives using SharpCompress into the TF2 custom folder.
    /// </summary>
    private async Task ExtractArchiveAsync(string archivePath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"tf2mod_{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            await Task.Run(() =>
            {
                using var stream = File.OpenRead(archivePath);
                using var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(tempDir, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            });

            InstallExtractedContents(tempDir, Path.GetFileNameWithoutExtension(archivePath));
        }
        finally
        {
            TryCleanup(tempDir);
        }
    }

    /// <summary>
    /// After extraction, determines how to install the contents with the following priority:
    /// 
    /// 1. VPK files anywhere in the archive → copy each VPK directly to custom/ (ignore folders, readmes, images)
    /// 2. Folders with TF2 structure (materials/, models/, sound/, etc.) → install those folders
    /// 3. Fallback → single top-level folder, or wrap multiple items
    /// </summary>
    private void InstallExtractedContents(string tempDir, string archiveName)
    {
        // ── Priority 1: VPK files (anywhere in the extracted tree) ──────────
        var vpkFiles = Directory.GetFiles(tempDir, "*.vpk", SearchOption.AllDirectories);
        if (vpkFiles.Length > 0)
        {
            foreach (var vpk in vpkFiles)
            {
                _modService.InstallMod(vpk);
                Logger.LogInfo($"Installed VPK from archive: {Path.GetFileName(vpk)}");
            }
            return;
        }

        // ── Priority 2: Folders that look like TF2 mods ─────────────────────
        var tf2ModFolders = FindTf2ModFolders(tempDir);
        if (tf2ModFolders.Count > 0)
        {
            foreach (var folder in tf2ModFolders)
            {
                _modService.InstallMod(folder);
                Logger.LogInfo($"Installed TF2 mod folder from archive: {Path.GetFileName(folder)}");
            }
            return;
        }

        // ── Priority 3: Fallback ─────────────────────────────────────────────
        var topDirs = Directory.GetDirectories(tempDir);
        var topFiles = Directory.GetFiles(tempDir);

        if (topDirs.Length == 1 && topFiles.Length == 0)
        {
            // Single folder — install it directly
            _modService.InstallMod(topDirs[0]);
        }
        else if (topDirs.Length > 0 || topFiles.Length > 0)
        {
            // Multiple items — wrap in a folder named after the archive
            var wrapperDir = Path.Combine(tempDir, archiveName);
            if (!Directory.Exists(wrapperDir))
            {
                Directory.CreateDirectory(wrapperDir);
                foreach (var file in topFiles)
                    File.Move(file, Path.Combine(wrapperDir, Path.GetFileName(file)));
                foreach (var dir in topDirs)
                    Directory.Move(dir, Path.Combine(wrapperDir, Path.GetFileName(dir)));
            }
            _modService.InstallMod(wrapperDir);
        }
    }

    /// <summary>
    /// Known TF2 mod subdirectories. A folder containing any of these is considered a valid TF2 mod.
    /// </summary>
    private static readonly string[] Tf2KnownSubdirs =
    [
        "materials", "models", "sound", "scripts", "cfg",
        "particles", "resource", "maps", "media", "expressions"
    ];

    /// <summary>
    /// Finds folders that contain TF2 mod structure, searching up to 2 levels deep.
    /// </summary>
    private static List<string> FindTf2ModFolders(string rootDir)
    {
        var result = new List<string>();

        // Check rootDir itself
        if (IsTf2ModFolder(rootDir))
        {
            result.Add(rootDir);
            return result;
        }

        // Check direct children
        foreach (var dir in Directory.GetDirectories(rootDir))
        {
            if (IsTf2ModFolder(dir))
            {
                result.Add(dir);
            }
            else
            {
                // One more level deep (e.g. archive → outer-folder → mod-folder)
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    if (IsTf2ModFolder(subDir))
                        result.Add(subDir);
                }
            }
        }

        return result;
    }

    private static bool IsTf2ModFolder(string path)
    {
        if (!Directory.Exists(path)) return false;

        var childNames = Directory.GetDirectories(path)
            .Select(d => Path.GetFileName(d).ToLowerInvariant())
            .ToHashSet();

        return Tf2KnownSubdirs.Any(known => childNames.Contains(known));
    }


    private static void TryCleanup(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Filter predicate for the CollectionView.
    /// </summary>
    private bool FilterMod(object obj)
    {
        if (obj is not ModModel mod)
            return false;

        // State filter
        var matchesFilter = CurrentFilter switch
        {
            "Enabled" => mod.IsEnabled,
            "Disabled" => !mod.IsEnabled,
            "VPK" => mod.ModType == ModType.Vpk,
            "Folder" => mod.ModType == ModType.Folder,
            _ => true // "All"
        };

        if (!matchesFilter) return false;

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim();
            return mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   mod.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   mod.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    /// <summary>
    /// Notifies the UI that stat properties have changed.
    /// </summary>
    private void RefreshStats()
    {
        OnPropertyChanged(nameof(TotalModsCount));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
    }
}
