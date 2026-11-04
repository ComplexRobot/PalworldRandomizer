using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using PalworldRandomizer.Models;

namespace PalworldRandomizer.ViewModels;

/// <summary>Base row view model for one catalog setting, bound to the shared FormData contract.</summary>
/// <remarks>
/// The row owns only presentation and a typed view of one FormData member. The RandomizerViewModel remains the
/// single writer, so automatic save and radio-group exclusivity stay centralized. Rows are rebuilt when a template
/// is loaded or recovery restores values, never while a user edit is in flight.
/// </remarks>
public abstract class SettingRowViewModel : ObservableObject {
    /// <summary>Creates a row for one catalog definition.</summary>
    /// <param name="owner">The shared Randomizer session.</param>
    /// <param name="definition">The catalog definition.</param>
    /// <param name="label">The exact English label.</param>
    /// <param name="description">The exact English description.</param>
    protected SettingRowViewModel(RandomizerViewModel owner, SettingDefinition definition, string label,
        string description) {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Label = label;
        Description = description;
    }

    /// <summary>Gets the shared session that owns the FormData contract.</summary>
    protected RandomizerViewModel Owner { get; }

    /// <summary>Gets the catalog definition.</summary>
    public SettingDefinition Definition { get; }

    /// <summary>Gets the unchanged FormData member name.</summary>
    public string Name => Definition.Name;

    /// <summary>Gets the exact English label.</summary>
    public string Label { get; }

    /// <summary>Gets the exact English description.</summary>
    public string Description { get; }

    /// <summary>Gets the original control kind for this setting, or TextBox when unspecified.</summary>
    public string Kind => Definition.Controls.FirstOrDefault()?.Kind ?? "TextBox";

    /// <summary>Gets the stable automation identifier used by the actual control.</summary>
    public string AutomationId => "Randomizer.Setting." + Name;

    /// <summary>Refreshes the row after a template load or recovery restore.</summary>
    public abstract void Refresh();
}

/// <summary>A Boolean row bound to one FormData member.</summary>
public sealed class BooleanSettingRowViewModel : SettingRowViewModel {
    /// <summary>Creates a Boolean row.</summary>
    public BooleanSettingRowViewModel(RandomizerViewModel owner, SettingDefinition definition, string label,
        string description) : base(owner, definition, label, description) { }

    /// <summary>Gets whether this Boolean is an exclusive radio-group member.</summary>
    public bool IsRadio => Definition.RadioGroup is not null
        || string.Equals(Kind, "RadioButton", StringComparison.Ordinal);

    /// <summary>Gets whether this Boolean is an independent toggle.</summary>
    public bool IsToggle => !IsRadio;

    /// <summary>Gets or changes the shared FormData Boolean and its automatic save.</summary>
    public bool IsChecked {
        get => Owner.GetBoolValue(Name);
        set {
            if (IsChecked == value) {
                return;
            }

            Owner.SetBoolValue(Name, value);
            OnPropertyChanged();
        }
    }

    /// <inheritdoc />
    public override void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>An Int32 row bound to one FormData member.</summary>
public sealed class IntSettingRowViewModel : SettingRowViewModel {
    /// <summary>Creates an Int32 row.</summary>
    public IntSettingRowViewModel(RandomizerViewModel owner, SettingDefinition definition, string label,
        string description) : base(owner, definition, label, description) { }

    /// <summary>Gets the catalog minimum, or the definition default when no numeric bound exists.</summary>
    public int Minimum => Owner.NumericBound(Name, "MinimumExpression", (int)Definition.DefaultValue);

    /// <summary>Gets the catalog maximum, or the definition default when no numeric bound exists.</summary>
    public int Maximum => Owner.NumericBound(Name, "MaximumExpression", (int)Definition.DefaultValue);

    /// <summary>Gets the catalog empty-input fallback, or the definition default.</summary>
    public int Fallback => Owner.NumericBound(Name, "FallbackExpression", (int)Definition.DefaultValue);

    /// <summary>Gets or changes the shared FormData Int32 and its automatic save.</summary>
    public int Value {
        get => Owner.GetIntValue(Name);
        set {
            if (Value == value) {
                return;
            }

            Owner.SetIntValue(Name, value);
            OnPropertyChanged();
        }
    }

    /// <inheritdoc />
    public override void Refresh() => OnPropertyChanged(nameof(Value));
}

/// <summary>A Decimal row bound to one FormData member.</summary>
public sealed class DecimalSettingRowViewModel : SettingRowViewModel {
    /// <summary>Creates a Decimal row.</summary>
    public DecimalSettingRowViewModel(RandomizerViewModel owner, SettingDefinition definition, string label,
        string description) : base(owner, definition, label, description) { }

    /// <summary>Gets or changes the shared FormData Decimal and its automatic save.</summary>
    public decimal Value {
        get => Owner.GetDecimalValue(Name);
        set {
            if (Value == value) {
                return;
            }

            Owner.SetDecimalValue(Name, value);
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or changes the invariant text shown in the editor.</summary>
    public string Text {
        get => Value.ToString(CultureInfo.InvariantCulture);
        set {
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)) {
                Value = parsed;
            }

            OnPropertyChanged();
        }
    }

    /// <inheritdoc />
    public override void Refresh() {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Text));
    }
}

/// <summary>An Int32-array row bound to one FormData member.</summary>
public sealed class IntArraySettingRowViewModel : SettingRowViewModel {
    /// <summary>Creates an Int32-array row.</summary>
    public IntArraySettingRowViewModel(RandomizerViewModel owner, SettingDefinition definition, string label,
        string description) : base(owner, definition, label, description) { }

    /// <summary>Gets or changes the comma-separated invariant values shown in the editor.</summary>
    public string Text {
        get => string.Join(", ", Owner.GetIntArrayValue(Name));
        set {
            Owner.SetIntArrayValue(Name, value);
            OnPropertyChanged();
        }
    }

    /// <inheritdoc />
    public override void Refresh() => OnPropertyChanged(nameof(Text));
}

/// <summary>A String row bound to one FormData member.</summary>
public sealed class StringSettingRowViewModel : SettingRowViewModel {
    /// <summary>Creates a String row.</summary>
    public StringSettingRowViewModel(RandomizerViewModel owner, SettingDefinition definition, string label,
        string description) : base(owner, definition, label, description) { }

    private IReadOnlyList<SettingOptionChoice>? _choices;

    /// <summary>Gets the localized choices for a persisted string selector.</summary>
    public IReadOnlyList<SettingOptionChoice> Choices => _choices ??= Definition.Options
        .Select(option => new SettingOptionChoice(option.Value, Owner.Text(option.LabelKey, option.Value)))
        .ToList()
        .AsReadOnly();

    /// <summary>Gets whether this setting is a selector instead of free text.</summary>
    public bool HasChoices => Choices.Count > 0;

    /// <summary>Gets whether this setting uses a plain text box.</summary>
    public bool IsPlainText => !HasChoices;

    /// <summary>Gets or changes the shared FormData String and its automatic save.</summary>
    public string Value {
        get => Owner.GetStringValue(Name);
        set {
            if (string.Equals(Value, value, StringComparison.Ordinal)) {
                return;
            }

            Owner.SetStringValue(Name, value);
            OnPropertyChanged();
        }
    }

    /// <summary>Gets or changes the selected choice for a selector setting.</summary>
    public SettingOptionChoice? SelectedChoice {
        get => Choices.FirstOrDefault(choice => string.Equals(choice.Value, Value, StringComparison.Ordinal));
        set {
            if (value is null || string.Equals(value.Value, Value, StringComparison.Ordinal)) {
                return;
            }

            Value = value.Value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc />
    public override void Refresh() {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(SelectedChoice));
    }
}

/// <summary>One localized persisted choice in a string selector.</summary>
/// <param name="Value">The unchanged persisted token.</param>
/// <param name="Label">The localized display label.</param>
public sealed record SettingOptionChoice(string Value, string Label);
