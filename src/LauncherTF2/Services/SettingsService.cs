using LauncherTF2.Models;

namespace LauncherTF2.Services;

public class SettingsService
{
    private const string SettingsFile = "settings.json";
    private SettingsModel _currentSettings = new SettingsModel();

    public SettingsService()
    {
        LoadSettings();
    }

    public SettingsModel GetSettings()
    {
        return _currentSettings;
    }

    public void SaveSettings(SettingsModel settings)
    {
        _currentSettings = settings;
        try
        {
            // Save to JSON
            string json = System.Text.Json.JsonSerializer.Serialize(_currentSettings);
            System.IO.File.WriteAllText(SettingsFile, json);

            // Save to Autoexec
            AutoexecWriter.WriteToAutoexec(_currentSettings, _currentSettings.SteamPath);
        }
        catch { }
    }

    public void ResetSettings()
    {
        _currentSettings = new SettingsModel();
        SaveSettings(_currentSettings);
    }

    private void LoadSettings()
    {
        try
        {
            if (System.IO.File.Exists(SettingsFile))
            {
                string json = System.Text.Json.JsonSerializer.Serialize(new SettingsModel());
                string fileContent = System.IO.File.ReadAllText(SettingsFile);
                _currentSettings = System.Text.Json.JsonSerializer.Deserialize<SettingsModel>(fileContent) ?? new SettingsModel();
            }
            else
            {
                _currentSettings = new SettingsModel();
            }
        }
        catch
        {
            _currentSettings = new SettingsModel();
        }
    }
}
