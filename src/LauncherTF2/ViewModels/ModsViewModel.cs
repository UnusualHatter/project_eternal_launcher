using LauncherTF2.Core;
using LauncherTF2.Services;
using System.Diagnostics;
using System.Windows.Input;
using System.IO;

namespace LauncherTF2.ViewModels;

public class ModsViewModel : ViewModelBase
{
    private readonly ModManagerService _modService;
    private bool _isInstalled;
    private string _statusMessage;

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand InstallCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RunCommand { get; }

    public ICommand ResetPreloaderCommand { get; }

    public ModsViewModel()
    {
        _modService = new ModManagerService();
        _statusMessage = "Ready";
        CheckStatus();

        InstallCommand = new RelayCommand(async o => await Install());
        UpdateCommand = new RelayCommand(async o => await Update());
        RemoveCommand = new RelayCommand(async o => await Remove());
        RunCommand = new RelayCommand(o => Run(), o => IsInstalled);
        ResetPreloaderCommand = new RelayCommand(o => ResetPreloader());
    }

    private void ResetPreloader()
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to factory reset the Casual Preloader?\n\n" +
            "This will delete all your settings and downloaded mods for the preloader.\n" +
            "The preloader will restart as if it were the first time.",
            "Factory Reset Confirmation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                Cleanup();

                string preloaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "CasualPreloader");
                string settingsFile = Path.Combine(preloaderPath, "app_settings.json");
                string modsDir = Path.Combine(preloaderPath, "mods");
                string modsInfoFile = Path.Combine(preloaderPath, "modsinfo.json");

                if (File.Exists(settingsFile)) File.Delete(settingsFile);
                if (Directory.Exists(modsDir)) Directory.Delete(modsDir, true);
                if (File.Exists(modsInfoFile)) File.Delete(modsInfoFile);

                System.Windows.MessageBox.Show("Reset complete! The preloader will now restart.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                Initialize();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error during reset: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void CheckStatus()
    {
        IsInstalled = _modService.IsModInstalled();
        StatusMessage = IsInstalled ? "Installed" : "Not Installed";
    }

    private async Task Install()
    {
        StatusMessage = "Verifying...";
        await _modService.InstallModAsync();
        StatusMessage = "Ready";
        IsInstalled = true;
    }

    private async Task Update()
    {
        try
        {
            StatusMessage = "Pulling changes...";
            await _modService.UpdateModAsync();
            StatusMessage = "Updated!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task Remove()
    {
        StatusMessage = "Cannot remove bundled mod.";
        await Task.Delay(1000);
        StatusMessage = "Ready";
    }

    private Process? _preloaderProcess;
    public Process? PreloaderProcess
    {
        get => _preloaderProcess;
        set => SetProperty(ref _preloaderProcess, value);
    }

    private bool _isActive;

    public void Initialize()
    {
        _isActive = true;
        if (IsInstalled)
        {
            Run();
        }
    }

    private IntPtr _preloaderHwnd;
    public IntPtr PreloaderHwnd
    {
        get => _preloaderHwnd;
        set => SetProperty(ref _preloaderHwnd, value);
    }

    private async void Run()
    {
        if (_preloaderProcess != null) return;

        try
        {
            StatusMessage = "Launching...";
            var result = await _modService.RunPreloader(embedded: true);

            if (!_isActive)
            {
                try { result.Process.Kill(); result.Process.Dispose(); } catch { }
                return;
            }

            PreloaderProcess = result.Process;
            PreloaderHwnd = result.Hwnd;
            StatusMessage = "Running";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error running: {ex.Message}";
        }
    }

    public void Cleanup()
    {
        _isActive = false;
        if (_preloaderProcess != null && !_preloaderProcess.HasExited)
        {
            try
            {
                _preloaderProcess.Kill();
                _preloaderProcess.Dispose();
            }
            catch { }
            finally
            {
                _preloaderProcess = null;
                PreloaderHwnd = IntPtr.Zero;
                StatusMessage = "Ready";
            }
        }
    }

    public void HandleEmbeddingError()
    {
        StatusMessage = "Error: Embedding failed. Retrying...";
        Cleanup();
        // Optional: Retry? Or just stop. Let's just stop to be safe and let user try again.
        StatusMessage = "Error: Failed to embed window";
    }
}
