using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CUE4Parse.Utils;
using Newtonsoft.Json;
using PalworldRandomizer.Randomizer.PalSpawn;
using PalworldRandomizer.Serializer;
using PalworldRandomizer.Window;
using Stfu.Linq;

namespace PalworldRandomizer.Randomizer;

public static partial class Randomize
{
    public static List<AreaData> GeneratedAreaList { get; private set; } = [];
    private static Dictionary<string, SpawnEntry> basicSpawns = [];
    private static Dictionary<string, SpawnEntry> bossSpawns = [];
    private static List<SpawnEntry> humanSpawns = [];
    private static int minBossLevel = 4;
    private static int maxBossLevel = 8;
    private static int minAddLevel = 2;
    private static int maxAddLevel = 6;
    private static bool _areaListChanged = false;
    public static bool AreaListChanged
    { 
        get => _areaListChanged;

        set
        {
            _areaListChanged = value;

            if (value)
            {
                MainPage.Instance.savePalSchema.IsEnabled = false;
            }
        }
    }
    public static bool AutoSaveRestoreBackups { get; set; } = true;
    public static bool AutoSaveGenerationData { get; set; } = true;

    public static void Initialize()
    {
        GeneratedAreaList = Data.AreaDataCopy();
    }
    private static void GenerateSpawnLists(FormData formData)
    {
        minBossLevel = 100 - 1;
        maxBossLevel = 100 + 1;
        minAddLevel = Math.Max(1, formData.BossAddLevel) - 1;
        maxAddLevel = Math.Max(1, formData.BossAddLevel) + 1;
        basicSpawns = [];
        bossSpawns = [];
        humanSpawns = [];
        if (formData.GroupVanilla)
        {
            IEnumerable<string> palList = [
                .. formData.SpawnPals || formData.SpawnAlphas ? Data.PalList : [],
                .. formData.SpawnTerraria || formData.SpawnTerrariaBosses ? Data.TerrariaMonsters : []
            ];
            foreach (string key in palList)
            {
                bool isTerraria = key.Contains("Yakushima");
                bool spawnBasicPal = formData.SpawnPals && !isTerraria || formData.SpawnTerraria && isTerraria;
                if (spawnBasicPal)
                {
                    SpawnEntry basicEntry = new();
                    basicSpawns.Add(key, basicEntry);
                    if (Data.SoloEntries.TryGetValue(key, out List<SpawnEntry>? soloValue))
                    {
                        SpawnData spawnData = new() { Name = key };
                        basicEntry.SpawnList.Add(spawnData);
                        int minCount = 1;
                        int maxCount = 1;
                        foreach (SpawnEntry spawnEntry in soloValue)
                        {
                            minCount = Math.Max(minCount, spawnEntry.SpawnList[0].MinCount);
                            maxCount = Math.Max(maxCount, spawnEntry.SpawnList[0].MaxCount);
                        }
                        spawnData.MinCount = minCount;
                        spawnData.MaxCount = maxCount;
                    }
                    else if (Data.GroupEntries.Exists(entry => entry.SpawnList.Exists(spawnData => spawnData.Name == key)))
                    {
                        foreach (SpawnEntry spawnEntry in Data.GroupEntries)
                        {
                            int palCount = 0;
                            if (spawnEntry.SpawnList[0].Name == key)
                            {
                                int currentCount = 0;
                                foreach (SpawnData currentData in spawnEntry.SpawnList)
                                {
                                    currentCount += currentData.MinCount + currentData.MaxCount;
                                }
                                if (currentCount > palCount)
                                {
                                    basicEntry.SpawnList = spawnEntry.SpawnList; // shallow copy
                                    palCount = currentCount;
                                }
                            }
                        }
                    }
                    else
                    {
                        SpawnData spawnData = new() { Name = key };
                        basicEntry.SpawnList.Add(spawnData);
                        if (key.Contains('_'))
                        {
                            string baseName = key[..key.IndexOf('_')];
                            if (Data.SoloEntries.TryGetValue(baseName, out List<SpawnEntry>? soloValue2))
                            {
                                spawnData.MinCount = soloValue2[0].SpawnList[0].MinCount;
                                spawnData.MaxCount = soloValue2[0].SpawnList[0].MaxCount;
                            }
                        }
                    }
                }
                if (formData.SpawnAlphas && !isTerraria || formData.SpawnTerrariaBosses && isTerraria)
                {
                    // TODO: Make this dynamic
                    // Skip Necromus since it is included with Paladius
                    // + Celesdir is included with Celesdir Noct
                    if (key is "BlackCentaur" or "WhiteDeer") {
                        continue;
                    }

                    // Problem here if there are ever vanilla bosses with no non-boss counterpart
                    if (Data.BossName.TryGetValue(key, out string? bossKey))
                    {
                        SpawnEntry bossEntry = new();
                        bossSpawns.Add(bossKey, bossEntry);
                        if (Data.BossEntries.TryGetValue(bossKey, out List<SpawnEntry>? value))
                        {
                            bossEntry.SpawnList.AddRange(value.MaxBy(spawnEntry => spawnEntry.SpawnList.Sum(x => x.MinCount + x.MaxCount))!.SpawnList.ConvertAll(x => x.Clone()));
                        }
                        else
                        {
                            SpawnData bossData = new() { Name = bossKey };
                            bossEntry.SpawnList.Add(bossData);
                            if (bossKey["BOSS_".Length..].Contains('_'))
                            {
                                SpawnEntry baseEntry = bossSpawns[bossKey[..bossKey.LastIndexOf('_')]];
                                if (baseEntry.SpawnList.Count > 1)
                                {
                                    bossEntry.SpawnList.Add(new()
                                    {
                                        Name = key,
                                        MinLevel = baseEntry.SpawnList[1].MinLevel,
                                        MaxLevel = baseEntry.SpawnList[1].MaxLevel,
                                        MinCount = baseEntry.SpawnList[1].MinCount,
                                        MaxCount = baseEntry.SpawnList[1].MaxCount
                                    });
                                    bossData.MinLevel = baseEntry.SpawnList[0].MinLevel;
                                    bossData.MaxLevel = baseEntry.SpawnList[0].MaxLevel;
                                }
                            }
                        }
                        if (bossEntry.SpawnList.Count == 1 && spawnBasicPal && (basicSpawns[key].SpawnList[0].MaxCount > 1 || Data.PalData[key].Rarity < 6))
                        {
                            bossEntry.SpawnList.Add(new()
                            {
                                Name = key,
                                MinCount = Math.Max(2, basicSpawns[key].SpawnList[0].MinCount),
                                MaxCount = Math.Max(2, basicSpawns[key].SpawnList[0].MaxCount),
                                MinLevel = minAddLevel,
                                MaxLevel = maxAddLevel
                            });
                            bossEntry.SpawnList[0].MinLevel = minBossLevel;
                            bossEntry.SpawnList[0].MaxLevel = maxBossLevel;
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, List<SpawnEntry>> keyPair in Data.SoloEntries)
            {
                if (!Data.PalData[keyPair.Key].IsPal &&
                    ((formData.SpawnHumans && Data.HumanNames.Contains(keyPair.Key))
                    || (formData.SpawnPolice && Data.PoliceNames.Contains(keyPair.Key)))
                    )
                {
                    SpawnData spawnData = new() { Name = keyPair.Key };
                    humanSpawns.Add(new() { SpawnList = [spawnData] });
                    int minCount = 1;
                    int maxCount = 1;
                    foreach (SpawnEntry spawnEntry in keyPair.Value)
                    {
                        spawnData.MinCount = Math.Max(minCount, spawnEntry.SpawnList[0].MinCount);
                        spawnData.MaxCount = Math.Max(maxCount, spawnEntry.SpawnList[0].MaxCount);
                    }
                }
            }
            List<SpawnEntry> groupEntriesCopy = [.. Data.GroupEntries];
            for (int i = 0; i < groupEntriesCopy.Count; ++i)
            {
                SpawnEntry spawnEntry = groupEntriesCopy[i];
                if (!spawnEntry.SpawnList[0].IsPal &&
                    ((formData.SpawnHumans && Data.HumanNames.Contains(spawnEntry.SpawnList[0].Name))
                    || (formData.SpawnPolice && Data.PoliceNames.Contains(spawnEntry.SpawnList[0].Name)))
                    )
                {
                    humanSpawns.Add(spawnEntry); // shallow copy
                    int humanCount = 0;
                    foreach (SpawnData spawnData in spawnEntry.SpawnList)
                    {
                        humanCount += spawnData.MinCount + spawnData.MaxCount;
                    }
                    for (int j = i + 1; j < groupEntriesCopy.Count;)
                    {
                        SpawnEntry spawnEntry2 = groupEntriesCopy[j];
                        if (spawnEntry2.SpawnList.Count != spawnEntry.SpawnList.Count)
                        {
                            ++j;
                            continue;
                        }
                        int currentCount = 0;
                        if (((Func<bool>) (() =>
                        {
                            for (int k = 0; k < spawnEntry.SpawnList.Count; ++k)
                            {
                                if (spawnEntry.SpawnList[k].Name != spawnEntry2.SpawnList[k].Name)
                                {
                                    return false;
                                }
                                currentCount += spawnEntry2.SpawnList[k].MinCount + spawnEntry2.SpawnList[k].MaxCount;
                            }
                            return true;
                        }))())
                        {
                            if (currentCount > humanCount)
                            {
                                humanSpawns[^1] = spawnEntry2; // shallow copy
                                humanCount = currentCount;
                            }
                            groupEntriesCopy.RemoveAt(j);
                        }
                        else
                        {
                            ++j;
                        }
                    }
                }
            }

            static void AddSimpleHumanSpawns(IEnumerable<string> names) =>
                humanSpawns.AddRange(names
                    .Where(name => !humanSpawns.Exists(x => x.SpawnList.Exists(y => y.Name == name)))
                    .Select(name =>
                    Data.PalData[name].Weapon switch {
                        "FlameThrower" or "RocketLauncher" or "MissileLauncher" or "GrenadeLauncher"
                            => new SpawnEntry { SpawnList = [new(name, 1, 2)] },
                        string n when n is "GatlingGun" || name.Contains("Fat")
                            => new SpawnEntry { SpawnList = [new(name)] },
                        _ => new SpawnEntry { SpawnList = [new(name, 2, 3)] },
                    }
                ));

            if (formData.SpawnPolice) {
                AddSimpleHumanSpawns(Data.PoliceNames);
            }

            if (formData.SpawnGuards) {
                AddSimpleHumanSpawns(Data.GuardNames);
            }

            if (formData.SpawnHumans) {
                AddSimpleHumanSpawns(Data.HumanNames);
            }

            if (formData.SpawnTraders)
            {
                humanSpawns.AddRange(Data.TraderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }
            if (formData.SpawnPalTraders)
            {
                humanSpawns.AddRange(Data.PalTraderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }
            if (formData.SpawnSpecial)
            {
                humanSpawns.AddRange(Data.SpecialNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }
            if (formData.SpawnTowerHumans)
            {
                humanSpawns.AddRange(Data.TowerHumanNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }

            if (formData.SpawnPolicePals) {
                Data.PolicePalNames.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name, 1, 3)] }));
            }

            if (formData.SpawnVillagers) {
                humanSpawns.AddRange(Data.VillagerNames.Select(name => new SpawnEntry { SpawnList = [new(name, 1, 3)] }));
            }
        }
        else if (formData.GroupRandom)
        {
            if (formData.SpawnPals)
            {
                Data.PalList.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            if (formData.SpawnAlphas)
            {
                Data.PalList.FindAll(Data.BossName.ContainsKey).ConvertAll(name => Data.BossName[name])
                    .ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            if (formData.SpawnTerraria)
            {
                Data.TerrariaMonsters.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            if (formData.SpawnTerrariaBosses)
            {
                Data.TerrariaMonstersBosses.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            humanSpawns.AddRange(((IEnumerable<string>)
            [
                .. formData.SpawnHumans ? Data.HumanNames : [],
                .. formData.SpawnPolice ? Data.PoliceNames : [],
                .. formData.SpawnGuards ? Data.GuardNames : [],
                .. formData.SpawnTraders ? Data.TraderNames : [],
                .. formData.SpawnPalTraders ? Data.PalTraderNames : [],
                .. formData.SpawnSpecial ? Data.SpecialNames : [],
                .. formData.SpawnTowerHumans ? Data.TowerHumanNames : [],
            ]).Select(name => new SpawnEntry { SpawnList = [new(name)] }));

            if (formData.SpawnPolicePals) {
                Data.PolicePalNames.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }

            if (formData.SpawnVillagers) {
                humanSpawns.AddRange(Data.VillagerNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }
        }
        if (formData.SpawnTowerBosses)
        {
            Data.TowerBossNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            Data.TowerNonBossNames.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name)] }));
        }
        if (formData.SpawnRaidBosses)
        {
            Data.RaidBossNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
        }
        if (formData.SpawnPredators)
        {
            Data.PredatorNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
        }
        if (formData.SpawnHumanBosses)
        {
            Data.HumanBossNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
        }

        if (formData.SpawnUselessHumans) {
            humanSpawns.AddRange(Data.UselessHumanNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
        }

    }
    private static ICollection<string> GetAllowedNames(FormData formData) => [
        .. formData.SpawnPals ? Data.PalList : [],
        .. formData.SpawnAlphas ? Data.PalList.FindAll(Data.BossName.ContainsKey).ConvertAll(name => Data.BossName[name]) : [],
        .. formData.SpawnTowerBosses ? Data.TowerBossNames : [],
        .. formData.SpawnRaidBosses ? Data.RaidBossNames : [],
        .. formData.SpawnPredators ? Data.PredatorNames : [],
        .. formData.SpawnHumanBosses ? Data.HumanBossNames : [],
        .. formData.SpawnHumans ? Data.HumanNames : [],
        .. formData.SpawnPolice ? Data.PoliceNames : [],
        .. formData.SpawnGuards ? Data.GuardNames : [],
        .. formData.SpawnTraders ? Data.TraderNames : [],
        .. formData.SpawnPalTraders ? Data.PalTraderNames : [],
        .. formData.SpawnSpecial ? Data.SpecialNames : [],
        .. formData.SpawnTerraria ? Data.TerrariaMonsters : [],
        .. formData.SpawnTerrariaBosses ? Data.TerrariaMonstersBosses : [],
        .. formData.SpawnTowerHumans ? Data.TowerHumanNames : [],
        .. formData.SpawnPolicePals ? Data.PolicePalNames : [],
        .. formData.SpawnUselessHumans ? Data.UselessHumanNames : [],
        .. formData.SpawnVillagers ? Data.VillagerNames : [],
        "RowName"
    ];

    [GeneratedRegex("^(?<prefix>Quest(_[^_]+)?_)?(?<name>.+)$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex questNameRegex();

    private static void RandomizeAndSaveAssets(FormData formData)
    {
        SaveBackup();
        bool outputLog = false;
        MainPage.Instance.Dispatcher.Invoke(() => outputLog = MainPage.Instance.outputLog.IsChecked == true);
        StringBuilder outputLogBuilder = outputLog ? new($"Random Seed: {formData.RandomSeed}\n\n") : null!;
        double predatorChance = Math.Clamp(formData.PredatorChance, 0, 100) / 100.0;
        int minGroup = Math.Max(1, formData.GroupMin);
        int maxGroup = Math.Max(minGroup, formData.GroupMax);
        int minGroupBoss = Math.Max(1, formData.GroupMinBoss);
        int maxGroupBoss = Math.Max(minGroupBoss, formData.GroupMaxBoss);
        int spawnListSize = Math.Max(1, formData.SpawnListSize);
        double vanillaPlusChance = Math.Clamp(formData.VanillaPlusChance, 1, 99) / 100.0;
        float fieldLevel = Math.Max(0, formData.FieldLevel) / 100.0f;
        float dungeonLevel = Math.Max(0, formData.DungeonLevel) / 100.0f;
        float fieldBossLevel = Math.Max(0, formData.FieldBossLevel) / 100.0f;
        float dungeonBossLevel = Math.Max(0, formData.DungeonBossLevel) / 100.0f;
        float predatorLevel = Math.Max(0, formData.PredatorLevel) / 100.0f;
        float cageLevel = Math.Max(0, formData.CageLevel) / 100.0f;
        int levelCap = formData.EnableLevelCap ? Math.Max(1, formData.LevelCap) : 80;
        int randomLevelMin = Math.Clamp(formData.RandomLevelMin, 1, levelCap);
        int randomLevelMax = Math.Clamp(formData.RandomLevelMax, randomLevelMin, levelCap);
        int rarity67MinLevel = Math.Clamp(formData.Rarity67MinLevel, 1, levelCap);
        int rarity8UpMinLevel = Math.Clamp(formData.Rarity8UpMinLevel, 1, levelCap);
        double bossesEverywhereChance = Math.Clamp(formData.BossesEverywhereChance, 1, 100) / 100.0;
        double bossesEverywhereDungeonsChance = Math.Clamp(formData.BossesEverywhereDungeonsChance, 1, 100) / 100.0;
        double bossEggsChance = Math.Clamp(formData.BossEggsChance, 1, 100) / 100.0;
        int weightUniformMin = Math.Max(1, formData.WeightUniformMin);
        int weightUniformMax = Math.Max(weightUniformMin, formData.WeightUniformMax);
        int humanRarity = Math.Clamp(formData.HumanRarity, 1, 20);
        int humanBossRarity = Math.Clamp(formData.HumanBossRarity, 1, 20);
        int maxCageRarity = Math.Clamp(formData.MaxCageRarity, 1, 20);
        float humanWeight = Math.Max(0, formData.HumanWeight) / 100.0f;
        float humanWeightAggro = Math.Max(0, formData.HumanWeightAggro) / 100.0f;
        double weightNightOnly = Math.Max(0, Convert.ToDouble(formData.WeightNightOnly));
        int baseCountMin = Math.Max(0, formData.BaseCountMin);
        int baseCountMax = Math.Max(Math.Max(1, baseCountMin), formData.BaseCountMax);
        float fieldCount = Math.Max(0, formData.FieldCount) / 100.0f;
        float dungeonCount = Math.Max(0, formData.DungeonCount) / 100.0f;
        float fieldBossCount = Math.Max(0, formData.FieldBossCount) / 100.0f;
        float dungeonBossCount = Math.Max(0, formData.DungeonBossCount) / 100.0f;
        float predatorCount = Math.Max(0, formData.PredatorCount) / 100.0f;
        int countClampMin = Math.Max(0, formData.CountClampMin);
        int countClampMax = Math.Max(Math.Max(1, countClampMin), formData.CountClampMax);
        int countClampFirstMin = Math.Max(0, formData.CountClampFirstMin);
        int countClampFirstMax = Math.Max(Math.Max(1, countClampFirstMin), formData.CountClampFirstMax);
        int countClampBossMin = Math.Max(0, formData.CountClampBossMin);
        int countClampBossMax = Math.Max(Math.Max(1, countClampBossMin), formData.CountClampBossMax);
        int countClampFirstBossMin = Math.Max(0, formData.CountClampFirstBossMin);
        int countClampFirstBossMax = Math.Max(Math.Max(1, countClampFirstBossMin), formData.CountClampFirstBossMax);
        float eggRespawnTime = formData.EggRespawnTime();
        int totalSpeciesCount = 0;
        string basePath = UAssetData.AppDataPath(@"Create-Pak");
        string outputPath = basePath + @"\Pal\Content\Pal\Blueprint\Spawner\SheetsVariant";
        string eggOutputPath = basePath + @"\Pal\Content\Pal\Blueprint\MapObject\Spawner";
        if (Directory.Exists(basePath))
        {
            Directory.GetFiles(basePath).ForAll(File.Delete);
            Directory.GetDirectories(basePath).ForAll(x => Directory.Delete(x, true));
        }
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(eggOutputPath);
        Dictionary<string, SpawnEntry> swapMap = [];
        List<SpawnEntry> basicSpawnsOriginal = [.. basicSpawns.Values, .. humanSpawns];
        List<SpawnEntry> bossSpawnsOriginal = [.. bossSpawns.Values];
        if (!formData.MethodNone)
        {
            if (formData.WeightTypeCustom)
            {
                FilterSpawns(basicSpawnsOriginal, bossSpawnsOriginal, spawnEntry =>
                {
                    spawnEntry.SpawnList.RemoveAll(spawnData =>
                    {
                        if (!Data.PalData[spawnData.Name].IsPal)
                        {
                            return false;
                        }
                        int rarity = Math.Clamp(Rarity(spawnData), 1, 20);
                        return !(rarity > 10 && rarity < 20) && formData.WeightCustom[rarity] == 0;
                    });
                });
                formData.WeightCustom = [.. formData.WeightCustom.Select(x => Math.Max(1, x))];
            }
            if (formData.Rarity9UpBossOnly)
            {
                FilterSpawns(basicSpawnsOriginal, bossSpawnsOriginal, spawnEntry =>
                {
                    spawnEntry.SpawnList.RemoveAll(x => !x.IsBoss && Rarity8Up(x, 9));
                });
            }
            void FilterSpawns(List<SpawnEntry> basicOrig, List<SpawnEntry> bossOrig, Action<SpawnEntry> filterAction)
            {
                basicSpawnsOriginal = basicSpawnsOriginal.ConvertAll(x => x.Clone());
                bossSpawnsOriginal = bossSpawnsOriginal.ConvertAll(x => x.Clone());
                foreach (SpawnEntry spawnEntry in (IEnumerable<SpawnEntry>) [.. basicSpawnsOriginal, .. bossSpawnsOriginal])
                {
                    filterAction(spawnEntry);
                }
                if (basicSpawnsOriginal.RemoveAll(x => x.SpawnList.Count == 0) != 0 && basicSpawnsOriginal.Count == 0)
                {
                    basicSpawnsOriginal = basicOrig;
                }
                if (bossSpawnsOriginal.RemoveAll(x => x.SpawnList.Count == 0 || !x.SpawnList[0].IsBoss) != 0 && bossSpawnsOriginal.Count == 0)
                {
                    bossSpawnsOriginal = bossOrig;
                }
            }
            if (formData.ForceAddLevel && formData.GroupVanilla)
            {
                foreach (SpawnEntry spawnEntry in bossSpawnsOriginal)
                {
                    if (spawnEntry.SpawnList.Exists(x => !x.IsBoss))
                    {
                        foreach (SpawnData spawnData in spawnEntry.SpawnList)
                        {
                            if (spawnData.IsBoss)
                            {
                                spawnData.MinLevel = minBossLevel;
                                spawnData.MaxLevel = maxBossLevel;
                            }
                            else
                            {
                                spawnData.MinLevel = minAddLevel;
                                spawnData.MaxLevel = maxAddLevel;
                            }
                        }
                    }
                }
            }
        }
        List<SpawnEntry> basicSpawnsCurrent = [.. basicSpawnsOriginal];
        List<SpawnEntry> bossSpawnsCurrent = [.. bossSpawnsOriginal];
        List<SpawnEntry> basicSpawnsOriginalBackup = [.. basicSpawnsOriginal];
        List<SpawnEntry> bossSpawnsOriginalBackup = [.. bossSpawnsOriginal];
        HashSet<string> allowedNames = [.. GetAllowedNames(formData)];
        foreach (string name in Data.PalData.Keys)
        {
            Match questNameMatch = questNameRegex().Match(name);
            if (questNameMatch.Groups["prefix"].Value.Length != 0 && allowedNames.Contains(questNameMatch.Groups["name"].Value)
                || name.EndsWith("_Invader", StringComparison.OrdinalIgnoreCase) && allowedNames.Contains(questNameMatch.Groups["name"].Value[..^"_Invader".Length])
                )
            {
                allowedNames.Add(name);
            }
        }
        Random random = new(formData.RandomSeed);
        List<AreaData> areaList = Data.AreaDataCopy();
        List<AreaData> subList = areaList.FindAll(area =>
            (
                (formData.RandomizeField || !area.IsField)
                && (formData.RandomizeDungeons || !area.IsDungeon)
                && (formData.RandomizeDungeonBosses || !area.IsDungeonBoss)
                && (formData.RandomizeFieldBosses || !area.IsFieldBoss)
                && (formData.RandomizeCages || !area.IsCage)
                && (formData.RandomizeEggs || !area.IsEgg)
                && (formData.RandomizeAllArea || !area.IsAllArea)
                && (formData.RandomizeSingleSpawns || !area.IsSingleSpawn)
                // Make global spawn setting take priority
                || (formData.RandomizeAllArea || !area.IsAllArea)
                &&
                    (formData.RandomizeAllArea && area.IsAllArea
                    || formData.RandomizeSingleSpawns && area.IsSingleSpawn)
            )
            && (!formData.StartSheepBall || area.Filename != "BP_PalSpawner_Sheets_green_A_SheepBall.uasset")
            && !area.IsHumanBoss);

        if (!formData.MethodNone)
        {
            List<AreaData> addedBosses = subList.FindAll(area => !area.IsBoss && !area.IsCage
                && BossesEverywhere(area)).ConvertAll(x => x.Clone());
            foreach (AreaData area in addedBosses)
            {
                area.IsBoss = true;
                area.Filename = $"~{area.Filename}";
            }
            subList.AddRange(addedBosses);
        }
        subList.Sort((x, y) =>
        {
            if (x.IsEgg != y.IsEgg)
                return (x.IsEgg ? 1 : 0) - (y.IsEgg ? 1 : 0);
            if (x.IsCage != y.IsCage)
                return (x.IsCage ? 1 : 0) - (y.IsCage ? 1 : 0);

            bool bossesEverywhereX = x.Filename.StartsWith('~');
            bool bossesEverywhereY = y.Filename.StartsWith('~');
            if (bossesEverywhereX != bossesEverywhereY) {
                return (bossesEverywhereX ? 1 : 0) - (bossesEverywhereY ? 1 : 0);
            }

            if (x.IsBoss != y.IsBoss)
                return (x.IsBoss ? 1 : 0) - (y.IsBoss ? 1 : 0);
            if (x.IsInDungeon != y.IsInDungeon)
                return (x.IsInDungeon ? 1 : 0) - (y.IsInDungeon ? 1 : 0);

            if (x.IsAllArea != y.IsAllArea) {
                return (y.IsAllArea ? 1 : 0) - (x.IsAllArea ? 1 : 0);
            }

            bool nightOnlyX = NightOnly(x);
            bool nightOnlyY = NightOnly(y);
            if (nightOnlyX != nightOnlyY)
                return (nightOnlyX ? 1 : 0) - (nightOnlyY ? 1 : 0);
            return string.Compare(x.Filename, y.Filename);
        });
        bool equalizeAreaRarity = formData.EqualizeAreaRarity && !formData.MethodNone && !formData.MethodFull && !formData.MethodGlobalSwap && !formData.VanillaRestrict;
        int progress = 0;
        int progressTotal = subList.Count;

        (int palsAdded, int pals8UpAdded) = (0, 0);
        (int bossesAdded, int bosses8UpAdded) = (0, 0);

        bool ShouldAdd8UpPal(List<SpawnEntry> original, int groupSize, bool isBoss) {
            if (formData.Rarity8UpSolo || !formData.Rarity8UpSanity || groupSize == 1) {
                return false;
            }

            int pals8UpCount = original.Count(x => Rarity8Up(x.SpawnList[0]));

            return isBoss
                ? (bosses8UpAdded + 1) * original.Count <= (bossesAdded + groupSize) * pals8UpCount
                : (pals8UpAdded + 1) * original.Count <= (palsAdded + groupSize) * pals8UpCount;
        }

        bool NightOnly(AreaData area, bool condition = true) => (formData.NightOnly == condition && area.IsField)
                || (formData.NightOnlyDungeons == condition && area.IsDungeon)
                || (formData.NightOnlyDungeonBosses == condition && area.IsDungeonBoss)
                || (formData.NightOnlyBosses == condition && area.IsFieldBoss);

        bool BossesEverywhere(AreaData area) => (area.IsEgg ? formData.BossEggs
            : (area.IsInDungeon ? formData.BossesEverywhereDungeons : formData.BossesEverywhere))
            && (!formData.VanillaRestrict || area.IsEgg || area.IsCage || !area.IsOnlyHumans);

        double BossesEverywhereChance(AreaData area) => area.IsEgg ? bossEggsChance : (area.IsInDungeon ? bossesEverywhereDungeonsChance : bossesEverywhereChance);
        int Rarity(SpawnData spawnData)
        {
            if (!Data.PalData[spawnData.Name].IsPal)
            {
                return spawnData.IsBoss ? humanBossRarity : humanRarity;
            }
            if (!spawnData.IsBoss || !spawnData.Name.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase))
            {
                int rarity = Data.PalData[spawnData.Name].Rarity;
                if (spawnData.IsBoss)
                {
                    int baseRarity = Data.PalData[spawnData.Name[(spawnData.Name.IndexOf('_') + 1)..]].Rarity;
                    return Math.Max(baseRarity, rarity);
                }
                return rarity;
            }
            return Data.PalData.TryGetValue(spawnData.Name["BOSS_".Length..], out var data) ? data.Rarity : Data.PalData[spawnData.Name].Rarity;
        }
        int RarityEx(SpawnData spawnData, bool customHumanRarity = true) =>
            !Data.PalData[spawnData.Name].IsPal && !customHumanRarity ? Data.PalData[spawnData.Name].Rarity : Rarity(spawnData);
        bool Rarity8Up(SpawnData spawnData, int min = 8)
        {
            return Rarity(spawnData) >= min && !spawnData.Name.EndsWith("PlantSlime_Flower", StringComparison.OrdinalIgnoreCase);
        }
        float LevelMultiplierEx(SpawnData spawnData, bool isDungeon, bool isCage, bool isEgg)
        {
            if (isEgg)
            {
                return 1;
            }
            if (isCage)
            {
                return cageLevel;
            }
            if (spawnData.Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase))
            {
                return predatorLevel;
            }
            if (isDungeon)
            {
                return spawnData.IsBoss ? dungeonBossLevel : dungeonLevel;
            }
            return spawnData.IsBoss ? fieldBossLevel : fieldLevel;
        }
        float CountMultiplierEx(SpawnData spawnData, bool isDungeon, bool isCage)
        {
            if (isCage)
            {
                return 1;
            }
            if (spawnData.Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase))
            {
                return predatorCount;
            }
            if (isDungeon)
            {
                return spawnData.IsBoss ? dungeonBossCount : dungeonCount;
            }
            return spawnData.IsBoss ? fieldBossCount : fieldCount;
        }
        void GenerateLevels(SpawnEntry spawnEntry, AreaData area, Func<SpawnData, float> LevelMultiplier)
        {
            int minLevel, maxLevel;
            if (spawnEntry.NightOnly && area.MinLevelNight != 0)
            {
                minLevel = area.MinLevelNight;
                maxLevel = area.MaxLevelNight;
            }
            else
            {
                minLevel = area.MinLevel;
                maxLevel = area.MaxLevel;
            }
            float range = maxLevel - minLevel;
            float average = (maxLevel + minLevel) / 2.0f;
            if (spawnEntry.SpawnList.Count > 1)
            {
                float firstRange = spawnEntry.SpawnList[0].MaxLevel - spawnEntry.SpawnList[0].MinLevel;
                float firstAverage = (spawnEntry.SpawnList[0].MaxLevel + spawnEntry.SpawnList[0].MinLevel) / 2.0f;
                for (int i = 1; i < spawnEntry.SpawnList.Count; ++i)
                {
                    float currentRange = spawnEntry.SpawnList[i].MaxLevel - spawnEntry.SpawnList[i].MinLevel;
                    float currentAverage = (spawnEntry.SpawnList[i].MaxLevel + spawnEntry.SpawnList[i].MinLevel) / 2.0f;
                    float newRange = (firstRange == 0 ? currentRange : range * currentRange / firstRange);
                    ApplyLevelRange(spawnEntry.SpawnList[i], LevelMultiplier(spawnEntry.SpawnList[i]), average * currentAverage / firstAverage, newRange, !area.IsCage && !area.IsEgg);
                }
            }
            ApplyLevelRange(spawnEntry.SpawnList[0], LevelMultiplier(spawnEntry.SpawnList[0]), average, range, !area.IsCage && !area.IsEgg);
        }
        void ApplyLevelRange(SpawnData spawnData, float multiplier, float average, float range, bool rarityCheck = true)
        {
            int forcedMinimum = 1;
            if (formData.RarityLevelBoost && rarityCheck && spawnData.IsPal)
            {
                int rarity = Rarity(spawnData);
                if ((rarity == 6 || rarity == 7) && !spawnData.Name.EndsWith("NightFox", StringComparison.OrdinalIgnoreCase))
                {
                    forcedMinimum = rarity67MinLevel;
                }
                else if (rarity >= 8 && !spawnData.Name.EndsWith("PlantSlime_Flower", StringComparison.OrdinalIgnoreCase))
                {
                    forcedMinimum = rarity8UpMinLevel;
                }
            }
            if (formData.LevelScaleMode == LevelScaleMode.Random)
            {
                spawnData.MinLevel = Math.Max(randomLevelMin, forcedMinimum);
                spawnData.MaxLevel = Math.Clamp(randomLevelMax, spawnData.MinLevel, levelCap);
            }
            else
            {
                int minLevel = Math.Clamp(Convert.ToInt32(average - range / 2.0f), 1, levelCap);
                int maxLevel = Math.Clamp(Convert.ToInt32(average + range / 2.0f), minLevel, levelCap);
                if (formData.LevelScaleMode == LevelScaleMode.MaxLevel)
                {
                    spawnData.MinLevel = Math.Max(minLevel, forcedMinimum);
                    spawnData.MaxLevel = Math.Clamp(Convert.ToInt32(maxLevel * multiplier), spawnData.MinLevel, levelCap);
                }
                else if (formData.LevelScaleMode == LevelScaleMode.MinLevel)
                {
                    spawnData.MaxLevel = Math.Max(maxLevel, forcedMinimum);
                    spawnData.MinLevel = Math.Clamp(Convert.ToInt32(minLevel * multiplier), forcedMinimum, spawnData.MaxLevel);
                }
                else if (formData.LevelScaleMode == LevelScaleMode.BothLevels)
                {
                    spawnData.MinLevel = Math.Clamp(Convert.ToInt32(minLevel * multiplier), forcedMinimum, levelCap);
                    spawnData.MaxLevel = Math.Clamp(Convert.ToInt32(maxLevel * multiplier), spawnData.MinLevel, levelCap);
                }
                else
                {
                    float newAverage = Math.Clamp(average * multiplier, 1, levelCap);
                    if (formData.LevelScaleMode == LevelScaleMode.Average)
                    {
                        spawnData.MinLevel = Math.Clamp(Convert.ToInt32(newAverage - range / 2.0f), 1, levelCap);
                        spawnData.MaxLevel = Math.Clamp(Convert.ToInt32(newAverage + range / 2.0f), spawnData.MinLevel, levelCap);
                    }
                    else if (formData.LevelScaleMode == LevelScaleMode.LockExtreme)
                    {
                        if (newAverage > average)
                        {
                            spawnData.MinLevel = minLevel;
                            ExtendToAverageMax(newAverage);
                        }
                        else
                        {
                            spawnData.MaxLevel = maxLevel;
                            ExtendToAverageMin(newAverage);
                        }
                    }
                    else if (formData.LevelScaleMode == LevelScaleMode.MaxRange)
                    {
                        if (newAverage < (levelCap + 1) / 2.0f)
                        {
                            spawnData.MinLevel = 1;
                            ExtendToAverageMax(newAverage);
                        }
                        else
                        {
                            spawnData.MaxLevel = levelCap;
                            ExtendToAverageMin(newAverage);
                        }
                    }
                    if ((spawnData.MinLevel == 1 || spawnData.MaxLevel == levelCap) && Math.Abs((spawnData.MinLevel + spawnData.MaxLevel) / 2.0f - newAverage) > 0.49f)
                    {
                        if (spawnData.MinLevel == 1)
                        {
                            ExtendToAverageMax(newAverage);
                        }
                        else if (spawnData.MaxLevel == levelCap)
                        {
                            ExtendToAverageMin(newAverage);
                        }
                    }
                    if (forcedMinimum != 1)
                    {
                        spawnData.MinLevel = Math.Max(spawnData.MinLevel, forcedMinimum);
                        spawnData.MaxLevel = Math.Clamp(spawnData.MaxLevel, spawnData.MinLevel, levelCap);
                    }
                }
                void ExtendToAverageMin(float avg)
                {
                    spawnData.MinLevel = Math.Clamp(Convert.ToInt32(2 * avg - spawnData.MaxLevel), 1, spawnData.MaxLevel);
                }
                void ExtendToAverageMax(float avg)
                {
                    spawnData.MaxLevel = Math.Clamp(Convert.ToInt32(2 * avg - spawnData.MinLevel), spawnData.MinLevel, levelCap);
                }
            }
        }
        foreach (AreaData area in subList)
        {
            ++progress;
            MainPage.Instance.Dispatcher.BeginInvoke(() => MainPage.Instance.progressBar.Value = Math.Ceiling(100.0 * progress / progressTotal));
            bool nightOnly = NightOnly(area);
            float LevelMultiplier(SpawnData spawnData)
            {
                return LevelMultiplierEx(spawnData, area.IsInDungeon, area.IsCage, area.IsEgg);
            }
            float CountMultiplier(SpawnData spawnData)
            {
                return CountMultiplierEx(spawnData, area.IsInDungeon, area.IsCage || area.IsEgg);
            }
            float CustomWeight(float rarity, bool lerp, float scale)
            {
                if (lerp)
                {
                    if (rarity < 10)
                    {
                        return float.Lerp(formData.WeightCustom[(int) rarity], formData.WeightCustom[(int) rarity + 1], Math.Max(0, rarity - float.Truncate(rarity))) * scale;
                    }
                    else
                    {
                        return float.Lerp(formData.WeightCustom[10], formData.WeightCustom[20], (Math.Min(rarity, 20) - 10) / 10) * scale;
                    }
                }
                else
                {
                    if (rarity < 10)
                    {
                        return formData.WeightCustom[Convert.ToInt32(rarity)] * scale;
                    }
                    else
                    {
                        return formData.WeightCustom[Convert.ToInt32((Math.Min(rarity, 20) - 10) / 10) * 10 + 10] * scale;
                    }
                }
            }
            float WeightScale(SpawnData spawnData) => spawnData.IsPal ? 1 : (Data.PalData[spawnData.Name].AIResponse == "Kill_All" ? humanWeightAggro : humanWeight);
            // NOT No Randomization
            if (!formData.MethodNone)
            {
                List<SpawnEntry> spawnEntries = [];
                List<SpawnEntry> spawnEntriesOriginal = area.SpawnEntries;
                area.SpawnEntries = spawnEntries;
                if (area.IsEgg)
                {
                    area.EggRespawnTime = eggRespawnTime;
                }
                if (BossesEverywhere(area) && !area.IsBoss && BossesEverywhereChance(area) == 1 && !area.IsCage
                    && (!area.IsEgg || area.Filename != Data.FirstEgg))
                {
                    continue;
                }
                string StripQuestPrefix(string name)
                {
                    return questNameRegex().Match(name).Groups["name"].Value;
                }
                HashSet<string> vanillaNames = [.. spawnEntriesOriginal.FindAll(x => x.Weight != 0).ConvertAll(x => x.SpawnList.ConvertAll(y => StripQuestPrefix(y.Name)))
                    .SelectMany(x => x).Distinct()];
                if (BossesEverywhere(area) && area.Filename.StartsWith('~'))
                {
                    vanillaNames.UnionWith(vanillaNames.ToList().FindAll(x => Data.PalData[x].IsPal && !Data.PalData[x].IsBoss)
                        .ConvertAll(x => Data.BossName.TryGetValue(x, out string? bossName) ? bossName : x));
                }
                long weightSum = 0;
                if (formData.VanillaRestrict && !formData.MethodGlobalSwap)
                {
                    List<SpawnEntry> bossBackupClone = bossSpawnsOriginalBackup.ConvertAll(x => x.Clone());
                    basicSpawnsOriginal = FilterSpawnList(basicSpawnsOriginalBackup.ConvertAll(x => x.Clone()), false);
                    bossSpawnsOriginal = FilterSpawnList(bossBackupClone, true);
                    basicSpawnsCurrent = [.. basicSpawnsOriginal];
                    bossSpawnsCurrent = [.. bossSpawnsOriginal];
                    List<SpawnEntry> FilterSpawnList(List<SpawnEntry> spawnList, bool bossList)
                    {
                        spawnList.ForEach(x => x.SpawnList.RemoveAll(y => !vanillaNames.Contains(y.Name)));
                        spawnList.RemoveAll(x => x.SpawnList.Count == 0 || (bossList && !x.SpawnList.Exists(y => y.IsBoss)));
                        return spawnList;
                    }
                }
                if (area.IsCage && (area.Filename == Data.FirstCage || (formData.VanillaRestrict && !formData.MethodGlobalSwap)))
                {
                    if (formData.VanillaRestrict && !formData.MethodGlobalSwap)
                    {
                        basicSpawnsOriginal = basicSpawnsOriginal.FindAll(x => RarityEx(x.SpawnList[0], formData.WeightTypeCustom) <= maxCageRarity);
                    }
                    else
                    {
                        basicSpawnsOriginal = basicSpawnsOriginalBackup.ConvertAll(x => x.Clone()).FindAll(x => RarityEx(x.SpawnList[0], formData.WeightTypeCustom) <= maxCageRarity);
                    }
                    if (!formData.AllowCagedHumans)
                    {
                        basicSpawnsOriginal = basicSpawnsOriginal.FindAll(x => Data.PalData[x.SpawnList[0].Name].IsPal);
                    }
                    basicSpawnsCurrent = [.. basicSpawnsOriginal];
                }
                if (area.IsEgg && (area.Filename == Data.FirstEgg || (formData.VanillaRestrict && !formData.MethodGlobalSwap)))
                {
                    if (formData.VanillaRestrict && !formData.MethodGlobalSwap)
                    {
                        basicSpawnsOriginal = basicSpawnsOriginal.FindAll(x => Data.PalData[x.SpawnList[0].Name].IsPal);
                        bossSpawnsOriginal = bossSpawnsOriginal.FindAll(x => Data.PalData[x.SpawnList[0].Name].IsPal);
                    }
                    else
                    {
                        basicSpawnsOriginal = basicSpawnsOriginalBackup.ConvertAll(x => x.Clone()).FindAll(x => Data.PalData[x.SpawnList[0].Name].IsPal);
                        bossSpawnsOriginal = bossSpawnsOriginalBackup.ConvertAll(x => x.Clone()).FindAll(x => Data.PalData[x.SpawnList[0].Name].IsPal);
                    }
                    basicSpawnsCurrent = [.. basicSpawnsOriginal];
                    bossSpawnsCurrent = [.. bossSpawnsOriginal];
                }

                if (area.IsEgg && area.Filename == Data.FirstEgg) {
                    bossSpawnsOriginal =
                        bossSpawnsOriginal.FindAll(x => x.SpawnList[0].Name != "GYM_WorldTreeDragon");
                }

                if (BossesEverywhere(area) && !area.IsBoss && BossesEverywhereChance(area) == 1 && !area.IsCage)
                {
                    continue;
                }
                // All Species Everywhere
                if (formData.MethodFull)
                {
                    // area.filename check for first boss area to reset the lists when changing from non-boss to bosses
                    if (!area.IsFieldBoss || formData.FieldBossExtended || area.Filename == Data.FirstBoss)
                    {
                        basicSpawnsCurrent = [.. basicSpawnsOriginal];
                        bossSpawnsCurrent = [.. bossSpawnsOriginal];
                    }
                    int speciesCount = 0;
                    int maxSpecies = area.IsFieldBoss && !formData.FieldBossExtended
                        ? 1
                        : (area.IsBoss ? bossSpawnsOriginal : basicSpawnsOriginal).Sum(entry => entry.SpawnList.Count);
                    if (formData.GroupVanilla)
                    {
                        List<SpawnEntry> spawns = area.IsBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                        List<SpawnEntry> original = area.IsBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
                        while (speciesCount < maxSpecies)
                        {
                            if (spawns.Count == 0)
                            {
                                spawns.AddRange(original);
                            }
                            int i = random.Next(0, spawns.Count);
                            AddEntry(spawns[i]);
                            speciesCount += spawns[i].SpawnList.Count;
                            spawns.RemoveAt(i);
                        }
                    }
                    else if (formData.GroupRandom)
                    {
                        palsAdded = pals8UpAdded = bossesAdded = bosses8UpAdded = 0;

                        if (area.IsBoss && !formData.MultiBoss)
                        {
                            while (speciesCount < maxSpecies)
                            {
                                AddEntry(GetRandomGroup());
                                ++speciesCount;
                            }
                        }
                        else
                        {
                            HashSet<string> speciesAdded = [];
                            while (speciesCount < maxSpecies)
                            {
                                SpawnEntry spawnEntry = GetRandomGroup();
                                AddEntry(spawnEntry);
                                spawnEntry.SpawnList.ForEach(entry => speciesCount += speciesAdded.Add(entry.Name) ? 1 : 0);
                            }
                        }
                    }
                }
                // NOT All Species Everywhere
                else
                {
                    if (formData.MethodGlobalSwap) {
                        spawnEntriesOriginal = spawnEntriesOriginal.FindAll(x => x.SpawnList[0].Name != "RowName");

                        if (area.IsEgg)
                        {
                            spawnEntriesOriginal = spawnEntriesOriginal.FindAll(x => !x.SpawnList[0].IsBoss);
                        }
                        if (BossesEverywhere(area) && area.Filename.StartsWith('~'))
                        {
                            spawnEntriesOriginal = spawnEntriesOriginal
                                .FindAll(x => Data.PalData[x.SpawnList[0].Name].IsPal && Data.BossName.ContainsKey(x.SpawnList[0].Name))
                                .ConvertAll(x =>
                            {
                                x.SpawnList[0].Name = Data.BossName[x.SpawnList[0].Name];
                                return x;
                            });
                        }
                    }
                    int entryCount = area.IsFieldBoss && !formData.FieldBossExtended
                        ? 1
                        : (formData.MethodCustomSize ? spawnListSize : spawnEntriesOriginal.Count);
                    for (int i = 0; i < entryCount; ++i)
                    {
                        if (formData.MethodGlobalSwap)
                        {
                            SpawnEntry spawnEntry = spawnEntriesOriginal[i];
                            if ((area.IsEgg || area.IsCage && !formData.AllowCagedHumans)
                                && swapMap.TryGetValue(spawnEntry.SpawnList[0].Name, out SpawnEntry? value)
                                && !Data.PalData[value.SpawnList[0].Name].IsPal) {
                                swapMap.Remove(spawnEntry.SpawnList[0].Name);
                            }
                            if (!swapMap.ContainsKey(spawnEntry.SpawnList[0].Name))
                            {
                                if (formData.GroupVanilla)
                                {
                                    List<SpawnEntry> spawns = area.IsBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                                    List<SpawnEntry> original = area.IsBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
                                    if (spawns.Count == 0)
                                    {
                                        spawns.AddRange(original);
                                    }
                                    int j = random.Next(0, spawns.Count);
                                    SpawnEntry newValue = spawns[j];
                                    spawns.RemoveAt(j);
                                    swapMap.Add(spawnEntry.SpawnList[0].Name, newValue);
                                }
                                else if (formData.GroupRandom)
                                {
                                    swapMap.Add(spawnEntry.SpawnList[0].Name, GetRandomGroup());
                                }
                            }
                            AddEntry(swapMap[spawnEntry.SpawnList[0].Name]);
                        }
                        else
                        {
                            if (formData.GroupVanilla)
                            {
                                List<SpawnEntry> spawns = area.IsBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                                List<SpawnEntry> original = area.IsBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
                                if (spawns.Count == 0)
                                {
                                    spawns.AddRange(original);
                                }
                                int j = random.Next(0, spawns.Count);
                                AddEntry(spawns[j]);
                                spawns.RemoveAt(j);
                            }
                            else if (formData.GroupRandom)
                            {
                                AddEntry(GetRandomGroup());
                            }
                        }
                    }
                }
                if (!equalizeAreaRarity)
                {
                    PostProcessArea(area, weightSum, nightOnly);
                }
                void AddEntry(SpawnEntry value)
                {
                    SpawnEntry spawnEntry = new()
                    {
                        Weight = formData.WeightTypeUniform ? random.Next(weightUniformMin, weightUniformMax + 1) : 10,
                        SpawnList = value.SpawnList.ConvertAll(spawnData =>
                            new SpawnData(spawnData.Name, area.IsCage || area.IsEgg ? 1 : spawnData.MinCount, area.IsCage || area.IsEgg ? 1 : spawnData.MaxCount)
                            {
                                MinLevel = spawnData.MinLevel,
                                MaxLevel = spawnData.MaxLevel
                            })
                    };
                    if ((area.IsCage || area.IsEgg) && spawnEntry.SpawnList.Count > 1)
                    {
                        spawnEntry.SpawnList.RemoveRange(1, spawnEntry.SpawnList.Count - 1);
                    }
                    spawnEntries.Add(spawnEntry);
                    long weight = spawnEntry.Weight;
                    if (formData.WeightTypeCustom)
                    {
                        var spawnList = spawnEntry.SpawnList;

                        if (formData.WeightPrioritizeBoss) {
                            var bossList = spawnList.Where(x => x.IsBoss);
                            if (bossList.Any()) {
                                spawnList = [.. bossList];
                            }
                        }

                        float averageRarity = spawnList.Sum(Rarity) / (float)spawnList.Count;

                        weight = formData.WeightCustomMode switch {
                            string x when x == GroupWeightMode.WeightSum =>
                                spawnList.Sum(spawnData => Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))),
                            string x when x == GroupWeightMode.WeightAverage =>
                                Convert.ToInt64(spawnList.Sum(spawnData => Convert.ToInt64(
                                CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))) / (float)spawnList.Count),
                            string x when x == GroupWeightMode.WeightMinimum =>
                                spawnList.Min(spawnData => Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))),
                            string x when x == GroupWeightMode.WeightMaximum =>
                                spawnList.Max(spawnData => Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))),
                            string x when x == GroupWeightMode.RarityMinimum =>
                                spawnList.MinBy(Rarity) is SpawnData spawnData
                                ? Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))
                                : throw new Exception("Group Weight Mode: 'Rarity Minimum' failed."),
                            string x when x == GroupWeightMode.RarityMaximum =>
                                spawnList.MaxBy(Rarity) is SpawnData spawnData
                                ? Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))
                                : throw new Exception("Group Weight Mode: 'Rarity Maximum' failed."),
                            string x when x == GroupWeightMode.RarityAverageRounded => Convert.ToInt64(CustomWeight(
                                spawnList.Sum(Rarity) / (float)spawnList.Count, false,
                                spawnList.Sum(WeightScale) / spawnList.Count)),
                            string x when x == GroupWeightMode.RarityAverageBlend => Convert.ToInt64(CustomWeight(
                                spawnList.Sum(Rarity) / (float)spawnList.Count, true,
                                spawnList.Sum(WeightScale) / spawnList.Count)),
                            string x when x == GroupWeightMode.RarityAverageBlend10To20 =>
                                Convert.ToInt64(CustomWeight(averageRarity, averageRarity >= 10,
                                spawnList.Sum(WeightScale) / spawnList.Count)),
                            _ => throw new Exception($"Unsupported Group Weight Mode '{formData.WeightCustomMode}'"),
                        };
                    }
                    else
                    {
                        weight = Convert.ToInt64(weight * spawnEntry.SpawnList.Sum(WeightScale) / spawnEntry.SpawnList.Count);
                    }
                    if (Data.PalData[spawnEntry.SpawnList[0].Name].Nocturnal && Data.PalData[spawnEntry.SpawnList[0].Name].IsPal && nightOnly)
                    {
                        spawnEntry.NightOnly = true;
                        if ((!formData.WeightTypeCustom || !formData.WeightAdjustProbability) && !formData.VanillaPlus && (!BossesEverywhere(area) || area.IsBoss))
                        {
                            weight = Convert.ToInt64(weight * weightNightOnly);
                        }
                    }
                    try
                    {
                        spawnEntry.Weight = Convert.ToInt32(weight);
                    }
                    catch // OverflowException
                    {
                        spawnEntry.Weight = int.MaxValue;
                    }
                    spawnEntry.Weight = Math.Max(1, spawnEntry.Weight);
                    weightSum += spawnEntry.Weight;
                    if (!equalizeAreaRarity)
                    {
                        GenerateLevels(spawnEntry, area, LevelMultiplier);
                    }
                    if (area.IsCage || area.IsEgg)
                    {
                        return;
                    }
                    if (formData.GroupRandom)
                    {
                        spawnEntry.SpawnList.Sort((x, y) =>
                        {
                            if (x.IsBoss != y.IsBoss)
                                return (y.IsBoss ? 1 : 0) - (x.IsBoss ? 1 : 0);
                            if (x.IsPal != y.IsPal)
                                return (y.IsPal ? 1 : 0) - (x.IsPal ? 1 : 0);
                            return Rarity(y) - Rarity(x);
                        });
                    }
                    for (int i = 0; i < spawnEntry.SpawnList.Count; ++i)
                    {
                        SpawnData spawnData = spawnEntry.SpawnList[i];
                        if (!formData.GroupVanilla)
                        {
                            spawnData.MinCount = baseCountMin;
                            spawnData.MaxCount = baseCountMax;
                        }

                        float countMultiplier = CountMultiplier(spawnData);
                        int min = spawnData.IsBoss ? countClampBossMin : countClampMin;
                        int max = spawnData.IsBoss ? countClampBossMax : countClampMax;
                        int firstMin = spawnData.IsBoss ? countClampFirstBossMin : countClampFirstMin;
                        int firstMax = spawnData.IsBoss ? countClampFirstBossMax : countClampFirstMax;

                        if (formData.MinOneCountSolo && spawnEntry.SpawnList.Count == 1) {
                            firstMin = Math.Max(1, firstMin);
                        }

                        if (i == 0) {
                            spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), firstMin, firstMax);
                            spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), firstMin, firstMax);
                        } else {
                            spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), min, max);
                            spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), min, max);
                        }
                    }
                }
                // Generate a random group. Not used with Vanilla-Based mode
                SpawnEntry GetRandomGroup()
                {
                    SpawnData NextSpecies(List<SpawnEntry> spawns, List<SpawnEntry> original,
                        List<SpawnData>? currentSpawns = null) {
                        IEnumerable<KeyValuePair<int, string>> IndexedSpawns() =>
                            spawns.Select((x, i) => new KeyValuePair<int, string>(i, x.SpawnList[0].Name));

                        var indexedSpawns = IndexedSpawns();

                        IEnumerable<KeyValuePair<int, string>> SpawnsUsed() =>
                            currentSpawns != null
                            ? indexedSpawns.Where(entry =>
                                !currentSpawns.Exists(spawnData => entry.Value == spawnData.Name))
                            : indexedSpawns;

                        var spawnsUsed = SpawnsUsed();

                        if (!spawnsUsed.Any()) {
                            if (currentSpawns != null && !original.Exists(entry =>
                                !currentSpawns.Exists(spawnData => entry.SpawnList[0].Name == spawnData.Name))) {
                                currentSpawns = null;

                                if (!indexedSpawns.Any()) {
                                    spawns.AddRange(original);
                                }
                            } else {
                                spawns.Clear();
                                spawns.AddRange(original);
                            }

                            indexedSpawns = IndexedSpawns();
                            spawnsUsed = SpawnsUsed();
                        }

                        var spawnsUsedList = spawnsUsed.ToList();
                        var nextElement = spawnsUsedList[random.Next(spawnsUsedList.Count)];
                        string name = nextElement.Value;
                        spawns.RemoveAt(nextElement.Key);

                        return new(name);
                    }

                    List<SpawnEntry> FilterRarity8Up(List<SpawnEntry> spawns) =>
                        spawns.FindAll(x => Rarity8Up(x.SpawnList[0]));

                    SpawnEntry spawnEntry = new();

                    void Rarity8UpSanityCheck(List<SpawnEntry> spawns, List<SpawnEntry> original, int maxGroup,
                        bool isBoss) {
                        if (!area.IsCage && !area.IsEgg && ShouldAdd8UpPal(original, maxGroup, isBoss)) {
                            var filteredSpawns = FilterRarity8Up(spawns);
                            var filteredOriginal = FilterRarity8Up(original);

                            spawnEntry.SpawnList.Add(NextSpecies(filteredSpawns, filteredOriginal));
                            spawns.Remove(spawns.Find(x => x.SpawnList[0].Name == spawnEntry.SpawnList[0].Name)!);
                        } else {
                            spawnEntry.SpawnList.Add(NextSpecies(spawns, original));
                        }
                    }

                    List<SpawnEntry> spawns = basicSpawnsCurrent;
                    List<SpawnEntry> original = basicSpawnsOriginal;
                    if (area.IsBoss)
                    {
                        Rarity8UpSanityCheck(bossSpawnsCurrent, bossSpawnsOriginal,
                            formData.MultiBoss ? maxGroupBoss : 1, true);

                        if (formData.MultiBoss)
                        {
                            spawns = bossSpawnsCurrent;
                            original = bossSpawnsOriginal;
                        }
                    }
                    else
                    {
                        Rarity8UpSanityCheck(basicSpawnsCurrent, basicSpawnsOriginal, maxGroup, false);
                    }
                    if (area.IsCage || area.IsEgg)
                    {
                        return spawnEntry;
                    }
                    if (original.Count != 0 && !(formData.Rarity8UpSolo && Rarity8Up(spawnEntry.SpawnList[0])))
                    {
                        int groupSize = area.IsBoss ? random.Next(minGroupBoss, maxGroupBoss + 1) : random.Next(minGroup, maxGroup + 1);
                        if (nightOnly || !formData.MixHumanAndPal || formData.Rarity8UpSolo
                            || formData.Rarity8UpSanity || formData.SeparateAggroHumans || formData.SeparateFlying
                            ) {
                            List<SpawnEntry> spawnsUsed = [.. spawns];
                            List<SpawnEntry> originalsUsed = [.. original];
                            if (nightOnly)
                            {
                                SeparateGroupsByCondition(entry => Data.PalData[entry.SpawnList[0].Name].Nocturnal && Data.PalData[entry.SpawnList[0].Name].IsPal);
                            }
                            if (!formData.MixHumanAndPal)
                            {
                                SeparateGroupsByCondition(entry => Data.PalData[entry.SpawnList[0].Name].IsPal);
                            }
                            if (formData.SeparateFlying)
                            {
                                SeparateGroupsByCondition(entry => Data.FlyingNames.Contains(entry.SpawnList[0].Name));
                            }
                            bool separateAggroHumansApplied = false;
                            if (formData.SeparateAggroHumans)
                            {
                                SeparateAggroHumanFilter();
                            }
                            void SeparateAggroHumanFilter()
                            {
                                if (!spawnEntry.SpawnList[^1].IsPal && Data.PalData[spawnEntry.SpawnList[^1].Name].AIResponse != "Kill_All")
                                {
                                    FilterGroupsByCondition(entry =>
                                        Data.PalData[entry.SpawnList[0].Name].AIResponse != "Kill_All"
                                        && Data.PalData[entry.SpawnList[0].Name].AIResponse != "Warlike"
                                        && Data.PalData[entry.SpawnList[0].Name].AIResponse != "Boss"
                                        && !entry.SpawnList[0].IsBoss);
                                    separateAggroHumansApplied = true;
                                }
                                else if (Data.PalData[spawnEntry.SpawnList[^1].Name].AIResponse == "Kill_All"
                                        || Data.PalData[spawnEntry.SpawnList[^1].Name].AIResponse == "Warlike"
                                        || Data.PalData[spawnEntry.SpawnList[^1].Name].AIResponse == "Boss"
                                        || spawnEntry.SpawnList[^1].IsBoss)
                                {
                                    FilterGroupsByCondition(entry =>
                                        Data.PalData[entry.SpawnList[0].Name].IsPal || Data.PalData[entry.SpawnList[0].Name].AIResponse == "Kill_All");
                                    separateAggroHumansApplied = true;
                                }
                            }

                            bool rarity8UpFilterApplied = false;

                            void Rarity8UpFilter() {
                                if (Rarity8Up(spawnEntry.SpawnList[^1])) {
                                    FilterGroupsByCondition(entry => !entry.SpawnList.Exists(x => Rarity8Up(x)));
                                    rarity8UpFilterApplied = true;
                                }
                            }

                            if (formData.Rarity8UpSolo)
                            {
                                FilterGroupsByCondition(entry => !entry.SpawnList.Exists(x => Rarity8Up(x)));
                                rarity8UpFilterApplied = true;
                            } else if (formData.Rarity8UpSanity) {
                                Rarity8UpFilter();
                            }

                            void FilterGroupsByCondition(Func<SpawnEntry, bool> condition)
                            {
                                spawnsUsed = spawnsUsed.FindAll(entry => condition(entry));
                                originalsUsed = originalsUsed.FindAll(entry => condition(entry));
                            }
                            void SeparateGroupsByCondition(Func<SpawnEntry, bool> condition)
                            {
                                if (condition(spawnEntry))
                                {
                                    FilterGroupsByCondition(condition);
                                }
                                else
                                {
                                    FilterGroupsByCondition(entry => !condition(entry));
                                }
                            }
                            if (originalsUsed.Count != 0)
                            {
                                while (spawnEntry.SpawnList.Count < groupSize)
                                {
                                    spawnEntry.SpawnList.Add(NextSpecies(spawnsUsed, originalsUsed, spawnEntry.SpawnList));
                                    if (formData.SeparateAggroHumans && !separateAggroHumansApplied)
                                    {
                                        SeparateAggroHumanFilter();
                                    }

                                    if (formData.Rarity8UpSanity && !rarity8UpFilterApplied) {
                                        Rarity8UpFilter();
                                    }
                                }
                                if (spawns.Count == 0)
                                    spawns.AddRange(original);

                                if (!formData.Rarity8UpSolo && formData.Rarity8UpSanity
                                    && !spawnEntry.SpawnList[1..].All(x =>
                                        spawns.Exists(y => y.SpawnList[0].Name == x.Name))) {
                                    spawns.AddRange(original.Where(x => !Rarity8Up(x.SpawnList[0])
                                        && !spawns.Exists(y => y.SpawnList[0].Name == x.SpawnList[0].Name)));
                                }

                                spawns.RemoveAll(entry => spawnEntry.SpawnList[1..].Exists(spawnData => entry.SpawnList[0].Name == spawnData.Name));
                            }
                        }
                        else
                        {
                            while (spawnEntry.SpawnList.Count < groupSize)
                            {
                                spawnEntry.SpawnList.Add(NextSpecies(spawns, original, spawnEntry.SpawnList));
                            }
                        }
                    }
                    if (area.IsBoss && spawnEntry.SpawnList.Count > 1 && !formData.MultiBoss)
                    {
                        spawnEntry.SpawnList[0].MinLevel = minBossLevel;
                        spawnEntry.SpawnList[0].MaxLevel = maxBossLevel;
                        spawnEntry.SpawnList[1..].ForEach(spawnData => { spawnData.MinLevel = minAddLevel; spawnData.MaxLevel = maxAddLevel; });
                    }

                    foreach (var spawnData in spawnEntry.SpawnList) {
                        if (Rarity8Up(spawnData)) {
                            if (spawnData.IsBoss) {
                                ++bosses8UpAdded;
                            } else {
                                ++pals8UpAdded;
                            }
                        } else {
                            if (spawnData.IsBoss) {
                                ++bossesAdded;
                            } else {
                                ++palsAdded;
                            }
                        }
                    }

                    return spawnEntry;
                }
            }
            // No Randomization
            else
            {
                WriteAreaAsset(area, FilterVanillaSpawns(area.SpawnEntries, area));
            }
        }
        bool FilterVanillaSpawns(List<SpawnEntry> spawnEntries, AreaData area)
        {
            int changes = 0;
            foreach (SpawnEntry spawnEntry in spawnEntries)
            {
                changes += spawnEntry.SpawnList.RemoveAll(spawnData => !allowedNames.Contains(spawnData.Name)
                    && formData.MethodNone);
            }
            changes += spawnEntries.RemoveAll(entry => entry.SpawnList.Count == 0);
            if (area.IsEgg && area.EggRespawnTime != eggRespawnTime)
            {
                ++changes;
                area.EggRespawnTime = eggRespawnTime;
            }
            foreach (SpawnEntry spawnEntry in spawnEntries)
            {
                if (NightOnly(area, false))
                {
                    changes += spawnEntry.NightOnly == true ? 1 : 0;
                    spawnEntry.NightOnly = false;
                }
                if (spawnEntry.SpawnList[0].Name == "RowName")
                {
                    continue;
                }
                float firstAverage = 0;
                for (int i = 0; i < spawnEntry.SpawnList.Count; ++i)
                {
                    SpawnData spawnData = spawnEntry.SpawnList[i];
                    int originalMin = spawnData.MinLevel;
                    int originalMax = spawnData.MaxLevel;
                    int originalCountMin = spawnData.MinCount;
                    int originalCountMax = spawnData.MaxCount;
                    float range = spawnData.MaxLevel - spawnData.MinLevel;
                    float average;
                    if (formData.ForceAddLevel && area.IsBoss && i > 0 && !spawnData.IsBoss)
                    {
                        average = firstAverage * formData.BossAddLevel / 100;
                    }
                    else
                    {
                        average = (spawnData.MaxLevel + spawnData.MinLevel) / 2.0f;
                    }
                    ApplyLevelRange(spawnData, LevelMultiplierEx(spawnData, area.IsInDungeon, area.IsCage, area.IsEgg), average, range, false);
                    float countMultiplier = CountMultiplierEx(spawnData, area.IsInDungeon, area.IsCage || area.IsEgg);

                    int min = spawnData.IsBoss ? countClampBossMin : countClampMin;
                    int max = spawnData.IsBoss ? countClampBossMax : countClampMax;
                    int firstMin = spawnData.IsBoss ? countClampFirstBossMin : countClampFirstMin;
                    int firstMax = spawnData.IsBoss ? countClampFirstBossMax : countClampFirstMax;

                    if (i == 0)
                    {
                        firstAverage = average;
                        spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), firstMin, firstMax);
                        spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), firstMin, firstMax);
                    }
                    else
                    {
                        spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), min, max);
                        spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), min, max);
                    }
                    changes += (spawnData.MinLevel != originalMin ? 1 : 0) + (spawnData.MaxLevel != originalMax ? 1 : 0)
                        + (spawnData.MinCount != originalCountMin ? 1 : 0) + (spawnData.MaxCount != originalCountMax ? 1 : 0);
                }
            }
            return changes != 0;
        }
        void WriteAreaAsset(AreaData area, bool saveData = true)
        {
            if (outputLog)
            {
                outputLogBuilder.AppendLine(area.SimpleName);
                area.SpawnEntries.ForEach(entry => { entry.Print(outputLogBuilder); totalSpeciesCount += entry.SpawnList.Count; });
                outputLogBuilder.AppendLine();
            }
            if (saveData)
            {
                area.Modified = true;
            }
        }
        if (equalizeAreaRarity)
        {
            List<AreaData> fieldList = [];
            List<AreaData> dungeonList = [];
            List<AreaData> fieldBossList = [];
            List<AreaData> dungeonBossList = [];
            List<AreaData> fieldBossNoctSplitList = [];
            List<AreaData> dungeonBossNoctSplitList = [];
            List<AreaData> cageList = [];
            List<AreaData> eggList = [];
            List<AreaData> eggBossList = [];
            foreach (AreaData area in subList)
            {
                if (area.IsCage)
                {
                    cageList.Add(area);
                }
                else if (area.IsEgg)
                {
                    if (area.IsBoss)
                    {
                        eggBossList.Add(area);
                    }
                    else
                    {
                        eggList.Add(area);
                    }
                }
                else if (area.IsBoss)
                {
                    // Fix Bosses Everywhere mixing together lists with nocturnal separation and ones without
                    if (NightOnly(area))
                    {
                        if (area.IsInDungeon)
                        {
                            dungeonBossNoctSplitList.Add(area);
                        }
                        else
                        {
                            fieldBossNoctSplitList.Add(area);
                        }
                    }
                    else
                    {
                        if (area.IsInDungeon)
                        {
                            dungeonBossList.Add(area);
                        }
                        else
                        {
                            fieldBossList.Add(area);
                        }
                    }
                }
                else
                {
                    if (area.IsInDungeon)
                    {
                        dungeonList.Add(area);
                    }
                    else
                    {
                        fieldList.Add(area);
                    }
                }
            }
            EqualizeSpawns(fieldList);
            EqualizeSpawns(dungeonList);
            EqualizeSpawns(fieldBossList);
            EqualizeSpawns(dungeonBossList);
            EqualizeSpawns(fieldBossNoctSplitList);
            EqualizeSpawns(dungeonBossNoctSplitList);
            EqualizeSpawns(cageList);
            EqualizeSpawns(eggList);
            EqualizeSpawns(eggBossList);
            void EqualizeSpawns(List<AreaData> editList)
            {
                if (editList.Count  == 0)
                {
                    return;
                }
                List<SpawnEntry> diurnalSpawns = [];
                List<SpawnEntry> nocturnalSpawns = [];
                long[] rarityCountsDay = new long[21];
                long[] rarityCountsNight = new long[21];
                long raritySumDay = 0;
                long raritySumNight = 0;
                long rarityCountDay = 0;
                long rarityCountNight = 0;
                long groupCountDay = 0;
                long groupCountNight = 0;
                int maxEntries = 0;
                foreach (AreaData area in editList)
                {
                    maxEntries = Math.Max(maxEntries, area.SpawnEntries.Count);
                    foreach (SpawnEntry spawnEntry in area.SpawnEntries)
                    {
                        long raritySum = spawnEntry.SpawnList.Sum(x => (long) Rarity(x));
                        if (spawnEntry.NightOnly)
                        {
                            nocturnalSpawns.Add(spawnEntry);
                            raritySumNight += raritySum;
                            rarityCountNight += spawnEntry.SpawnList.Count;
                            spawnEntry.SpawnList.ForEach(x => ++rarityCountsNight[Rarity(x)]);
                            ++groupCountNight;
                        }
                        else
                        {
                            diurnalSpawns.Add(spawnEntry);
                            raritySumDay += raritySum;
                            rarityCountDay += spawnEntry.SpawnList.Count;
                            spawnEntry.SpawnList.ForEach(x => ++rarityCountsDay[Rarity(x)]);
                            ++groupCountDay;
                        }
                    }
                }
                if (maxEntries < 2)
                {
                    return;
                }
                double rarityAverageDay = (double) raritySumDay / rarityCountDay;
                double rarityAverageNight = (double) raritySumNight / rarityCountNight;
                double[] rarityAveragesDay = new double[21];
                double[] rarityAveragesNight = new double[21];
                for (int i = 0; i < 21; ++i)
                {
                    rarityAveragesDay[i] = (double) rarityCountsDay[i] / rarityCountDay;
                    rarityAveragesNight[i] = (double) rarityCountsNight[i] / rarityCountNight;
                }
                double groupSizeDay = (double) rarityCountDay / groupCountDay;
                double groupSizeNight = (double) rarityCountNight / groupCountNight;
                double nightRatio = diurnalSpawns.Count == 0 ? 1 : (double) nocturnalSpawns.Count / diurnalSpawns.Count;
                diurnalSpawns = DuplicatesSplitShuffle(diurnalSpawns);
                nocturnalSpawns = DuplicatesSplitShuffle(nocturnalSpawns);
                List<SpawnEntry> DuplicatesSplitShuffle(List<SpawnEntry> spawns)
                {
                    Dictionary<string, int> uniqueGroupCounts = [];
                    List<List<SpawnEntry>> duplicateLists = [];
                    foreach (SpawnEntry spawnEntry in spawns)
                    {
                        string key = UniqueKey(spawnEntry);
                        int count = 1;
                        if (!uniqueGroupCounts.TryAdd(key, 1))
                        {
                            count = ++uniqueGroupCounts[key];
                        }
                        while (duplicateLists.Count < count)
                        {
                            duplicateLists.Add([]);
                        }
                        duplicateLists[count - 1].Add(spawnEntry);
                    }
                    int[] indices = [.. Enumerable.Range(0, uniqueGroupCounts.Count)];
                    random.Shuffle(indices);
                    int i = 0;
                    foreach (KeyValuePair<string, int> keyPair in uniqueGroupCounts)
                    {
                        uniqueGroupCounts[keyPair.Key] = indices[i++];
                    }
                    foreach (List<SpawnEntry> spawnList in duplicateLists)
                    {
                        spawnList.Sort((x, y) => uniqueGroupCounts[UniqueKey(x)] - uniqueGroupCounts[UniqueKey(y)]);
                    }
                    return [.. duplicateLists.SelectMany(x => x)];
                    string UniqueKey(SpawnEntry spawnEntry)
                    {
                        List<SpawnData> spawnList = spawnEntry.SpawnList;
                        if (spawnList.Exists(x => x.IsBoss))
                        {
                            spawnList = spawnList.FindAll(x => x.IsBoss);
                        }
                        string[] names = [.. spawnList.ConvertAll(x => x.Name)];
                        Array.Sort(names, string.Compare);
                        return string.Join(",", names);
                    }
                }
                List<List<SpawnEntry>> spawnListsDay = [];
                List<List<SpawnEntry>> spawnListsNight = [];
                List<int> nightCounts = [];
                int nightSum = 0;
                foreach (AreaData area in editList)
                {
                    int countNight = Convert.ToInt32(area.SpawnEntries.Count * nightRatio);
                    nightSum += countNight;
                    nightCounts.Add(countNight);
                }
                while (nightSum < nocturnalSpawns.Count)
                {
                    int[] indices = [.. Enumerable.Range(0, nightCounts.Count)];
                    random.Shuffle(indices);
                    for (int i = 0; i < indices.Length && nightSum < nocturnalSpawns.Count; ++i)
                    {
                        if (nightCounts[indices[i]] < editList[indices[i]].SpawnEntries.Count)
                        {
                            ++nightCounts[indices[i]];
                            ++nightSum;
                        }
                    }
                }
                while (nightSum > nocturnalSpawns.Count)
                {
                    int[] indices = [.. Enumerable.Range(0, nightCounts.Count)];
                    random.Shuffle(indices);
                    for (int i = 0; i < indices.Length && nightSum > nocturnalSpawns.Count; ++i)
                    {
                        if (nightCounts[indices[i]] > 0)
                        {
                            --nightCounts[indices[i]];
                            --nightSum;
                        }
                    }
                }
                for (int i = 0; i < editList.Count; ++i)
                {
                    AreaData area = editList[i];
                    int countNight = nightCounts[i];
                    int countDay = area.SpawnEntries.Count - countNight;
                    spawnListsDay.Add(FitToRarityCounts(diurnalSpawns, countDay, rarityAveragesDay, groupSizeDay));
                    spawnListsNight.Add(FitToRarityCounts(nocturnalSpawns, countNight, rarityAveragesNight, groupSizeNight));
                    List<SpawnEntry> FitToRarityCounts(List<SpawnEntry> spawns, int count, double[] rarityAverages, double groupSize)
                    {
                        if (count == 0)
                        {
                            return [];
                        }
                        List<SpawnEntry> spawnList = [];
                        long[] rarityCounts = new long[21];
                        for (int i = 0; i < spawns.Count; )
                        {
                            if (!spawns[i].SpawnList.Exists(spawnData =>
                                {
                                    int rarity = Rarity(spawnData);
                                    return rarityCounts[rarity] >= rarityAverages[rarity] * groupSize * count;
                                }))
                            {
                                foreach (SpawnData spawnData in spawns[i].SpawnList)
                                {
                                    ++rarityCounts[Rarity(spawnData)];
                                }
                                spawnList.Add(spawns[i]);
                                spawns.RemoveAt(i);
                                if (spawnList.Count >= count)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                ++i;
                            }
                        }
                        return spawnList;
                    }
                }
                for (int i = 0; i < editList.Count; ++i)
                {
                    AreaData area = editList[i];
                    int countNight = nightCounts[i];
                    int countDay = area.SpawnEntries.Count - countNight;
                    area.SpawnEntries = [.. FitToRarityAverage(spawnListsDay[i], diurnalSpawns, countDay, rarityAverageDay),
                        .. FitToRarityAverage(spawnListsNight[i], nocturnalSpawns, countNight, rarityAverageNight)];
                    List<SpawnEntry> FitToRarityAverage(List<SpawnEntry> spawnList, List< SpawnEntry> spawns, int count, double rarityAverage)
                    {
                        long raritySum = 0;
                        long rarityCount = 0;
                        foreach (SpawnEntry spawnEntry in spawnList)
                        {
                            raritySum += spawnEntry.SpawnList.Sum(x => (long) Rarity(x));
                            rarityCount += spawnEntry.SpawnList.Count;
                        }
                        while (spawnList.Count < count)
                        {
                            int minIndex = -1;
                            int maxIndex = -1;
                            double minValue = double.MaxValue;
                            double maxValue = double.MinValue;
                            for (int i = 0; i < spawns.Count; ++i)
                            {
                                SpawnEntry spawnEntry = spawns[i];
                                double average = (double) (raritySum + spawnEntry.SpawnList.Sum(x => (long) Rarity(x))) / (rarityCount + spawnEntry.SpawnList.Count);
                                if (average < minValue)
                                {
                                    minValue = average;
                                    minIndex = i;
                                }
                                if (average <= rarityAverage && average > maxValue)
                                {
                                    maxValue = average;
                                    maxIndex = i;
                                }
                            }
                            int addIndex = maxIndex != -1 ? maxIndex : minIndex;
                            spawnList.Add(spawns[addIndex]);
                            raritySum += spawns[addIndex].SpawnList.Sum(x => (long) Rarity(x));
                            rarityCount += spawns[addIndex].SpawnList.Count;
                            spawns.RemoveAt(addIndex);
                        }
                        return spawnList;
                    }
                }
            }
            foreach (AreaData area in subList)
            {
                foreach (SpawnEntry spawnEntry in area.SpawnEntries)
                {
                    GenerateLevels(spawnEntry, area, x => LevelMultiplierEx(x, area.IsInDungeon, area.IsCage, area.IsEgg));
                }
                PostProcessArea(area, area.SpawnEntries.Sum(x => (long) x.Weight), NightOnly(area));
            }
        }
        void PostProcessArea(AreaData area, long weightSum, bool nightOnly)
        {
            List<SpawnEntry> spawnEntries = area.SpawnEntries;
            // Convert To Percentage
            if (formData.WeightTypeCustom && formData.WeightAdjustProbability)
            {
                List<SpawnEntry> diurnalEntries = [];
                List<SpawnEntry> nocturnalEntries = [];
                foreach (SpawnEntry spawnEntry in spawnEntries)
                {
                    if (spawnEntry.NightOnly)
                    {
                        nocturnalEntries.Add(spawnEntry);
                    }
                    else
                    {
                        diurnalEntries.Add(spawnEntry);
                    }
                }
                diurnalEntries.Sort((x, y) => x.Weight - y.Weight);
                nocturnalEntries.Sort((x, y) => x.Weight - y.Weight);
                if (spawnEntries.Count > 1)
                {
                    long totalSum = Math.Max(300L * spawnEntries.Count, 100);
                    WeightsToPercents(diurnalEntries, totalSum);
                    WeightsToPercents(nocturnalEntries, Convert.ToInt32(totalSum * (formData.VanillaPlus ? 1.0 : weightNightOnly)));
                    weightSum = spawnEntries.Sum(x => (long) x.Weight);
                }
                void WeightsToPercents(List<SpawnEntry> entries, long totalSum)
                {
                    if (entries.Count == 0)
                    {
                        return;
                    }
                    int[] breakpoints = [ 1, 5, 10, 25, 59 ];
                    int[] minWeights = [ 5, 10, 16, 31, int.MaxValue ];
                    int skippedPercent = 0;
                    for (int i = 0; i < breakpoints.Length - 1; ++i)
                    {
                        int endIndex = entries.FindIndex(x => x.Weight >= minWeights[i]);
                        if (endIndex == -1 || endIndex == 0)
                        {
                            skippedPercent += breakpoints[i];
                        }
                    }
                    if (entries[^1].Weight < breakpoints[^1])
                    {
                        totalSum = Convert.ToInt32(totalSum * 100.0 / (100 - skippedPercent - breakpoints[^1] + entries[^1].Weight));
                    }
                    for (int i = 0; i < breakpoints.Length; ++i)
                    {
                        int percent = breakpoints[i];
                        int endIndex = entries.FindIndex(x => x.Weight >= minWeights[i]);
                        if (endIndex == -1 || endIndex == 0)
                        {
                            if (i == breakpoints.Length - 1)
                            {
                                endIndex = entries.Count;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        if (i == breakpoints.Length - 1)
                        {
                            if (entries[^1].Weight < breakpoints[^1])
                            {
                                percent = entries[^1].Weight;
                            }
                            else
                            {
                                percent += skippedPercent;
                            }
                        }
                        List<SpawnEntry> adjustedEntries = entries[..endIndex];
                        entries = entries[endIndex..];
                        double scale = percent / 100.0 * totalSum / adjustedEntries.Sum(x => (long) x.Weight);
                        adjustedEntries.ForEach(x => x.Weight = Math.Max(1, Convert.ToInt32(x.Weight * scale)));
                    }
                }
            }
            // Add To Vanilla
            List<SpawnEntry> vanillaSpawns = [];
            if (formData.VanillaPlus && (!area.Filename.StartsWith('~') || BossesEverywhereChance(area) == 1))
            {
                vanillaSpawns = Data.AreaData[area.Filename.StartsWith('~') ? area.Filename[1..] : $"{(area.IsCage ? "Cage:" : "")}{area.Filename}"]
                    .SpawnEntries.ConvertAll(entry => entry.Clone());
                if (formData.VanillaPlusFilter)
                {
                    FilterVanillaSpawns(vanillaSpawns, area);
                }
                long vanillaWeightSum = vanillaSpawns.Sum(x => (long) x.Weight);
                long vanillaNightSum = vanillaSpawns.FindAll(x => x.NightOnly).Sum(x => (long) x.Weight);
                List<SpawnEntry> nightSpawns = spawnEntries.FindAll(x => x.NightOnly);
                long nightSum = nightSpawns.Sum(x => (long) x.Weight);
                bool rescaleNight = false;
                if (nightSpawns.Count != 0)
                {
                    if (vanillaNightSum != 0)
                    {
                        if (nightSum != weightSum && vanillaNightSum != vanillaWeightSum)
                        {
                            double nightRatio = (double) vanillaNightSum / (vanillaWeightSum - vanillaNightSum);
                            double nightScale = nightRatio / ((double) nightSum / (weightSum - nightSum));
                            if (nightScale >= 1)
                            {
                                MultiplyWeights(nightSpawns, nightScale);
                            }
                            else
                            {
                                MultiplyWeights(spawnEntries.FindAll(x => !x.NightOnly), 1 / nightScale);
                            }
                            weightSum = spawnEntries.Sum(x => (long) x.Weight);
                            SelectiveScale(weightSum, vanillaWeightSum, vanillaSpawns, spawnEntries, vanillaPlusChance);
                        }
                        else
                        {
                            SelectiveScale(weightSum, vanillaNightSum, vanillaSpawns, spawnEntries, vanillaPlusChance);
                        }
                    }
                    else
                    {
                        SelectiveScale(weightSum - nightSum, vanillaWeightSum, vanillaSpawns, spawnEntries.FindAll(x => !x.NightOnly), vanillaPlusChance);
                        rescaleNight = true;
                    }
                }
                else
                {
                    if (vanillaNightSum != 0 && vanillaNightSum != vanillaWeightSum)
                    {
                        SelectiveScale(weightSum, vanillaWeightSum - vanillaNightSum, vanillaSpawns, spawnEntries, vanillaPlusChance);
                    }
                    else
                    {
                        SelectiveScale(weightSum, vanillaWeightSum, vanillaSpawns, spawnEntries, vanillaPlusChance);
                    }
                }
                spawnEntries.InsertRange(0, vanillaSpawns);
                if (rescaleNight)
                {
                    List<SpawnEntry> newDaySpawns = spawnEntries.FindAll(x => !x.NightOnly);
                    List<SpawnEntry> newNightSpawns = spawnEntries.FindAll(x => x.NightOnly);
                    SelectiveScale(newNightSpawns.Sum(x => (long) x.Weight), newDaySpawns.Sum(x => (long) x.Weight), newDaySpawns, newNightSpawns, vanillaPlusChance);
                }
                weightSum = spawnEntries.Sum(x => (long) x.Weight);
                nightOnly = false;
            }
            IntOverflowFix(spawnEntries, weightSum, nightOnly);
            CollectionsMarshal.AsSpan(spawnEntries)[vanillaSpawns.Count..].Sort((x, y) =>
            {
                if (x.NightOnly != y.NightOnly)
                    return (x.NightOnly ? 1 : 0) - (y.NightOnly ? 1 : 0);
                if (x.Weight != y.Weight)
                    return y.Weight - x.Weight;
                if (x.SpawnList[0].IsPal != y.SpawnList[0].IsPal)
                    return (y.SpawnList[0].IsPal ? 1 : 0) - (x.SpawnList[0].IsPal ? 1 : 0);
                if (x.SpawnList[0].IsPal)
                {
                    bool bossX = x.SpawnList[0].Name.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase);
                    bool bossY = y.SpawnList[0].Name.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase);
                    if (bossX != bossY)
                        return (bossY ? 1 : 0) - (bossX ? 1 : 0);
                    string baseNameX = x.SpawnList[0].BaseName;
                    string baseNameY = y.SpawnList[0].BaseName;
                    if (Data.PalData[baseNameX].ZukanIndex != Data.PalData[baseNameY].ZukanIndex)
                    {
                        bool negativeX = Data.PalData[baseNameX].ZukanIndex < 0;
                        bool negativeY = Data.PalData[baseNameY].ZukanIndex < 0;
                        if (negativeX != negativeY)
                            return (negativeX ? 1 : 0) - (negativeY ? 1 : 0);
                        return Data.PalData[baseNameX].ZukanIndex - Data.PalData[baseNameY].ZukanIndex;
                    }
                    if (Data.PalData[baseNameX].ZukanIndexSuffix != Data.PalData[baseNameY].ZukanIndexSuffix)
                        return string.Compare(Data.PalData[baseNameX].ZukanIndexSuffix, Data.PalData[baseNameY].ZukanIndexSuffix);
                }
                return string.Compare(x.SpawnList[0].Name, y.SpawnList[0].Name);
            });
            if (formData.VanillaMerge && !area.IsCage && !area.IsEgg)
            {
                List<SpawnEntry> mergedVanillaSpawns = Data.AreaData[area.Filename.StartsWith('~') ? area.Filename[1..] : area.Filename].SpawnEntries.ConvertAll(x => x.Clone());
                if (formData.VanillaMergeFilter)
                {
                    FilterVanillaSpawns(mergedVanillaSpawns, area);
                }
                List<SpawnEntry> vanillaSpawnsDay = mergedVanillaSpawns.FindAll(x => !x.NightOnly && x.Weight != 0);
                List<SpawnEntry> vanillaSpawnsNight = mergedVanillaSpawns.FindAll(x => x.NightOnly && x.Weight != 0);
                List<SpawnEntry> spawnsDay = spawnEntries[vanillaSpawns.Count..].FindAll(x => !x.NightOnly);
                List<SpawnEntry> spawnsNight = spawnEntries[vanillaSpawns.Count..].FindAll(x => x.NightOnly);
                MergeGroups(vanillaSpawnsDay, spawnsDay);
                MergeGroups(vanillaSpawnsNight, spawnsNight);
                void MergeGroups(List<SpawnEntry> vanSpawns, List<SpawnEntry> newSpawns)
                {
                    if (vanSpawns.Count == 0 || newSpawns.Count == 0)
                    {
                        return;
                    }
                    int currentWeight = newSpawns[0].Weight;
                    int lastIndex = 0;
                    for (int i = 0; i < newSpawns.Count; ++i)
                    {
                        if (newSpawns[i].Weight != currentWeight)
                        {
                            random.Shuffle(CollectionsMarshal.AsSpan(newSpawns)[lastIndex..i]);
                            currentWeight = newSpawns[i].Weight;
                            lastIndex = i;
                        }
                    }
                    random.Shuffle(CollectionsMarshal.AsSpan(newSpawns)[lastIndex..]);
                    // The simplest way to do a stable sort?
                    MemoryExtensions.Sort([.. vanSpawns.Select((x, i) => new KeyValuePair<SpawnEntry, int>(x, i))], CollectionsMarshal.AsSpan(vanSpawns), (x, y) =>
                    {
                        if (x.Key.Weight != y.Key.Weight)
                            return y.Key.Weight - x.Key.Weight;
                        return x.Value - y.Value;
                    });
                    int vanSum = vanSpawns.Sum(x => x.Weight);
                    int newSum = newSpawns.Sum(x => x.Weight);
                    int vanCount = vanSpawns[0].Weight;
                    int newCount = 0;
                    int index = 0;
                    foreach (SpawnEntry entry in newSpawns)
                    {
                        entry.SpawnList.InsertRange(0, vanSpawns[index].SpawnList.ConvertAll(x => x.Clone()));
                        newCount += entry.Weight;
                        if (index + 1 < vanSpawns.Count && newCount >= Convert.ToInt32((double) vanCount / vanSum * newSum))
                        {
                            ++index;
                            vanCount += vanSpawns[index].Weight;
                        }
                    }
                }
            }

            if (!area.Filename.StartsWith('~') && (formData.MethodNone || !BossesEverywhere(area) || area.IsBoss || area.IsCage))
            {
                WriteAreaAsset(area);
            }
        }
        // Multiply weights so that one list of spawns fits an exact percentage of the total weight sum of both lists - minimizes rounding error by only scaling up
        void SelectiveScale(long fittedSum, long oppositeFittedSum, List<SpawnEntry> oppositeFittedSpawns, List<SpawnEntry> fittedSpawns, double percentageToFit)
        {
            double oppositeFittedScale = fittedSum * (1 - percentageToFit) / (oppositeFittedSum * percentageToFit);
            if (oppositeFittedScale >= 1)
            {
                MultiplyWeights(oppositeFittedSpawns, oppositeFittedScale);
            }
            else
            {
                MultiplyWeights(fittedSpawns, 1 / oppositeFittedScale);
            }
        }
        void MultiplyWeights(List<SpawnEntry> entries, double scale)
        {
            foreach (SpawnEntry spawnEntry in entries)
            {
                try
                {
                    spawnEntry.Weight = Convert.ToInt32(spawnEntry.Weight * scale);
                }
                catch // OverflowException
                {
                    spawnEntry.Weight = int.MaxValue;
                }
            }
        }
        void IntOverflowFix(List<SpawnEntry> spawnEntries, long weightSum, bool nightOnly)
        {
            if (weightSum > int.MaxValue)
            {
                void ScaleWeights(List<SpawnEntry> entries, long sum, int maxValue)
                {
                    double startSum = sum - entries.Count;
                    double endSum = maxValue - entries.Count * 1.5;
                    double scale = endSum / startSum;
                    entries.ForEach(spawnEntry => spawnEntry.Weight = Convert.ToInt32((spawnEntry.Weight - 1) * scale) + 1);
                }
                if (formData.OverflowFixMode == OverflowFixMode.ScaleAll || !nightOnly)
                {
                    ScaleWeights(spawnEntries, weightSum, int.MaxValue);
                }
                else
                {
                    long diurnalSum = 0;
                    long nocturnalSum = 0;
                    int diurnalMin = int.MaxValue;
                    List<SpawnEntry> diurnalEntries = [];
                    List<SpawnEntry> nocturnalEntries = [];
                    foreach (SpawnEntry spawnEntry in spawnEntries)
                    {
                        if (spawnEntry.NightOnly)
                        {
                            nocturnalSum += spawnEntry.Weight;
                            nocturnalEntries.Add(spawnEntry);
                        }
                        else
                        {
                            diurnalSum += spawnEntry.Weight;
                            diurnalMin = Math.Min(diurnalMin, spawnEntry.Weight);
                            diurnalEntries.Add(spawnEntry);
                        }
                    }
                    if (formData.OverflowFixMode == OverflowFixMode.Dynamic && diurnalMin > 1)
                    {
                        diurnalSum = 0;
                        foreach (SpawnEntry spawnEntry in diurnalEntries)
                        {
                            spawnEntry.Weight = Math.Max(1, Convert.ToInt32(spawnEntry.Weight / (double) diurnalMin));
                            diurnalSum += spawnEntry.Weight;
                        }
                    }
                    if (diurnalSum + nocturnalSum > int.MaxValue)
                    {
                        if (formData.OverflowFixMode == OverflowFixMode.ScaleNightOnly && int.MaxValue - diurnalSum > nocturnalEntries.Count * 1.5)
                        {
                            ScaleWeights(nocturnalEntries, nocturnalSum, Convert.ToInt32(int.MaxValue - diurnalSum));
                        }
                        else
                        {
                            ScaleWeights(spawnEntries, nocturnalSum + diurnalSum, int.MaxValue);
                        }
                    }
                }
            }
        }
        if (!formData.MethodNone)
        {
            foreach (AreaData area in subList.FindAll(area => !area.IsBoss && !area.IsCage && BossesEverywhere(area)))
            {
                AreaData addedBosses = subList.Find(x => x.Filename == $"~{area.Filename}")!;
                if (BossesEverywhereChance(area) == 1)
                {
                    area.SpawnEntries = addedBosses.SpawnEntries;
                }
                else
                {
                    List<SpawnEntry> diurnalSpawns = area.SpawnEntries.FindAll(x => !x.NightOnly);
                    List<SpawnEntry> nocturnalSpawns = area.SpawnEntries.FindAll(x => x.NightOnly);
                    List<SpawnEntry> addedDiurnalSpawns = addedBosses.SpawnEntries.FindAll(x => !x.NightOnly);
                    List<SpawnEntry> addedNocturnalSpawns = addedBosses.SpawnEntries.FindAll(x => x.NightOnly);
                    if (addedDiurnalSpawns.Count != 0)
                    {
                        if (diurnalSpawns.Count != 0)
                        {
                            SelectiveScale(addedDiurnalSpawns.Sum(x => (long)x.Weight), diurnalSpawns.Sum(x => (long)x.Weight),
                                formData.VanillaPlus ? area.SpawnEntries : diurnalSpawns, addedDiurnalSpawns, BossesEverywhereChance(area));
                        }
                        else
                        {
                            List<SpawnEntry> nullSpawn = [new() { Weight = 1, SpawnList = [new("RowName") { MaxLevel = 1 }] }];
                            SelectiveScale(addedDiurnalSpawns.Sum(x => (long)x.Weight), 1, nullSpawn, addedDiurnalSpawns, BossesEverywhereChance(area));
                            addedDiurnalSpawns.Add(nullSpawn[0]);
                        }
                        area.SpawnEntries.AddRange(addedDiurnalSpawns);
                        diurnalSpawns.AddRange(addedDiurnalSpawns);
                    }
                    if (nocturnalSpawns.Count != 0 && diurnalSpawns.Count != 0 && !formData.VanillaPlus)
                    {
                        SelectiveScale(nocturnalSpawns.Sum(x => (long)x.Weight), diurnalSpawns.Sum(x => (long)x.Weight), diurnalSpawns,
                            nocturnalSpawns, (double)weightNightOnly / (weightNightOnly + 1));
                    }
                    if (addedNocturnalSpawns.Count != 0)
                    {
                        SelectiveScale(addedNocturnalSpawns.Sum(x => (long)x.Weight), area.SpawnEntries.Sum(x => (long)x.Weight),
                            area.SpawnEntries, addedNocturnalSpawns, BossesEverywhereChance(area));
                        area.SpawnEntries.AddRange(addedNocturnalSpawns);
                    }
                    IntOverflowFix(area.SpawnEntries, area.SpawnEntries.Sum(x => (long)x.Weight), NightOnly(area));
                }
                WriteAreaAsset(area);
            }

            if (formData.RandomizeHumanBosses) {
                int minHumanBoss = Math.Clamp(formData.HumanBossMin, 1, 3);
                int maxHumanBoss = Math.Clamp(formData.HumanBossMax, minHumanBoss, 3);
                int minHumanBossBoss = Math.Clamp(formData.HumanBossBossMin, 1, maxHumanBoss);
                int maxHumanBossBoss = Math.Clamp(formData.HumanBossBossMax, minHumanBossBoss, maxHumanBoss);

                var humanNamesOriginal = humanSpawns.SelectMany(x => x.SpawnList.Select(y => y.Name)).Distinct();
                List<string> humanNames = [.. humanNamesOriginal];
                List<string> humanBossNames = [.. Data.HumanBossNames];

                foreach (var area in areaList.Where(x => x.IsHumanBoss)) {
                    area.SpawnEntries[0].SpawnList[0].Name = RandomHumanBossName();

                    if (area.IsHumanBossSquad) {
                        int count = random.Next(minHumanBoss, maxHumanBoss + 1);
                        int bossCount = random.Next(minHumanBossBoss, maxHumanBossBoss + 1);
                        (int namesAdded, int bossNamesAdded) = (1, 1);

                        while (namesAdded < count) {
                            if (bossNamesAdded <  bossCount) {
                                area.SpawnEntries[namesAdded].SpawnList[0].Name = RandomHumanBossName();
                                ++bossNamesAdded;
                            } else {
                                area.SpawnEntries[namesAdded].SpawnList[0].Name = RandomHumanName();
                            }

                            ++namesAdded;
                        }

                        area.SpawnEntries = area.SpawnEntries[..count];
                    }
                }

                string RandomHumanName() => RandomName(humanNames, humanNamesOriginal);
                string RandomHumanBossName() => RandomName(humanBossNames, Data.HumanBossNames);
            }

            if (formData.RandomizeHumanBossesPals) {
                int minHumanBossPal = Math.Clamp(formData.HumanBossPalMin, 0, 3);
                int maxHumanBossPal = Math.Clamp(formData.HumanBossPalMax, minHumanBossPal, 3);
                int minHumanBossPalBoss = Math.Clamp(formData.HumanBossPalBossMin, 0, maxHumanBossPal);
                int maxHumanBossPalBoss = Math.Clamp(formData.HumanBossPalBossMax, minHumanBossPalBoss,
                    maxHumanBossPal);

                var palNamesOriginal = basicSpawnsOriginal.SelectMany(x => x.SpawnList.Where(y => y.IsPal)
                    .Select(z => z.Name)).Distinct();
                var palBossNamesOriginal = bossSpawnsOriginal.SelectMany(x => x.SpawnList.Where(y => y.IsBoss && y.IsPal)
                    .Select(z => z.Name)).Distinct();
                List<string> palNames = [.. palNamesOriginal];
                List<string> palBossNames = [.. palBossNamesOriginal];

                foreach (var area in areaList.Where(x => x.IsHumanBoss)) {
                    int count = random.Next(Math.Min(area.SpawnEntries.Count, minHumanBossPal),
                        Math.Min(area.SpawnEntries.Count, maxHumanBossPal) + 1);
                    int bossCount = random.Next(Math.Min(area.SpawnEntries.Count, minHumanBossPalBoss),
                        Math.Min(area.SpawnEntries.Count, maxHumanBossPalBoss) + 1);
                    (int namesAdded, int bossNamesAdded) = (0, 0);

                    foreach (var entry in area.SpawnEntries) {
                        entry.SpawnList = entry.SpawnList[..1];
                    }

                    while (namesAdded < count) {
                        if (bossNamesAdded <  bossCount) {
                            area.SpawnEntries[namesAdded].SpawnList.Add(new() { Name = RandomPalBossName() });
                            ++bossNamesAdded;
                        } else {
                            area.SpawnEntries[namesAdded].SpawnList.Add(new() { Name = RandomPalName() });
                        }

                        ++namesAdded;
                    }
                }

                string RandomPalName() => RandomName(palNames, palNamesOriginal);
                string RandomPalBossName() => RandomName(palBossNames, palBossNamesOriginal);
            }

            if (formData.RandomizeHumanBosses || formData.RandomizeHumanBossesPals) {
                foreach (var area in areaList.Where(x => x.IsHumanBoss)) {
                    foreach (var entry in area.SpawnEntries) {
                        int level = entry.SpawnList[0].MinLevel;
                        foreach (var spawnData in entry.SpawnList) {
                            ApplyLevelRange(spawnData, LevelMultiplierEx(spawnData, false, false, false), level, 0,
                                false);
                        }
                    }

                    WriteAreaAsset(area);
                }
            }

            string RandomName(List<string> names, IEnumerable<string> original) {
                int index = random.Next(names.Count);
                string name = names[index];
                names.RemoveAt(index);

                if (names.Count == 0) {
                    names.AddRange(original);
                }

                return name;
            }
        }

        MainPage.Instance.Dispatcher.Invoke(() => MainPage.Instance.progressBar.Visibility = Visibility.Collapsed);
        areaList.Sort(FileModify.AreaSortFunc);
        GeneratedAreaList = areaList;
        PalSpawnPage.Instance.Dispatcher.Invoke(() => PalSpawnPage.Instance.areaList.ItemsSource = GeneratedAreaList);
        if (outputLog)
        {
            outputLogBuilder.AppendJoin(' ', [totalSpeciesCount, "Total Entries"]);
            outputLogBuilder.AppendLine("\n");
            outputLogBuilder.AppendLine(JsonConvert.SerializeObject(formData, Formatting.Indented, new JsonSerializerSettings { Converters = [new JsonWriterDecimal()] }));
            try
            {
                File.WriteAllText("Palworld-Randomizer-Log.txt", outputLogBuilder.ToString());
            }
            catch (Exception e)
            {
                MainPage.Instance.Dispatcher.Invoke(() =>
                    MessageBox.Show(MainPage.Instance.GetWindow(), "Error: Failed to write output log.\n\n" + e.Message, "Output Log Failed",
                        MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }
        try
        {
            if (AutoSaveGenerationData)
            {
                string date = $"{DateTime.Now:MM-dd-yy-HH-mm-ss}";
                Directory.CreateDirectory(UAssetData.AppDataPath("Log"));
                File.WriteAllText(UAssetData.AppDataPath($"Log\\{date}.csv"), FileModify.GenerateCSV(areaList), Encoding.UTF8);
                File.WriteAllText(UAssetData.AppDataPath($"Log\\{date}.json"),
                    JsonConvert.SerializeObject(formData, Formatting.Indented, new JsonSerializerSettings { Converters = [new JsonWriterDecimal()] }));
            }
        }
        catch (Exception exception)
        {
            App.LogException(exception);
        }
    }
    
    public static void GeneratePalSpawns(FormData formData)
    {
        GenerateSpawnLists(formData);
        RandomizeAndSaveAssets(formData);
        MainPage.Instance.Dispatcher.Invoke(() => MainPage.Instance.statusBar.Text = "💾 Creating...");
    }
    public static string GetRandomPal() => ((string[])[.. Data.PalList, .. Data.TerrariaMonsters])[new Random().Next(Data.PalList.Count + Data.TerrariaMonsters.Count)];

    public static void SaveBackup()
    {
        try
        {
            if (AreaListChanged)
            {
                AreaListChanged = false;
                if (AutoSaveRestoreBackups)
                {
                    List<AreaData> areaList = (List<AreaData>) PalSpawnPage.Instance.areaList.ItemsSource;
                    Directory.CreateDirectory(UAssetData.AppDataPath("Backups"));
                    File.WriteAllText(UAssetData.AppDataPath($"Backups\\{DateTime.Now:MM-dd-yy-HH-mm-ss}.csv"), FileModify.GenerateCSV(areaList), Encoding.UTF8);
                }
            }
        }
        catch (Exception exception)
        {
            App.LogException(exception);
        }
    }

    public static void RestoreBackup()
    {
        try
        {
            if (AutoSaveRestoreBackups)
            {
                DirectoryInfo backupDir = new(UAssetData.AppDataPath("Backups"));
                DirectoryInfo logDir = new(UAssetData.AppDataPath("Log"));
                string? backupPath = ((IEnumerable<FileInfo>) [.. (backupDir.Exists ? backupDir.GetFiles("*.csv") : []), .. (logDir.Exists ? logDir.GetFiles("*.csv") : [])])
                    .MaxBy(x => x.LastWriteTime)?.FullName;
                if (backupPath != null)
                {
                    GeneratedAreaList = FileModify.ConvertCSV(backupPath);
                }
            }
        }
        catch (Exception exception)
        {
            App.LogException(exception);
        }
    }
}
