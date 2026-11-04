using System.Text.Json;
using PalworldRandomizer.Models;

namespace PalworldRandomizer.Serializer;

/// <summary>Serializes metadata through atomic platform storage operations, never game file contents.</summary>
/// <param name="read">Reads one complete document or null when absent.</param>
/// <param name="commit">Atomically replaces one document; failed commits must preserve the old document.</param>
public sealed class AppPreferencesStore(Func<Task<string?>> read, Func<string, Task> commit) {
    private readonly SemaphoreSlim _writes = new(1, 1);

    /// <summary>The platform-equivalent standard configuration directory; accessing it performs no writes.</summary>
    public static string DefaultMetadataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Palworld-Randomizer", "Config");

    /// <summary>Creates a native store at an explicitly chosen configuration root.</summary>
    public AppPreferencesStore(string metadataRoot) : this(
        () => ReadFileAsync(Path.Combine(Path.GetFullPath(metadataRoot), "preferences.json")),
        json => CommitFileAsync(Path.Combine(Path.GetFullPath(metadataRoot), "preferences.json"), json)) { }

    /// <summary>Reads saved metadata, returning defaults only when the document is absent.</summary>
    public async Task<AppPreferences> LoadAsync() {
        string? json = await read().ConfigureAwait(false);
        if (json is null) {
            return new();
        }

        var preferences = JsonSerializer.Deserialize<AppPreferences>(json)
            ?? throw new InvalidDataException("Preference document is null.");
        Validate(preferences);
        return preferences;
    }

    /// <summary>Rejects unsupported values instead of silently changing the saved preference.</summary>
    public static void Validate(AppPreferences preferences) {
        if (preferences.Locale != "en-US" || preferences.Renderer is not ("gpu-preferred" or "software")
            || !IsValidFilenamePattern(preferences.FilenamePattern)) {
            throw new InvalidDataException("Unsupported preference value.");
        }
    }

    /// <summary>Checks syntax and cross-platform filename safety using an actual formatted value.</summary>
    public static bool IsValidFilenamePattern(string? pattern) {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 120) {
            return false;
        }

        try {
            string sample = DateTime.Now.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture);
            return sample.Length is > 0 and <= 200 && sample is not ("." or "..")
                && !sample.Any(character => char.IsControl(character) || "<>:\"/\\|?*".Contains(character));
        } catch (FormatException) {
            return false;
        }
    }

    /// <summary>Serializes complete snapshots and awaits the actual platform commit.</summary>
    public async Task SaveAsync(AppPreferences preferences) {
        Validate(preferences);
        await _writes.WaitAsync().ConfigureAwait(false);
        try {
            await commit(JsonSerializer.Serialize(preferences)).ConfigureAwait(false);
        } finally {
            _writes.Release();
        }
    }

    private static async Task<string?> ReadFileAsync(string path) => File.Exists(path)
        ? await File.ReadAllTextAsync(path).ConfigureAwait(false) : null;

    private static async Task CommitFileAsync(string path, string json) {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(temporary, json).ConfigureAwait(false);
            File.Move(temporary, path, true);
        } finally {
            if (File.Exists(temporary)) {
                File.Delete(temporary);
            }
        }
    }
}