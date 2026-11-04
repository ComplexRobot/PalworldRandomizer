using PalworldRandomizer.Models;

namespace PalworldRandomizer.Serializer;

/// <summary>Identifies where the loaded Default template values came from.</summary>
public enum TemplateSource {
    /// <summary>No saved template exists; the original FormData defaults are in memory only.</summary>
    Default,
    /// <summary>The YAML Default template was read.</summary>
    Yaml,
    /// <summary>The exact legacy Default.json was read through the compatible JSON loader.</summary>
    LegacyJson
}

/// <summary>A validated, detached set of template values and its durable source.</summary>
/// <param name="Values">The complete FormData contract read from the source.</param>
/// <param name="Source">Where the values came from.</param>
public sealed record TemplateSnapshot(FormData Values, TemplateSource Source);

/// <summary>Reads, validates and atomically replaces the Default template without game file access.</summary>
/// <remarks>
/// The YAML document is the only write target. A legacy Default.json is read through TemplateJson.Load and is never
/// modified. A corrupt or unsupported existing document throws before any live edit or save is enabled.
/// </remarks>
/// <param name="readYaml">Reads the YAML document or null when absent.</param>
/// <param name="readLegacy">Reads the legacy JSON document or null when absent.</param>
/// <param name="commitYaml">Atomically replaces the YAML document.</param>
public sealed class TemplateStore(Func<Task<string?>> readYaml, Func<Task<string?>> readLegacy,
    Func<string, Task> commitYaml) {
    /// <summary>The single supported Default template file name.</summary>
    public const string TemplateName = "Default.yaml";

    /// <summary>The exact legacy JSON file name read when no YAML exists.</summary>
    public const string LegacyName = "Default.json";

    private readonly Func<Task<string?>> _readYaml =
        readYaml ?? throw new ArgumentNullException(nameof(readYaml));
    private readonly Func<Task<string?>> _readLegacy =
        readLegacy ?? throw new ArgumentNullException(nameof(readLegacy));
    private readonly Func<string, Task> _commitYaml =
        commitYaml ?? throw new ArgumentNullException(nameof(commitYaml));
    private readonly SemaphoreSlim _writes = new(1, 1);

    /// <summary>Gets the truthful template file name shown by the settings page.</summary>
    public string Name => TemplateName;

    /// <summary>Gets the native Templates directory beside the existing application configuration.</summary>
    public static string DefaultTemplatesRoot => UAssetData.AppDataPath("Templates");

    /// <summary>Creates the native store at an isolated or standard application Templates directory.</summary>
    public static TemplateStore CreateNative(string? templatesRoot = null) {
        string root = Path.GetFullPath(templatesRoot ?? DefaultTemplatesRoot);
        string yaml = Path.Combine(root, TemplateName);
        string legacy = Path.Combine(root, LegacyName);
        return new TemplateStore(() => ReadFileAsync(yaml), () => ReadFileAsync(legacy),
            text => CommitFileAsync(yaml, text));
    }

    /// <summary>Reads YAML first, then the exact legacy JSON, otherwise the original FormData defaults.</summary>
    /// <exception cref="InvalidDataException">An existing document is empty or unsupported.</exception>
    public async Task<TemplateSnapshot> LoadAsync() {
        string? yaml = await _readYaml().ConfigureAwait(false);
        if (yaml is not null) {
            return new(TemplateYaml.Deserialize(yaml), TemplateSource.Yaml);
        }

        string? legacy = await _readLegacy().ConfigureAwait(false);
        if (legacy is not null) {
            FormData? values = TemplateJson.Load(legacy, () => new FormData());
            return new(values ?? throw new InvalidDataException("The legacy Default template is null."),
                TemplateSource.LegacyJson);
        }

        return new(new FormData(), TemplateSource.Default);
    }

    /// <summary>Serializes one complete snapshot and awaits its atomic replacement.</summary>
    public async Task SaveAsync(FormData values) {
        string yaml = TemplateYaml.Serialize(values);
        await _writes.WaitAsync().ConfigureAwait(false);
        try {
            await _commitYaml(yaml).ConfigureAwait(false);
        } finally {
            _writes.Release();
        }
    }

    private static async Task<string?> ReadFileAsync(string path) => File.Exists(path)
        ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : null;

    private static async Task CommitFileAsync(string path, string yaml) {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(temporary, yaml).ConfigureAwait(false);
            File.Move(temporary, path, true);
        } finally {
            if (File.Exists(temporary)) {
                File.Delete(temporary);
            }
        }
    }
}