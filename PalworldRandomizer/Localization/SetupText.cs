using System.Globalization;
using System.Text.Json;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace PalworldRandomizer.Localization;

/// <summary>Resolves the installed English Setup resource without claiming unavailable translations.</summary>
/// <param name="key">Stable locale resource key.</param>
public sealed class SetupText(string key) : MarkupExtension {
    private static readonly Dictionary<string, string> _strings = Load();

    /// <summary>Resolves one locale key; absent resources fail visibly.</summary>
    public static string Get(string key) => _strings[key];

    /// <summary>Returns the label for this markup extension.</summary>
    public override object ProvideValue(IServiceProvider serviceProvider) => Get(key);

    private static Dictionary<string, string> Load() {
        using var stream = typeof(SetupText).Assembly
            .GetManifestResourceStream("PalworldRandomizer.Metadata.Locales.Setup.en-US.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}

/// <summary>Resolves message keys inside the actual toast item template.</summary>
public sealed class SetupTextConverter : IValueConverter {
    /// <summary>Converts a stable key into an installed label.</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        SetupText.Get((string)value!);

    /// <summary>Labels are not editable.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}