using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using LauncherTF2.Core;
using LauncherTF2.Services;

namespace LauncherTF2.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly SteamDetectionService _steamDetectionService;
    private readonly SteamInventoryService _steamInventoryService;
    private readonly InventoryPricingService _pricingService;
    private readonly List<BackpackGridItem> _allBackpackItems = [];

    private readonly Dictionary<string, InventoryPricingService.PriceSnapshot> _sessionPriceCache = new(StringComparer.OrdinalIgnoreCase);

    private string _steamId64 = string.Empty;
    private string _statusMessage = "Preparing inventory...";
    private string _cookieStatus = "Attempting Steam desktop session (cookie auth).";
    private bool _isLoading;
    private bool _isDetailPanelOpen;
    private bool _isDetailPanelExpanded = true;
    private bool _isFilterPanelOpen;
    private double _inventoryCardWidth = 156;
    private int _selectedInventoryTabIndex;
    private SortMode _currentSortMode = SortMode.NameAsc;

    private string _selectedClassFilter = "Any";
    private string _selectedCollectionFilter = "Any";
    private string _selectedExteriorFilter = "Any";
    private string _selectedQualityFilter = "Any";
    private string _selectedGradeFilter = "Any";
    private string _selectedTypeFilter = "Any";
    private double _minPriceFilter;
    private double _maxPriceFilter = 10000;

    private BackpackGridItem? _selectedItem;

    public string SteamId64
    {
        get => _steamId64;
        set => SetProperty(ref _steamId64, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CookieStatus
    {
        get => _cookieStatus;
        set => SetProperty(ref _cookieStatus, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string LoadoutUrl { get; } = "https://loadout.tf/";

    public double InventoryCardWidth
    {
        get => _inventoryCardWidth;
        set => SetProperty(ref _inventoryCardWidth, value);
    }

    public bool IsFilterPanelOpen
    {
        get => _isFilterPanelOpen;
        set => SetProperty(ref _isFilterPanelOpen, value);
    }

    public int SelectedInventoryTabIndex
    {
        get => _selectedInventoryTabIndex;
        set
        {
            if (SetProperty(ref _selectedInventoryTabIndex, value))
            {
                OnPropertyChanged(nameof(IsBackpackTabActive));
                OnPropertyChanged(nameof(IsLoadoutTabActive));
            }
        }
    }

    public bool IsBackpackTabActive => SelectedInventoryTabIndex == 0;
    public bool IsLoadoutTabActive => SelectedInventoryTabIndex == 1;

    public string CurrentSortLabel => _currentSortMode switch
    {
        SortMode.NameAsc => "Sort: Name A-Z",
        SortMode.NameDesc => "Sort: Name Z-A",
        SortMode.PriceAsc => "Sort: Price Low-High",
        SortMode.PriceDesc => "Sort: Price High-Low",
        _ => "Sort"
    };

    public ObservableCollection<BackpackGridItem> BackpackItems { get; } = [];
    public ObservableCollection<StorePriceRow> SelectedItemStorePrices { get; } = [];

    public ObservableCollection<string> ClassFilterOptions { get; } = ["Any"];
    public ObservableCollection<string> CollectionFilterOptions { get; } = ["Any"];
    public ObservableCollection<string> ExteriorFilterOptions { get; } = ["Any"];
    public ObservableCollection<string> QualityFilterOptions { get; } = ["Any"];
    public ObservableCollection<string> GradeFilterOptions { get; } = ["Any"];
    public ObservableCollection<string> TypeFilterOptions { get; } = ["Any"];

    public bool HasBackpackItems => BackpackItems.Count > 0;

    public string SelectedClassFilter
    {
        get => _selectedClassFilter;
        set
        {
            if (SetProperty(ref _selectedClassFilter, value))
                ApplyCurrentView();
        }
    }

    public string SelectedCollectionFilter
    {
        get => _selectedCollectionFilter;
        set
        {
            if (SetProperty(ref _selectedCollectionFilter, value))
                ApplyCurrentView();
        }
    }

    public string SelectedExteriorFilter
    {
        get => _selectedExteriorFilter;
        set
        {
            if (SetProperty(ref _selectedExteriorFilter, value))
                ApplyCurrentView();
        }
    }

    public string SelectedQualityFilter
    {
        get => _selectedQualityFilter;
        set
        {
            if (SetProperty(ref _selectedQualityFilter, value))
                ApplyCurrentView();
        }
    }

    public string SelectedGradeFilter
    {
        get => _selectedGradeFilter;
        set
        {
            if (SetProperty(ref _selectedGradeFilter, value))
                ApplyCurrentView();
        }
    }

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
                ApplyCurrentView();
        }
    }

    public double MinPriceFilter
    {
        get => _minPriceFilter;
        set
        {
            if (SetProperty(ref _minPriceFilter, value))
                ApplyCurrentView();
        }
    }

    public double MaxPriceFilter
    {
        get => _maxPriceFilter;
        set
        {
            if (SetProperty(ref _maxPriceFilter, value))
                ApplyCurrentView();
        }
    }

    public BackpackGridItem? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(HasSelectedItem));
                OnPropertyChanged(nameof(DetailPanelToggleGlyph));
            }
        }
    }

    public bool HasSelectedItem => SelectedItem != null;

    public bool IsDetailPanelOpen
    {
        get => _isDetailPanelOpen;
        set
        {
            if (SetProperty(ref _isDetailPanelOpen, value))
            {
                OnPropertyChanged(nameof(DetailPanelWidth));
            }
        }
    }

    public bool IsDetailPanelExpanded
    {
        get => _isDetailPanelExpanded;
        set
        {
            if (SetProperty(ref _isDetailPanelExpanded, value))
            {
                OnPropertyChanged(nameof(DetailPanelWidth));
                OnPropertyChanged(nameof(DetailPanelToggleGlyph));
            }
        }
    }

    public double DetailPanelWidth => IsDetailPanelOpen ? (IsDetailPanelExpanded ? 360 : 92) : 0;

    public string DetailPanelToggleGlyph => IsDetailPanelExpanded ? ">" : "<";

    public ICommand RefreshBackpackCommand { get; }
    public ICommand SelectItemCommand { get; }
    public ICommand ToggleDetailPanelCommand { get; }
    public ICommand CloseDetailPanelCommand { get; }
    public ICommand OpenStoreLinkCommand { get; }
    public ICommand ToggleFilterPanelCommand { get; }
    public ICommand ToggleSortCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand SwitchToBackpackTabCommand { get; }
    public ICommand SwitchToLoadoutTabCommand { get; }

    public InventoryViewModel()
    {
        _steamDetectionService = new SteamDetectionService();
        _steamInventoryService = new SteamInventoryService();
        _pricingService = new InventoryPricingService();

        BackpackItems.CollectionChanged += OnBackpackItemsChanged;

        RefreshBackpackCommand = new RelayCommand(async _ => await RefreshBackpackAsync(), _ => !IsLoading);
        SelectItemCommand = new RelayCommand(async o => await SelectItemAsync(o as BackpackGridItem));
        ToggleDetailPanelCommand = new RelayCommand(_ => ToggleDetailPanel());
        CloseDetailPanelCommand = new RelayCommand(_ => CloseDetailPanel());
        OpenStoreLinkCommand = new RelayCommand(o => OpenStoreLink(o?.ToString()));
        ToggleFilterPanelCommand = new RelayCommand(_ => IsFilterPanelOpen = !IsFilterPanelOpen);
        ToggleSortCommand = new RelayCommand(_ => CycleSortMode());
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        SwitchToBackpackTabCommand = new RelayCommand(_ => SelectedInventoryTabIndex = 0);
        SwitchToLoadoutTabCommand = new RelayCommand(_ => SelectedInventoryTabIndex = 1);

        Initialize();
    }

    private void Initialize()
    {
        SteamId64 = _steamDetectionService.GetActiveSteamId() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(SteamId64))
        {
            StatusMessage = "No active Steam user detected. Open Steam and try again.";
            CookieStatus = "Steam cookie status unavailable.";
            Logger.LogWarning("[Inventory] Active Steam user was not detected from registry.");
            return;
        }

        StatusMessage = "Ready. Click Refresh Backpack to load your inventory.";
        CookieStatus = "Waiting to fetch inventory.";
    }

    private async Task RefreshBackpackAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamId64))
        {
            StatusMessage = "No SteamID64 detected. Open Steam and try again.";
            return;
        }

        IsLoading = true;
        _allBackpackItems.Clear();
        BackpackItems.Clear();
        SelectedItemStorePrices.Clear();
        SelectedItem = null;
        IsDetailPanelOpen = false;

        StatusMessage = "Loading backpack from Steam Community...";

        try
        {
            var result = await _steamInventoryService.GetBackpackItemsAsync(SteamId64.Trim());

            foreach (var item in result.Items)
            {
                var vmItem = new BackpackGridItem
                {
                    Name = item.Name,
                    ImageUrl = item.ImageUrl,
                    BorderColorHex = item.BorderColorHex,
                    IsEquipped = item.IsEquipped,
                    QualityName = item.QualityName,
                    Type = item.Type,
                    TradableLabel = item.Tradable ? "Tradable" : "Not tradable",
                    ItemKey = BuildItemKey(item.Name, item.QualityName, item.Tradable),
                    Rarity = item.Rarity,
                    UnusualEffect = item.UnusualEffect,
                    Paint = item.Paint,
                    KillstreakTier = item.KillstreakTier,
                    KillstreakSheen = item.KillstreakSheen,
                    Killstreaker = item.Killstreaker,
                    Spell = item.Spell,
                    CraftNumber = item.CraftNumber,
                    EquippedOn = item.EquippedOn
                };

                vmItem.PricePureLabel = "Pure: loading...";
                _allBackpackItems.Add(vmItem);
            }

            RefreshFilterOptions();
            ApplyCurrentView();

            CookieStatus = result.CookieStatus;
            StatusMessage = string.IsNullOrWhiteSpace(result.Message)
                ? $"Backpack loaded: {BackpackItems.Count} items."
                : result.Message;

            _ = PrimeItemPriceLabelsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("[Inventory] Failed to fetch backpack", ex);

            if (ex.Message.Contains("Steam must be running and logged in", StringComparison.OrdinalIgnoreCase))
            {
                CookieStatus = "Steam cookies not found.";
                StatusMessage = "Steam must be running and logged in.";
            }
            else if (ex.Message.Contains("Rate limited", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Rate limited - try again in a few minutes.";
            }
            else
            {
                StatusMessage = ex.Message;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PrimeItemPriceLabelsAsync()
    {
        if (_allBackpackItems.Count == 0)
            return;

        await Task.Delay(200);

        using var limiter = new SemaphoreSlim(5);

        var tasks = _allBackpackItems.Select(async item =>
        {
            if (_sessionPriceCache.TryGetValue(item.ItemKey, out var cached))
            {
                item.PricePureLabel = $"Pure: {cached.PureSummary}";
                return;
            }

            await limiter.WaitAsync();
            try
            {
                var snapshot = await _pricingService.GetPriceSnapshotAsync(item.Name, item.QualityName, item.TradableLabel == "Tradable");
                _sessionPriceCache[item.ItemKey] = snapshot;
                item.PricePureLabel = $"Pure: {snapshot.PureSummary}";
            }
            catch
            {
                item.PricePureLabel = "Pure: unavailable";
            }
            finally
            {
                limiter.Release();
            }
        });

        await Task.WhenAll(tasks);
        ApplyCurrentView();
    }

    private async Task SelectItemAsync(BackpackGridItem? item)
    {
        if (item == null)
            return;

        if (SelectedItem == item)
        {
            CloseDetailPanel();
            return;
        }

        SelectedItem = item;
        IsDetailPanelOpen = true;
        IsDetailPanelExpanded = true;

        await LoadSelectedItemPricesAsync(item);
    }

    private void ToggleDetailPanel()
    {
        if (!IsDetailPanelOpen)
            return;

        IsDetailPanelExpanded = !IsDetailPanelExpanded;
    }

    private void CloseDetailPanel()
    {
        IsDetailPanelOpen = false;
        SelectedItem = null;
        SelectedItemStorePrices.Clear();
    }

    private static void OpenStoreLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.IsWellFormedUriString(url, UriKind.Absolute))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Inventory] Failed to open store link: {url}", ex);
        }
    }

    private async Task LoadSelectedItemPricesAsync(BackpackGridItem item)
    {
        SelectedItemStorePrices.Clear();



        var initialRows = InventoryPricingService.StoreOrder.Select(name => new StorePriceRow
        {
            StoreName = name,
            Status = "Loading...",
            IsLoading = true,
            CanRetry = false,
            ListingUrl = InventoryPricingService.GetStoreSearchUrl(name, item.Name)
        }).ToList();

        foreach (var row in initialRows)
            SelectedItemStorePrices.Add(row);

        var snapshot = _sessionPriceCache.TryGetValue(item.ItemKey, out var cached)
            ? cached
            : await _pricingService.GetPriceSnapshotAsync(item.Name, item.QualityName, item.TradableLabel == "Tradable");

        _sessionPriceCache[item.ItemKey] = snapshot;
        item.PricePureLabel = $"Pure: {snapshot.PureSummary}";

        foreach (var row in SelectedItemStorePrices)
        {
            if (snapshot.StoreResults.TryGetValue(row.StoreName, out var result))
            {
                ApplyStoreResult(row, result);
            }
            else
            {
                row.IsLoading = false;
                row.Status = "Unavailable";
            }
        }
    }

    private static void ApplyStoreResult(StorePriceRow row, InventoryPricingService.PriceResult result)
    {
        row.IsLoading = false;
        row.Status = result.Status;
        row.PriceKeys = result.PriceKeys;
        row.PriceRef = result.PriceRef;
        row.ListingUrl = !string.IsNullOrWhiteSpace(result.ListingUrl) ? result.ListingUrl : result.FallbackUrl;
        row.CanRetry = false;
    }

    private void CycleSortMode()
    {
        _currentSortMode = _currentSortMode switch
        {
            SortMode.NameAsc => SortMode.NameDesc,
            SortMode.NameDesc => SortMode.PriceAsc,
            SortMode.PriceAsc => SortMode.PriceDesc,
            _ => SortMode.NameAsc
        };

        OnPropertyChanged(nameof(CurrentSortLabel));
        ApplyCurrentView();
    }

    private void ClearFilters()
    {
        SelectedClassFilter = "Any";
        SelectedCollectionFilter = "Any";
        SelectedExteriorFilter = "Any";
        SelectedQualityFilter = "Any";
        SelectedGradeFilter = "Any";
        SelectedTypeFilter = "Any";
        MinPriceFilter = 0;
        MaxPriceFilter = 10000;
    }

    private void RefreshFilterOptions()
    {
        ReplaceOptions(QualityFilterOptions, _allBackpackItems.Select(i => i.QualityName));
        ReplaceOptions(TypeFilterOptions, _allBackpackItems.Select(i => i.Type));
        ReplaceOptions(ClassFilterOptions, _allBackpackItems.Select(i => i.EquippedOn));
        ReplaceOptions(CollectionFilterOptions, _allBackpackItems.Select(i => i.Rarity));
        ReplaceOptions(GradeFilterOptions, _allBackpackItems.Select(i => i.Rarity));
        ReplaceOptions(ExteriorFilterOptions, _allBackpackItems.Select(i => DetectExterior(i.Name)));
    }

    private void ApplyCurrentView()
    {
        IEnumerable<BackpackGridItem> query = _allBackpackItems;

        if (!IsAny(SelectedQualityFilter))
            query = query.Where(i => string.Equals(i.QualityName, SelectedQualityFilter, StringComparison.OrdinalIgnoreCase));

        if (!IsAny(SelectedTypeFilter))
            query = query.Where(i => string.Equals(i.Type, SelectedTypeFilter, StringComparison.OrdinalIgnoreCase));

        if (!IsAny(SelectedClassFilter))
            query = query.Where(i => (i.EquippedOn ?? string.Empty).Contains(SelectedClassFilter, StringComparison.OrdinalIgnoreCase));

        if (!IsAny(SelectedCollectionFilter))
            query = query.Where(i => string.Equals(i.Rarity, SelectedCollectionFilter, StringComparison.OrdinalIgnoreCase));

        if (!IsAny(SelectedGradeFilter))
            query = query.Where(i => string.Equals(i.Rarity, SelectedGradeFilter, StringComparison.OrdinalIgnoreCase));

        if (!IsAny(SelectedExteriorFilter))
            query = query.Where(i => string.Equals(DetectExterior(i.Name), SelectedExteriorFilter, StringComparison.OrdinalIgnoreCase));

        query = query.Where(i =>
        {
            var price = ParsePureUsd(i.PricePureLabel);
            return price >= (decimal)MinPriceFilter && price <= (decimal)MaxPriceFilter;
        });

        query = _currentSortMode switch
        {
            SortMode.NameDesc => query.OrderByDescending(i => i.Name),
            SortMode.PriceAsc => query.OrderBy(i => ParsePureUsd(i.PricePureLabel)).ThenBy(i => i.Name),
            SortMode.PriceDesc => query.OrderByDescending(i => ParsePureUsd(i.PricePureLabel)).ThenBy(i => i.Name),
            _ => query.OrderBy(i => i.Name)
        };

        BackpackItems.Clear();
        foreach (var item in query)
            BackpackItems.Add(item);
    }

    private static decimal ParsePureUsd(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return 0m;

        var cleaned = label
            .Replace("Pure:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value;

        cleaned = cleaned.Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            ? value
            : 0m;
    }

    private static string DetectExterior(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Any";

        var exteriors = new[]
        {
            "Factory New",
            "Minimal Wear",
            "Field-Tested",
            "Well-Worn",
            "Battle Scarred"
        };

        return exteriors.FirstOrDefault(e => name.Contains(e, StringComparison.OrdinalIgnoreCase)) ?? "Any";
    }

    private static bool IsAny(string value)
        => string.IsNullOrWhiteSpace(value) || value.Equals("Any", StringComparison.OrdinalIgnoreCase);

    private static void ReplaceOptions(ObservableCollection<string> target, IEnumerable<string?> source)
    {
        var values = source
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        target.Clear();
        target.Add("Any");
        foreach (var value in values)
            target.Add(value);
    }

    private static string BuildItemKey(string name, string quality, bool tradable)
        => $"{name}|{quality}|{(tradable ? "T" : "NT")}";

    private void OnBackpackItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasBackpackItems));
    }

    public class BackpackGridItem : ViewModelBase
    {
        private string _pricePureLabel = "Pure: unavailable";

        public string ItemKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string BorderColorHex { get; set; } = "#444444";
        public bool IsEquipped { get; set; }
        public string QualityName { get; set; } = "Unknown";
        public string Type { get; set; } = "Unknown";
        public string TradableLabel { get; set; } = "Unknown";

        public string? Rarity { get; set; }
        public string? UnusualEffect { get; set; }
        public string? Paint { get; set; }
        public string? KillstreakTier { get; set; }
        public string? KillstreakSheen { get; set; }
        public string? Killstreaker { get; set; }
        public string? Spell { get; set; }
        public string? CraftNumber { get; set; }
        public string? EquippedOn { get; set; }

        public string PricePureLabel
        {
            get => _pricePureLabel;
            set => SetProperty(ref _pricePureLabel, value);
        }
    }

    public class StorePriceRow : ViewModelBase
    {
        private string _status = "Loading...";
        private string _priceKeys = string.Empty;
        private string _priceRef = string.Empty;
        private bool _isLoading;
        private bool _canRetry;
        private string _listingUrl = string.Empty;

        public string StoreName { get; set; } = string.Empty;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string PriceKeys
        {
            get => _priceKeys;
            set => SetProperty(ref _priceKeys, value);
        }

        public string PriceRef
        {
            get => _priceRef;
            set => SetProperty(ref _priceRef, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool CanRetry
        {
            get => _canRetry;
            set => SetProperty(ref _canRetry, value);
        }

        public string ListingUrl
        {
            get => _listingUrl;
            set
            {
                if (SetProperty(ref _listingUrl, value))
                {
                    OnPropertyChanged(nameof(HasListingUrl));
                }
            }
        }

        public bool HasListingUrl => !string.IsNullOrWhiteSpace(ListingUrl);
    }

    private enum SortMode
    {
        NameAsc,
        NameDesc,
        PriceAsc,
        PriceDesc
    }
}
