using PalworldRandomizer.ViewModels;

namespace PalworldRandomizer.Desktop;

/// <summary>The Desktop host keeps native workflows discoverable before their inputs are ready.</summary>
/// <remarks>
/// Availability describes whether a real page exists, not whether the current inputs have validated yet.
/// The Generate command owns that readiness gate dynamically, so a launcher created before asynchronous
/// validation completes still exposes Generate instead of freezing the destination as unavailable.
/// </remarks>
public static class DesktopPagePolicy {
    /// <summary>Returns whether this host can show a real workflow for the destination.</summary>
    public static bool IsAvailable(AppPage page) => page is AppPage.Settings or AppPage.Generate;
}