using System;
using System.IO;

namespace LauncherTF2.Core;

public static class Logger
{
    private static string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_debug.log");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
        catch { }
    }
}
