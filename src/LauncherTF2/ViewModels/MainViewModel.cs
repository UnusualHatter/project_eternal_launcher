using LauncherTF2.Core;
using System.Windows.Input;
using System.Windows;
using LauncherTF2.Services;

namespace LauncherTF2.ViewModels;

public class MainViewModel : ViewModelBase
{
    private object _currentView;
    private readonly GameService _gameService;

    public HomeViewModel HomeVM { get; }
    public InventoryViewModel InventoryVM { get; }
    public BlogViewModel BlogVM { get; }
    public ModsViewModel ModsVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public RpcViewModel RpcVM { get; }

    public bool IsHome { get; private set; } = true;

    public object CurrentView
    {
        get => _currentView;
        set
        {
            // No cleanup needed when switching away from Mods
            SetProperty(ref _currentView, value);

            bool wasHome = IsHome;
            IsHome = value == HomeVM;
            if (wasHome != IsHome)
            {
                OnPropertyChanged(nameof(IsHome));
            }
        }
    }

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
        _gameService = new GameService();
        
        HomeVM = new HomeViewModel();
        InventoryVM = new InventoryViewModel();
        BlogVM = new BlogViewModel();
        ModsVM = new ModsViewModel();
        SettingsVM = new SettingsViewModel();
        RpcVM = new RpcViewModel();

        _currentView = HomeVM;

        HomeViewCommand = new RelayCommand(o => CurrentView = HomeVM);
        InventoryViewCommand = new RelayCommand(o => CurrentView = InventoryVM);
        BlogViewCommand = new RelayCommand(o => CurrentView = BlogVM);
        ModsViewCommand = new RelayCommand(o =>
        {
            CurrentView = ModsVM;
            ModsVM.LoadMods();
        });
        SettingsViewCommand = new RelayCommand(o => CurrentView = SettingsVM);
        RestoreWindowCommand = new RelayCommand(o => RestoreWindow());
        QuitCommand = new RelayCommand(o =>
        {
            Cleanup();
            Application.Current.Shutdown();
        });

        GlobalPlayCommand = new RelayCommand(o => _gameService.LaunchTF2());

        // Clean up any zombie processes from previous sessions
        KillOrphanedPreloaders();
    }

    private void KillOrphanedPreloaders()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("casual_preloader");
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    Logger.LogInfo($"Killed orphaned preloader process: {process.Id}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to kill preloader process {process.Id}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error while killing orphaned preloaders", ex);
        }
    }

    public void Cleanup()
    {
        try
        {
            // Ensure RPC service stops
            Tf2RichPresenceService.Instance.Stop();
            
            // Stop injection monitoring
            InjectionService.Instance.Dispose();
            
            Logger.LogInfo("MainViewModel cleanup completed");
        }
        catch (Exception ex)
        {
            Logger.LogError("Error during MainViewModel cleanup", ex);
        }
    }

    private void RestoreWindow()
    {
        CurrentView = HomeVM;

        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow == null)
        {
            Logger.LogWarning("Não foi possível restaurar a janela principal porque ela não foi encontrada");
            return;
        }

        if (mainWindow.Visibility != Visibility.Visible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
        mainWindow.Focus();
    }
}
