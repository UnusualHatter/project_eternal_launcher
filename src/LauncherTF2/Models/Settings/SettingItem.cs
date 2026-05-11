using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LauncherTF2.Models.Settings;

/// <summary>
/// Base type for one schema-driven setting. Subclasses wrap a single property
/// on <see cref="SettingsModel"/> via a getter/setter delegate pair so the UI
/// never has to know the underlying field name.
///
/// Two-way data binding is preserved in both directions:
///   - View → model: <see cref="ToggleSetting.IsOn"/> / slider value / choice
///     setter forwards to the underlying property.
///   - Model → view: every wrapper subscribes to <see cref="SettingsModel"/>
///     PropertyChanged for its bound property name and re-raises a Value
///     change, so bulk updates from presets refresh the UI automatically.
///
/// Autoexec generation reads <see cref="EmitCvarLines"/>. A setting that is
/// off / default / nothing-to-write returns an empty enumerable, which keeps
/// the generated cfg clean (no junk lines for unused features).
/// </summary>
public abstract class SettingItem : INotifyPropertyChanged
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    /// <summary>Primary cvar name — used in tooltips and the autoexec comment header for this row.</summary>
    public string Cvar { get; init; } = "";
    /// <summary>True for settings the user should only touch knowingly. UI may collapse/hide these behind an Advanced toggle.</summary>
    public bool IsAdvanced { get; init; }

    /// <summary>
    /// True when the cvar this setting emits is blocked / inert under sv_pure
    /// (typical TF2 Casual / community pub servers). UI renders a small chip
    /// next to the title so users can predict the cvar won't take effect.
    /// </summary>
    public bool NotCasualCompatible { get; init; }

    /// <summary>
    /// Names of <see cref="SettingsModel"/> properties whose changes affect
    /// <see cref="IsEnabled"/>. Each subclass subscribes to these alongside
    /// its own bound property and re-raises IsEnabled when they change.
    /// </summary>
    public IReadOnlyList<string>? DependsOn { get; init; }

    /// <summary>
    /// Optional predicate that gates whether the user can interact with this
    /// setting. When false, the UI dims the control and disables input — used
    /// for child rows (e.g. Medic autocall threshold depends on Medic autocall).
    /// </summary>
    public Func<bool>? IsEnabledPredicate { get; init; }

    public bool IsEnabled => IsEnabledPredicate?.Invoke() ?? true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Subclasses call this from their model PropertyChanged handler so that
    /// any change to a property listed in <see cref="DependsOn"/> raises an
    /// <see cref="IsEnabled"/> notification.
    /// </summary>
    protected void RaiseIfDependency(string? propertyName)
    {
        if (DependsOn != null && propertyName != null && DependsOn.Contains(propertyName))
            OnPropertyChanged(nameof(IsEnabled));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Returns the cvar lines this setting wants written to autoexec.cfg.
    /// Settings that aren't applicable (toggle off, default value, etc.)
    /// should return an empty enumerable to keep the cfg lean.
    /// </summary>
    public virtual IEnumerable<string> EmitCvarLines() => [];
}
