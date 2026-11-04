using PalworldRandomizer.Models;

namespace PalworldRandomizer.Serializer;

/// <summary>Identifies where a loaded template's values came from.</summary>
public enum TemplateSource {
    /// <summary>No saved template exists; the original FormData defaults are in memory only.</summary>
    Default,
    /// <summary>The YAML template was read.</summary>
    Yaml,
    /// <summary>The exact legacy Default.json was read through the compatible JSON loader.</summary>
    LegacyJson
}

/// <summary>A validated, detached set of template values and its durable source.</summary>
/// <param name="Values">The complete FormData contract read from the source.</param>
/// <param name="Source">Where the values came from.</param>
public sealed record TemplateSnapshot(FormData Values, TemplateSource Source);

/// <summary>One template file or virtual template exposed by the Templates directory.</summary>
/// <param name="Name">The relative template path without the hidden .yaml extension.</param>
/// <param name="FilePath">The absolute file that stores or will store the template.</param>
/// <param name="FileExists">Whether the template already has a YAML file.</param>
/// <param name="IsLegacyJson">Whether the template is backed by the legacy Default.json file.</param>
public sealed record TemplateEntry(string Name, string FilePath, bool FileExists, bool IsLegacyJson);

/// <summary>The outcome of deleting a template, including automatic Default recreation.</summary>
/// <param name="Name">The deleted template name.</param>
/// <param name="DefaultRecreated">Whether deleting the last template recreated Default with defaults.</param>
public sealed record TemplateDeleteResult(string Name, bool DefaultRecreated);

/// <summary>Reads, lists, creates, renames and deletes templates without game file access.</summary>
/// <remarks>
/// A directory-backed store uses the native Templates folder and saves every template as YAML. The parameterless
/// delegate construction remains the single-template test seam and never touches the file system. In directory mode,
/// the active template name is the relative file path without its hidden extension. Absolute paths and any ".."
/// segment are rejected, and duplicate names compare case-insensitively. A corrupt or unsupported document throws
/// before a live edit or save is enabled. The YAML document is the only write target; a legacy Default.json is read
/// through TemplateJson.Load and is never modified except when its template is explicitly renamed or deleted.
/// </remarks>
public sealed class TemplateStore {
    /// <summary>The default template name shown in the UI without its extension.</summary>
    public const string DefaultName = "Default";

    /// <summary>The hidden extension appended to every template file on disk.</summary>
    public const string Extension = ".yaml";

    /// <summary>The default template file name on disk.</summary>
    public const string DefaultFileName = "Default.yaml";

    /// <summary>The exact legacy JSON file name read when no YAML exists.</summary>
    public const string LegacyName = "Default.json";

    private static readonly char[] InvalidNameCharacters = Path.GetInvalidFileNameChars()
        .Concat(['*', '?', ':', '<', '>', '|', '"'])
        .Distinct()
        .ToArray();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase) {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private readonly Func<Task<string?>>? _readYaml;
    private readonly Func<Task<string?>>? _readLegacy;
    private readonly Func<string, Task>? _commitYaml;
    private readonly string? _root;
    private readonly SemaphoreSlim _writes = new(1, 1);

    /// <summary>Creates the single-template test seam over explicit read and write delegates.</summary>
    /// <param name="readYaml">Reads the YAML document or null when absent.</param>
    /// <param name="readLegacy">Reads the legacy JSON document or null when absent.</param>
    /// <param name="commitYaml">Atomically replaces the YAML document.</param>
    public TemplateStore(Func<Task<string?>> readYaml, Func<Task<string?>> readLegacy,
        Func<string, Task> commitYaml) {
        _readYaml = readYaml ?? throw new ArgumentNullException(nameof(readYaml));
        _readLegacy = readLegacy ?? throw new ArgumentNullException(nameof(readLegacy));
        _commitYaml = commitYaml ?? throw new ArgumentNullException(nameof(commitYaml));
        // The original single-template seam keeps reporting its truthful file name.
        Name = DefaultFileName;
    }

    /// <summary>Creates a directory-backed multi-template store over the native Templates folder.</summary>
    /// <param name="templatesRoot">The Templates folder, or null for the standard application location.</param>
    public TemplateStore(string? templatesRoot = null) {
        _root = Path.GetFullPath(templatesRoot ?? DefaultTemplatesRoot);
    }

    /// <summary>Gets the active template name without the hidden .yaml extension.</summary>
    public string Name { get; private set; } = DefaultName;

    /// <summary>Gets whether this store manages more than one template on disk.</summary>
    public bool SupportsMultipleTemplates => _root is not null;

    /// <summary>Gets the native Templates directory, or null for the delegate test seam.</summary>
    public string? RootPath => _root;

    /// <summary>Gets the native Templates directory beside the existing application configuration.</summary>
    public static string DefaultTemplatesRoot => UAssetData.AppDataPath("Templates");

    /// <summary>Creates the native multi-template store at an isolated or standard Templates directory.</summary>
    public static TemplateStore CreateNative(string? templatesRoot = null) => new(templatesRoot);

    /// <summary>Normalizes a UI template name into its hidden-extension-free relative path form.</summary>
    /// <param name="name">The typed or stored template name.</param>
    public static string NormalizeName(string name) {
        ArgumentNullException.ThrowIfNull(name);
        string normalized = name.Trim().Replace('\\', '/');
        if (normalized.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) {
            normalized = normalized[..^Extension.Length];
        } else if (normalized.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) {
            normalized = normalized[..^4];
        }

        return normalized.TrimEnd('/');
    }

    /// <summary>Validates a relative template name and returns an actionable error or null when acceptable.</summary>
    /// <param name="name">The typed or stored template name.</param>
    public static string? ValidateName(string? name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return "Template name cannot be empty.";
        }

        string normalized = NormalizeName(name);
        if (normalized.Length == 0) {
            return "Template name cannot be empty.";
        }

        if (normalized.Length > 200) {
            return "Template name is too long.";
        }

        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || normalized.Contains(':')) {
            return "Absolute paths are not allowed. Enter a name relative to the Templates folder.";
        }

        string[] segments = normalized.Split('/');
        foreach (string segment in segments) {
            if (segment.Length == 0) {
                return "Template names cannot contain empty path segments.";
            }

            if (segment is "." or "..") {
                return "Template names cannot contain '.' or '..' path segments.";
            }

            if (segment.IndexOfAny(InvalidNameCharacters) >= 0 || segment.Any(char.IsControl)) {
                return "Template names cannot contain path or file name special characters.";
            }

            if (segment.EndsWith(' ') || segment.EndsWith('.')) {
                return "Template path segments cannot end with a space or a dot.";
            }
        }

        string baseName = segments[0].Split('.')[0];
        if (ReservedNames.Contains(baseName)) {
            return "That name is reserved by the operating system.";
        }

        return null;
    }

    /// <summary>Reads the active template, or the original FormData defaults when nothing is saved.</summary>
    /// <exception cref="InvalidDataException">An existing document is empty or unsupported.</exception>
    public Task<TemplateSnapshot> LoadAsync() => LoadAsync(Name);

    /// <summary>Reads one named template without changing the active selection.</summary>
    /// <param name="name">The relative template name without its extension.</param>
    /// <exception cref="InvalidDataException">The template is absent or its document is unsupported.</exception>
    public async Task<TemplateSnapshot> LoadAsync(string name) {
        string normalized = NormalizeName(name);
        if (_root is null) {
            if (!IsActiveName(normalized)) {
                throw new InvalidDataException("Only the Default template is available in this session.");
            }

            string? yaml = await _readYaml!().ConfigureAwait(false);
            if (yaml is not null) {
                return new(TemplateYaml.Deserialize(yaml), TemplateSource.Yaml);
            }

            string? legacy = await _readLegacy!().ConfigureAwait(false);
            if (legacy is not null) {
                FormData? values = TemplateJson.Load(legacy, () => new FormData());
                return new(values ?? throw new InvalidDataException("The legacy Default template is null."),
                    TemplateSource.LegacyJson);
            }

            return new(new FormData(), TemplateSource.Default);
        }

        string yamlPath = PathFor(normalized);
        if (File.Exists(yamlPath)) {
            string yaml = await File.ReadAllTextAsync(yamlPath).ConfigureAwait(false);
            return new(TemplateYaml.Deserialize(yaml), TemplateSource.Yaml);
        }

        string legacyPath = LegacyPath;
        if (string.Equals(normalized, DefaultName, StringComparison.Ordinal) && File.Exists(legacyPath)) {
            string legacy = await File.ReadAllTextAsync(legacyPath).ConfigureAwait(false);
            FormData? values = TemplateJson.Load(legacy, () => new FormData());
            return new(values ?? throw new InvalidDataException("The legacy Default template is null."),
                TemplateSource.LegacyJson);
        }

        if (string.Equals(normalized, DefaultName, StringComparison.Ordinal)
            && !HasPersistedTemplates()) {
            return new(new FormData(), TemplateSource.Default);
        }

        throw new InvalidDataException($"The template '{normalized}' does not exist.");
    }

    /// <summary>Loads a named template and makes it the active save target.</summary>
    /// <param name="name">The relative template name without its extension.</param>
    public async Task<TemplateSnapshot> SelectAsync(string name) {
        string normalized = NormalizeName(name);
        TemplateSnapshot snapshot = await LoadAsync(normalized).ConfigureAwait(false);
        if (_root is not null) {
            Name = normalized;
        }

        return snapshot;
    }

    /// <summary>Lists every YAML template and adds a virtual Default when the directory is empty.</summary>
    public Task<IReadOnlyList<TemplateEntry>> ListAsync() {
        if (_root is null) {
            return Task.FromResult<IReadOnlyList<TemplateEntry>>(
                [new TemplateEntry(Name, DefaultFileName, true, false)]);
        }

        var entries = new Dictionary<string, TemplateEntry>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(_root)) {
            foreach (string file in Directory.EnumerateFiles(_root, "*" + Extension,
                         SearchOption.AllDirectories)) {
                string? name = NameFromPath(file);
                if (name is null) {
                    continue;
                }

                entries.TryAdd(name, new TemplateEntry(name, file, true, false));
            }
        }

        string legacyPath = LegacyPath;
        if (!entries.ContainsKey(DefaultName) && File.Exists(legacyPath)) {
            entries[DefaultName] = new TemplateEntry(DefaultName, legacyPath, false, true);
        }

        if (entries.Count == 0) {
            entries[DefaultName] = new TemplateEntry(DefaultName, PathFor(DefaultName), false, false);
        }

        return Task.FromResult<IReadOnlyList<TemplateEntry>>(entries.Values
            .OrderBy(entry => string.Equals(entry.Name, DefaultName, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly());
    }

    /// <summary>Serializes one complete snapshot and awaits its atomic replacement.</summary>
    /// <param name="values">The complete FormData contract to save.</param>
    public Task SaveAsync(FormData values) => SaveAsync(Name, values);

    /// <summary>Serializes one complete snapshot to a named template and awaits its atomic replacement.</summary>
    /// <param name="name">The relative template name without its extension.</param>
    /// <param name="values">The complete FormData contract to save.</param>
    public async Task SaveAsync(string name, FormData values) {
        ArgumentNullException.ThrowIfNull(values);
        string normalized = NormalizeName(name);
        if (_root is null) {
            if (!IsActiveName(normalized)) {
                throw new InvalidDataException("Only the Default template is available in this session.");
            }

            string yaml = TemplateYaml.Serialize(values);
            await _writes.WaitAsync().ConfigureAwait(false);
            try {
                await _commitYaml!(yaml).ConfigureAwait(false);
            } finally {
                _writes.Release();
            }

            return;
        }

        string? error = ValidateName(normalized);
        if (error is not null) {
            throw new InvalidDataException(error);
        }

        string path = PathFor(normalized);
        string yamlText = TemplateYaml.Serialize(values);
        await _writes.WaitAsync().ConfigureAwait(false);
        try {
            await CommitFileAsync(path, yamlText).ConfigureAwait(false);
        } finally {
            _writes.Release();
        }
    }

    /// <summary>Creates a new YAML template and fails when its name already exists.</summary>
    /// <param name="name">The relative template name without its extension.</param>
    /// <param name="values">The initial values, or the original FormData defaults when null.</param>
    /// <exception cref="InvalidDataException">The name is invalid or already exists.</exception>
    public async Task<TemplateEntry> CreateAsync(string name, FormData? values = null) {
        EnsureDirectoryMode();
        string normalized = NormalizeName(name);
        string? error = ValidateName(normalized);
        if (error is not null) {
            throw new InvalidDataException(error);
        }

        await _writes.WaitAsync().ConfigureAwait(false);
        try {
            if (await NameExistsAsync(normalized).ConfigureAwait(false)) {
                throw new InvalidDataException($"A template named '{normalized}' already exists.");
            }

            if (!HasPersistedTemplates() && !string.Equals(normalized, DefaultName, StringComparison.Ordinal)) {
                // Keep the initial virtual Default in the list when the first real template is created.
                await CommitFileAsync(PathFor(DefaultName), TemplateYaml.Serialize(new FormData()))
                    .ConfigureAwait(false);
            }

            string path = PathFor(normalized);
            await CommitFileAsync(path, TemplateYaml.Serialize(values ?? new FormData()))
                .ConfigureAwait(false);
            return new TemplateEntry(normalized, path, true, false);
        } finally {
            _writes.Release();
        }
    }

    /// <summary>Renames a template file so its on-disk path continues to match its UI name.</summary>
    /// <param name="name">The existing relative template name without its extension.</param>
    /// <param name="newName">The new relative template name without its extension.</param>
    /// <param name="values">Values used when the old template has no YAML file yet.</param>
    /// <exception cref="InvalidDataException">
    /// The new name is invalid, duplicate or the old template is absent.
    /// </exception>
    public async Task<TemplateEntry> RenameAsync(string name, string newName, FormData? values = null) {
        EnsureDirectoryMode();
        string oldNormalized = NormalizeName(name);
        string newNormalized = NormalizeName(newName);
        string? error = ValidateName(newNormalized);
        if (error is not null) {
            throw new InvalidDataException(error);
        }

        await _writes.WaitAsync().ConfigureAwait(false);
        try {
            if (!await NameExistsAsync(oldNormalized).ConfigureAwait(false)) {
                throw new InvalidDataException($"The template '{oldNormalized}' does not exist.");
            }

            if (string.Equals(oldNormalized, newNormalized, StringComparison.Ordinal)) {
                string existing = PathFor(oldNormalized);
                if (!File.Exists(existing) && string.Equals(oldNormalized, DefaultName,
                        StringComparison.Ordinal)) {
                    await CommitFileAsync(existing, TemplateYaml.Serialize(values ?? new FormData()))
                        .ConfigureAwait(false);
                }

                return new TemplateEntry(newNormalized, existing, File.Exists(existing), false);
            }

            if (await NameExistsExceptAsync(newNormalized, oldNormalized).ConfigureAwait(false)) {
                throw new InvalidDataException($"A template named '{newNormalized}' already exists.");
            }

            string oldPath = PathFor(oldNormalized);
            string newPath = PathFor(newNormalized);
            bool hasYaml = File.Exists(oldPath);
            bool hasLegacy = string.Equals(oldNormalized, DefaultName, StringComparison.Ordinal)
                && File.Exists(LegacyPath);

            if (hasYaml) {
                Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                MoveCaseSafely(oldPath, newPath);
                if (string.Equals(oldNormalized, DefaultName, StringComparison.Ordinal)
                    && File.Exists(LegacyPath)) {
                    File.Delete(LegacyPath);
                }
            } else if (hasLegacy) {
                FormData data = values ?? TemplateJson.Load(
                    await File.ReadAllTextAsync(LegacyPath).ConfigureAwait(false), () => new FormData())
                    ?? throw new InvalidDataException("The legacy Default template is null.");
                await CommitFileAsync(newPath, TemplateYaml.Serialize(data)).ConfigureAwait(false);
                File.Delete(LegacyPath);
            } else {
                await CommitFileAsync(newPath, TemplateYaml.Serialize(values ?? new FormData()))
                    .ConfigureAwait(false);
            }

            if (string.Equals(Name, oldNormalized, StringComparison.Ordinal)) {
                Name = newNormalized;
            }

            PruneEmptyDirectories(Path.GetDirectoryName(oldPath)!);
            return new TemplateEntry(newNormalized, newPath, true, false);
        } finally {
            _writes.Release();
        }
    }

    /// <summary>Deletes a template and recreates Default with default values when none remain.</summary>
    /// <param name="name">The relative template name without its extension.</param>
    /// <exception cref="InvalidDataException">The template does not exist.</exception>
    public async Task<TemplateDeleteResult> DeleteAsync(string name) {
        EnsureDirectoryMode();
        string normalized = NormalizeName(name);
        string? error = ValidateName(normalized);
        if (error is not null) {
            throw new InvalidDataException(error);
        }

        await _writes.WaitAsync().ConfigureAwait(false);
        try {
            if (!await NameExistsAsync(normalized).ConfigureAwait(false)) {
                throw new InvalidDataException($"The template '{normalized}' does not exist.");
            }

            string path = PathFor(normalized);
            bool persisted = HasPersistedTemplates();
            bool deleted = false;
            if (File.Exists(path)) {
                File.Delete(path);
                deleted = true;
            }

            if (string.Equals(normalized, DefaultName, StringComparison.Ordinal) && File.Exists(LegacyPath)) {
                File.Delete(LegacyPath);
                deleted = true;
            }

            if (!deleted) {
                if (string.Equals(normalized, DefaultName, StringComparison.Ordinal) && !persisted) {
                    // The initial Default is virtual until its first save; deleting it still removes it from the list.
                    deleted = true;
                } else {
                    throw new InvalidDataException($"The template '{normalized}' does not exist.");
                }
            }

            PruneEmptyDirectories(Path.GetDirectoryName(path)!);
            bool recreated = false;
            if (!HasPersistedTemplates()) {
                await CommitFileAsync(PathFor(DefaultName), TemplateYaml.Serialize(new FormData()))
                    .ConfigureAwait(false);
                recreated = true;
            }

            return new TemplateDeleteResult(normalized, recreated);
        } finally {
            _writes.Release();
        }
    }

    /// <summary>Imports a YAML or JSON template file into the Templates directory.</summary>
    /// <param name="sourcePath">The YAML or JSON file to read without modifying it.</param>
    /// <param name="name">The optional destination name; defaults to the source file name.</param>
    /// <exception cref="InvalidDataException">The document is unsupported or its name already exists.</exception>
    public async Task<TemplateEntry> ImportAsync(string sourcePath, string? name = null) {
        EnsureDirectoryMode();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        string fullSource = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSource)) {
            throw new FileNotFoundException("The imported template file was not found.", fullSource);
        }

        string text = await File.ReadAllTextAsync(fullSource).ConfigureAwait(false);
        FormData values;
        try {
            values = ParseTemplateText(text, Path.GetExtension(fullSource));
        } catch (InvalidDataException) {
            throw;
        } catch (Exception exception) {
            throw new InvalidDataException("The imported template is not valid YAML or JSON.", exception);
        }

        string requested = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(fullSource)
            : name!;
        return await CreateAsync(requested, values).ConfigureAwait(false);
    }

    /// <summary>Exports one template as YAML or JSON without changing it.</summary>
    /// <param name="name">The relative template name without its extension.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="json">Whether to write JSON instead of YAML.</param>
    public async Task ExportAsync(string name, string destinationPath, bool json) {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        TemplateSnapshot snapshot = await LoadAsync(name).ConfigureAwait(false);
        string text = json ? TemplateJson.Serialize(snapshot.Values) : TemplateYaml.Serialize(snapshot.Values);
        await CommitFileAsync(Path.GetFullPath(destinationPath), text).ConfigureAwait(false);
    }

    private string LegacyPath => Path.Combine(_root!, LegacyName);

    private bool IsActiveName(string normalizedName) =>
        string.Equals(normalizedName, NormalizeName(Name), StringComparison.OrdinalIgnoreCase);

    private bool HasPersistedTemplates() {
        if (_root is null) {
            return true;
        }

        if (File.Exists(LegacyPath)) {
            return true;
        }

        return Directory.Exists(_root)
            && Directory.EnumerateFiles(_root, "*" + Extension, SearchOption.AllDirectories).Any();
    }

    private void EnsureDirectoryMode() {
        if (_root is null) {
            throw new NotSupportedException(
                "Multi-template operations require the native Templates directory.");
        }
    }

    private string PathFor(string normalizedName) {
        string relative = normalizedName.Replace('/', Path.DirectorySeparatorChar) + Extension;
        string full = Path.GetFullPath(Path.Combine(_root!, relative));
        string rootPrefix = _root!.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("Template paths must stay inside the Templates folder.");
        }

        return full;
    }

    private string? NameFromPath(string filePath) {
        string relative = Path.GetRelativePath(_root!, filePath);
        if (relative.StartsWith("..", StringComparison.Ordinal)) {
            return null;
        }

        string withoutExtension = relative.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? relative[..^Extension.Length]
            : relative;
        return withoutExtension.Replace(Path.DirectorySeparatorChar, '/');
    }

    private async Task<bool> NameExistsAsync(string normalizedName) =>
        (await ListAsync().ConfigureAwait(false))
        .Any(entry => string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> NameExistsExceptAsync(string normalizedName, string exceptName) =>
        (await ListAsync().ConfigureAwait(false))
        .Any(entry => !string.Equals(entry.Name, exceptName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase));

    private static void MoveCaseSafely(string oldPath, string newPath) {
        if (string.Equals(oldPath, newPath, StringComparison.Ordinal)) {
            return;
        }

        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(newPath)) {
            File.Move(oldPath, newPath);
            return;
        }

        // A case-only rename on a case-insensitive file system needs an intermediate name.
        string temporary = oldPath + "." + Guid.NewGuid().ToString("N") + ".rename";
        File.Move(oldPath, temporary);
        try {
            File.Move(temporary, newPath);
        } catch {
            if (File.Exists(temporary) && !File.Exists(oldPath)) {
                File.Move(temporary, oldPath);
            }

            throw;
        }
    }

    private void PruneEmptyDirectories(string? directory) {
        string root = _root!;
        while (!string.IsNullOrEmpty(directory)
               && !string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
               && Directory.Exists(directory)
               && !Directory.EnumerateFileSystemEntries(directory).Any()) {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    private static FormData ParseTemplateText(string text, string extension) {
        if (string.IsNullOrWhiteSpace(text)) {
            throw new InvalidDataException("The imported template is empty.");
        }

        string trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        bool json = string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith('{');
        if (json) {
            try {
                FormData? values = TemplateJson.Load(text, () => new FormData());
                return values ?? throw new InvalidDataException("The imported JSON template is empty.");
            } catch (InvalidDataException) {
                throw;
            } catch (Exception) {
                // A .json-named file may still contain ordinary YAML; importing either form is identical.
                return TemplateYaml.Deserialize(text);
            }
        }

        return TemplateYaml.Deserialize(text);
    }

    private static async Task CommitFileAsync(string path, string text) {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(temporary, text).ConfigureAwait(false);
            File.Move(temporary, path, true);
        } finally {
            if (File.Exists(temporary)) {
                File.Delete(temporary);
            }
        }
    }
}
