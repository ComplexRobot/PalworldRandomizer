using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.Utils;
using PalworldRandomizer.Randomizer.PalSpawn;

namespace PalworldRandomizer.Serializer;

/// <summary>
/// Contains game data originating from a uasset resource.<br/>
/// Uses dynamically-typed properties with no type safety.<br/>
/// Generic object containing all possible properties.
/// </summary>
public class GameStruct {
    /// <summary>
    /// Constructs an empty object.
    /// </summary>
    public GameStruct() { }

    /// <summary>
    /// Loads data from a uasset path.
    /// </summary>
    public GameStruct(VfsFileProvider fileProvider, string path) {
        var exports = fileProvider.LoadAsset(path);

        // Data Table
        if (exports.First() is UDataTable dataTable) {
            DataTable = dataTable.RowMap.Select(x =>
                    new KeyValuePair<string, GameStruct>(x.Key.Text, new(x.Value.Properties)))
                .ToDictionary(StringComparer.OrdinalIgnoreCase);
        // Blueprint
        } else {
            var properties = exports.FirstOrDefault(x => x.Class!.Name.Text.EndsWith("_C"))!.Properties;
            LoadFromAssetProperties(properties);
        }
    }

    /// <summary>
    /// Loads the properties from an asset's property list.
    /// </summary>
    public GameStruct(List<FPropertyTag> properties) => LoadFromAssetProperties(properties);

    private void LoadFromAssetProperties(List<FPropertyTag> properties) {
        foreach (var property in properties) {
            Properties.Add(property.Name.Text, PropertyTagToValue(property.Tag));

            static object? PropertyTagToValue(FPropertyTagType? tag) => tag switch {
                NameProperty p => p.Value.IsNone ? null : p.Value.Text,
                EnumProperty p => p.Value.IsNone ? null : p.Value.Text.SubstringAfterLast(':'),
                StrProperty p => p.Value,
                BoolProperty p => p.Value,
                IntProperty p => p.Value,
                FloatProperty p => p.Value,
                DoubleProperty p => p.Value,
                StructProperty p => p.Value is null ? null
                    : new GameStruct(((AbstractPropertyHolder)p.Value.StructType).Properties),
                ArrayProperty p => p.Value?.Properties.Select(PropertyTagToValue),
                ObjectProperty p => p.Value?.Name,
                _ => throw new Exception($"Unknown property type '{(tag is null ? "null" : tag.GetType())}'"),
            };
        }
    }

    /// <summary>
    /// Try to get a value that might not exist in the properties.<br/>
    /// Used to set default values when they don't exist.
    /// </summary>
    /// <typeparam name="T">The type the output will be converted to.</typeparam>
    /// <param name="key">The property name to get.</param>
    /// <param name="value">The resulting value if it exists, otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if the key was found, <see langword="false"/> otherwise.</returns>
    public bool PropertyExists<T>(string key, out T value) {
        if (!Properties.TryGetValue(key, out object? obj)) {
            value = default!;
            return false;
        }

        value = (T)obj!;
        return true;
    }

    /// <summary>
    /// Converts Pal spawn sheet data to an enumerable of <see cref="SpawnEntry"/>.
    /// </summary>
    public IEnumerable<SpawnEntry> PalSpawnsToSpawnEntries() => SpawnGroupList.Select(entry =>
        new SpawnEntry {
            Weight = entry.Weight,
            NightOnly = entry.PropertyExists(nameof(OnlyTime), out string onlyTime)
                && onlyTime is "Night" or "EPalOneDayTimeType::Night",
            SpawnList = [.. entry.PalList.Select(spawn => {
                    string characterId =
                        (!spawn.PropertyExists(nameof(PalId), out GameStruct palId) || palId.Key == "None" ? null
                            : palId.Key)
                        ?? (!spawn.PropertyExists(nameof(NPCID), out GameStruct npcId) || npcId.Key == "None" ? null
                            : npcId.Key)
                        ?? "RowName";

                    return new SpawnData {
                        Name = characterId,
                        MinLevel = spawn.Level,
                        MaxLevel = spawn.Level_Max,
                        MinCount = spawn.Num,
                        MaxCount = spawn.Num_Max
                    };
                }
            )]
        }
    );

    /// <summary>
    /// Converts Pal egg spawn data to an enumerable of <see cref="SpawnEntry"/>.
    /// </summary>
    public IEnumerable<SpawnEntry> PalEggsToSpawnEntries() => SpawnPalEggLotteryDataArray.Select(entry =>
        new SpawnEntry {
            Weight = Convert.ToInt32(entry.Weight_F * 40),
            SpawnList = [
                new SpawnData {
                    Name = entry.PalEggData.PalMonsterId.Key!
                }
            ]
        }
    );

    /// <summary>
    /// Dictionary containing all defined properties.<br/>
    /// Values can be <see cref="GameStruct"/>, primitives, List (for arrays) or <see langword="null"/>.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Dictionary containing all objects of a data table.
    /// </summary>
    public Dictionary<string, GameStruct> DataTable { get; set; } = null!;
    /// <summary>A Character ID.</summary>
    public string? Key {
        get => (string?)Properties["Key"];
        set => Properties["Key"] = value;
    }
    /// <summary>Properties: { Key }<br/>Character ID of a pal.</summary>
    public GameStruct PalId {
        get => (GameStruct)Properties["PalId"]!;
        set => Properties["PalId"] = value;
    }
    /// <summary>Properties: { Key }<br/>Character ID of a human.</summary>
    public GameStruct NPCID {
        get => (GameStruct)Properties["NPCID"]!;
        set => Properties["NPCID"] = value;
    }
    /// <summary>Minimum level of a character spawn.</summary>
    public int Level {
        get => (int)Properties["Level"]!;
        set => Properties["Level"] = value;
    }
    /// <summary>Maximum level of a character spawn.</summary>
    public int Level_Max {
        get => (int)Properties["Level_Max"]!;
        set => Properties["Level_Max"] = value;
    }
    /// <summary>Minimum count of a character spawn.</summary>
    public int Num {
        get => (int)Properties["Num"]!;
        set => Properties["Num"] = value;
    }
    /// <summary>Maximum count of a character spawn.</summary>
    public int Num_Max {
        get => (int)Properties["Num_Max"]!;
        set => Properties["Num_Max"] = value;
    }
    /// <summary>Weight of a spawn group.</summary>
    public int Weight {
        get => (int)Properties["Weight"]!;
        set => Properties["Weight"] = value;
    }
    /// <summary>Time of day restriction (enum).</summary>
    public string OnlyTime {
        get => (string)Properties["OnlyTime"]!;
        set => Properties["OnlyTime"] = value;
    }
    /// <summary>List of { PalId, NPCID, Level, Level_Max, Num, Num_Max }.</summary>
    public List<GameStruct> PalList {
        get => Properties["PalList"]! switch {
            var x when x is IEnumerable<object?> e => [.. e.Select(y => (GameStruct)y!)],
            var x => (List<GameStruct>)x,
        };
        set => Properties["PalList"] = value;
    }
    /// <summary>List of { Weight, OnlyTime, PalList }.</summary>
    public List<GameStruct> SpawnGroupList {
        get => Properties["SpawnGroupList"]! switch {
            var x when x is IEnumerable<object?> e => [.. e.Select(y => (GameStruct)y!)],
            var x => (List<GameStruct>)x,
        };
        set => Properties["SpawnGroupList"] = value;
    }
    /// <summary>Properties: { Key }<br/>Character ID of a pal.</summary>
    public GameStruct PalMonsterId {
        get => (GameStruct)Properties["PalMonsterId"]!;
        set => Properties["PalMonsterId"] = value;
    }
    /// <summary>Properties: { PalMonsterId }<br/>Character ID of a pal.</summary>
    public GameStruct PalEggData {
        get => (GameStruct)Properties["PalEggData"]!;
        set => Properties["PalEggData"] = value;
    }
    /// <summary>List of { PalEggData, WeightF }.</summary>
    public List<GameStruct> SpawnPalEggLotteryDataArray {
        get => Properties["SpawnPalEggLotteryDataArray"]! switch {
            var x when x is IEnumerable<object?> e => [.. e.Select(y => (GameStruct)y!)],
            var x => (List<GameStruct>)x,
        };
        set => Properties["SpawnPalEggLotteryDataArray"] = value;
    }
    /// <summary>Respawn time for eggs.</summary>
    public float RespawnTimeMinutesObtained {
        get => (float)Properties["RespawnTimeMinutesObtained"]!;
        set => Properties["RespawnTimeMinutesObtained"] = value;
    }
    /// <summary>Name of spawn area type.</summary>
    public string FieldName {
        get => (string)Properties["FieldName"]!;
        set => Properties["FieldName"] = value;
    }
    /// <summary>Character ID of a pal (string).</summary>
    public string PalId_S {
        get => (string)Properties["PalId"]!;
        set => Properties["PalId"] = value;
    }
    /// <summary>Weight of a character spawn (float).</summary>
    public float Weight_F {
        get => (float)Properties["Weight"]!;
        set => Properties["Weight"] = value;
    }
    /// <summary>Minimum level of a character spawn.</summary>
    public int MinLevel {
        get => (int)Properties["MinLevel"]!;
        set => Properties["MinLevel"] = value;
    }
    /// <summary>Minimum level of a character spawn.</summary>
    public int MaxLevel {
        get => (int)Properties["MaxLevel"]!;
        set => Properties["MaxLevel"] = value;
    }
}