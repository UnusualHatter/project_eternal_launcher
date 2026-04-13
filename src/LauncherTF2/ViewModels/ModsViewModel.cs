using LauncherTF2.Core;
using LauncherTF2.Models;
using LauncherTF2.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

public class ModsViewModel : ViewModelBase
{
    private readonly ModManagerService _modService;
    private ObservableCollection<ModModel> _allMods;
    private ICollectionView _filteredModsView;
    private string _searchQuery = string.Empty;
    private string _currentFilter = "All"; // All, Enabled, Disabled
    private bool _isGridView = true;

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
                _filteredModsView.Refresh();
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
                _filteredModsView.Refresh();
            }
        }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    public ICommand ChangeFilterCommand { get; }
    public ICommand ToggleViewModeCommand { get; }
    public ICommand ToggleModActivationCommand { get; }

    public ModsViewModel()
    {
        _modService = new ModManagerService();
        _allMods = new ObservableCollection<ModModel>(_modService.GetInstalledMods());
        
        _filteredModsView = CollectionViewSource.GetDefaultView(_allMods);
        _filteredModsView.Filter = FilterMods;

        ChangeFilterCommand = new RelayCommand(o => CurrentFilter = o?.ToString() ?? "All");
        ToggleViewModeCommand = new RelayCommand(o => IsGridView = !IsGridView);
        ToggleModActivationCommand = new RelayCommand(o => {
            if (o is ModModel mod) {
                _modService.ToggleMod(mod);
            }
        });
    }

    private bool FilterMods(object obj)
    {
        if (obj is not ModModel mod) return false;

        // Search Filter
        bool matchesSearch = string.IsNullOrEmpty(SearchQuery) || 
                            mod.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                            mod.Author.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

        // State Filter
        bool matchesState = CurrentFilter switch
        {
            "Enabled" => mod.IsEnabled,
            "Disabled" => !mod.IsEnabled,
            _ => true
        };

        return matchesSearch && matchesState;
    }

    public void Initialize()
    {
        // For now, nothing special. In the future, we could re-scan directories.
    }

    public void Cleanup()
    {
        // Cleanup resources if any.
    }
}
