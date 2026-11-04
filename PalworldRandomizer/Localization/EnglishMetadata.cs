using System.Text.Json;

namespace PalworldRandomizer.Localization;

/// <summary>Looks up the currently supported English shell resources by stable locale keys.</summary>
public static class EnglishMetadata {
    /// <summary>The compiled English resources; other locales are intentionally deferred.</summary>
    private static readonly IReadOnlyDictionary<string, string> _labels = Load();

    /// <summary>Loads the stable embedded locale resource without reflection-based JSON serialization.</summary>
    private static Dictionary<string, string> Load() {
        using var stream = typeof(EnglishMetadata).Assembly
            .GetManifestResourceStream("PalworldRandomizer.Metadata.Locales.en-US.json")
            ?? throw new InvalidOperationException("Missing compiled English locale metadata.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject()
            .ToDictionary(entry => entry.Name, entry => entry.Value.GetString()
                ?? throw new InvalidOperationException("Locale values must be strings."), StringComparer.Ordinal);
    }

    /// <summary>Resolves a resource key, exposing an unknown key rather than silently losing its label.</summary>
    public static string Get(string key) => _labels.TryGetValue(key, out string? value) ? value : key;
}