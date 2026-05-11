using System.ComponentModel;

namespace LauncherTF2.Models.Settings;

/// <summary>
/// One preset option (e.g. "Competitive" network tuning).
/// <see cref="Apply"/> takes the live model and bulk-updates whichever
/// underlying properties the preset cares about.
/// </summary>
public sealed record PresetOption(
    string Id,
    string Label,
    string Description,
    Action<SettingsModel> Apply);

/// <summary>
/// Bulk-apply control. Holds the active preset id and exposes a list of
/// available presets. Selecting a preset invokes its <see cref="PresetOption.Apply"/>
/// against the live <see cref="SettingsModel"/>, which fires PropertyChanged
/// for every individual cvar wrapper that observes it.
/// </summary>
public sealed class PresetSetting : SettingItem, IDisposable
{
    private readonly SettingsModel _model;
    private readonly string _modelProperty;
    private readonly Func<string?> _get;
    private readonly Action<string?> _set;

    public IReadOnlyList<PresetOption> Presets { get; init; } = [];

    public PresetSetting(SettingsModel model, string modelProperty, Func<string?> get, Action<string?> set)
    {
        _model = model;
        _modelProperty = modelProperty;
        _get = get;
        _set = set;
        _model.PropertyChanged += OnModelPropertyChanged;
    }

    public string? SelectedId
    {
        get => _get();
        set
        {
            if (string.Equals(_get(), value, StringComparison.OrdinalIgnoreCase)) return;
            _set(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Selected));

            var match = Presets.FirstOrDefault(p => string.Equals(p.Id, value, StringComparison.OrdinalIgnoreCase));
            match?.Apply(_model);
        }
    }

    public PresetOption? Selected =>
        Presets.FirstOrDefault(p => string.Equals(p.Id, _get(), StringComparison.OrdinalIgnoreCase));

    public void ApplyById(string id)
    {
        // Force a re-apply even when the same preset is selected — useful for the
        // "Re-apply" affordance when the user has tweaked individual cvars.
        var match = Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (match == null) return;
        match.Apply(_model);
        if (!string.Equals(_get(), id, StringComparison.OrdinalIgnoreCase))
        {
            _set(id);
            OnPropertyChanged(nameof(SelectedId));
            OnPropertyChanged(nameof(Selected));
        }
    }

    /// <summary>Presets never emit cvar lines themselves — they push values onto the model, which other wrappers serialize.</summary>
    public override IEnumerable<string> EmitCvarLines() => [];

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == _modelProperty)
        {
            OnPropertyChanged(nameof(SelectedId));
            OnPropertyChanged(nameof(Selected));
        }
    }

    public void Dispose() => _model.PropertyChanged -= OnModelPropertyChanged;
}
