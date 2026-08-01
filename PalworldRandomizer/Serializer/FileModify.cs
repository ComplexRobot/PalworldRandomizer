using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalworldRandomizer.Randomizer;
using PalworldRandomizer.Randomizer.PalSpawn;
using PalworldRandomizer.Window;

namespace PalworldRandomizer.Serializer;

public static partial class FileModify
{
    public static int AreaSortFunc(AreaData x, AreaData y)
    {
        if (x.modified != y.modified)
            return (y.modified? 1 : 0) - (x.modified? 1 : 0);
        if (x.isEgg != y.isEgg)
            return (x.isEgg? 1 : 0) - (y.isEgg? 1 : 0);
        if (x.isCage != y.isCage)
            return (x.isCage? 1 : 0) - (y.isCage? 1 : 0);
        return string.Compare(x.filename, y.filename);
    }

    public static Dictionary<string, AreaData> ReadCageData(IEnumerable<GameStruct> cagePalDataList)
    {
        Dictionary<string, AreaData> cageList = [];
        foreach (var cagePalData in cagePalDataList)
        {
            if (!cageList.TryGetValue($"Cage:{cagePalData.FieldName}", out AreaData? areaData))
            {
                areaData = new(new(), cagePalData.FieldName!)
                {
                    isCage = true,
                    minLevel = cagePalData.MinLevel,
                    maxLevel = cagePalData.MaxLevel
                };
                cageList.Add(areaData.SimpleName, areaData);
            }
            areaData.SpawnEntries.Add(new()
            {
                Weight = Convert.ToInt32(cagePalData.Weight_F * 10),
                SpawnList = [new()
                {
                    Name = cagePalData.PalId_S,
                    MinLevel = cagePalData.MinLevel,
                    MaxLevel = cagePalData.MaxLevel
                }]
            });
        }
        return cageList;
    }

    public static AreaData ReadEggData(string filename, GameStruct spawner) =>
        new(new(), $"PalEgg\\{filename}") {
        isEgg = true,
        minLevel = 1,
        maxLevel = 1,
        SpawnEntries = [.. spawner.PalEggsToSpawnEntries()],
        eggRespawnTime = spawner.RespawnTimeMinutesObtained,
        eggLotteryCooldown = 180
    };

    public static void SaveCSV(List<AreaData> areaList)
    {
        SaveFileDialog saveDialog = new()
        {
            FileName = $"PalworldSpawns-{DateTime.Now:MM-dd-yy-HH-mm-ss}",
            DefaultExt = ".csv",
            Filter = "Comma-separated values|*.csv|All files|*.*"
        };
        if (saveDialog.ShowDialog() == true)
        {
            File.WriteAllText(saveDialog.FileName, GenerateCSV(areaList), Encoding.UTF8);
        }
    }

    public static string GenerateCSV(List<AreaData> areaList)
    {
        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine("Area Name,Group Index,Weight,Night Only,Character Name,Is Boss,Min. Level,Max. Level,Min. Count,Max. Count");
        foreach (AreaData area in areaList)
        {
            string areaName = area.SimpleName;
            if (area.SpawnEntries.Count == 0)
            {
                stringBuilder.Append(areaName);
                stringBuilder.Append(',', 9);
                stringBuilder.AppendLine();
                continue;
            }
            for (int i = 0; i < area.SpawnEntries.Count; ++i)
            {
                SpawnEntry entry = area.SpawnEntries[i];
                for (int j = 0; j < entry.SpawnList.Count; ++j)
                {
                    SpawnData spawnData = entry.SpawnList[j];
                    stringBuilder.AppendJoin(',', [areaName, i, (j == 0 ? entry.Weight : ""), (j == 0 ? entry.NightOnly : ""), spawnData.SimpleName, spawnData.IsBoss,
                            spawnData.MinLevel, spawnData.MaxLevel, spawnData.MinCount, spawnData.MaxCount]);
                    stringBuilder.AppendLine();
                }
            }
        }
        return stringBuilder.ToString();
    }

    public static string? LoadCSV()
    {
        OpenFileDialog openDialog = new()
        {
            DefaultExt = ".csv",
            Filter = "Comma-separated values|*.csv|All files|*.*"
        };
        if (openDialog.ShowDialog() == true && openDialog.FileName != string.Empty)
        {
            try
            {
                Randomize.SaveBackup();
                PalSpawnPage.Instance.areaList.ItemsSource = ConvertCSV(openDialog.FileName);
                Randomize.AreaListChanged = true;
            }
            catch (Exception e)
            {
                if (e.Source == "FileIO")
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
                return e.Message;
            }
        }
        else
            return "Cancel";
        return null;
    }

    public static List<AreaData> ConvertCSV(string filename)
    {
        string[] fileLines = [];
        try
        {
            fileLines = File.ReadAllLines(filename, Encoding.UTF8)[1..];
        }
        catch (Exception e)
        {
            e.Source = "FileIO";
            ExceptionDispatchInfo.Capture(e).Throw();
        }
        List<string[]> csvData = [.. fileLines.Select(x => x.Split(',').Select(x => x.Trim()).ToArray())/*.Where(x => x.Length > 1)*/];
        Dictionary<string, AreaData> areaDict = Data.AreaDataCopy().ToDictionary(x => x.SimpleName, x => x);
        HashSet<string> addedNames = [];
        foreach (string[] list in csvData)
        {
            AreaData area = areaDict[list[0]];
            if (addedNames.Add(list[0]))
            {
                area.SpawnEntries = [];
            }
            if (list[1].Length == 0)
                continue;
            int spawnIndex = int.Parse(list[1]);
            while (area.SpawnEntries.Count < spawnIndex + 1)
            {
                area.SpawnEntries.Add(new());
            }
            SpawnEntry spawnEntry = area.SpawnEntries[spawnIndex];
            if (list[2].Length != 0)
                spawnEntry.Weight = int.Parse(list[2]);
            if (list[3].Length != 0)
                spawnEntry.NightOnly = bool.Parse(list[3]);
            if (!Data.SimpleName.TryGetValue(list[4], out string? palName))
            {
                if (Data.PalData.ContainsKey(list[4]))
                {
                    palName = list[4];
                }
                else
                {
                    throw new Exception($"Unknown Character Name: \"{list[4]}\"");
                }
            }
            spawnEntry.SpawnList.Add(new()
            {
                Name = palName,
                IsBoss = bool.Parse(list[5]),
                MinLevel = (int) uint.Parse(list[6]),
                MaxLevel = (int) uint.Parse(list[7]),
                MinCount = (int) uint.Parse(list[8]),
                MaxCount = (int) uint.Parse(list[9])
            });
        }
        List<AreaData> areaList = [.. areaDict.Values];
        areaList.ForEach(area => area.SpawnEntries.RemoveAll(entry => entry.SpawnList.Count == 0));
        Data.AreaForEachIfDiff(areaList, area => area.modified = true);
        areaList.Sort(AreaSortFunc);
        return areaList;
    }

    public class PalSchemaJson
    {
        public string FilePath { get; set; } = string.Empty;
        public string JsonData { get; set; } = string.Empty;
    }

    public static void SavePalSchema(List<AreaData> areaList)
    {
        SaveFileDialog saveDialog = new()
        {
            FileName = $"PalworldSpawns-PalSchema-{DateTime.Now:MM-dd-yy-HH-mm-ss}",
            DefaultExt = ".zip",
            Filter = "ZIP Archive|*.zip"
        };

        if (saveDialog.ShowDialog() == true)
        {
            List<PalSchemaJson> schemaList = GeneratePalSchema(areaList);

            using FileStream fileStream = File.Create(saveDialog.FileName);
            using ZipArchive zipArchive = new(fileStream, ZipArchiveMode.Create);
            foreach (PalSchemaJson schema in schemaList)
            {
                using Stream entryStream = zipArchive.CreateEntry($"CustomPalSpawns/" + schema.FilePath).Open();
                using StreamWriter streamWriter = new(entryStream, Encoding.UTF8);
                streamWriter.Write(schema.JsonData);
            }
        }
    }

    /// <summary>
    /// Creates a list of <see cref="PalSchemaJson"/> containing spawn data.
    /// </summary>
    /// <param name="areaList">List of areas to create a PalSchema from.</param>
    public static List<PalSchemaJson> GeneratePalSchema(List<AreaData> areaList) {
        Dictionary<string, GameStruct> palSpawnSchema = [];
        Dictionary<string, GameStruct> eggSchema = [];
        float eggRespawnTime = new FormData().EggRespawnTime();

        foreach (var area in areaList.Where(x => !x.isCage)) {
            if (area.isEgg) {
                eggSchema.Add($"{area.FileNameWithoutExtension}_C",
                    new GameStruct {
                        SpawnPalEggLotteryDataArray = [.. area.SpawnEntries.Select(entry =>
                            new GameStruct {
                                PalEggData = new() { PalMonsterId = new() { Key = entry.SpawnList[0].Name } },
                                Weight_F = entry.Weight / 40.0f,
                            }
                        )],
                        RespawnTimeMinutesObtained = eggRespawnTime,
                    }
                );
            } else {
                palSpawnSchema.Add($"{area.FileNameWithoutExtension}_C",
                    new GameStruct {
                        SpawnGroupList = [.. area.SpawnEntries.Select(entry => {
                            var spawnEntry = new GameStruct {
                                Weight = entry.Weight,
                            };

                            if (entry.NightOnly) {
                                spawnEntry.OnlyTime = "Night";
                            }

                            spawnEntry.PalList = [.. entry.SpawnList.Select(spawn => {
                                var spawnData = new GameStruct();

                                if (spawn.IsPal) {
                                    spawnData.PalId = new() { Key = spawn.Name };
                                } else {
                                    spawnData.NPCID = new() { Key = spawn.Name };
                                }

                                spawnData.Level = spawn.MinLevel;
                                spawnData.Level_Max = spawn.MaxLevel;
                                spawnData.Num = spawn.MinCount;
                                spawnData.Num_Max = spawn.MaxCount;

                                return spawnData;
                            })];

                            return spawnEntry;
                        })]
                    }
                );
            }
        }

        List<PalSchemaJson> schemas = [];

        if (palSpawnSchema.Count != 0) {
            schemas.Add(new() {
                FilePath = $"blueprints/PalSpawns.json",
                JsonData = JsonConvert.SerializeObject(palSpawnSchema, Formatting.Indented, JsonSerializerSettings),
            });
        }

        if (eggSchema.Count != 0) {
            schemas.Add(new() {
                FilePath = $"blueprints/EggSpawns.json",
                JsonData = JsonConvert.SerializeObject(eggSchema, Formatting.Indented, JsonSerializerSettings),
            });
        }

        IEnumerable<AreaData> cages = areaList.Where(x => x.isCage);

        if (cages.Any()) {
            Dictionary<string, Dictionary<string, GameStruct?>> cageSchema = new() { ["DT_CapturedCagePal"] = [] };
            var originalCageList = Data.AreaDataCopy().Where(x => x.isCage);

            // Save the changed cages - unmodified cages remain vanilla
            foreach (var area in cages) {
                originalCageList.First(x => x.SimpleName == area.SimpleName).SpawnEntries = area.SpawnEntries;
            }

            cageSchema["DT_CapturedCagePal"].Add("*", null);

            int i = 0;
            foreach (var area in originalCageList) {
                foreach (var entry in area.SpawnEntries) {
                    cageSchema["DT_CapturedCagePal"].Add($"{++i}",
                        new GameStruct {
                            FieldName = area.filename,
                            PalId_S = entry.SpawnList[0].Name,
                            Weight_F = entry.Weight / 10.0f,
                            MinLevel = entry.SpawnList[0].MinLevel,
                            MaxLevel = entry.SpawnList[0].MaxLevel,
                        }
                    );
                }
            }

            schemas.Add(new() {
                FilePath = "raw/Cages.json",
                JsonData = JsonConvert.SerializeObject(cageSchema, Formatting.Indented, JsonSerializerSettings),
            });
        }

        return schemas;
    }
    public static string? LoadPalSchema()
    {
        OpenFileDialog openDialog = new()
        {
            DefaultExt = ".zip",
            Filter = "Zip Archive|*.zip|JSON file|*.json|All files|*.*"
        };

        if (openDialog.ShowDialog() != true || openDialog.FileName.Length == 0)
        {
            return "Cancel";
        }

        try
        {
            List<AreaData> areaList = Data.AreaDataCopy();

            if (string.Equals(Path.GetExtension(openDialog.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                ConvertPalSchemaZIP(areaList, openDialog.FileName);
            }
            else
            {
                ConvertPalSchemaJSON(areaList, File.ReadAllText(openDialog.FileName, Encoding.UTF8));
            }

            Data.AreaForEachIfDiff(areaList, x => x.modified = true);
            areaList.Sort(AreaSortFunc);

            Randomize.SaveBackup();
            PalSpawnPage.Instance.areaList.ItemsSource = areaList;
            Randomize.AreaListChanged = true;
        }
        catch (Exception e)
        {
            return e.Message;
        }

        return null;
    }

    public static void ConvertPalSchemaZIP(List<AreaData> areaList, string filename)
    {
        using ZipArchive zipArchive = ZipFile.OpenRead(filename);
        foreach (ZipArchiveEntry entry in zipArchive.Entries)
        {
            if (!string.Equals(Path.GetExtension(entry.Name), ".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream entryStream = entry.Open();
            using StreamReader streamReader = new(entryStream, Encoding.UTF8);
            ConvertPalSchemaJSON(areaList, streamReader.ReadToEnd());
        }
    }

    /// <summary>JSON serializer settings with a custom converter for <see cref="GameStruct"/>s.</summary>
    public static JsonSerializerSettings JsonSerializerSettings { get; } =
        new() { Converters = [new JsonConverterGameStruct()] };
    /// <summary>JSON serializer with a custom <see cref="GameStruct"/> converter.</summary>
    public static JsonSerializer JsonSerializer { get; } = JsonSerializer.CreateDefault(JsonSerializerSettings);

    [GeneratedRegex("^(/Game/Pal/Blueprint/(?<folder>.+?)/(?<package>[^./]+)\\.)?(?<class>[^./]+?)_C$", RegexOptions.ExplicitCapture)]
    private static partial Regex schemaPathRegex();

    /// <summary>
    /// Replaces the data in the area list with that of a PalSchema json file.
    /// </summary>
    /// <param name="areaList">List of areas to mutate.</param>
    /// <param name="jsonData">Json-formatted string in PalSchema format.</param>
    public static void ConvertPalSchemaJSON(List<AreaData> areaList, string jsonData) {
        var areaDict = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(jsonData);

        if (areaDict == null) {
            return;
        }

        foreach (var (key, value) in areaDict) {
            if (key == "DT_CapturedCagePal") {
                var dataTable = value.ToObject<Dictionary<string, GameStruct>>(JsonSerializer);

                if (dataTable == null) {
                    continue;
                }

                var cageDictionary = areaList.Where(x => x.isCage).ToDictionary(x => x.filename, x => x);
                foreach (var (_, area) in cageDictionary) {
                    area.SpawnEntries.Clear();
                }

                foreach (var (_, entry) in dataTable) {
                    if (entry == null) {
                        continue;
                    }

                    cageDictionary[entry.FieldName].SpawnEntries.Add(new SpawnEntry {
                        Weight = Convert.ToInt32(entry.Weight_F * 10),
                        SpawnList = [
                            new SpawnData {
                                Name = entry.PalId_S,
                                MinLevel = entry.MinLevel,
                                MaxLevel = entry.MaxLevel
                            }
                        ]
                    });
                }
            } else {
                Match regexMatch = schemaPathRegex().Match(key);

                if (!regexMatch.Success) {
                    continue;
                }

                var area = areaList.Find(x => x.FileNameWithoutExtension.Equals(regexMatch.Groups["class"].Value,
                    StringComparison.OrdinalIgnoreCase));

                if (area == null) {
                    continue;
                }

                var spawner = value.ToObject<GameStruct>(JsonSerializer);

                if (spawner == null) {
                    continue;
                }

                area.SpawnEntries = regexMatch.Groups["class"].Value switch {
                    var x when x.StartsWith("BP_PalSpawner_Sheets_", StringComparison.OrdinalIgnoreCase) =>
                        [.. spawner.PalSpawnsToSpawnEntries()],
                    var x when x.StartsWith("bp_palmapobjectspawner_", StringComparison.OrdinalIgnoreCase) =>
                        [.. spawner.PalEggsToSpawnEntries()],
                    var x => throw new Exception($"Unidentified spawn type '{x}'"),
                };
            }
        }
    }
}