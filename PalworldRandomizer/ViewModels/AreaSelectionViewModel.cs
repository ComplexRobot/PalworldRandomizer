using CommunityToolkit.Mvvm.ComponentModel;

namespace PalworldRandomizer.ViewModels;

/// <summary>One real Area Selections Boolean row bound to the shared FormData contract.</summary>
/// <param name="owner">The shared Randomizer session.</param>
/// <param name="name">The original FormData member name.</param>
/// <param name="label">The catalog label.</param>
/// <param name="description">The catalog description.</param>
public sealed class AreaSelectionViewModel(RandomizerViewModel owner, string name, string label, string description)
    : ObservableObject {
    private readonly RandomizerViewModel _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>Gets the original FormData member name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the exact catalog English label.</summary>
    public string Label { get; } = label;

    /// <summary>Gets the exact catalog English description.</summary>
    public string Description { get; } = description;

    /// <summary>Gets the stable automation identifier used by the actual ToggleSwitch.</summary>
    public string AutomationId => "Randomizer.AreaSelection." + Name;

    /// <summary>Gets or changes the shared FormData Boolean and its automatic save.</summary>
    public bool IsSelected {
        get => _owner.GetAreaSelection(Name);
        set {
            if (IsSelected == value) {
                return;
            }

            _owner.SetAreaSelection(Name, value);
            OnPropertyChanged();
        }
    }

    /// <summary>Refreshes the binding after a template load or recovery restore.</summary>
    internal void Refresh() => OnPropertyChanged(nameof(IsSelected));
}