using LauncherTF2.Services;

namespace LauncherTF2.Core;

/// <summary>
/// Lightweight composition root for shared service instances.
/// Prevents the anti-pattern of each ViewModel creating its own service copies.
/// Initialized once in App.xaml.cs.
/// </summary>
public static class ServiceLocator
{
    public static SettingsService Settings { get; private set; } = null!;
    public static GameService Game { get; private set; } = null!;
    public static SteamDetectionService SteamDetection { get; private set; } = null!;
    public static ModManagerService ModManager { get; private set; } = null!;
    public static GameBananaEnrichmentService Enrichment { get; private set; } = null!;
    public static HomeFeedService HomeFeed { get; private set; } = null!;

    /// <summary>
    /// Wires up all shared services. Must be called once during app startup.
    /// </summary>
    public static void Initialize()
    {
        Settings = new SettingsService();
        SteamDetection = new SteamDetectionService();
        Game = new GameService(Settings);
        ModManager = new ModManagerService(Settings);
        Enrichment = new GameBananaEnrichmentService();
        HomeFeed = new HomeFeedService();

        Logger.LogInfo("[ServiceLocator] All services initialized successfully");
    }
}

