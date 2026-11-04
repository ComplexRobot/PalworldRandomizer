using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer;

namespace PalworldRandomizer.Browser;

/// <summary>
/// A host boundary that turns real selected game files into one PalSchema ZIP without exposing raw assets.
/// </summary>
/// <remarks>
/// Implementations own every game-file handle for the duration of a call. The request and its settings are a
/// snapshot: the host must not mutate the captured <see cref="FormData"/> while generation is running.
/// </remarks>
public interface IBrowserGenerationService {
    /// <summary>Generates a complete PalSchema ZIP and returns its small summary.</summary>
    /// <param name="request">The selected file references, destination ZIP path and captured settings.</param>
    /// <param name="cancellationToken">Cancels waiting or opening before generation commits its output.</param>
    /// <returns>The same summary contract as the native generation service.</returns>
    Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default);
}
