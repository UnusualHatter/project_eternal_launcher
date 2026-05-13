using System.Text.Json.Serialization;

namespace LauncherTF2.Models;

public sealed class Profile
{
    // Short 8-char hex ID. Built-in profiles use stable hardcoded IDs.
    // User profiles use Guid.NewGuid().ToString("N")[..8].
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    // "graphics" | "network" | "audio" | "viewmodels" | "user"
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    // true for user-created profiles; false for built-in profiles
    [JsonPropertyName("isUserCreated")]
    public bool IsUserCreated { get; set; }

    // Full snapshot of every non-default SettingsModel property value.
    // Keys are SettingItem.PropertyName strings.
    // null values mean "do not apply / intentionally omitted" (e.g. aa_msaa no-op).
    [JsonPropertyName("settings")]
    public Dictionary<string, object?> Settings { get; set; } = new();

    // Launch-option flags to apply alongside this profile (e.g. "-dxlevel 90 -novid -console").
    // Only set on built-in profiles linked to native preset cfgs. Null for user profiles.
    [JsonPropertyName("launchOptions")]
    public string? LaunchOptions { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    public override string ToString() => Name;
}
