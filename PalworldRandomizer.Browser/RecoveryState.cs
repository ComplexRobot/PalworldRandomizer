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
public sealed record RecoveryState(
    int Schema,
    AppPreferences Preferences,
    ShellState Shell,
    string PakDraft,
    string MappingDraft,
    string FilenameDraft) {
    /// <summary>The only recovery schema understood by this stage.</summary>
    public const int CurrentSchema = 1;
}