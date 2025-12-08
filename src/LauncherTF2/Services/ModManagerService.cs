using LauncherTF2.Models;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace LauncherTF2.Services;

public class ModManagerService
{
    private const string PreloaderFolderName = @"Resources\CasualPreloader";
    private const string MainScript = "casual_preloader.exe";

    private string GetInstallPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreloaderFolderName);
    }

    public bool IsModInstalled()
    {
        string path = Path.Combine(GetInstallPath(), MainScript);
        return File.Exists(path);
    }

    public async Task InstallModAsync()
    {
        await Task.CompletedTask;
    }

    public async Task UpdateModAsync()
    {
        string installPath = GetInstallPath();
        if (Directory.Exists(Path.Combine(installPath, ".git")))
        {
            await Task.Run(() =>
            {
                var info = new ProcessStartInfo("git", "pull")
                {
                    WorkingDirectory = installPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(info)?.WaitForExit();
            });
        }
    }

    public async Task RemoveModAsync()
    {
        await Task.CompletedTask;
    }

    public async Task<(Process Process, IntPtr Hwnd)> RunPreloader(bool embedded = false)
    {
        string path = Path.Combine(GetInstallPath(), MainScript);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Casual Preloader executable not found.", path);
        }

        var settingsService = new SettingsService();
        string tfPath = settingsService.GetSettings().SteamPath;
        if (string.IsNullOrEmpty(tfPath))
        {
            tfPath = @"C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2\tf";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = $"{(embedded ? " --embedded" : "")} \"--tf-dir={tfPath.TrimEnd('\\')}\"",
            WorkingDirectory = GetInstallPath(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = embedded
        };

        Process? process = null;
        try
        {
            process = Process.Start(startInfo);

            if (process == null) throw new InvalidOperationException("Failed to start process.");

            IntPtr hwnd = IntPtr.Zero;
            if (embedded)
            {
                await Task.Run(() =>
                {
                    var startTime = DateTime.Now;
                    while (!process.HasExited && (DateTime.Now - startTime).TotalSeconds < 10)
                    {
                        string? line = process.StandardOutput.ReadLine();
                        if (line != null)
                        {
                            string cleanLine = line.Trim();
                            if (cleanLine.StartsWith("WINDOW_ID:"))
                            {
                                if (long.TryParse(cleanLine.Substring(10), out long id))
                                {
                                    IntPtr potentialHwnd = new IntPtr(id);
                                    if (IsWindow(potentialHwnd))
                                    {
                                        hwnd = potentialHwnd;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                });

                if (hwnd == IntPtr.Zero)
                {
                    try { process.Kill(); } catch { }
                    throw new Exception("Failed to capture Casual Preloader window handle (or window invalidated).");
                }
            }

            return (process, hwnd);
        }
        catch
        {
            process?.Dispose();
            throw;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
}
