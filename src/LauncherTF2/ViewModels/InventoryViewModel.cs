using LauncherTF2.Core;
using LauncherTF2.Services;
using System;

namespace LauncherTF2.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly SteamDetectionService _steamDetectionService;

    private string _sourceUrl = "about:blank";
    public string SourceUrl
    {
        get => _sourceUrl;
        set => SetProperty(ref _sourceUrl, value);
    }

    public InventoryViewModel()
    {
        _steamDetectionService = new SteamDetectionService();
        Initialize();
    }

    private void Initialize()
    {
        string? steamId = _steamDetectionService.GetActiveSteamId();

        if (!string.IsNullOrEmpty(steamId))
        {
            SourceUrl = $"https://next.backpack.tf/profiles/{steamId}";
        }
        else
        {
            SourceUrl = "https://next.backpack.tf/";
            Logger.Log("[InventoryViewModel] No SteamID detected. Defaulting to homepage.");
        }
    }
}
