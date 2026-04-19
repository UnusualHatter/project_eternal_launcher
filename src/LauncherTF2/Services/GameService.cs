using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using LauncherTF2.Core;

namespace LauncherTF2.Services;

public class GameService
{
    private readonly SettingsService _settingsService;

    public GameService()
    {
        _settingsService = new SettingsService();
    }

    public bool LaunchTF2()
    {
        try
        {
            Logger.LogInfo("Tentando iniciar o TF2");

            var settings = _settingsService.GetSettings();
            if (settings == null)
            {
                Logger.LogError("As configurações estão nulas, não é possível iniciar o jogo");
                return false;
            }

            var userArgs = settings.LaunchArgs ?? string.Empty;
            var finalArgs = userArgs.Trim();

            if (!IsSteamPathValid(settings.SteamPath))
            {
                Logger.LogWarning($"O caminho do Steam é inválido ou não existe: {settings.SteamPath}");
            }

            // Run the steam patcher and orchestrate the steam restart + TF2 launch in background
            _ = Task.Run(() => OrchestrateSteamPatcherAndLaunch(finalArgs, settings.SteamPath));

            MinimizarJanelaParaTray();

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Erro inesperado ao iniciar o TF2", ex);
            return false;
        }
    }

    private async Task OrchestrateSteamPatcherAndLaunch(string finalArgs, string steamPath)
    {
        try
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var steamPatcherPath = Path.Combine(basePath, "native", "steam_patcher.exe");
            if (!File.Exists(steamPatcherPath))
            {
                steamPatcherPath = Path.Combine(basePath, "steam_patcher.exe");
            }

            try
            {
                if (File.Exists(steamPatcherPath))
                {
                    Logger.LogInfo($"Starting steam_patcher: {steamPatcherPath}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamPatcherPath,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                else
                {
                    Logger.LogWarning("steam_patcher.exe not found; proceeding to launch TF2 directly");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to start steam_patcher", ex);
            }

            // Avalia se a Steam está rodando no ato do Launch
            bool initialRunning = IsProcessRunning("steam");

            if (initialRunning)
            {
                Logger.LogDebug("Aguardando possível reinício rápido da Steam (pelo patcher)...");
                // Dá um intervalo muito curto de 5 a 10s para ver se o patcher vai dar kill no processo
                await WaitForProcessAsync("steam", waitForExit: true, timeoutSeconds: 6);
            }

            // O patcher pode ter matado a steam, ou ela pode nunca ter estado aberta
            bool isSteamStarting = !IsProcessRunning("steam");

            if (isSteamStarting)
            {
                Logger.LogDebug("Aguardando processo da Steam iniciar na memória...");
                await WaitForProcessAsync("steam", waitForExit: false, timeoutSeconds: 60);

                // Aguarda até que a interface principal da Steam carregue (ou timeout de 15s para Steams ocultas no Tray)
                Logger.LogDebug("Aguardando a inicialização da Steam...");
                await WaitForProcessWindowAsync("steam", timeoutSeconds: 15);

                // Um pequeno tempo de "respiro" após o processo estabilizar para fluir IPC sem fila longa.
                await Task.Delay(2000);
            }
            else
            {
                Logger.LogDebug("A Steam já estava rodando e pronta. Prosseguindo...");
            }

            // Launch TF2 via Steam URL
            TryLaunchThroughSteam(finalArgs);

            // After TF2 starts, launch pure_patcher (no -condebug needed)
            await WaitForTf2AndLaunchPurePatcher();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error while orchestrating steam patcher and TF2 launch", ex);
        }
    }

    private async Task WaitForTf2AndLaunchPurePatcher()
    {
        try
        {
            // Wait up to 120s for tf_win64 to appear
            await WaitForProcessAsync("tf_win64", waitForExit: false, timeoutSeconds: 120);

            if (!IsProcessRunning("tf_win64"))
            {
                Logger.LogWarning("tf_win64 process not detected after 120s; skipping pure_patcher launch");
                return;
            }

            // Wait for TF2 main window to appear — this happens when the game reaches the main menu
            await WaitForProcessWindowAsync("tf_win64", timeoutSeconds: 120);

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var purePatcher = Path.Combine(basePath, "native", "pure_patcher.exe");
            if (!File.Exists(purePatcher))
                purePatcher = Path.Combine(basePath, "pure_patcher.exe");

            if (!File.Exists(purePatcher))
            {
                Logger.LogWarning($"pure_patcher.exe not found at: {purePatcher}");
                return;
            }

            Logger.LogInfo($"Launching pure_patcher: {purePatcher}");
            Process.Start(new ProcessStartInfo
            {
                FileName = purePatcher,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError("Error while waiting for TF2 to launch pure_patcher", ex);
        }
    }

    public bool ValidateGameInstallation()
    {
        try
        {
            var settings = _settingsService.GetSettings();
            if (settings == null || string.IsNullOrWhiteSpace(settings.SteamPath))
            {
                Logger.LogWarning("Não foi possível validar a instalação do jogo: configurações ou caminho do Steam ausentes");
                return false;
            }

            var tf2Path = settings.SteamPath;

            if (!Directory.Exists(tf2Path))
            {
                Logger.LogWarning($"O diretório do TF2 não existe: {tf2Path}");
                return false;
            }

            var tf2Exe = Path.Combine(tf2Path, "tf_win64.exe");
            if (!File.Exists(tf2Exe))
            {
                Logger.LogWarning($"O arquivo tf_win64.exe não foi encontrado em: {tf2Path}");
                return false;
            }

            Logger.LogInfo("Instalação do jogo validada com sucesso");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Erro ao validar a instalação do jogo", ex);
            return false;
        }
    }

    public bool IsGameRunning()
    {
        try
        {
            var isRunning = IsProcessRunning("tf_win64");
            Logger.LogDebug($"Verificação de jogo em execução: {isRunning}");
            return isRunning;
        }
        catch (Exception ex)
        {
            Logger.LogError("Erro ao verificar se o jogo está em execução", ex);
            return false;
        }
    }

    private bool TryLaunchThroughSteam(string args)
    {
        // URL-encode the args portion to preserve spaces and +/- characters
        var encoded = string.IsNullOrWhiteSpace(args) ? string.Empty : Uri.EscapeDataString(args);
        var steamUrl = string.IsNullOrWhiteSpace(encoded) ? "steam://rungameid/440" : $"steam://rungameid/440//{encoded}";
        Logger.LogInfo($"Iniciando o TF2 com a URL: {steamUrl}");

        var startInfo = new ProcessStartInfo
        {
            FileName = steamUrl,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        Logger.LogInfo("Comando de início do TF2 enviado com sucesso");
        return true;
    }

    private bool StartRuntimeMonitoring()
    {
        // Injection via native Xenos was removed — runtime monitoring for injection is deprecated.
        Logger.LogInfo("Runtime injection monitoring removed (legacy Xenos injector)");
        return true;
    }

    private void MinimizarJanelaParaTray()
    {
        var mainWindow = Application.Current?.MainWindow;
        if (mainWindow == null)
        {
            Logger.LogWarning("A janela principal não foi encontrada, então não foi possível minimizar para o tray");
            return;
        }

        mainWindow.Hide();
        Logger.LogInfo("Launcher minimizado para o tray");
    }

    private static bool IsProcessRunning(string processName)
    {
        var procs = Process.GetProcessesByName(processName);
        bool running = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return running;
    }

    private static async Task WaitForProcessAsync(string processName, bool waitForExit, int timeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var isRunning = IsProcessRunning(processName);
            if (waitForExit && !isRunning) return;
            if (!waitForExit && isRunning) return;
            await Task.Delay(500);
        }
    }

    private static async Task WaitForProcessWindowAsync(string processName, int timeoutSeconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds))
        {
            var procs = Process.GetProcessesByName(processName);
            bool hasWindow = false;
            foreach (var p in procs)
            {
                p.Refresh();
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    hasWindow = true;
                }
                p.Dispose();
            }
            
            if (hasWindow) return;
            
            await Task.Delay(500);
        }
    }

    private static bool IsSteamPathValid(string? steamPath)
    {
        return !string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath);
    }
}
