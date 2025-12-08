using System.Diagnostics;

namespace LauncherTF2.Services;

public class GameService
{
    public void LaunchTF2()
    {
        try
        {
            var settingsService = new SettingsService();
            var settings = settingsService.GetSettings();
            var args = settings.LaunchArgs ?? "";

            // Escape arguments for URL if necessary, but Steam usually handles raw strings after //
            // Example: steam://rungameid/440//-novid -high

            var steamUrl = $"steam://rungameid/440//{args}";

            Process.Start(new ProcessStartInfo
            {
                FileName = steamUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error launching game: {ex.Message}");
        }
    }
}
