using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

public class ModsViewModel : ViewModelBase
{
    private const int PageSize = 8;
    private readonly ModManagerService _modService;
    private readonly GameBananaModService _gameBananaModService;
    private readonly List<ModModel> _installedMods = new();
    private readonly List<ModModel> _onlineMods = new();
    private ObservableCollection<ModModel> _allMods;
    private ObservableCollection<ModModel> _displayedMods;
    private ICollectionView _displayedModsView;
    private int _currentPage = 1;
    private bool _hasMoreMods;
    private bool _hasMoreOnlinePages;
    private int _onlinePage = 1;
    private int _onlinePerPage;
    private int _onlineTotalRecords;
    private bool _isDownloadingMod;
    private bool _isAutoPagingFromScroll;
    private string _searchQuery = string.Empty;
    private string _currentFilter = "All";
    private bool _isGridView = true;
    private ObservableCollection<GameSection> _gameSections = new();
    private GameSection? _selectedSection;
    private string _selectedSort = "new";
    private bool _applyFiltersOnlyToInstalled;
    private bool _isLoadingOnlineCatalog;
    private string _catalogStatus = "Local catalog loaded.";

    public ObservableCollection<ModModel> AllMods
    {
        get => _allMods;
        set => SetProperty(ref _allMods, value);
    }

    public ICollectionView DisplayedModsView
    {
        get => _displayedModsView;
        set => SetProperty(ref _displayedModsView, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ResetPagination();
                _ = LoadOnlineCatalogAsync();
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
                ResetPagination();
            }
        }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    public ObservableCollection<GameSection> GameSections
    {
        get => _gameSections;
        private set => SetProperty(ref _gameSections, value);
    }

    public GameSection? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                ResetPagination();
                _ = LoadOnlineCatalogAsync();
            }
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
                ResetPagination();
                _ = LoadOnlineCatalogAsync();
            }
        }
    }

    public ObservableCollection<string> SortOptions { get; } = new(new[] { "new", "updated" });

    public bool ApplyFiltersOnlyToInstalled
    {
        get => _applyFiltersOnlyToInstalled;
        set
        {
            if (SetProperty(ref _applyFiltersOnlyToInstalled, value))
            {
                ResetPagination();
            }
        }
    }

    public bool HasMoreMods
    {
        get => _hasMoreMods;
        set => SetProperty(ref _hasMoreMods, value);
    }

    public bool IsDownloadingMod
    {
        get => _isDownloadingMod;
        set => SetProperty(ref _isDownloadingMod, value);
    }

    public bool IsLoadingOnlineCatalog
    {
        get => _isLoadingOnlineCatalog;
        set => SetProperty(ref _isLoadingOnlineCatalog, value);
    }

    public string CatalogStatus
    {
        get => _catalogStatus;
        set => SetProperty(ref _catalogStatus, value);
    }

    public string DebugPaginationStatus
    {
        get => $"Page: {_onlinePage}, HasMore: {_hasMoreOnlinePages}, PerPage: {_onlinePerPage}, Total: {_onlineTotalRecords}, DisplayedCount: {_displayedMods.Count}";
    }

    public ICommand ChangeFilterCommand { get; }
    public ICommand ToggleViewModeCommand { get; }
    public ICommand ToggleModActivationCommand { get; }
    public ICommand LoadMoreModsCommand { get; }
    public ICommand DownloadOnlineModCommand { get; }
    public ICommand OpenModPageCommand { get; }
    public ICommand RefreshCatalogCommand { get; }
    public ICommand DebugLoadMoreCommand { get; }

    public ModsViewModel()
    {
        _modService = new ModManagerService();
        _gameBananaModService = new GameBananaModService();
        _installedMods.AddRange(_modService.GetInstalledMods());
        _allMods = new ObservableCollection<ModModel>(_installedMods);
        _displayedMods = new ObservableCollection<ModModel>(_allMods.Take(PageSize));

        _displayedModsView = CollectionViewSource.GetDefaultView(_displayedMods);
        RebuildVisibleMods();

        GameSections.Add(new GameSection { Id = 0, Name = "All" });
        SelectedSection = GameSections.First();

        ChangeFilterCommand = new RelayCommand(o => CurrentFilter = o?.ToString() ?? "All");
        ToggleViewModeCommand = new RelayCommand(o => IsGridView = !IsGridView);
        ToggleModActivationCommand = new RelayCommand(o =>
        {
            if (o is ModModel mod && mod.IsInstalled)
            {
                _modService.ToggleMod(mod);
                ResetPagination();
            }
        });
        LoadMoreModsCommand = new RelayCommand(async _ => await LoadMoreAsync(), _ => HasMoreMods && !IsLoadingOnlineCatalog);
        DownloadOnlineModCommand = new RelayCommand(async o => await DownloadOnlineModAsync(o as ModModel), o => o is ModModel mod && !mod.IsInstalled && !mod.IsDownloading);
        OpenModPageCommand = new RelayCommand(o => OpenModPage(o as ModModel), o => o is ModModel mod && !string.IsNullOrWhiteSpace(mod.SourceUrl));
        RefreshCatalogCommand = new RelayCommand(async _ => await LoadOnlineCatalogAsync(), _ => !IsLoadingOnlineCatalog);
        DebugLoadMoreCommand = new RelayCommand(async _ => 
        {
            Logger.LogInfo($"[DEBUG-MANUAL] Before: {DebugPaginationStatus}");
            await LoadNextOnlinePageAsync();
            Logger.LogInfo($"[DEBUG-MANUAL] After: {DebugPaginationStatus}");
        }, _ => true);
    }

    private bool MatchesFilters(ModModel mod)
    {
        if (ApplyFiltersOnlyToInstalled && mod.SourceKind != ModSourceKind.Installed)
        {
            return false;
        }

        var searchQuery = SearchQuery.Trim();
        var matchesSearch = string.IsNullOrWhiteSpace(searchQuery) ||
                            mod.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                            mod.Author.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                            mod.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                            mod.Categories.Any(category => category.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

        bool matchesState = CurrentFilter switch
        {
            "Enabled" => mod.SourceKind == ModSourceKind.Installed && mod.IsEnabled,
            "Disabled" => mod.SourceKind == ModSourceKind.Installed && !mod.IsEnabled,
            _ => true
        };

        bool matchesCategory = SelectedSection == null ||
                                SelectedSection.Id == 0 ||
                                mod.Categories.Any(category => category.Equals(SelectedSection.Name, StringComparison.OrdinalIgnoreCase));

        return matchesSearch && matchesState && matchesCategory;
    }

    private void RebuildVisibleMods()
    {
        var filtered = _allMods
            .Where(MatchesFilters)
            .OrderBy(mod => mod.SourceKind)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalToShow = Math.Min(_currentPage * PageSize, filtered.Count);

        _displayedMods.Clear();
        foreach (var mod in filtered.Take(totalToShow))
        {
            _displayedMods.Add(mod);
        }

        HasMoreMods = totalToShow < filtered.Count || _hasMoreOnlinePages;
        CommandManager.InvalidateRequerySuggested();
    }

    private void ResetPagination()
    {
        _currentPage = 1;
        RebuildVisibleMods();
    }

    private async Task LoadMoreAsync()
    {
        if (IsLoadingOnlineCatalog || _isAutoPagingFromScroll)
        {
            return;
        }

        Logger.LogInfo($"[LoadMore] Starting - hasMoreLocal={HasMoreMods}, hasMoreOnline={_hasMoreOnlinePages}");

        var filtered = _allMods
            .Where(MatchesFilters)
            .OrderBy(mod => mod.SourceKind)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalShown = Math.Min(_currentPage * PageSize, filtered.Count);
        if (totalShown < filtered.Count)
        {
            Logger.LogInfo($"[LoadMore] Loading local page {_currentPage + 1}");
            LoadMoreMods();
            return;
        }

        if (_hasMoreOnlinePages)
        {
            Logger.LogInfo($"[LoadMore] Loading online page {_onlinePage + 1}");
            await LoadNextOnlinePageAsync();
        }
        else
        {
            Logger.LogInfo("[LoadMore] No more pages available");
        }
    }

    private void LoadMoreMods()
    {
        if (!HasMoreMods)
        {
            return;
        }

        _currentPage++;
        RebuildVisibleMods();
    }

    private async Task LoadNextOnlinePageAsync()
    {
        if (!_hasMoreOnlinePages || IsLoadingOnlineCatalog)
        {
            Logger.LogInfo($"[NextPage] Skipped - hasMore={_hasMoreOnlinePages}, loading={IsLoadingOnlineCatalog}");
            return;
        }

        IsLoadingOnlineCatalog = true;
        CatalogStatus = "Loading more online mods...";
        Logger.LogInfo($"[NextPage] Fetching page {_onlinePage + 1}");

        try
        {
            int? sectionId = SelectedSection?.Id > 0 ? SelectedSection.Id : null;
            var nextPage = _onlinePage + 1;
            var pageResult = await _gameBananaModService.GetCatalogPageAsync(sectionId, SelectedSort, SearchQuery, nextPage);

            Logger.LogInfo($"[NextPage] Got {pageResult.Mods.Count} mods, hasMore={pageResult.HasMore}");
            Logger.LogInfo($"[DEBUG] {DebugPaginationStatus}");

            _onlinePage = nextPage;
            _hasMoreOnlinePages = pageResult.HasMore;
            _onlinePerPage = pageResult.PerPage;
            _onlineTotalRecords = pageResult.TotalRecords;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _onlineMods.AddRange(pageResult.Mods);
                RebuildCatalog();
                CatalogStatus = pageResult.Mods.Count > 0
                    ? $"Loaded additional mods: {pageResult.Mods.Count} items"
                    : "No additional mods were found.";
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load next online catalog page", ex);
            CatalogStatus = "Failed to load additional mods.";
        }
        finally
        {
            IsLoadingOnlineCatalog = false;
        }
    }

    private void RebuildCatalog()
    {
        _allMods.Clear();

        foreach (var mod in _installedMods.Concat(_onlineMods)
                     .OrderBy(mod => mod.SourceKind)
                     .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase))
        {
            _allMods.Add(mod);
        }

        ResetPagination();
    }

    public void Initialize()
    {
        _ = LoadSectionsAsync();
        _ = LoadOnlineCatalogAsync();
    }

    public void Cleanup()
    {
        foreach (var mod in _onlineMods)
        {
            mod.IsDownloading = false;
        }
    }

    public async Task TryLoadMoreFromScrollAsync(double verticalOffset, double viewportHeight, double extentHeight)
    {
        if (IsLoadingOnlineCatalog || _isAutoPagingFromScroll)
        {
            return;
        }

        var shouldAutoFillPage = extentHeight <= viewportHeight + 1;
        var reachedBottom = extentHeight > viewportHeight && verticalOffset + viewportHeight >= extentHeight - 120;

        Logger.LogInfo($"[Scroll] offset={verticalOffset:F0}, viewport={viewportHeight:F0}, extent={extentHeight:F0}, reached={reachedBottom}, autoFill={shouldAutoFillPage}, hasMore={_hasMoreOnlinePages}");

        if (!shouldAutoFillPage && !reachedBottom)
        {
            return;
        }

        try
        {
            _isAutoPagingFromScroll = true;
            Logger.LogInfo("[Scroll] Triggering LoadMoreAsync");
            await LoadMoreAsync();
        }
        finally
        {
            _isAutoPagingFromScroll = false;
        }
    }

    private void OpenModPage(ModModel? mod)
    {
        if (mod == null || string.IsNullOrWhiteSpace(mod.SourceUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mod.SourceUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to open mod page: {mod.SourceUrl}", ex);
            CatalogStatus = "Could not open mod page.";
        }
    }

    private async Task DownloadOnlineModAsync(ModModel? mod)
    {
        if (mod == null || mod.IsInstalled || mod.IsDownloading || IsDownloadingMod)
        {
            return;
        }

        try
        {
            mod.IsDownloading = true;
            IsDownloadingMod = true;
            CatalogStatus = $"Downloading {mod.Name}...";

            var success = await _gameBananaModService.DownloadAndInstallModAsync(mod, _modService);

            if (success)
            {
                _installedMods.Clear();
                _installedMods.AddRange(_modService.GetInstalledMods());
                RebuildCatalog();
                CatalogStatus = $"Mod downloaded and installed: {mod.Name}";
            }
            else
            {
                CatalogStatus = $"Failed to download/install mod: {mod.Name}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error while downloading online mod: {mod.Name}", ex);
            CatalogStatus = $"Error downloading mod: {mod.Name}";
        }
        finally
        {
            mod.IsDownloading = false;
            IsDownloadingMod = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task LoadSectionsAsync()
    {
        try
        {
            var sections = await _gameBananaModService.GetSectionsAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                GameSections.Clear();
                GameSections.Add(new GameSection { Id = 0, Name = "All" });
                foreach (var section in sections.OrderBy(s => s.Name))
                {
                    GameSections.Add(section);
                }

                if (SelectedSection == null)
                {
                    SelectedSection = GameSections.FirstOrDefault();
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load sections", ex);
        }
    }

    private async Task LoadOnlineCatalogAsync()
    {
        if (IsLoadingOnlineCatalog)
        {
            return;
        }

        IsLoadingOnlineCatalog = true;
        CatalogStatus = "Loading online mods...";

        try
        {
            int? sectionId = SelectedSection?.Id > 0 ? SelectedSection.Id : null;
            Logger.LogInfo($"[LoadCatalog] Page 1 - section={sectionId}, sort={SelectedSort}, search={SearchQuery}");
            var pageResult = await _gameBananaModService.GetCatalogPageAsync(sectionId, SelectedSort, SearchQuery, 1);

            _onlinePage = 1;
            _hasMoreOnlinePages = pageResult.HasMore;
            _onlinePerPage = pageResult.PerPage;
            _onlineTotalRecords = pageResult.TotalRecords;

            Logger.LogInfo($"[LoadCatalog] Got {pageResult.Mods.Count} mods, hasMore={_hasMoreOnlinePages}, perPage={_onlinePerPage}, total={_onlineTotalRecords}");
            Logger.LogInfo($"[DEBUG] {DebugPaginationStatus}");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _onlineMods.Clear();
                _onlineMods.AddRange(pageResult.Mods);
                RebuildCatalog();
                CatalogStatus = pageResult.Mods.Count > 0
                    ? $"Online catalog loaded: {pageResult.Mods.Count} mods"
                    : "No online mods found.";
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to load online catalog", ex);
            CatalogStatus = "Failed to load online catalog.";
        }
        finally
        {
            IsLoadingOnlineCatalog = false;
        }
    }
}
