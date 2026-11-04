using System.Collections.ObjectModel;
using System.Globalization;

namespace PalworldRandomizer.Models;

/// <summary>The original persisted CLR value kinds supported by the bundled catalog.</summary>
public enum SettingValueType {
    /// <summary>A System.Boolean.</summary>
    Boolean,
    /// <summary>A System.Int32.</summary>
    Int32,
    /// <summary>A System.Decimal.</summary>
    Decimal,
    /// <summary>A System.String containing a persisted mode value.</summary>
    String,
    /// <summary>A System.Int32 array.</summary>
    Int32Array
}

/// <summary>A read-only catalog definition, not a mutable runtime setting or UI control.</summary>
public sealed class SettingDefinition {
    /// <summary>The unchanged FormData member name.</summary>
    public string Name { get; }

    /// <summary>The original member's value kind.</summary>
    public SettingValueType Type { get; }

    /// <summary>The typed cold default; arrays are read-only integer collections.</summary>
    public object DefaultValue { get; }

    /// <summary>The stable category identifier.</summary>
    public string Category { get; }

    /// <summary>The localized category resource key.</summary>
    public string CategoryKey { get; }

    /// <summary>The stable label resource key.</summary>
    public string LabelKey { get; }

    /// <summary>The stable primary description resource key.</summary>
    public string DescriptionKey { get; }

    /// <summary>Nexus or Implementation; newly authored text is never attributed to Nexus.</summary>
    public string DescriptionOrigin { get; }

    /// <summary>The original audit's verified/shared-range/missing/ambiguous classification.</summary>
    public string OriginalDescriptionStatus { get; }

    /// <summary>Unchanged generic source paragraphs retained alongside implementation clarifications.</summary>
    public IReadOnlyList<string> AdditionalDescriptionKeys { get; }

    /// <summary>Paragraph IDs or code locations supporting the primary description.</summary>
    public IReadOnlyList<string> DescriptionReferences { get; }

    /// <summary>The original exclusive radio group, or null for independent settings.</summary>
    public string? RadioGroup { get; }

    /// <summary>The compound relationship identifier; members remain individually persisted.</summary>
    public string? CompoundGroup { get; }

    /// <summary>The persisted string choices, in the original selector order.</summary>
    public IReadOnlyList<SettingOption> Options { get; }

    /// <summary>The original controls and their source-only visibility, focus and numeric constraints.</summary>
    public IReadOnlyList<SettingControl> Controls { get; }

    internal SettingDefinition(SettingDocument source) {
        Name = source.Name;
        Category = source.Category;
        CategoryKey = source.CategoryKey;
        LabelKey = source.LabelKey;
        DescriptionKey = source.DescriptionKey;
        DescriptionOrigin = source.DescriptionOrigin;
        OriginalDescriptionStatus = source.OriginalDescriptionStatus;
        AdditionalDescriptionKeys = source.AdditionalDescriptionKeys.ToList().AsReadOnly();
        DescriptionReferences = source.DescriptionReferences.ToList().AsReadOnly();
        RadioGroup = source.RadioGroup;
        CompoundGroup = source.CompoundGroup;
        Options = source.Options.ConvertAll(x => new SettingOption(x.Value, x.LabelKey, x.DescriptionKey,
            x.DescriptionOrigin, x.DescriptionReferences.ToList().AsReadOnly())).AsReadOnly();
        Controls = source.Controls.ConvertAll(ToControl).AsReadOnly();
        Type = Enum.Parse<SettingValueType>(source.Type);
        DefaultValue = Type switch {
            SettingValueType.Boolean => bool.Parse(source.Default!),
            SettingValueType.Int32 => int.Parse(source.Default!, CultureInfo.InvariantCulture),
            SettingValueType.Decimal => decimal.Parse(source.Default!, CultureInfo.InvariantCulture),
            SettingValueType.String => source.Default!,
            SettingValueType.Int32Array => new ReadOnlyCollection<int>([.. source.DefaultArray]),
            _ => throw new ArgumentException("Unknown setting value type.")
        };
    }

    private static SettingControl ToControl(ControlDocument source) => new(
        source.Name, source.Kind, source.Role, source.ArrayIndex, source.LabelKeys.ToList().AsReadOnly(),
        source.Source,
        source.Numeric is { } n ? new(n.MinimumExpression, n.FallbackExpression, n.MaximumExpression,
            n.InputRegexCSharpLiteral, n.PasteRegexCSharpLiteral, n.SourceCall, n.Source,
            n.InputSource, n.PasteSource) : null,
        source.Focus is { } f ? new(f.Handler, f.TargetControl, f.IsChecked, f.Source) : null,
        source.Visibility.ConvertAll(v => new SourceVisibility(v.Element, v.LocalVisibility, v.StyleDefault,
            v.Triggers.ConvertAll(t => new SourceVisibilityTrigger(t.Kind,
                t.Conditions.ConvertAll(c => new SourceVisibilityCondition(c.Binding, c.ExpectedValue)).AsReadOnly(),
                t.Visibility)).AsReadOnly())).AsReadOnly());
}

/// <summary>A persisted mode token and its localized explanation, independent of its display translation.</summary>
/// <param name="Value">The original, untranslated persisted token.</param>
/// <param name="LabelKey">The localized choice label.</param>
/// <param name="DescriptionKey">The localized choice explanation.</param>
/// <param name="DescriptionOrigin">Nexus or Implementation.</param>
/// <param name="DescriptionReferences">Source paragraph IDs or implementation locations.</param>
public sealed record SettingOption(string Value, string LabelKey, string DescriptionKey, string DescriptionOrigin,
    IReadOnlyList<string> DescriptionReferences);

/// <summary>
/// Original control evidence. It is descriptive metadata, not executable WPF or a new expression language.
/// </summary>
/// <param name="Name">The original control name.</param>
/// <param name="Kind">The original control kind.</param>
/// <param name="Role">The component's role within its setting.</param>
/// <param name="ArrayIndex">The original array index, if any.</param>
/// <param name="LabelKeys">Ordered localized original label-path components.</param>
/// <param name="Source">The original control source location.</param>
/// <param name="Numeric">Input/paste and final numeric normalization evidence, if applicable.</param>
/// <param name="Focus">The original focus side effect, if applicable.</param>
/// <param name="Visibility">Ordered outer-to-inner visibility layers; local values override style triggers.</param>
public sealed record SettingControl(string Name, string Kind, string Role, int? ArrayIndex,
    IReadOnlyList<string> LabelKeys, string Source, SourceNumericConstraint? Numeric, SourceFocusEffect? Focus,
    IReadOnlyList<SourceVisibility> Visibility);

/// <summary>
/// Source expressions only: cold defaults, typing rules and final fallback values are not interchangeable.
/// </summary>
/// <param name="MinimumExpression">The original normalizer's lower-bound C# expression.</param>
/// <param name="FallbackExpression">The original invalid/empty-input C# expression.</param>
/// <param name="MaximumExpression">The upper-bound C# expression, or null when no explicit bound exists.</param>
/// <param name="InputRegexCSharpLiteral">The exact original typing regex C# literal.</param>
/// <param name="PasteRegexCSharpLiteral">The exact original paste regex C# literal.</param>
/// <param name="SourceCall">The complete original numeric-normalizer call.</param>
/// <param name="Source">The normalizer call's source location.</param>
/// <param name="InputSource">The typing regex's source location.</param>
/// <param name="PasteSource">The paste regex's source location.</param>
public sealed record SourceNumericConstraint(string MinimumExpression, string FallbackExpression,
    string? MaximumExpression, string InputRegexCSharpLiteral, string PasteRegexCSharpLiteral, string SourceCall,
    string Source, string InputSource, string PasteSource);

/// <summary>A documented focus side effect, not an action performed by the catalog loader.</summary>
/// <param name="Handler">The original event handler name.</param>
/// <param name="TargetControl">The original target control name, not a new setting.</param>
/// <param name="IsChecked">The value assigned when the source control receives focus.</param>
/// <param name="Source">The original source location.</param>
public sealed record SourceFocusEffect(string Handler, string TargetControl, bool IsChecked, string Source);

/// <summary>
/// One original ancestor/control layer; every layer must remain visible for the control to be visible.
/// </summary>
/// <param name="Element">The original element identifier or kind.</param>
/// <param name="LocalVisibility">The local value, which takes precedence over style triggers.</param>
/// <param name="StyleDefault">The original style's default value.</param>
/// <param name="Triggers">Ordered triggers; later applicable setters take precedence.</param>
public sealed record SourceVisibility(string Element, string? LocalVisibility, string? StyleDefault,
    IReadOnlyList<SourceVisibilityTrigger> Triggers);

/// <summary>Original trigger evidence, retained without evaluating or translating bindings.</summary>
/// <param name="Kind">The original trigger kind.</param>
/// <param name="Conditions">Conditions that must all match.</param>
/// <param name="Visibility">The source setter value.</param>
public sealed record SourceVisibilityTrigger(string Kind, IReadOnlyList<SourceVisibilityCondition> Conditions,
    string Visibility);

/// <summary>An original binding/value comparison, not a new runtime expression language.</summary>
/// <param name="Binding">The original binding text.</param>
/// <param name="ExpectedValue">The original comparison value.</param>
public sealed record SourceVisibilityCondition(string Binding, string ExpectedValue);

/// <summary>
/// The original UI exclusivity contract; the catalog never repairs imported conflicting boolean states.
/// </summary>
/// <param name="Id">The stable original group identifier.</param>
/// <param name="Members">The original FormData member names.</param>
/// <param name="DefaultMember">The single selected cold-default member.</param>
public sealed record SettingRadioGroup(string Id, IReadOnlyList<string> Members, string DefaultMember);

internal sealed class CatalogDocument {
    public List<RadioGroupDocument> RadioGroups { get; set; } = [];
    public List<SettingDocument> Settings { get; set; } = [];
}

internal sealed class SettingDocument {
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Default { get; set; }
    public List<int> DefaultArray { get; set; } = [];
    public string Category { get; set; } = "";
    public string CategoryKey { get; set; } = "";
    public string LabelKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public string DescriptionOrigin { get; set; } = "";
    public string OriginalDescriptionStatus { get; set; } = "";
    public List<string> AdditionalDescriptionKeys { get; set; } = [];
    public List<string> DescriptionReferences { get; set; } = [];
    public string? RadioGroup { get; set; }
    public string? CompoundGroup { get; set; }
    public List<OptionDocument> Options { get; set; } = [];
    public List<ControlDocument> Controls { get; set; } = [];
}

internal sealed class OptionDocument {
    public string Value { get; set; } = "";
    public string LabelKey { get; set; } = "";
    public string DescriptionKey { get; set; } = "";
    public string DescriptionOrigin { get; set; } = "";
    public List<string> DescriptionReferences { get; set; } = [];
}

internal sealed class ControlDocument {
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Role { get; set; } = "";
    public int? ArrayIndex { get; set; }
    public List<string> LabelKeys { get; set; } = [];
    public string Source { get; set; } = "";
    public NumericDocument? Numeric { get; set; }
    public FocusDocument? Focus { get; set; }
    public List<VisibilityDocument> Visibility { get; set; } = [];
}

internal sealed class NumericDocument {
    public string MinimumExpression { get; set; } = "";
    public string FallbackExpression { get; set; } = "";
    public string? MaximumExpression { get; set; }
    public string InputRegexCSharpLiteral { get; set; } = "";
    public string PasteRegexCSharpLiteral { get; set; } = "";
    public string SourceCall { get; set; } = "";
    public string Source { get; set; } = "";
    public string InputSource { get; set; } = "";
    public string PasteSource { get; set; } = "";
}

internal sealed class FocusDocument {
    public string Handler { get; set; } = "";
    public string TargetControl { get; set; } = "";
    public bool IsChecked { get; set; }
    public string Source { get; set; } = "";
}

internal sealed class VisibilityDocument {
    public string Element { get; set; } = "";
    public string? LocalVisibility { get; set; }
    public string? StyleDefault { get; set; }
    public List<TriggerDocument> Triggers { get; set; } = [];
}

internal sealed class TriggerDocument {
    public string Kind { get; set; } = "";
    public List<ConditionDocument> Conditions { get; set; } = [];
    public string Visibility { get; set; } = "";
}

internal sealed class ConditionDocument {
    public string Binding { get; set; } = "";
    public string ExpectedValue { get; set; } = "";
}

internal sealed class RadioGroupDocument {
    public string Id { get; set; } = "";
    public List<string> Members { get; set; } = [];
    public string DefaultMember { get; set; } = "";
}

internal sealed class LocaleDocument {
    public string Culture { get; set; } = "";
    public Dictionary<string, string> Strings { get; set; } = [];
}