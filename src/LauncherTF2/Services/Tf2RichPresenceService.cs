using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using DiscordRPC.Logging;

namespace LauncherTF2.Services;

public class Tf2RichPresenceService
{
    private static Tf2RichPresenceService? _instance;
    public static Tf2RichPresenceService Instance => _instance ??= new Tf2RichPresenceService();

    private DiscordRpcClient? _client;
    private CancellationTokenSource? _cts;
    private readonly string _clientId = "1314987056020521032"; // ID from original repo or new one? Using holder for now
    // NOTE: User didn't provide Client ID. I will use a placeholder or generic one.
    // Kataiser one: 800063852028657674 (from repo public usage). 
    // I will use a placeholder const for now and add a TODO.
    private const string ClientId = "800063852028657674"; // Kataiser's ID for compatibility/testing

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
    }

    public void Start()
    {
        if (IsRpcActive) return;

        _client = new DiscordRpcClient(ClientId);
        _client.Logger = new ConsoleLogger(LogLevel.Warning) { Coloured = true };
        _client.Initialize();

        _cts = new CancellationTokenSource();
        Task.Run(() => MonitorLogLoop(_cts.Token));

        IsRpcActive = true;
        RpcStateChanged?.Invoke(true);
        UpdatePresence("Main Menu", "Idle");
    }

    public void Stop()
    {
        if (!IsRpcActive) return;

        _cts?.Cancel();
        _client?.Dispose();
        _client = null;

        IsRpcActive = false;
        RpcStateChanged?.Invoke(false);
    }

    private async Task MonitorLogLoop(CancellationToken userToken)
    {
        string logPath = Path.Combine(Tf2Path, "tf", "console.log");
        long lastSize = 0;

        // Try to handle auto-detection logic or wait for file
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ignore transient file locks
                    System.Diagnostics.Debug.WriteLine($"RPC Log Read Error: {ex.Message}");
                }
            }

            // Check game running state if pause setting is on
            if (PauseWhenGameCloses)
            {
                var tf2Proc = System.Diagnostics.Process.GetProcessesByName("hl2");
                if (tf2Proc.Length == 0 && IsRpcActive)
                {
                    // Optionally stop or just idle?
                    // Original repo pauses updates. For now we just stay alive.
                }
            }

            await Task.Delay(1000, userToken);
        }
    }

    private void ParseLine(string line)
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

    private void UpdatePresence(string details, string state)
    {
        if (_client == null || !_client.IsInitialized) return;

        // Assets

        string largeImageKey = "tf2_logo"; // Needs to be uploaded to Discord App assets or standard
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
                LargeImageKey = "tf2_logo", // Fallback to logo for safety initially
                LargeImageText = largeImageText,
                SmallImageKey = "monitor",
                SmallImageText = "Rank: Casual"
            }
        });

        StatusUpdated?.Invoke($"RPC Updated: {details} | {state}");
    }
}
