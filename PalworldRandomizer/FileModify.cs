using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalworldRandomizer.PalSpawn;
using PalworldRandomizer.Window;

namespace PalworldRandomizer;

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

    public static Dictionary<string, AreaData> ReadCageData(IEnumerable<CagePalData> cagePalDataList)
    {
        Dictionary<string, AreaData> cageList = [];
        foreach (CagePalData cagePalData in cagePalDataList)
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
                Weight = Convert.ToInt32(cagePalData.Weight * 10),
                SpawnList = [new()
                {
                    Name = cagePalData.PalID!,
                    MinLevel = cagePalData.MinLevel,
                    MaxLevel = cagePalData.MaxLevel
                }]
            });
        }
        return cageList;
    }

    public static AreaData ReadEggData(string filename, PalMapObject.SpawnerPalEgg spawner) =>
        new(new(), $"PalEgg\\{filename}") {
        isEgg = true,
        minLevel = 1,
        maxLevel = 1,
        SpawnEntries = [.. spawner.ToSpawnEntries()],
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

    public class PalMonsterData
    {
        public string? Key { get; set; } = null;
    }

    public class PalSpawnerOneTribeInfo
    {
        public PalMonsterData PalId { get; set; } = new();
        public PalMonsterData NPCID { get; set; } = new();
        public int Level { get; set; } = 1;
        public int Level_Max { get; set; } = 1;
        public int Num { get; set; } = 1;
        public int Num_Max { get; set; } = 1;
    }

    public class PalSpawnerGroupInfo
    {
        public int Weight { get; set; } = 1;
        public string OnlyTime { get; set; } = "Undefined";
        public PalSpawnerOneTribeInfo[] PalList { get; set; } = [];
    }

    public class PalSpawner
    {
        public PalSpawnerGroupInfo[] SpawnGroupList { get; set; } = [];

        public IEnumerable<SpawnEntry> ToSpawnEntries() => SpawnGroupList.Select(entry =>
            new SpawnEntry {
                Weight = entry.Weight,
                NightOnly = entry.OnlyTime == "Night" || entry.OnlyTime == "EPalOneDayTimeType::Night",
                SpawnList = [.. entry.PalList.Select(spawn => {
                        string characterId = (spawn.PalId.Key == "None" ? null : spawn.PalId.Key)
                            ?? (spawn.NPCID.Key == "None" ? null : spawn.NPCID.Key)
                            ?? "RowName";

                        return new SpawnData
                        {
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
    }

    public static class PalMapObject
    {
        public static class PickupItem
        {
            public class PalEggData
            {
                public PalMonsterData PalMonsterId { get; set; } = new();
            }

            public class PalEggLotteryData
            {
                public PalEggData PalEggData { get; set; } = new();
                public float Weight { get; set; } = 1.0f;
            }
        }

        public class SpawnerPalEgg
        {
            public PickupItem.PalEggLotteryData[] SpawnPalEggLotteryDataArray { get; set; } = [];
            public float RespawnTimeMinutesObtained { get; set; } = 180.0f;

            public IEnumerable<SpawnEntry> ToSpawnEntries() => SpawnPalEggLotteryDataArray.Select(entry =>
                new SpawnEntry {
                    Weight = Convert.ToInt32(entry.Weight * 40),
                    SpawnList =
                    [
                        new SpawnData
                        {
                            Name = entry.PalEggData.PalMonsterId.Key!
                        }
                    ]
                }
            );
        }
    }

    public class PalCapturedCageInfoDatabaseRow
    {
        public string FieldName { get; set; } = string.Empty;
        public string PalId { get; set; } = string.Empty;
        public float Weight { get; set; } = 1.0f;
        public int MinLevel { get; set; } = 1;
        public int MaxLevel { get; set; } = 1;
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

    public static List<PalSchemaJson> GeneratePalSchema(List<AreaData> areaList)
    {
        Dictionary<string, PalSpawner> PalSpawnSchema = [];
        Dictionary<string, PalMapObject.SpawnerPalEgg> EggSchema = [];
        float eggRespawnTime = new FormData().EggRespawnTime();

        foreach (AreaData area in areaList.Where(x => !x.isCage))
        {
            if (area.isEgg)
            {
                EggSchema.Add($"{area.FileNameWithoutExtension}_C",
                    new PalMapObject.SpawnerPalEgg
                    {
                        SpawnPalEggLotteryDataArray = [.. area.SpawnEntries.Select(entry =>
                            new PalMapObject.PickupItem.PalEggLotteryData
                            {
                                PalEggData = new() { PalMonsterId = new() { Key = entry.SpawnList[0].Name } },
                                Weight = entry.Weight / 40.0f
                            }
                        )],
                        RespawnTimeMinutesObtained = eggRespawnTime
                    }
                );
            }
            else
            {
                PalSpawnSchema.Add($"{area.FileNameWithoutExtension}_C",
                    new PalSpawner
                    {
                        SpawnGroupList = [.. area.SpawnEntries.Select(entry =>
                            new PalSpawnerGroupInfo
                            {
                                Weight = entry.Weight,
                                OnlyTime = entry.NightOnly ? "Night" : "Undefined",
                                PalList = [.. entry.SpawnList.Select(spawn =>
                                    new PalSpawnerOneTribeInfo
                                    {
                                        PalId = new() { Key = Data.PalData[spawn.Name].IsPal ? spawn.Name : "None" },
                                        NPCID = new() { Key = !Data.PalData[spawn.Name].IsPal ? spawn.Name : "None" },
                                        Level = spawn.MinLevel,
                                        Level_Max = spawn.MaxLevel,
                                        Num = spawn.MinCount,
                                        Num_Max = spawn.MaxCount
                                    }
                                )]
                            }
                        )]
                    }
                );
            }
        }

        List<PalSchemaJson> schemas = [];

        if (PalSpawnSchema.Count != 0)
        {
            schemas.Add(new() { FilePath = $"blueprints/PalSpawns.json", JsonData = JsonConvert.SerializeObject(PalSpawnSchema, Formatting.Indented) });
        }

        if (EggSchema.Count != 0)
        {
            schemas.Add(new() { FilePath = $"blueprints/EggSpawns.json", JsonData = JsonConvert.SerializeObject(EggSchema, Formatting.Indented) });
        }

        IEnumerable<AreaData> cages = areaList.Where(x => x.isCage);
        if (cages.Any())
        {
            Dictionary<string, Dictionary<string, PalCapturedCageInfoDatabaseRow?>> cageSchema = new() { ["DT_CapturedCagePal"] = [] };

            IEnumerable<AreaData> originalCageList = Data.AreaDataCopy().Where(x => x.isCage);

            // Save the changed cages - unmodified cages remain vanilla
            foreach (AreaData area in cages)
            {
                originalCageList.First(x => x.SimpleName == area.SimpleName).SpawnEntries = area.SpawnEntries;
            }

            cageSchema["DT_CapturedCagePal"].Add("*", null);

            int i = 0;
            foreach (AreaData area in originalCageList)
            {
                foreach (SpawnEntry entry in area.SpawnEntries)
                {
                    cageSchema["DT_CapturedCagePal"].Add($"{++i}",
                        new PalCapturedCageInfoDatabaseRow
                        {
                            FieldName = area.filename,
                            PalId = entry.SpawnList[0].Name,
                            Weight = entry.Weight / 10.0f,
                            MinLevel = entry.SpawnList[0].MinLevel,
                            MaxLevel = entry.SpawnList[0].MaxLevel
                        }
                    );
                }
            }

            schemas.Add(new() { FilePath = "raw/Cages.json", JsonData = JsonConvert.SerializeObject(cageSchema, Formatting.Indented) });
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

    [GeneratedRegex("^(/Game/Pal/Blueprint/(?<folder>.+?)/(?<package>[^./]+)\\.)?(?<class>[^./]+?)_C$", RegexOptions.ExplicitCapture)]
    private static partial Regex schemaPathRegex();

    public static void ConvertPalSchemaJSON(List<AreaData> areaList, string jsonData)
    {
        Dictionary<string, JObject>? areaDict = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(jsonData);

        if (areaDict == null)
        {
            return;
        }

        foreach ((string key, JObject value) in areaDict)
        {
            if (key == "DT_CapturedCagePal")
            {
                Dictionary<string, PalCapturedCageInfoDatabaseRow?>? rows = value.ToObject<Dictionary<string, PalCapturedCageInfoDatabaseRow?>>();
                if (rows == null)
                {
                    continue;
                }

                Dictionary<string, AreaData> cageDictionary = areaList.Where(x => x.isCage).ToDictionary(x => x.filename, x => x);
                foreach ((_, AreaData area) in cageDictionary)
                {
                    area.SpawnEntries.Clear();
                }

                foreach ((_, PalCapturedCageInfoDatabaseRow? row) in rows)
                {
                    if (row == null)
                    {
                        continue;
                    }

                    cageDictionary[row.FieldName].SpawnEntries.Add(new SpawnEntry
                    {
                        Weight = Convert.ToInt32(row.Weight * 10),
                        SpawnList =
                        [
                            new SpawnData
                            {
                                Name = row.PalId,
                                MinLevel = row.MinLevel,
                                MaxLevel = row.MaxLevel
                            }
                        ]
                    });
                }
            }
            else
            {
                Match regexMatch = schemaPathRegex().Match(key);
                if (!regexMatch.Success)
                {
                    continue;
                }

                AreaData? area = areaList.Find(x => x.FileNameWithoutExtension.Equals(regexMatch.Groups["class"].Value, StringComparison.OrdinalIgnoreCase));
                if (area == null)
                {
                    continue;
                }

                if (regexMatch.Groups["class"].Value.StartsWith("BP_PalSpawner_Sheets_", StringComparison.OrdinalIgnoreCase))
                {
                    PalSpawner? spawner = value.ToObject<PalSpawner>();
                    if (spawner == null)
                    {
                        continue;
                    }

                    area.SpawnEntries = [.. spawner.ToSpawnEntries()];

                }
                else if (regexMatch.Groups["class"].Value.StartsWith("bp_palmapobjectspawner_", StringComparison.OrdinalIgnoreCase))
                {
                    PalMapObject.SpawnerPalEgg? spawner = value.ToObject<PalMapObject.SpawnerPalEgg>();
                    if (spawner == null)
                    {
                        continue;
                    }

                    area.SpawnEntries = [.. spawner.ToSpawnEntries()];
                }
            }
        }
    }
}