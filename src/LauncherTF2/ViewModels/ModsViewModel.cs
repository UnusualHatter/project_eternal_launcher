using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
    private readonly ModInstallationService _installService;
    private readonly GameBananaEnrichmentService _enrichmentService;
    private ObservableCollection<ModModel> _allMods;
    private ICollectionView _filteredModsView;
    private string _searchQuery = string.Empty;
    private string _currentFilter = "All";
    private bool _isGridView = true;
    private bool _isDropPanelExpanded = true;
    private bool _isInstalling;
    private string _statusMessage = string.Empty;
    private CancellationTokenSource? _enrichmentCts;
    private int _loadVersion;

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
        // Wire up shared services from the composition root
        _modService = ServiceLocator.ModManager;
        _installService = new ModInstallationService(_modService);
        _enrichmentService = ServiceLocator.Enrichment;
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
                var confirmed = Views.ConfirmDialog.Show(
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
                        Logger.LogInfo($"[Mods] Removed mod: {mod.Name}");
                    }
                    else
                    {
                        StatusMessage = $"Failed to remove: {mod.Name}";
                        Logger.LogWarning($"[Mods] Failed to remove mod: {mod.Name}");
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
                Logger.LogError("[Mods] Failed to open mods folder", ex);
                StatusMessage = "Could not open mods folder.";
            }
        });

        LoadMods();
    }

    /// <summary>
    /// Loads / refreshes the installed mods list from the TF2 custom folder.
    /// </summary>
    public void LoadMods()
    {
        CancelPreviousEnrichment();

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
        var modsToEnrich = _allMods.ToList();
        var currentVersion = Interlocked.Increment(ref _loadVersion);
        var cts = new CancellationTokenSource();
        _enrichmentCts = cts;

        _ = EnrichModsAsync(modsToEnrich, currentVersion, cts.Token).ContinueWith(t =>
        {
            if (t.Exception != null)
                Logger.LogError("[Mods] Background enrichment crashed", t.Exception.Flatten());
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Enriches each mod with GameBanana metadata (thumbnail + author) in the background.
    /// Runs concurrently with max 3 simultaneous requests.
    /// </summary>
    private async Task EnrichModsAsync(List<ModModel> mods, int loadVersion, CancellationToken cancellationToken)
    {
        if (mods.Count == 0) return;

        Logger.LogInfo($"[Mods] Starting GameBanana enrichment for {mods.Count} mods");

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            Logger.LogWarning("[Mods] Skipping enrichment — no WPF dispatcher available");
            return;
        }

        using var semaphore = new SemaphoreSlim(3);
        int enriched = 0;

        var tasks = mods.Select(async mod =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (cancellationToken.IsCancellationRequested || loadVersion != _loadVersion)
                    return;

                var snapshot = new ModModel
                {
                    Name = mod.Name,
                    Author = mod.Author,
                    ThumbnailPath = mod.ThumbnailPath
                };

                await _enrichmentService.EnrichModAsync(snapshot).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested || loadVersion != _loadVersion)
                    return;

                var author = snapshot.Author;
                var thumb = snapshot.ThumbnailPath;
                var gotEnriched = snapshot.IsEnriched;

                if (gotEnriched)
                    Interlocked.Increment(ref enriched);

                // Build BitmapImage on thread pool (frozen for cross-thread safety)
                System.Windows.Media.Imaging.BitmapImage? bitmap = null;
                if (gotEnriched && !string.IsNullOrEmpty(thumb) && Path.IsPathRooted(thumb) && File.Exists(thumb))
                {
                    try
                    {
                        var uri = new Uri(thumb);
                        bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = uri;
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                    catch (Exception bex)
                    {
                        Logger.LogWarning($"[Mods] Failed to load thumbnail for '{mod.Name}': {bex.Message}");
                        bitmap = null;
                    }
                }

                // Fire-and-forget UI update — no await, no deadlock risk
                var capturedBitmap = bitmap;
                var capturedAuthor = author;
                var capturedEnriched = gotEnriched;
                _ = dispatcher.BeginInvoke(() =>
                {
                    if (cancellationToken.IsCancellationRequested || loadVersion != _loadVersion)
                        return;

                    mod.Author = capturedAuthor;
                    mod.IsEnriched = capturedEnriched;
                    if (capturedBitmap != null)
                        mod.ThumbnailImage = capturedBitmap;
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when refreshing the mod list
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[Mods] Enrichment failed for '{mod.Name}': {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested && loadVersion == _loadVersion)
                Logger.LogInfo($"[Mods] Enrichment complete — {enriched}/{mods.Count} mods matched on GameBanana");
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("[Mods] Enrichment canceled — newer LoadMods cycle started");
        }
    }

    /// <summary>
    /// Handles files/folders dropped onto the install panel.
    /// Delegates archive extraction and installation to ModInstallationService.
    /// </summary>
    public async Task HandleDropAsync(string[] paths)
    {
        if (IsInstalling || paths == null || paths.Length == 0)
            return;

        IsInstalling = true;
        StatusMessage = "Installing...";

        try
        {
            var (success, fail) = await _installService.InstallFromPathsAsync(paths);

            LoadMods();

            StatusMessage = fail == 0
                ? $"Installed {success} mod(s) successfully!"
                : $"Installed {success}, failed {fail}.";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private void CancelPreviousEnrichment()
    {
        try
        {
            _enrichmentCts?.Cancel();
            _enrichmentCts?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[Mods] Failed to cancel previous enrichment cycle", ex);
        }
        finally
        {
            _enrichmentCts = null;
        }
    }

    /// <summary>
    /// Filter predicate for the CollectionView.
    /// </summary>
    private bool FilterMod(object obj)
    {
        if (obj is not ModModel mod)
            return false;

        var matchesFilter = CurrentFilter switch
        {
            "Enabled" => mod.IsEnabled,
            "Disabled" => !mod.IsEnabled,
            "VPK" => mod.ModType == ModType.Vpk,
            "Folder" => mod.ModType == ModType.Folder,
            _ => true // "All"
        };

        if (!matchesFilter) return false;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim();
            return mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   mod.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   mod.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private void RefreshStats()
    {
        OnPropertyChanged(nameof(TotalModsCount));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
    }
}
