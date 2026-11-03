namespace PalworldRandomizer.Models;

/// <summary>Persisted application preferences and game-installation metadata.</summary>
public class ConfigData {
    /// <summary>The selected Palworld installation directory.</summary>
    public string InstallationDirectory { get; set; } = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";

    /// <summary>The last game version read from the selected archive.</summary>
    public string GameVersion { get; set; } = "0.0.0.0";

    /// <summary>The version of the application's extracted resources.</summary>
    public float AssetVersion { get; set; } = 0;

    /// <summary>Whether outdated extracted resources are automatically replaced.</summary>
    public bool AutoReplaceOldFiles { get; set; } = true;

    /// <summary>Whether the last randomizer template is restored at startup.</summary>
    public bool AutoRestoreTemplate { get; set; } = true;

    /// <summary>Whether generation writes a text log.</summary>
    public bool OutputLog { get; set; } = false;

    /// <summary>Whether edited spawn data is automatically backed up and restored.</summary>
    public bool AutoSaveRestoreBackups { get; set; } = true;

    /// <summary>Whether generated spawn data is automatically saved.</summary>
    public bool AutoSaveGenerationData { get; set; } = true;
}