using System.IO;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using static PalworldRandomizer.Serializer.FileModify;

namespace PalworldRandomizer.Serializer;

public class VfsFileProvider()
    : AbstractVfsFileProvider(new VersionContainer(EGame.GAME_UE5_1), StringComparer.OrdinalIgnoreCase),
    IAsyncDisposable {
    /// <summary><see langword="true"/> if a game version change was detected during initial load.</summary>
    public bool GameVersionUpdated { get; set; } = false;

    private readonly HashSet<string> _savedImagePaths = [];
    private readonly List<Task> _fileSaveTasks = [];

    public override void Initialize() { }

    public void AddFile(string file, string mountPoint)
    {
        OsGameFile gameFile = new(new(Path.GetDirectoryName(file)!), new(file), mountPoint, new VersionContainer(EGame.GAME_UE5_1));
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

        string filename = saveFolder + '\\' + gameFile.NameWithoutExtension + ".png";

        // Prevent saving the same file multiple times
        if (!_savedImagePaths.Add(filename)) {
            return filename;
        }

        if ((forceOverwrite || !File.Exists(filename)) && TryLoadPackage(gameFile, out var package)) {
            foreach (var export in package.GetExports()) {
                if (export is UTexture texture) {
                    _fileSaveTasks.Add(Task.Run(() =>
                        File.WriteAllBytes(filename,
                            [.. texture.Decode()!.Encode(ETextureFormat.Png, false, out _)])));
                    return filename;
                }
            }

            throw new Exception($"'{path}' does not contain texture data.");
        }

        return filename;
    }

    /// <summary>
    /// Waits for any pending asynchronous file save tasks to complete.
    /// </summary>
    public async ValueTask DisposeAsync() => await Task.WhenAll(_fileSaveTasks);

    public IEnumerable<UObject> LoadAsset(string path)
    {
        if (TryLoadPackage(path, out IPackage? package))
        {
            return package.GetExports();
        }
        throw new Exception($"Failed to load package '{path}'.");
    }

    public Dictionary<FName, FStructFallback> LoadDataTable(string path)
    {
        if (LoadAsset(path).First() is UDataTable dataTable)
        {
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

        return dataTable.RowMap.Select(kvp =>
            new KeyValuePair<string, string?>(
                kvp.Key.Text,
                NoneCheck(((SoftObjectProperty)kvp.Value.Properties.First(x => x.PropertyType.Text == "SoftObjectProperty")
                    .Tag!).Value.AssetPathName)
            )
        ).ToDictionary(StringComparer.OrdinalIgnoreCase);

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

        return dataTable.RowMap.Select(kvp =>
            new KeyValuePair<string, string?>(
                kvp.Key.Text,
                ((TextProperty)kvp.Value.Properties[0].Tag!).Value?.Text
            )
        ).ToDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads a data table asset containing cage pal data.
    /// </summary>
    public IEnumerable<CagePalData> LoadDataTableCagePal(string path) {
        if (LoadAsset(path).First() is not UDataTable dataTable) {
            throw new Exception($"'{path}' is not a data table.");
        }

        return  dataTable.RowMap.Select(kvp => {
                var properties = kvp.Value.Properties.Select(x =>
                    new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag))
                    .ToDictionary(StringComparer.OrdinalIgnoreCase);

                return new CagePalData {
                    FieldName = ((NameProperty)properties["FieldName"]!).Value.Text,
                    PalID = ((NameProperty)properties["PalID"]!).Value.Text,
                    Weight = ((FloatProperty)properties["Weight"]!).Value,
                    MinLevel = ((IntProperty)properties["MinLevel"]!).Value,
                    MaxLevel = ((IntProperty)properties["MaxLevel"]!).Value
                };
            }
        );
    }

    /// <summary>
    /// Loads a blueprint asset containing a pal spawner.
    /// </summary>
    public PalSpawner LoadBlueprintPalSpawner(string path) {
        var spawnClass = LoadAsset(path).FirstOrDefault(x => {
            string? className = x.Class is null || x.Class.Name.IsNone ? null : x.Class.Name.Text;
            return className is not null && className.StartsWith("BP_PalSpawner_") && className.EndsWith("_C");
        });

        if (spawnClass is null) {
            throw new Exception($"'{path}' does not contain a Pal Spawner Blueprint.");
        }

        var spawnData = ((ArrayProperty)spawnClass.Properties.First(x => {
            string? propertyName = x.Name.IsNone ? null : x.Name.Text;
            return propertyName == "SpawnGroupList";
        }).Tag!).Value!.Properties.Select(y => ((AbstractPropertyHolder)((StructProperty)y).Value!.StructType).Properties
            .Select(x => new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag))
            .ToDictionary(StringComparer.OrdinalIgnoreCase));

        return new PalSpawner {
            SpawnGroupList = [.. spawnData.Select(group =>
                new PalSpawnerGroupInfo {
                    Weight = ((IntProperty)group["Weight"]!).Value,
                    OnlyTime = ((EnumProperty)group["OnlyTime"]!).Value.Text,
                    PalList = [.. ((ArrayProperty)group["PalList"]!).Value!.Properties.Select(palList => {
                            var properties = ((AbstractPropertyHolder)((StructProperty)palList).Value!.StructType).Properties.Select(x =>
                            new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag)).ToDictionary(StringComparer.OrdinalIgnoreCase);

                            var palId = ((NameProperty)((AbstractPropertyHolder)((StructProperty)properties["PalId"]!)
                                    .Value!.StructType).Properties[0].Tag!).Value;
                            var npcId = ((NameProperty)((AbstractPropertyHolder)((StructProperty)properties["NPCID"]!)
                                    .Value!.StructType).Properties[0].Tag!).Value;

                            return new PalSpawnerOneTribeInfo
                            {
                                PalId = new() { Key = palId.IsNone ? null : palId.Text },
                                NPCID = new() { Key = npcId.IsNone ? null : npcId.Text },
                                Level = ((IntProperty)properties["Level"]!).Value,
                                Level_Max = ((IntProperty)properties["Level_Max"]!).Value,
                                Num = ((IntProperty)properties["Num"]!).Value,
                                Num_Max = ((IntProperty)properties["Num_Max"]!).Value
                            };
                        }
                    )]
                }
            )]
        };
    }

    /// <summary>
    /// Loads a blueprint asset containing a pal egg spawner.
    /// </summary>
    public PalMapObject.SpawnerPalEgg LoadBlueprintPalEggSpawner(string path) {
        var spawnClass = LoadAsset(path).FirstOrDefault(x => {
            string? className = x.Class is null || x.Class.Name.IsNone ? null : x.Class.Name.Text;
            return className is not null && className.StartsWith("bp_palmapobjectspawner_palegg_") && className.EndsWith("_C");
        });

        if (spawnClass is null) {
            throw new Exception($"'{path}' does not contain a Pal Egg Spawner Blueprint.");
        }

        var spawnData = ((ArrayProperty)spawnClass.Properties.First(x => x.Name.Text == "SpawnPalEggLotteryDataArray")
            .Tag!).Value!.Properties.Select(y => ((AbstractPropertyHolder)((StructProperty)y).Value!.StructType).Properties
            .Select(x => new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag))
            .ToDictionary(StringComparer.OrdinalIgnoreCase));

        float respawnTime = ((FloatProperty)spawnClass.Properties.First(x => x.Name.Text == "RespawnTimeMinutesObtained")
            .Tag!).Value;

        return new PalMapObject.SpawnerPalEgg {
            SpawnPalEggLotteryDataArray = [.. spawnData.Select(group =>
                new PalMapObject.PickupItem.PalEggLotteryData {
                    PalEggData = new() { PalMonsterId = new() {
                        Key = ((NameProperty)((AbstractPropertyHolder)((StructProperty)((AbstractPropertyHolder)((StructProperty)group["PalEggData"]!)
                        .Value!.StructType).Properties[0].Tag!).Value!.StructType).Properties[0].Tag!).Value.Text }
                    },
                    Weight = ((FloatProperty)group["Weight"]!).Value,
                }
            )],
            RespawnTimeMinutesObtained = respawnTime
        };
    }

    public string GetOsFileName(string path)
    {
        if (this[path] is OsGameFile gameFile)
        {
            return gameFile.ActualFile.FullName;
        }
        throw new Exception($"'{path}' is not located in the user's file system.");
    }
}