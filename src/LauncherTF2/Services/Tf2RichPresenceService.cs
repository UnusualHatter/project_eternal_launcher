using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using DiscordRPC;
using DiscordRPC.Logging;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

public class Tf2RichPresenceService : IDisposable
{
    private static Tf2RichPresenceService? _instance;
    public static Tf2RichPresenceService Instance => _instance ??= new Tf2RichPresenceService();

    private DiscordRpcClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _monitoringTask;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _purePatcherLaunchedInSession;
    
    // NOTE: Uses Kataiser's public Discord Application ID for compatibility/testing
    private const string ClientId = "800063852028657674";

    // State
    public bool IsRpcActive { get; private set; }
    public string CurrentMap { get; private set; } = "Main Menu";
    public string QueueStatus { get; private set; } = "Idle";

    // Config
    public string Tf2Path { get; set; } = "";
    public bool AutoStartRpc { get; set; }
    public bool AutoStartWhenGameDetected { get; set; }
    public bool PauseWhenGameCloses { get; set; }

    // Events
    public event Action<string>? StatusUpdated;
    public event Action<bool>? RpcStateChanged;

    private Tf2RichPresenceService()
    {
    }

    public void Initialize(string tf2Path)
    {
        Tf2Path = tf2Path;
        Logger.LogInfo($"TF2 Rich Presence initialized with path: {tf2Path}");
    }

    public void Start()
    {
        lock (_lock)
        {
            if (IsRpcActive)
            {
                Logger.LogWarning("RPC is already active");
                return;
            }

            try
            {
                _client = new DiscordRpcClient(ClientId);
                _client.Logger = new ConsoleLogger(DiscordRPC.Logging.LogLevel.Info) { Coloured = true };
                
                if (!_client.Initialize())
                {
                    Logger.LogError("Failed to initialize Discord RPC client");
                    return;
                }

                _cts = new CancellationTokenSource();
                _monitoringTask = Task.Run(() => MonitorLogLoop(_cts.Token), _cts.Token);

                IsRpcActive = true;
                RpcStateChanged?.Invoke(true);
                UpdatePresence("Main Menu", "Idle");
                
                Logger.LogInfo("TF2 Rich Presence started");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start TF2 Rich Presence", ex);
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!IsRpcActive)
            {
                return;
            }

            try
            {
                _cts?.Cancel();
                
                try
                {
                    _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }

                _client?.Dispose();
                _client = null;
                _cts?.Dispose();
                _cts = null;

                IsRpcActive = false;
                RpcStateChanged?.Invoke(false);
                
                Logger.LogInfo("TF2 Rich Presence stopped");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error stopping TF2 Rich Presence", ex);
            }
        }
    }

    private async Task MonitorLogLoop(CancellationToken userToken)
    {
        string logPath = Path.Combine(Tf2Path, "tf", "console.log");
        long lastSize = 0;

        Logger.LogDebug($"Starting log monitoring for: {logPath}");

        try
        {
            while (!userToken.IsCancellationRequested)
            {
                if (File.Exists(logPath))
                {
                    try
                    {
                        using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            // Seek to end initially to ignore old history
                            if (lastSize == 0) lastSize = fs.Length;

                            if (fs.Length > lastSize)
                            {
                                fs.Seek(lastSize, SeekOrigin.Begin);
                                using (var sr = new StreamReader(fs))
                                {
                                    string? line;
                                    while ((line = await sr.ReadLineAsync()) != null)
                                    {
                                        ParseLine(line);
                                    }
                                }
                                lastSize = fs.Length;
                            }
                            else if (fs.Length < lastSize)
                            {
                                // File truncated (restarted game?)
                                lastSize = 0;
                                Logger.LogDebug("Log file truncated, resetting position");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignore transient file locks
                        Logger.LogDebug($"RPC Log Read Error: {ex.Message}");
                    }
                }
                else
                {
                    Logger.LogDebug($"Log file not found: {logPath}");
                }

                // Reset per-session flags when the game is not running
                try
                {
                    var tf2Proc = Process.GetProcessesByName("tf_win64");
                    if (tf2Proc.Length == 0)
                    {
                        _purePatcherLaunchedInSession = false;
                    }

                    foreach (var proc in tf2Proc)
                    {
                        proc.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"Error checking game process: {ex.Message}");
                }

                await Task.Delay(1000, userToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("Log monitoring loop cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in log monitoring loop", ex);
        }
    }

    private void ParseLine(string line)
    {
        try
        {
            // Simple regex or string contains matching based on original repo logic

            // Map Loading
            // "Loading map "cp_dustbowl""
            if (line.Contains("Loading map \""))
            {
                var match = Regex.Match(line, "Loading map \"(.*?)\"");
                if (match.Success)
                {
                    CurrentMap = match.Groups[1].Value;
                    QueueStatus = "In Game";
                    UpdatePresence(CurrentMap, "Playing");
                }
            }
            else if (line.Contains("Connected to"))
            {
                // "Connected to 192.168.1.1:27015"
                // Confirmation of join
                UpdatePresence(CurrentMap, "Connected");
            }
            else if (line.Contains("CTFGCClientSystem::PostInitGC"))
            {
                // Game Start / Main Menu
                CurrentMap = "Main Menu";
                QueueStatus = "Idle";
                UpdatePresence("Main Menu", "Idle");
            }
            else if (line.Contains("Connection to game coordinator established."))
            {
                // Trigger pure_patcher once per game session when GC connection is established
                if (!_purePatcherLaunchedInSession)
                {
                    _purePatcherLaunchedInSession = true;
                    Task.Run(() => TryLaunchPurePatcher());
                }
            }
            else if (line.Contains("TF_Matchmaking_Queue_Caption"))
            {
                // "TF_Matchmaking_Queue_Caption" "Queued for Casual"
                // Need to parse key values often found in console
                // This might be tricky without full KV parser, but let's try simple regex
                // "state" "Queued" 
            }
            // Additional parsing can be added

            StatusUpdated?.Invoke($"Parselog: {CurrentMap} - {QueueStatus}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error parsing log line: {line}", ex);
        }
    }

    private void TryLaunchPurePatcher()
    {
        try
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var purePatcher = Path.Combine(basePath, "native", "pure_patcher.exe");
            if (!File.Exists(purePatcher))
                purePatcher = Path.Combine(basePath, "pure_patcher.exe");

            if (!File.Exists(purePatcher))
            {
                Logger.LogWarning($"pure_patcher.exe not found at expected locations: {purePatcher}");
                return;
            }

            try
            {
                Logger.LogInfo($"Starting pure_patcher: {purePatcher}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = purePatcher,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to start pure_patcher.exe", ex);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Unexpected error while launching pure_patcher", ex);
        }
    }

    private void UpdatePresence(string details, string state)
    {
        lock (_lock)
        {
            try
            {
                if (_client == null || !_client.IsInitialized)
                {
                    Logger.LogDebug("RPC client not initialized, cannot update presence");
                    return;
                }

                // Assets
                string largeImageKey = "tf2_logo";
                string largeImageText = "Team Fortress 2";

                // Map image logic (requires map assets in Discord app)
                // For now use generic
                if (details != "Main Menu")
                {
                    largeImageKey = details.ToLower();
                }

                _client.SetPresence(new RichPresence()
                {
                    Details = details,
                    State = state,
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "tf2_logo",
                        LargeImageText = largeImageText,
                        SmallImageKey = "monitor",
                        SmallImageText = "Rank: Casual"
                    }
                });

                StatusUpdated?.Invoke($"RPC Updated: {details} | {state}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error updating Discord presence", ex);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~Tf2RichPresenceService()
    {
        Dispose();
    }
}
