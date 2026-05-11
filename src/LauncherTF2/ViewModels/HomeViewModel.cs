using LauncherTF2.Core;
using LauncherTF2.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

/// <summary>
/// Backs the Home tab. Surfaces three pieces of state:
///   1. A friendly greeting (time-of-day + Windows username)
///   2. Quick "is the launcher healthy?" indicators (TF2 install / Steam API / GameBanana API / mod count)
///   3. Live news + GameBanana feeds (existing).
/// Quick-action commands open the relevant folders or trigger short utilities.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private bool _isLoading;
    private string _greeting = string.Empty;
    private string _userName = string.Empty;
    private string _tagline = "Optimized and ready when you are.";
    private bool _tf2Detected;
    private bool _steamApiDetected;
    private bool _gamebananaApiDetected;
    private bool _autoexecDetected;
    private int _modsCount;

    public ObservableCollection<NewsItem> NewsItems { get; } = new();
    public ObservableCollection<NewModItem> NewMods { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string Greeting
    {
        get => _greeting;
        private set => SetProperty(ref _greeting, value);
    }

    public string UserName
    {
        get => _userName;
        private set => SetProperty(ref _userName, value);
    }

    public string Tagline
    {
        get => _tagline;
        private set => SetProperty(ref _tagline, value);
    }

    public bool Tf2Detected
    {
        get => _tf2Detected;
        private set => SetProperty(ref _tf2Detected, value);
    }

    public bool SteamApiDetected
    {
        get => _steamApiDetected;
        private set => SetProperty(ref _steamApiDetected, value);
    }

    public bool GamebananaApiDetected
    {
        get => _gamebananaApiDetected;
        private set => SetProperty(ref _gamebananaApiDetected, value);
    }

    public bool AutoexecDetected
    {
        get => _autoexecDetected;
        private set => SetProperty(ref _autoexecDetected, value);
    }

    public int ModsCount
    {
        get => _modsCount;
        private set => SetProperty(ref _modsCount, value);
    }

    public ICommand PlayCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public ICommand OpenTf2FolderCommand { get; }
    public ICommand OpenCustomFolderCommand { get; }
    public ICommand OpenAutoexecCommand { get; }
    public ICommand BackupConfigCommand { get; }

    public HomeViewModel()
    {
        PlayCommand = new RelayCommand(_ => ServiceLocator.Game.LaunchTF2());
        RefreshCommand = new AsyncRelayCommand(_ => LoadFeedAsync(forceRefresh: true));
        OpenUrlCommand = new RelayCommand(OpenUrl);
        OpenTf2FolderCommand = new RelayCommand(_ => OpenFolder(GetTfPath()));
        OpenCustomFolderCommand = new RelayCommand(_ => OpenFolder(Path.Combine(GetTfPath(), "custom")));
        OpenAutoexecCommand = new RelayCommand(_ => OpenAutoexec());
        BackupConfigCommand = new RelayCommand(_ => BackupConfig());

        BuildGreeting();
        RefreshSystemStatus();

        _ = LoadFeedAsync(forceRefresh: false);
    }

    /// <summary>
    /// Re-evaluates the three status indicators. Called once during construction;
    /// could also be wired to a refresh button if we ever expose one.
    /// </summary>
    public void RefreshSystemStatus()
    {
        var tfPath = GetTfPath();
        Tf2Detected = !string.IsNullOrWhiteSpace(tfPath) && Directory.Exists(tfPath);
        AutoexecDetected = Tf2Detected && File.Exists(Path.Combine(tfPath, "cfg", "autoexec.cfg"));
        ModsCount = CountMods(tfPath);
        Tagline = Tf2Detected
            ? "Optimized and ready when you are."
            : "TF2 path not configured — set it up in Settings.";
    }

    private static int CountMods(string tfPath)
    {
        try
        {
            var customDir = Path.Combine(tfPath, "custom");
            if (!Directory.Exists(customDir)) return 0;
            return Directory.EnumerateDirectories(customDir)
                .Count(d => !Path.GetFileName(d).Equals("disabled", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return 0;
        }
    }

    private void BuildGreeting()
    {
        var hour = DateTime.Now.Hour;
        Greeting = hour switch
        {
            < 5 => "Still up",
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening"
        };
        UserName = Capitalize(Environment.UserName);
    }

    private static string Capitalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return name.Length == 1
            ? name.ToUpperInvariant()
            : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string GetTfPath() => ServiceLocator.Settings.GetSettings().SteamPath ?? string.Empty;

    private async Task LoadFeedAsync(bool forceRefresh)
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            if (forceRefresh) ServiceLocator.HomeFeed.InvalidateCache();

            var newsTask = ServiceLocator.HomeFeed.GetSteamNewsAsync(5);
            var modsTask = ServiceLocator.HomeFeed.GetNewModsAsync(8);
            await Task.WhenAll(newsTask, modsTask);

            ReplaceItems(NewsItems, await newsTask);
            ReplaceItems(NewMods, await modsTask);

            Logger.LogInfo($"[Home] Loaded {NewsItems.Count} news, {NewMods.Count} mods");
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[Home] Feed load failed", ex);
        }
        finally
        {
            SteamApiDetected = ServiceLocator.HomeFeed.LastSteamNewsLoadSucceeded;
            GamebananaApiDetected = ServiceLocator.HomeFeed.LastGameBananaLoadSucceeded;
            IsLoading = false;
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private static void OpenUrl(object? parameter)
    {
        if (parameter is not string url || string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Home] Could not open URL '{url}'", ex);
        }
    }

    private static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (!Directory.Exists(path))
            {
                Logger.LogWarning($"[Home] Folder doesn't exist: {path}");
                return;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Home] Could not open folder '{path}'", ex);
        }
    }

    private static void OpenAutoexec()
    {
        try
        {
            var tfPath = GetTfPath();
            if (string.IsNullOrWhiteSpace(tfPath)) return;

            var cfgDir = Path.Combine(tfPath, "cfg");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);

            var autoexec = Path.Combine(cfgDir, "autoexec.cfg");
            if (!File.Exists(autoexec)) File.WriteAllText(autoexec, "// autoexec.cfg\n");

            Process.Start(new ProcessStartInfo(autoexec) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[Home] Could not open autoexec.cfg", ex);
        }
    }

    private static void BackupConfig()
    {
        try
        {
            var ok = ServiceLocator.Settings.BackupSettings();
            Logger.LogInfo(ok
                ? "[Home] Settings backup written"
                : "[Home] Settings backup skipped (nothing to back up)");
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[Home] Backup failed", ex);
        }
    }
}
