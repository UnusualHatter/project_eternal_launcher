namespace LauncherTF2.Models;

public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string Version { get; set; } = "1.0.0";
}
