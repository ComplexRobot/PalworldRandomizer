using System.Globalization;
using Avalonia.Data.Converters;

namespace PalworldRandomizer.Localization;

/// <summary>Resolves bound locale keys without coupling the UI-independent state to translated strings.</summary>
public sealed class LocaleKeyConverter : IValueConverter {
    /// <summary>Looks up a locale key for display.</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => EnglishMetadata.Get(value as string ?? string.Empty);

    /// <summary>Rejects reverse translation because resource labels do not identify state.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}