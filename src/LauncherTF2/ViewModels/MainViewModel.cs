using LauncherTF2.Core;
using System.Windows.Input;
using System.Windows;

namespace LauncherTF2.ViewModels;

public class MainViewModel : ViewModelBase
{
    private object _currentView;

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
            if (_currentView == ModsVM && value != ModsVM)
            {
                ModsVM.Cleanup();
            }
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
    public ICommand RpcViewCommand { get; }
    public ICommand QuitCommand { get; }

    public MainViewModel()
    {
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
            ModsVM.Initialize();
        });
        SettingsViewCommand = new RelayCommand(o => CurrentView = SettingsVM);
        RpcViewCommand = new RelayCommand(o => CurrentView = RpcVM);
        QuitCommand = new RelayCommand(o =>
        {
            Cleanup();
            Application.Current.Shutdown();
        });

        // Clean up any zombie processes from previous sessions
        KillOrphanedPreloaders();
    }

    private void KillOrphanedPreloaders()
    {
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName("casual_preloader"))
            {
                try { process.Kill(); } catch { }
            }
        }
        catch { }
    }

    public void Cleanup()
    {
        ModsVM.Cleanup();
        // Ensure RPC service stops
        Services.Tf2RichPresenceService.Instance.Stop();
    }
}
