using LauncherTF2.Core;
using LauncherTF2.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace LauncherTF2.ViewModels;

public class HomeViewModel : ViewModelBase
{
    private bool _isLoading;

    public ObservableCollection<NewsItem> NewsItems { get; } = new();
    public ObservableCollection<NewModItem> NewMods { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand PlayCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenUrlCommand { get; }

    public HomeViewModel()
    {
        PlayCommand = new RelayCommand(o => ServiceLocator.Game.LaunchTF2());
        RefreshCommand = new AsyncRelayCommand(_ => LoadFeedAsync(forceRefresh: true));
        OpenUrlCommand = new RelayCommand(OpenUrl);

        _ = LoadFeedAsync(forceRefresh: false);
    }

    private async Task LoadFeedAsync(bool forceRefresh)
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            if (forceRefresh)
                ServiceLocator.HomeFeed.InvalidateCache();

            var newsTask = ServiceLocator.HomeFeed.GetSteamNewsAsync(5);
            var modsTask = ServiceLocator.HomeFeed.GetNewModsAsync(8);

            await Task.WhenAll(newsTask, modsTask);

            ReplaceItems(NewsItems, await newsTask);
            ReplaceItems(NewMods, await modsTask);

            Logger.LogInfo($"[HomeViewModel] Loaded {NewsItems.Count} news items and {NewMods.Count} mods");
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[HomeViewModel] Failed to load home feed", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }

    private void OpenUrl(object? parameter)
    {
        if (parameter is not string url || string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[HomeViewModel] Failed to open URL '{url}'", ex);
        }
    }
}
