using System.Diagnostics;
using System.IO;
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

            var args = settings.LaunchArgs ?? "";

            if (!IsSteamPathValid(settings.SteamPath))
            {
                Logger.LogWarning($"O caminho do Steam é inválido ou não existe: {settings.SteamPath}");
            }

            if (!StartRuntimeMonitoring())
            {
                Logger.LogWarning("O monitoramento de runtime não pôde ser iniciado");
            }

            if (!TryLaunchThroughSteam(args))
            {
                return false;
            }

            MinimizarJanelaParaTray();

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Erro inesperado ao iniciar o TF2", ex);
            return false;
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

            var hl2Exe = Path.Combine(tf2Path, "hl2.exe");
            if (!File.Exists(hl2Exe))
            {
                Logger.LogWarning($"O arquivo hl2.exe não foi encontrado em: {tf2Path}");
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
            var processes = Process.GetProcessesByName("hl2");
            var isRunning = processes.Length > 0;

            foreach (var proc in processes)
            {
                proc.Dispose();
            }

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
        var steamUrl = $"steam://rungameid/440//{args}";
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
        try
        {
            InjectionService.Instance.StartMonitoring();
            Logger.LogInfo("Monitoramento de runtime iniciado");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Falha ao iniciar o monitoramento de runtime", ex);
            return false;
        }
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

    private static bool IsSteamPathValid(string? steamPath)
    {
        return !string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath);
    }
}
