using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer.PalSpawn;
using static PalworldRandomizer.Serializer.FileModify;

namespace PalworldRandomizer.Window;

/// <summary>Platform-neutral PalSchema save adapter used by the shared and test hosts.</summary>
/// <remarks>
/// Native destination selection remains a desktop-host responsibility. Cancellation never captures
/// export settings, matching the original WPF adapter contract.
/// </remarks>
public static class SpawnFileDialogs {
    /// <summary>Writes the supplied areas only after a caller-confirmed destination is selected.</summary>
    /// <param name="areaList">The modified areas selected for export.</param>
    /// <param name="captureSettings">Captures settings only when the export is confirmed.</param>
    /// <param name="selectDestination">Destination selector; null requires a platform host implementation.</param>
    public static void SavePalSchema(List<AreaData> areaList, Func<FormData> captureSettings,
        Func<string?>? selectDestination = null) {
        string? filename = selectDestination is null
            ? throw new PlatformNotSupportedException("A native destination selector is required on this host.")
            : selectDestination();
        if (filename != null) {
            WritePalSchema(filename, areaList, captureSettings());
        }
    }
}