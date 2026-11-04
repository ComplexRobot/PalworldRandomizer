namespace PalworldRandomizer.Models;

/// <summary>A small compatibility summary, never an extracted asset catalog.</summary>
/// <param name="Entries">Mounted archive entries.</param>
/// <param name="CharacterRows">Successfully parsed representative character rows.</param>
/// <param name="GameVersion">Version read from the real game INI.</param>
public sealed record ArchiveCompatibility(int Entries, int CharacterRows, string GameVersion);

/// <summary>Identifies the stage whose compatibility check failed.</summary>
/// <param name="key">A stable contextual locale key.</param>
/// <param name="inner">The original parser error.</param>
/// <param name="detectedGameVersion">Version already read from the archive, or null if not detected.</param>
public sealed class ArchiveCompatibilityException(string key, Exception inner, string? detectedGameVersion = null)
    : Exception(inner.Message, inner) {
    /// <summary>Contextual resource key; mapping incompatibility is distinct from a game update.</summary>
    public string Key { get; } = key;

    /// <summary>Partial context survives a later representative mapping parse failure.</summary>
    public string? DetectedGameVersion { get; } = detectedGameVersion;
}