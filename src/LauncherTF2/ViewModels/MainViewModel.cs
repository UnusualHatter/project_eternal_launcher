using LauncherTF2.Core;
using System.Windows.Input;
using System.Windows;

namespace LauncherTF2.ViewModels;

/// <summary>
/// Root ViewModel — owns all tab ViewModels and handles
/// navigation, global commands, and app lifecycle.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private object _currentView;
    private DateTime _lastModsLoad = DateTime.MinValue;
    private static readonly TimeSpan ModsReloadCooldown = TimeSpan.FromSeconds(30);

    // Child ViewModels — one per tab
    public HomeViewModel HomeVM { get; }
    public InventoryViewModel InventoryVM { get; }
    public BlogViewModel BlogVM { get; }
    public ModsViewModel ModsVM { get; }
    public SettingsViewModel SettingsVM { get; }

    // True when the Home tab is active — drives sidebar background animation
    public bool IsHome { get; private set; } = true;

    public object CurrentView
    {
        get => _currentView;
        set
        {
            SetProperty(ref _currentView, value);

            bool wasHome = IsHome;
            IsHome = value == HomeVM;
            if (wasHome != IsHome)
            {
                OnPropertyChanged(nameof(IsHome));
            }
        }
    }

    // Navigation commands bound to sidebar buttons
    public ICommand HomeViewCommand { get; }
    public ICommand InventoryViewCommand { get; }
    public ICommand BlogViewCommand { get; }
    public ICommand ModsViewCommand { get; }
    public ICommand SettingsViewCommand { get; }
    public ICommand QuitCommand { get; }
    public ICommand GlobalPlayCommand { get; }
    public ICommand RestoreWindowCommand { get; }

    public MainViewModel()
    {
        HomeVM = new HomeViewModel();
        InventoryVM = new InventoryViewModel();
        BlogVM = new BlogViewModel();
        ModsVM = new ModsViewModel();
        SettingsVM = new SettingsViewModel();

        _currentView = HomeVM;

        // Tab navigation
        HomeViewCommand = new RelayCommand(o => CurrentView = HomeVM);
        InventoryViewCommand = new RelayCommand(o => CurrentView = InventoryVM);
        BlogViewCommand = new RelayCommand(o => CurrentView = BlogVM);
        ModsViewCommand = new RelayCommand(o =>
        {
            CurrentView = ModsVM;

            // Debounce mod reloads — avoids rescanning the filesystem and
            // cancelling in-flight GameBanana enrichment on every tab click
            if (DateTime.UtcNow - _lastModsLoad > ModsReloadCooldown)
            {
                ModsVM.LoadMods();
                _lastModsLoad = DateTime.UtcNow;
            }
        });
        SettingsViewCommand = new RelayCommand(o => CurrentView = SettingsVM);

        // Tray and lifecycle
        RestoreWindowCommand = new RelayCommand(o => RestoreWindow());
        QuitCommand = new RelayCommand(o =>
        {
            Logger.LogInfo("[App] User initiated shutdown from UI");
            Cleanup();
            Application.Current.Shutdown();
        });

        // Global play button in sidebar
        GlobalPlayCommand = new RelayCommand(o => ServiceLocator.Game.LaunchTF2());

        Logger.LogInfo("[App] MainViewModel initialized — all tabs ready");
    }

    /// <summary>
    /// Gracefully shuts down background services before the app exits.
    /// </summary>
    public void Cleanup()
    {
        Logger.LogInfo("[App] Cleanup completed");
    }

    /// <summary>
    /// Brings the launcher window back from the system tray.
    /// </summary>
    private void RestoreWindow()
    {
        CurrentView = HomeVM;

        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow == null)
        {
            Logger.LogWarning("[App] RestoreWindow called but MainWindow is null");
            return;
        }

        if (mainWindow.Visibility != Visibility.Visible)
            mainWindow.Show();

        if (mainWindow.WindowState == WindowState.Minimized)
            mainWindow.WindowState = WindowState.Normal;

        // Force the window to the front
        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
        mainWindow.Focus();

        Logger.LogInfo("[App] Window restored from tray");
    }
}
