using System.Text.Json.Serialization;

namespace LauncherTF2.Models;

public class GameSection
{
    [JsonPropertyName("_idRow")]
    public int Id { get; set; }

    [JsonPropertyName("_sName")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("_sIconUrl")]
    public string IconUrl { get; set; } = string.Empty;

    [JsonPropertyName("_nItemCount")]
    public int ItemCount { get; set; }

    public override string ToString()
    {
        return Name;
    }
}
