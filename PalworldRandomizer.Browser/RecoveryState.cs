using PalworldRandomizer.Models;
using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Browser;

/// <summary>Versioned, detached managed shell and Setup recovery data.</summary>
/// <param name="Schema">The recovery document schema version.</param>
/// <param name="Preferences">The saved application preferences, never a temporary override.</param>
/// <param name="Shell">Detached tab identities, order, histories, selection and Undo deadlines.</param>
/// <param name="PakDraft">The exact current PAK input text, which may be incomplete.</param>
/// <param name="MappingDraft">The exact current mapping input text, which may be incomplete.</param>
/// <param name="FilenameDraft">The exact current filename format text, which may be invalid.</param>
/// <param name="Randomizer">The detached first-category values and raw drafts, when this host has one.</param>
public sealed record RecoveryState(
    int Schema,
    AppPreferences Preferences,
    ShellState Shell,
    string PakDraft,
    string MappingDraft,
    string FilenameDraft,
    RandomizerState? Randomizer = null) {
    /// <summary>The current recovery schema, including the first Randomizer session.</summary>
    public const int CurrentSchema = 2;

    /// <summary>The earlier shell and Setup checkpoint shape, which has no Randomizer session.</summary>
    public const int LegacySchema = 1;
}