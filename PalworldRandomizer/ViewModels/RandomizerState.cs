using PalworldRandomizer.Models;

namespace PalworldRandomizer.ViewModels;

/// <summary>Detached Randomizer values and raw text drafts captured in a recovery checkpoint.</summary>
/// <param name="Schema">The explicit Randomizer checkpoint shape version.</param>
/// <param name="Values">All current FormData values, including fields this page does not edit.</param>
/// <param name="SpawnListSizeText">The exact raw custom-group-count text, which may be invalid.</param>
/// <param name="VanillaPlusChanceText">The exact raw vanilla-add percentage text, which may be invalid.</param>
/// <param name="SelectedCategory">The selected settings category identifier.</param>
public sealed record RandomizerState(
    int Schema,
    FormData Values,
    string SpawnListSizeText,
    string VanillaPlusChanceText,
    string SelectedCategory) {
    /// <summary>The only Randomizer checkpoint schema understood by this increment.</summary>
    public const int CurrentSchema = 1;

    /// <summary>The Area Group Type category identifier.</summary>
    public const string AreaGroupType = "AreaGroupType";

    /// <summary>The Area Selections category identifier.</summary>
    public const string AreaSelections = "AreaSelections";

    /// <summary>Rejects malformed or unsupported checkpoint data before any live session is changed.</summary>
    /// <exception cref="InvalidDataException">The state is missing, unsupported or incomplete.</exception>
    public static void Validate(RandomizerState? state) {
        if (state is null) {
            throw new InvalidDataException("The recovery Randomizer state is missing.");
        }

        if (state.Schema != CurrentSchema) {
            throw new InvalidDataException("The recovery Randomizer schema is not supported.");
        }

        if (state.Values is null) {
            throw new InvalidDataException("The recovery Randomizer values are missing.");
        }

        if (state.SpawnListSizeText is null || state.VanillaPlusChanceText is null) {
            throw new InvalidDataException("The recovery Randomizer raw drafts are incomplete.");
        }

        if (!string.Equals(state.SelectedCategory, AreaGroupType, StringComparison.Ordinal)
            && !string.Equals(state.SelectedCategory, AreaSelections, StringComparison.Ordinal)) {
            throw new InvalidDataException("The recovery Randomizer category is not supported.");
        }
    }
}