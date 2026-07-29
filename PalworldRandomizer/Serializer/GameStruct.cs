using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.Utils;
using Newtonsoft.Json;

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
                StructProperty p => p.Value is null ? null
                    : new GameStruct(((AbstractPropertyHolder)p.Value.StructType).Properties),
                ArrayProperty p => p.Value?.Properties.Select(PropertyTagToValue),
                _ => throw new Exception($"Unknown property type '{(tag is null ? "null" : tag.GetType())}'"),
            };
        }
    }
    /// <summary>
    /// Dictionary containing all defined properties.<br/>
    /// Values can be <see cref="GameStruct"/>, primitives, List (for arrays) or <see langword="null"/>.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Dictionary containing all objects of a data table.
    /// </summary>
    public Dictionary<string, GameStruct> DataTable { get; private set; } = null!;
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
    public string PalIdS {
        get => (string)Properties["PalId"]!;
        set => Properties["PalId"] = value;
    }
    /// <summary>Weight of a character spawn (float).</summary>
    public float WeightF {
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

/// <summary>
/// Converts a Blueprint <see cref="GameStruct"/> to and from json.
/// </summary>
public class JsonConverterBlueprint : JsonConverter<GameStruct> {
    /// <summary>
    /// Reads json into a <see cref="GameStruct"/>'s <see cref="GameStruct.Properties">Properties</see>.
    /// </summary>
    public override GameStruct? ReadJson(JsonReader reader, Type objectType, GameStruct? existingValue,
        bool hasExistingValue, JsonSerializer serializer) {
        var gameStruct = new GameStruct();
        serializer.Populate(reader, gameStruct.Properties);
        return gameStruct;
    }

    /// <summary>
    /// Writes the <see cref="GameStruct"/>'s <see cref="GameStruct.Properties">Properties</see>.
    /// </summary>
    public override void WriteJson(JsonWriter writer, GameStruct? value, JsonSerializer serializer) =>
        serializer.Serialize(writer, value?.Properties);
}

/// <summary>
/// Converts a Data Table <see cref="GameStruct"/> to and from json.
/// </summary>
public class JsonConverterDataTable : JsonConverter<GameStruct> {
    /// <summary>
    /// Reads json into a <see cref="GameStruct"/>'s <see cref="GameStruct.DataTable">DataTable</see>.
    /// </summary>
    public override GameStruct? ReadJson(JsonReader reader, Type objectType, GameStruct? existingValue,
        bool hasExistingValue, JsonSerializer serializer) {
        var gameStruct = new GameStruct();
        serializer.Populate(reader, gameStruct.DataTable);
        return gameStruct;
    }

    /// <summary>
    /// Writes the <see cref="GameStruct"/>'s <see cref="GameStruct.DataTable">DataTable</see>.
    /// </summary>
    public override void WriteJson(JsonWriter writer, GameStruct? value, JsonSerializer serializer) =>
        serializer.Serialize(writer, value?.DataTable);
}