using System.Runtime.ExceptionServices;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Pak;
using CUE4Parse.UE4.Readers;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;

namespace PalworldRandomizer.Serializer;

/// <summary>Reads game assets and tracks asynchronous texture saves for the application.</summary>
public class VfsFileProvider()
    : AbstractVfsFileProvider(new VersionContainer(EGame.GAME_UE5_1), StringComparer.OrdinalIgnoreCase),
        IAsyncDisposable {
    /// <summary><see langword="true"/> if a game version change was detected during initial load.</summary>
    public bool GameVersionUpdated { get; set; } = false;

    /// <summary>
    /// The version read during explicit archive opening, or the caller's fallback when no INI exists.
    /// </summary>
    public string GameVersion { get; internal set; } = "0.0.0.0";

    // CUE4Parse's public RegisterVfs/MountAsync log and swallow parser/codec failures. Keep the owned reader's
    // original exception while still using the provider's asynchronous mount and file-index publication.
    internal async Task MountPakAsync(FArchive archive) {
        CheckedPakReader reader = new(archive);
        PostLoadReader(reader, false);
        Initialize();
        int mounted = await MountAsync().ConfigureAwait(false);
        reader.MountFailure?.Throw();
        if (mounted != 1) {
            throw new InvalidDataException("The selected PAK was not mounted; an encryption key may be required.");
        }
    }

    private sealed class CheckedPakReader(FArchive archive) : PakFileReader(archive) {
        internal ExceptionDispatchInfo? MountFailure { get; private set; }

        public override void Mount(StringComparer pathComparer) {
            try {
                base.Mount(pathComparer);
            } catch (Exception exception) {
                MountFailure = ExceptionDispatchInfo.Capture(exception);
                throw;
            }
        }
    }

    private readonly HashSet<string> _savedImagePaths = [];
    private readonly List<Task> _fileSaveTasks = [];

    /// <summary>Leaves initialization to the explicitly registered game archive.</summary>
    public override void Initialize() { }

    /// <summary>Adds an operating-system file to the provider under a virtual mount point.</summary>
    public void AddFile(string file, string mountPoint) {
        OsGameFile gameFile = new(
            new(Path.GetDirectoryName(file)!),
            new(file),
            mountPoint,
            new VersionContainer(EGame.GAME_UE5_1)
        );
        Files.AddFiles(new Dictionary<string, GameFile> { { gameFile.Path, gameFile } });
    }

    /// <summary>
    /// Changes a soft resource path into a hard path.<br/>
    /// I.e., '/Game/...' -> 'Pal/Content/...uasset'
    /// </summary>
    public static string SoftPathToHardPath(string path) =>
        $"Pal/Content{path["/Game".Length..path.LastIndexOf('.')]}.uasset";

    /// <summary>
    /// Save a uasset file containing texture data to the user's disk as a .png.
    /// </summary>
    /// <param name="path">Path to the uasset data.</param>
    /// <param name="saveFolder">Folder on the disk to save the file.</param>
    /// <param name="forceOverwrite">
    /// If <see langword="true"/>, force the file to be overwritten instead of skipping it when it already exists.
    /// </param>
    /// <returns>The path of the saved .png file.</returns>
    public string SaveTexturePng(string path, string saveFolder, bool forceOverwrite) {
        var gameFile = this[path];

        string filename = Path.Combine(saveFolder, gameFile.NameWithoutExtension + ".png");

        // Prevent saving the same file multiple times
        if (_savedImagePaths.Contains(filename)) {
            return filename;
        }

        if (forceOverwrite || !File.Exists(filename)) {
            var package = LoadPackage(gameFile);
            foreach (var export in package.GetExports()) {
                if (export is UTexture texture) {
                    _savedImagePaths.Add(filename);
                    _fileSaveTasks.Add(
                        Task.Run(() =>
                            File.WriteAllBytes(
                                filename,
                                [.. texture.Decode()!.Encode(ETextureFormat.Png, false, out _)]
                            )
                        )
                    );
                    return filename;
                }
            }

            throw new Exception($"'{path}' does not contain texture data.");
        }

        _savedImagePaths.Add(filename);
        return filename;
    }

    /// <summary>
    /// Waits for scheduled image saves without releasing the archive needed by subsequent data loads.
    /// </summary>
    public Task WaitForTextureSavesAsync() => Task.WhenAll(_fileSaveTasks);

    /// <summary>Drains pending image saves, then releases archive readers even when an image save fails.</summary>
    public async ValueTask DisposeAsync() {
        try {
            await WaitForTextureSavesAsync();
        } finally {
            Dispose();
        }
    }

    /// <summary>Loads the exported objects from an asset package.</summary>
    public IEnumerable<UObject> LoadAsset(string path) => LoadPackage(path).GetExports();

    /// <summary>Loads the row map of a game data-table asset.</summary>
    public Dictionary<FName, FStructFallback> LoadDataTable(string path) {
        if (LoadAsset(path).First() is UDataTable dataTable) {
            return dataTable.RowMap;
        }

        throw new Exception($"'{path}' is not a data table.");
    }

    /// <summary>
    /// Loads a data table asset containing SoftObject data.<br/>
    /// i.e., name to asset path mappings.
    /// </summary>
    public Dictionary<string, string?> LoadDataTableSoftObject(string path) {
        if (LoadAsset(path).First() is not UDataTable dataTable) {
            throw new Exception($"'{path}' is not a data table.");
        }

        if (!dataTable.RowMap.First().Value.Properties.Exists(x => x.PropertyType.Text == "SoftObjectProperty")) {
            throw new Exception($"'{path}' is not a SoftObject data table.");
        }

        return dataTable.RowMap.ToDictionary(
            kvp => kvp.Key.Text,
            kvp =>
                NoneCheck(
                    (
                        (SoftObjectProperty)
                            kvp.Value.Properties.First(x => x.PropertyType.Text == "SoftObjectProperty").Tag!
                    )
                        .Value
                        .AssetPathName
                ),
            StringComparer.OrdinalIgnoreCase
        );

        static string? NoneCheck(FName fName) => fName.IsNone ? null : fName.Text;
    }

    /// <summary>
    /// Loads a data table asset containing text data.
    /// </summary>
    public Dictionary<string, string?> LoadDataTableText(string path) {
        if (LoadAsset(path).First() is not UDataTable dataTable) {
            throw new Exception($"'{path}' is not a data table.");
        }

        if (dataTable.RowMap.First().Value.Properties[0].PropertyType.Text != "TextProperty") {
            throw new Exception($"'{path}' is not a Text data table.");
        }

        return dataTable.RowMap.ToDictionary(
            kvp => kvp.Key.Text,
            kvp => ((TextProperty)kvp.Value.Properties[0].Tag!).Value?.Text,
            StringComparer.OrdinalIgnoreCase
        );
    }

    /// <summary>Returns the physical filename for a provider entry backed by an operating-system file.</summary>
    public string GetOsFileName(string path) {
        if (this[path] is OsGameFile gameFile) {
            return gameFile.ActualFile.FullName;
        }

        throw new Exception($"'{path}' is not located in the user's file system.");
    }
}