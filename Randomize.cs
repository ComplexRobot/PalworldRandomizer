using Microsoft.Win32;
using Stfu.Linq;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;
using System.Windows;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace PalworldRandomizer
{
    public static class Data
    {
        public static Dictionary<string, CharacterData> PalData { get; private set; } = [];
        public static Dictionary<string, string> PalName { get; private set; } = [];
        public static Dictionary<string, string> SimpleName { get; private set; } = [];
        public static List<string> SimpleNameValues { get; private set; } = [];
        public static Dictionary<string, string> PalIcon { get; private set; } = [];
        public static List<string> PalList { get; private set; } = [];
        public static Dictionary<string, string> BossName { get; private set; } = [];
        public static List<string> TowerBossNames { get; private set; } = [];
        public static Dictionary<string, List<SpawnEntry>> SoloEntries { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);
        public static Dictionary<string, List<SpawnEntry>> BossEntries { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);
        public static List<SpawnEntry> GroupEntries { get; private set; } = [];
        public static Dictionary<string, AreaData> AreaData { get; private set; } = [];
        public static readonly string[] humanNames =
        [
            "Believer_Bat",
            "Believer_CrossBow",
            "FireCult_FlameThrower",
            "FireCult_Rifle",
            "Hunter_Bat",
            "Hunter_Fat_GatlingGun",
            "Hunter_FlameThrower",
            "Hunter_Grenade",
            "Hunter_Handgun",
            "Hunter_Rifle",
            "Hunter_RocketLauncher",
            "Hunter_Shotgun",
            "Male_Scientist01_LaserRifle",
            "Scientist_FlameThrower"
        ];
        public static readonly string[] policeNames =
        [
            "Police_Handgun",
            "Police_Rifle",
            "Police_Shotgun"
        ];
        public static readonly string[] guardNames =
        [
            "Guard_Rifle",
            "Guard_Shotgun"
        ];
        public static readonly string[] traderNames =
        [
            "SalesPerson",
            "SalesPerson_Desert",
            "SalesPerson_Desert2",
            "SalesPerson_Volcano",
            "SalesPerson_Volcano2",
            "SalesPerson_Wander"
        ];
        public static readonly string[] palTraderNames =
        [
            "PalDealer",
            "PalDealer_Desert",
            "PalDealer_Volcano",
            "RandomEventShop",
            "Male_DarkTrader01"
        ];

        public static void Initialize(ResourceManager resourceManager)
        {
            PalData = UAssetData.CreatePalData();
            UAsset palNames = UAssetData.LoadAsset("Data\\DT_PalNameText.uasset");
            UAsset humanNames = UAssetData.LoadAsset("Data\\DT_HumanNameText.uasset");
            HashSet<string> resourceNames = new(resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true)!.Cast<DictionaryEntry>()
                .Select(x => (string) x.Key), StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> weapons = new()
            {
                { "AssaultRifle", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_AssaultRifle_Default1.png" },
                { "Handgun", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_HandGun_Default.png" },
                { "Shotgun", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_PumpActionShotgun.png" },
                { "RocketLauncher", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_Launcher_Default.png" },
                { "MeleeWeapon", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_Bat.png" },
                { "ThrowObject", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_FragGrenade.png" },
                { "FlameThrower", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_BowGun_Fire.png" },
                { "GatlingGun", "/Resources/Images/InventoryItemIcon/T_itemicon_Essential_SkillUnlock_Minigun.png" },
                { "BowGun", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_BowGun.png" },
                { "LaserRifle", "/Resources/Images/InventoryItemIcon/T_itemicon_Essential_SkillUnlock_AssaultRifle.png" }
            };
            foreach (KeyValuePair<string, CharacterData> keyPair in PalData)
            {
                if (keyPair.Value.IsPal)
                {
                    string nameString = ((TextPropertyData) ((DataTableExport) palNames.Exports[0]).Table.Data.Find(property =>
                        (PalData[keyPair.Key].OverrideNameTextID != "" &&
                        string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"{PalData[keyPair.Key].OverrideNameTextID}_TextData", true) == 0)
                        || string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"PAL_NAME_{keyPair.Key}_TextData", true) == 0)!.Value[0])
                        .CultureInvariantString.Value.Trim();
                    bool isTowerBoss = keyPair.Key.StartsWith("GYM_", StringComparison.InvariantCultureIgnoreCase);
                    bool isBoss = keyPair.Key.StartsWith("BOSS_", StringComparison.InvariantCultureIgnoreCase) || isTowerBoss;
                    PalName.Add(keyPair.Key, nameString == "en_text" ? (isBoss ? keyPair.Key[(keyPair.Key.IndexOf('_') + 1)..] : keyPair.Key) : nameString);
                    if (keyPair.Value.ZukanIndex > 0)
                    {
                        PalList.Add(keyPair.Key);
                    }
                    if (!isBoss)
                    {
                        BossName.Add(keyPair.Key, PalData.Keys.ToList().Find(key => string.Compare(key, $"BOSS_{keyPair.Key}", true) == 0)!);
                        SimpleName.Add(new SpawnData(keyPair.Key).SimpleName, keyPair.Key);
                    }
                    else if (isTowerBoss)
                    {
                        TowerBossNames.Add(keyPair.Key);
                        SimpleName.Add(new SpawnData(keyPair.Key).SimpleName, keyPair.Key);
                    }
                    string resourceKey = keyPair.Key.EndsWith("_Flower") ? keyPair.Key[..keyPair.Key.LastIndexOf('_')] : keyPair.Key;
                    resourceKey = isBoss ? resourceKey[(resourceKey.IndexOf('_') + 1)..] : resourceKey;
                    string resourceName = $"Resources/Images/PalIcon/T_{resourceKey}_icon_normal.png";
                    if (resourceNames.Contains(resourceName))
                    {
                        PalIcon.Add(keyPair.Key, $"/{resourceName}");
                    }
                    else
                    {
                        PalIcon.Add(keyPair.Key, $"/Resources/Images/PalIcon/T_icon_unknown.png");
                    }
                }
                else
                {
                    StructPropertyData? property = ((DataTableExport) humanNames.Exports[0]).Table.Data.Find(property =>
                        ((TextPropertyData) property.Value[0]).Value.Value == $"{PalData[keyPair.Key].OverrideNameTextID}_TextData");
                    SimpleName.Add(keyPair.Key, keyPair.Key);
                    if (property != null)
                    {
                        PalName.Add(keyPair.Key, ((TextPropertyData) property.Value[0]).CultureInvariantString.Value.Trim());
                    }
                    else
                    {
                        PalName.Add(keyPair.Key, "-");
                    }
                    if (weapons.TryGetValue(PalData[keyPair.Key].weapon, out string? value))
                    {
                        PalIcon.Add(keyPair.Key, value);
                    }
                    else
                    {
                        PalIcon.Add(keyPair.Key, $"/Resources/Images/PalIcon/T_CommonHuman_icon_normal.png");
                    }
                }
            }
            SimpleNameValues = [.. SimpleName.Keys];
            SimpleNameValues.Sort();
            string[] files = Directory.GetFiles(UAssetData.AppDataPath("Assets"), "*.uasset").Select(Path.GetFileName).ToArray()!;
            Array.Sort(files);
            foreach (string filename in files)
            {
                UAsset uAsset = UAssetData.LoadAsset($"Assets\\{filename}");
                SpawnExportData spawnExportData = PalSpawn.ReadAsset(uAsset);
                float averageLevel = 0;
                float levelRange = 0;
                float weightSum = 0;
                float nightAverageLevel = 0;
                float nightLevelRange = 0;
                float nightWeightSum = 0;
                foreach (SpawnEntry spawnEntry in spawnExportData.spawnEntries)
                {
                    if (spawnEntry.SpawnList.Count == 1 && !spawnEntry.SpawnList[0].Name.StartsWith("boss_", true, null))
                    {
                        if (!SoloEntries.TryGetValue(spawnEntry.SpawnList[0].Name, out List<SpawnEntry>? value))
                        {
                            value = [];
                            SoloEntries.Add(spawnEntry.SpawnList[0].Name, value);
                        }
                        value.Add(spawnEntry);
                    }
                    else if (spawnEntry.SpawnList[0].Name.StartsWith("boss_", true, null))
                    {
                        if (!BossEntries.TryGetValue(spawnEntry.SpawnList[0].Name, out List<SpawnEntry>? value))
                        {
                            value = [];
                            BossEntries.Add(spawnEntry.SpawnList[0].Name, value);
                        }
                        value.Add(spawnEntry);
                    }
                    else
                    {
                        GroupEntries.Add(spawnEntry);
                    }
                    int weightUsed = (spawnEntry.Weight == 0 ? 1 : spawnEntry.Weight);
                    if (spawnEntry.NightOnly)
                    {
                        nightAverageLevel += (float) (spawnEntry.SpawnList[0].MinLevel + spawnEntry.SpawnList[0].MaxLevel) * weightUsed / 2.0f;
                        nightLevelRange += (float) (spawnEntry.SpawnList[0].MaxLevel - spawnEntry.SpawnList[0].MinLevel) * weightUsed;
                        nightWeightSum += weightUsed;
                    }
                    else
                    {
                        averageLevel += (float) (spawnEntry.SpawnList[0].MinLevel + spawnEntry.SpawnList[0].MaxLevel) * weightUsed / 2.0f;
                        levelRange += (float) (spawnEntry.SpawnList[0].MaxLevel - spawnEntry.SpawnList[0].MinLevel) * weightUsed;
                        weightSum += weightUsed;
                    }
                }
                AreaData.Add(filename, new(uAsset, spawnExportData, filename));
                AreaData[filename].minLevel = (int) Math.Round(averageLevel / weightSum - levelRange / 2.0f / weightSum);
                AreaData[filename].maxLevel = (int) Math.Round(averageLevel / weightSum + levelRange / 2.0f / weightSum);
                if (nightWeightSum != 0)
                {
                    AreaData[filename].minLevelNight = (int) Math.Round(nightAverageLevel / nightWeightSum - nightLevelRange / 2.0f / nightWeightSum);
                    AreaData[filename].maxLevelNight = (int) Math.Round(nightAverageLevel / nightWeightSum + nightLevelRange / 2.0f / nightWeightSum);
                }
            }
            AreaData["BP_PalSpawner_Sheets_1_1_plain_begginer.uasset"].minLevel = 1;
            AreaData["BP_PalSpawner_Sheets_1_1_plain_begginer.uasset"].maxLevel = 3;
        }
        public static List<AreaData> AreaDataCopy()
        {
            return [.. AreaData.Values.Select(area =>
            new AreaData(new(), new(), area.filename)
            {
                minLevel = area.minLevel,
                maxLevel = area.maxLevel,
                minLevelNight = area.minLevelNight,
                maxLevelNight = area.maxLevelNight,
                uAsset = UAssetData.LoadAsset($"Assets\\{area.filename}"),
                spawnExportData =
                new SpawnExportData
                {
                    header = area.spawnExportData.header,
                    footer = area.spawnExportData.footer,
                    spawnEntries = area.SpawnEntries.ConvertAll(entry => entry.Clone())
                }
            })];
        }
        public static void AreaForEachIfDiff(List<AreaData> areaList, Action<AreaData>? func, Action<AreaData>? elseFunc = null)
        {
            foreach (AreaData area in areaList)
            {
                List<SpawnEntry> baseEntries = AreaData[area.filename].SpawnEntries;
                List<SpawnEntry> newEntries = area.SpawnEntries;
                if (baseEntries.Count != newEntries.Count
                    || ((Func<bool>) (() =>
                    {
                        for (int i = 0; i < baseEntries.Count; ++i)
                        {
                            List<SpawnData> baseList = baseEntries[i].SpawnList;
                            List<SpawnData> newList = newEntries[i].SpawnList;
                            if (baseList.Count != newList.Count || baseEntries[i].Weight != newEntries[i].Weight || baseEntries[i].NightOnly != newEntries[i].NightOnly)
                            {
                                return true;
                            }
                            if (((Func<bool>) (() =>
                            {
                                for (int j = 0; j < baseList.Count; ++j)
                                {
                                    if (baseList[j].Name != newList[j].Name
                                        || baseList[j].IsPal != newList[j].IsPal
                                        || baseList[j].MinLevel != newList[j].MinLevel
                                        || baseList[j].MaxLevel != newList[j].MaxLevel
                                        || baseList[j].MinGroupSize != newList[j].MinGroupSize
                                        || baseList[j].MaxGroupSize != newList[j].MaxGroupSize)
                                    {
                                        return true;
                                    }
                                }
                                return false;
                            }))())
                            {
                                return true;
                            }
                        }
                        return false;
                    }))())
                {
                    func?.Invoke(area);
                }
                else
                {
                    elseFunc?.Invoke(area);
                }
            }
        }
    }

    public static class Randomize
    {
        public static List<AreaData> GeneratedAreaList { get; private set; } = [];
        private static Dictionary<string, SpawnEntry> basicSpawns = [];
        private static Dictionary<string, SpawnEntry> bossSpawns = [];
        private static List<SpawnEntry> humanSpawns = [];

        public static void Initialize()
        {
            GeneratedAreaList = Data.AreaDataCopy();
        }
        private static void GenerateSpawnLists(FormData formData)
        {
            basicSpawns = [];
            bossSpawns = [];
            humanSpawns = [];
            if (formData.groupVanilla)
            {
                foreach (string key in Data.PalList)
                {
                    if (formData.spawnPals)
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
                                minCount = (int) Math.Max(minCount, spawnEntry.SpawnList[0].MinGroupSize);
                                maxCount = (int) Math.Max(maxCount, spawnEntry.SpawnList[0].MaxGroupSize);
                            }
                            spawnData.MinGroupSize = (uint) minCount;
                            spawnData.MaxGroupSize = (uint) maxCount;
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
                                        currentCount += (int) currentData.MinGroupSize + (int) currentData.MaxGroupSize;
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
                                    spawnData.MinGroupSize = soloValue2[0].SpawnList[0].MinGroupSize;
                                    spawnData.MaxGroupSize = soloValue2[0].SpawnList[0].MaxGroupSize;
                                }
                            }
                        }
                    }
                    if (key == "BlackCentaur")
                    {
                        continue;
                    }
                    string bossKey = Data.BossName[key];
                    SpawnEntry bossEntry = new();
                    bossSpawns.Add(bossKey, bossEntry);
                    if (Data.BossEntries.TryGetValue(bossKey, out List<SpawnEntry>? value))
                    {
                        if (value.Count == 1)
                        {
                            bossSpawns[bossKey] = value[0]; // shallow copy
                        }
                        else
                        {
                            int palCount = 0;
                            foreach (SpawnEntry spawnEntry in value)
                            {
                                int currentCount = 0;
                                foreach (SpawnData currentData in spawnEntry.SpawnList)
                                {
                                    currentCount += (int) currentData.MinGroupSize + (int) currentData.MaxGroupSize;
                                }
                                if (currentCount > palCount)
                                {
                                    bossEntry.SpawnList = spawnEntry.SpawnList; // shallow copy
                                    palCount = currentCount;
                                }
                            }
                        }
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
                                    MinGroupSize = baseEntry.SpawnList[1].MinGroupSize,
                                    MaxGroupSize = baseEntry.SpawnList[1].MaxGroupSize
                                });
                                bossData.MinLevel = baseEntry.SpawnList[0].MinLevel;
                                bossData.MaxLevel = baseEntry.SpawnList[0].MaxLevel;
                            }
                        }
                        if (bossEntry.SpawnList.Count == 1 && basicSpawns[key].SpawnList[0].MaxGroupSize > 1)
                        {
                            bossEntry.SpawnList.Add(new()
                            {
                                Name = key,
                                MinGroupSize = Math.Max(2, basicSpawns[key].SpawnList[0].MinGroupSize),
                                MaxGroupSize = basicSpawns[key].SpawnList[0].MaxGroupSize,
                                MinLevel = 2,
                                MaxLevel = 6
                            });
                            bossData.MinLevel = 4;
                            bossData.MaxLevel = 8;
                        }
                    }
                }
                foreach (KeyValuePair<string, List<SpawnEntry>> keyPair in Data.SoloEntries)
                {
                    if (!Data.PalData[keyPair.Key].IsPal &&
                        ((formData.spawnHumans && Data.humanNames.Contains(keyPair.Key))
                        || (formData.spawnPolice && Data.policeNames.Contains(keyPair.Key)))
                        )
                    {
                        SpawnData spawnData = new() { Name = keyPair.Key, IsPal = false };
                        humanSpawns.Add(new() { SpawnList = [spawnData] });
                        int minCount = 1;
                        int maxCount = 1;
                        foreach (SpawnEntry spawnEntry in keyPair.Value)
                        {
                            spawnData.MinGroupSize = (uint) Math.Max(minCount, spawnEntry.SpawnList[0].MinGroupSize);
                            spawnData.MaxGroupSize = (uint) Math.Max(maxCount, spawnEntry.SpawnList[0].MaxGroupSize);
                        }
                    }
                }
                List<SpawnEntry> groupEntriesCopy = [.. Data.GroupEntries];
                for (int i = 0; i < groupEntriesCopy.Count; ++i)
                {
                    SpawnEntry spawnEntry = groupEntriesCopy[i];
                    if (!spawnEntry.SpawnList[0].IsPal &&
                        ((formData.spawnHumans && Data.humanNames.Contains(spawnEntry.SpawnList[0].Name))
                        || (formData.spawnPolice && Data.policeNames.Contains(spawnEntry.SpawnList[0].Name)))
                        )
                    {
                        humanSpawns.Add(spawnEntry); // shallow copy
                        int humanCount = 0;
                        foreach (SpawnData spawnData in spawnEntry.SpawnList)
                        {
                            humanCount += (int) (spawnData.MinGroupSize + spawnData.MaxGroupSize);
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
                                    currentCount += (int) (spawnEntry2.SpawnList[k].MinGroupSize + spawnEntry2.SpawnList[k].MaxGroupSize);
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
                if (formData.spawnPolice)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Police_Handgun", 1, 2)] });
                }
                if (formData.spawnGuards)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Guard_Rifle", 1, 2), new("Guard_Shotgun", 1, 2)] });
                }
                if (formData.spawnHumans)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_FlameThrower", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Scientist_FlameThrower", 1, 2)] });
                }
                if (formData.spawnTraders)
                {
                    humanSpawns.AddRange(Data.traderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
                if (formData.spawnPalTraders)
                {
                    humanSpawns.AddRange(Data.palTraderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
            }
            else if (formData.groupRandom)
            {
                if (formData.spawnPals)
                {
                    Data.PalList.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name)] }));
                }
                Data.BossName.Where(keyPair => Data.PalData[keyPair.Key].ZukanIndex > 0).Select(keyPair => keyPair.Value)
                    .ForAll(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
                humanSpawns.AddRange(new Collection<string>
                ([
                    .. (formData.spawnHumans ? Data.humanNames : []),
                    .. (formData.spawnPolice ? Data.policeNames : []),
                    .. (formData.spawnGuards ? Data.guardNames : []),
                    .. (formData.spawnTraders ? Data.traderNames : []),
                    .. (formData.spawnPalTraders ? Data.palTraderNames : [])
                ]).Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }
            if (formData.spawnTowerBosses)
            {
                Data.TowerBossNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
        }
        private static ICollection<string> GetAllowedNames(FormData formData)
        {
            return
            [
                .. (formData.spawnPals ? Data.PalList : []),
                .. Data.BossName.Where(keyPair => Data.PalData[keyPair.Key].ZukanIndex > 0).Select(keyPair => keyPair.Value),
                .. (formData.spawnTowerBosses ? Data.TowerBossNames : []),
                .. (formData.spawnHumans ? Data.humanNames : []),
                .. (formData.spawnPolice ? Data.policeNames : []),
                .. (formData.spawnGuards ? Data.guardNames : []),
                .. (formData.spawnTraders ? Data.traderNames : []),
                .. (formData.spawnPalTraders ? Data.palTraderNames : [])
            ];
        }
        private static void RandomizeAndSaveAssets(FormData formData)
        {
            StringBuilder outputLog = formData.outputLog ? new() : null!;
            int minGroup = Math.Max(1, formData.groupMin);
            int maxGroup = Math.Max(minGroup, formData.groupMax);
            int spawnListSize = Math.Max(1, formData.spawnListSize);
            float fieldLevel = Math.Max(0, formData.fieldLevel) / 100.0f;
            float dungeonLevel = Math.Max(0, formData.dungeonLevel) / 100.0f;
            float fieldBossLevel = Math.Max(0, formData.fieldBossLevel) / 100.0f;
            float dungeonBossLevel = Math.Max(0, formData.dungeonBossLevel) / 100.0f;
            int levelCap = Math.Max(1, formData.levelCap);
            int totalSpeciesCount = 0;
            string outputPath = UAssetData.AppDataPath("Create-Pak\\Pal\\Content\\Pal\\Blueprint\\Spawner\\SheetsVariant");
            Directory.CreateDirectory(outputPath);
            Directory.GetFiles(outputPath).ForAll(File.Delete);
            Dictionary<string, SpawnEntry> swapMap = [];
            List<SpawnEntry> basicSpawnsOriginal = [.. basicSpawns.Values, .. humanSpawns];
            List<SpawnEntry> bossSpawnsOriginal = [.. bossSpawns.Values];
            List<SpawnEntry> basicSpawnsCurrent = [.. basicSpawnsOriginal];
            List<SpawnEntry> bossSpawnsCurrent = [.. bossSpawnsOriginal];
            HashSet<string> allowedNames = [.. GetAllowedNames(formData)];
            Random random = new();
            List<AreaData> areaList = Data.AreaDataCopy();
            List<AreaData> subList = areaList.FindAll(area =>
            {
                bool isFieldBoss = area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && !area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isDungeonBoss = area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isDungeon = !area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isField = !area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && !area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isBoss = area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase);
                return (formData.randomizeField || !isField)
                    && (formData.randomizeDungeons || !isDungeon)
                    && (formData.randomizeDungeonBosses || !isDungeonBoss)
                    && (formData.randomizeFieldBosses || !isFieldBoss);
            });
            subList.Sort((x, y) =>
            {
                if (x.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase) && !y.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase))
                    return 1;
                if (!x.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase) && y.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase))
                    return -1;
                if (x.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase) && !y.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase))
                    return 1;
                if (!x.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase) && y.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase))
                    return -1;
                return string.Compare(x.filename, y.filename);
            });
            int progress = 0;
            int progressTotal = subList.Count;
            foreach (AreaData area in subList)
            {
                ++progress;
                MainPage.Instance.Dispatcher.BeginInvoke(() => MainPage.Instance.progressBar.Value = Math.Ceiling(100.0 * progress / progressTotal));
                bool isFieldBoss = area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && !area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isDungeonBoss = area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isDungeon = !area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isField = !area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase)
                    && !area.filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                bool isBoss = area.filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase);
                bool nightOnly = (formData.nightOnly && isField)
                    || (formData.nightOnlyDungeons && isDungeon)
                    || (formData.nightOnlyDungeonBosses && isDungeonBoss)
                    || (formData.nightOnlyBosses && isFieldBoss);
                int changes = 1;
                float LevelMultiplier(SpawnData spawnData)
                {
                    if (isDungeon || isDungeonBoss)
                    {
                        return spawnData.IsBoss ? dungeonBossLevel : dungeonLevel;
                    }
                    return spawnData.IsBoss ? fieldBossLevel : fieldLevel;
                }
                if (!formData.methodNone)
                {
                    int speciesCount = 0;
                    List<SpawnEntry> spawnEntries = [];
                    List<SpawnEntry> spawnEntriesOriginal = area.SpawnEntries;
                    area.SpawnEntries = spawnEntries;
                    void AddEntry(SpawnEntry value)
                    {
                        SpawnEntry spawnEntry = new()
                        {
                            Weight = 10,
                            SpawnList = value.SpawnList.ConvertAll(spawnData =>
                                new SpawnData(spawnData.Name, spawnData.MinGroupSize, spawnData.MaxGroupSize) { IsPal = Data.PalData[spawnData.Name].IsPal })
                        };
                        spawnEntries.Add(spawnEntry);
                        int minLevel = area.minLevel;
                        int maxLevel = area.maxLevel;
                        if (Data.PalData[spawnEntry.SpawnList[0].Name].Nocturnal && nightOnly)
                        {
                            spawnEntry.NightOnly = true;
                            spawnEntry.Weight = 1000;
                            if (area.minLevelNight != 0)
                            {
                                minLevel = area.minLevelNight;
                                maxLevel = area.maxLevelNight;
                            }
                        }
                        float range = maxLevel - minLevel;
                        float average = (maxLevel + minLevel) / 2.0f;
                        float levelMultiplier = LevelMultiplier(spawnEntry.SpawnList[0]);
                        spawnEntry.SpawnList[0].MinLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average - range / 2.0f), 1, levelCap);
                        spawnEntry.SpawnList[0].MaxLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average + range / 2.0f), 1, levelCap);
                        if (!Data.PalData[spawnEntry.SpawnList[0].Name].IsPal && Data.PalData[spawnEntry.SpawnList[0].Name].AIResponse == "Kill_All" && formData.groupVanilla)
                        {
                            spawnEntry.Weight = 5;
                        }
                        if (spawnEntry.SpawnList.Count > 1)
                        {
                            float firstRange = value.SpawnList[0].MaxLevel - value.SpawnList[0].MinLevel;
                            float firstAverage = (value.SpawnList[0].MaxLevel + value.SpawnList[0].MinLevel) / 2.0f;
                            for (int i = 1; i < spawnEntry.SpawnList.Count; ++i)
                            {
                                float currentRange = value.SpawnList[i].MaxLevel - value.SpawnList[i].MinLevel;
                                float currentAverage = (value.SpawnList[i].MaxLevel + value.SpawnList[i].MinLevel) / 2.0f;
                                float newRange = (firstRange == 0 ? currentRange : range * currentRange / firstRange);
                                float newAverage = LevelMultiplier(spawnEntry.SpawnList[i]) * average * currentAverage / firstAverage;
                                spawnEntry.SpawnList[i].MinLevel = (uint) Math.Clamp((int) Math.Round(newAverage - newRange / 2.0f), 1, levelCap);
                                spawnEntry.SpawnList[i].MaxLevel = (uint) Math.Clamp((int) Math.Round(newAverage + newRange / 2.0f), 1, levelCap);
                            }
                        }
                    }
                    SpawnEntry GetRandomGroup()
                    {
                        SpawnData NextSpecies(List<SpawnEntry> spawns, List<SpawnEntry> original, List<SpawnData>? currentSpawns = null)
                        {
                            if (spawns.Count == 0)
                            {
                                spawns.AddRange(original);
                                if (currentSpawns != null && currentSpawns.Count < spawns.Count)
                                {
                                    spawns.RemoveAll(entry => currentSpawns.Exists(spawnData => entry.SpawnList[0].Name == spawnData.Name));
                                }
                            }
                            int i = random.Next(0, spawns.Count);
                            string name = spawns[i].SpawnList[0].Name;
                            spawns.RemoveAt(i);
                            return new(name) { IsPal = Data.PalData[name].IsPal };
                        }
                        int groupSize = random.Next(minGroup, maxGroup + 1);
                        SpawnEntry spawnEntry = new();
                        List<SpawnEntry> spawns = basicSpawnsCurrent;
                        List<SpawnEntry> original = basicSpawnsOriginal;
                        if (isBoss)
                        {
                            spawnEntry.SpawnList.Add(NextSpecies(bossSpawnsCurrent, bossSpawnsOriginal));
                            if (formData.multiBoss)
                            {
                                spawns = bossSpawnsCurrent;
                                original = bossSpawnsOriginal;
                            }
                        }
                        else
                        {
                            spawnEntry.SpawnList.Add(NextSpecies(basicSpawnsCurrent, basicSpawnsOriginal));
                        }
                        if (original.Count != 0)
                        {
                            if (nightOnly)
                            {
                                List<SpawnEntry> diurnal = [.. spawns.Where(entry => !Data.PalData[entry.SpawnList[0].Name].Nocturnal)];
                                List<SpawnEntry> nocturnal = [.. spawns.Where(entry => Data.PalData[entry.SpawnList[0].Name].Nocturnal)];
                                List<SpawnEntry> diurnalOriginal = [.. original.Where(entry => !Data.PalData[entry.SpawnList[0].Name].Nocturnal)];
                                List<SpawnEntry> nocturnalOriginal = [.. original.Where(entry => Data.PalData[entry.SpawnList[0].Name].Nocturnal)];
                                List<SpawnEntry> spawnsUsed = Data.PalData[spawnEntry.SpawnList[0].Name].Nocturnal ? nocturnal : diurnal;
                                List<SpawnEntry> originalsUsed = Data.PalData[spawnEntry.SpawnList[0].Name].Nocturnal ? nocturnalOriginal : diurnalOriginal;
                                while (spawnEntry.SpawnList.Count < groupSize)
                                {
                                    spawnEntry.SpawnList.Add(NextSpecies(spawnsUsed, originalsUsed, spawnEntry.SpawnList));
                                }
                                if (spawns.Count == 0)
                                    spawns.AddRange(original);
                                spawns.RemoveAll(entry => spawnEntry.SpawnList.Exists(spawnData => entry.SpawnList[0].Name == spawnData.Name));
                            }
                            else
                            {
                                while (spawnEntry.SpawnList.Count < groupSize)
                                {
                                    spawnEntry.SpawnList.Add(NextSpecies(spawns, original, spawnEntry.SpawnList));
                                }
                            }
                        }
                        if (isBoss && spawnEntry.SpawnList.Count > 1 && !formData.multiBoss)
                        {
                            spawnEntry.SpawnList[0].MinLevel = 4;
                            spawnEntry.SpawnList[0].MaxLevel = 8;
                            spawnEntry.SpawnList[1..].ForEach(spawnData => { spawnData.MinLevel = 2; spawnData.MaxLevel = 6; });
                        }
                        return spawnEntry;
                    }
                    if (formData.methodFull)
                    {
                        basicSpawnsCurrent = [.. basicSpawnsOriginal];
                        bossSpawnsCurrent = [.. bossSpawnsOriginal];
                        int maxSpecies = isFieldBoss && !formData.fieldBossExtended
                            ? 1
                            : Enumerable.Sum((isBoss ? bossSpawnsOriginal : basicSpawnsOriginal).ConvertAll(entry => entry.SpawnList.Count));
                        if (formData.groupVanilla)
                        {
                            List<SpawnEntry> spawns = isBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                            List<SpawnEntry> original = isBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
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
                        else if (formData.groupRandom)
                        {
                            if (isBoss && !formData.multiBoss)
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
                    else
                    {
                        int entryCount = isFieldBoss && !formData.fieldBossExtended
                            ? 1
                            : (formData.methodCustomSize ? spawnListSize : spawnEntriesOriginal.Count);
                        for (int i = 0; i < entryCount; ++i)
                        {
                            if (formData.methodGlobalSwap)
                            {
                                SpawnEntry spawnEntry = spawnEntriesOriginal[i];
                                if (!swapMap.ContainsKey(spawnEntry.SpawnList[0].Name))
                                {
                                    if (formData.groupVanilla)
                                    {
                                        List<SpawnEntry> spawns = isBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                                        List<SpawnEntry> original = isBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
                                        if (spawns.Count == 0)
                                        {
                                            spawns.AddRange(original);
                                        }
                                        int j = random.Next(0, spawns.Count);
                                        SpawnEntry newValue = spawns[j];
                                        spawns.RemoveAt(j);
                                        swapMap.Add(spawnEntry.SpawnList[0].Name, newValue);
                                    }
                                    else if (formData.groupRandom)
                                    {
                                        swapMap.Add(spawnEntry.SpawnList[0].Name, GetRandomGroup());
                                    }
                                }
                                AddEntry(swapMap[spawnEntry.SpawnList[0].Name]);
                            }
                            else
                            {
                                if (formData.groupVanilla)
                                {
                                    List<SpawnEntry> spawns = isBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                                    List<SpawnEntry> original = isBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
                                    if (spawns.Count == 0)
                                    {
                                        spawns.AddRange(original);
                                    }
                                    int j = random.Next(0, spawns.Count);
                                    AddEntry(spawns[j]);
                                    spawns.RemoveAt(j);
                                }
                                else if (formData.groupRandom)
                                {
                                    AddEntry(GetRandomGroup());
                                }
                            }
                        }
                    }
                }
                else
                {
                    changes = 0;
                    foreach (SpawnEntry spawnEntry in area.SpawnEntries)
                    {
                        changes += spawnEntry.SpawnList.RemoveAll(spawnData => !allowedNames.Contains(spawnData.Name));
                    }
                    changes += area.SpawnEntries.RemoveAll(entry => entry.SpawnList.Count == 0);
                    foreach (SpawnEntry spawnEntry in area.SpawnEntries)
                    {
                        if ((!formData.nightOnly && isField)
                            || (!formData.nightOnlyDungeons && isDungeon)
                            || (!formData.nightOnlyDungeonBosses && isDungeonBoss)
                            || (!formData.nightOnlyBosses && isFieldBoss))
                        {
                            changes += spawnEntry.NightOnly == true ? 1 : 0;
                            spawnEntry.NightOnly = false;
                        }
                        spawnEntry.SpawnList.ForEach(spawnData =>
                        {
                            uint originalMin = spawnData.MinLevel;
                            uint originalMax = spawnData.MaxLevel;
                            float range = spawnData.MaxLevel - spawnData.MinLevel;
                            float average = (spawnData.MaxLevel + spawnData.MinLevel) / 2.0f;
                            float levelMultiplier = LevelMultiplier(spawnData);
                            spawnData.MinLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average - range / 2.0f), 1, levelCap);
                            spawnData.MaxLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average + range / 2.0f), 1, levelCap);
                            changes += (spawnData.MinLevel != originalMin ? 1 : 0) + (spawnData.MaxLevel != originalMax ? 1 : 0);
                        });
                    }
                }
                if (!formData.methodNone)
                {
                    area.SpawnEntries.Sort((x, y) => (x.NightOnly ? 1 : 0) - (y.NightOnly ? 1 : 0));
                }
                if (formData.outputLog)
                {
                    outputLog.AppendLine(Path.GetFileNameWithoutExtension(area.filename)["BP_PalSpawner_Sheets_".Length..]);
                    area.SpawnEntries.ForEach(entry => { entry.Print(outputLog); totalSpeciesCount += entry.SpawnList.Count; });
                    outputLog.AppendLine();
                }
                if (changes == 0)
                    continue;
                area.modified = true;
                PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                area.uAsset.Write($"{outputPath}\\{area.filename}");
            }
            MainPage.Instance.Dispatcher.Invoke(() => MainPage.Instance.progressBar.Visibility = Visibility.Collapsed);
            areaList.Sort((x, y) =>
            {
                if (x.modified == y.modified)
                    return string.Compare(x.filename, y.filename);
                return (y.modified ? 1 : 0) - (x.modified ? 1 : 0);
            });
            GeneratedAreaList = areaList;
            PalSpawnPage.Instance.Dispatcher.Invoke(() => PalSpawnPage.Instance.areaList.ItemsSource = GeneratedAreaList);
            if (formData.outputLog)
            {
                outputLog.AppendJoin(' ', [totalSpeciesCount, "Total Entries"]);
                outputLog.AppendLine();
                try
                {
                    File.WriteAllText("Palworld-Randomizer-Log.txt", outputLog.ToString());
                }
                catch (Exception e)
                {
                    MainPage.Instance.Dispatcher.Invoke(() =>
                        MessageBox.Show(MainPage.Instance.GetWindow(), "Error: Failed to write output log.\n" + e.ToString(), "Output Log Failed",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                }
            }
        }
        
        public static bool GeneratePalSpawns(FormData formData)
        {
            GenerateSpawnLists(formData);
            RandomizeAndSaveAssets(formData);
            MainPage.Instance.Dispatcher.Invoke(() => MainPage.Instance.statusBar.Text = "💾 Creating PAK...");
            return FileModify.GenerateAndSavePak();
        }
        public static string GetRandomPal()
        {
            return Data.PalList[new Random().Next(0, Data.PalList.Count)];
        }
    }

    public static class FileModify
    {
        public static bool SaveAreaList(List<AreaData> areaList)
        {
            string outputPath = UAssetData.AppDataPath("Create-Pak\\Pal\\Content\\Pal\\Blueprint\\Spawner\\SheetsVariant");
            Directory.CreateDirectory(outputPath);
            Directory.GetFiles(outputPath).ForAll(File.Delete);
            bool changesDetected = false;
            Data.AreaForEachIfDiff(areaList, area =>
            {
                changesDetected = true;
                PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                area.uAsset.Write($"{outputPath}\\{area.filename}");
            });
            return changesDetected;
        }
        public static bool GenerateAndSavePak()
        {
            string outputPath = UAssetData.AppDataPath("Create-Pak\\Pal\\Content\\Pal\\Blueprint\\Spawner\\SheetsVariant");
            if (Directory.GetFiles(outputPath).Length == 0)
            {
                return false;
            }
            File.WriteAllText(UAssetData.AppDataPath("create-pak.txt"), $"\"{UAssetData.AppDataPath("Create-Pak\\*.*")}\" \"..\\..\\..\\*.*\" \n");
            Process unrealPak = Process.Start(new ProcessStartInfo(UAssetData.AppDataPath("UnrealPak.exe"),
                [UAssetData.AppDataPath("SpawnRandomizer_P.pak"), $"-create={UAssetData.AppDataPath("create-pak.txt")}", "-compress"]) { CreateNoWindow = true })!;
            unrealPak.WaitForExit();
            SaveFileDialog saveDialog = new()
            {
                FileName = "SpawnRandomizer_P",
                DefaultExt = ".pak",
                Filter = "PAK File|*.pak"
            };
            if (saveDialog.ShowDialog() == true)
            {
                File.Move(UAssetData.AppDataPath("SpawnRandomizer_P.pak"), saveDialog.FileName, true);
            }
            return true;
        }
        public static string? LoadPak()
        {
            OpenFileDialog openDialog = new()
            {
                DefaultExt = ".pak",
                Filter = "PAK File|*.pak|All files|*.*"
            };
            if (openDialog.ShowDialog() == true && openDialog.FileName != string.Empty)
            {
                string outputPath = UAssetData.AppDataPath("Extract-Pak");
                Directory.CreateDirectory(outputPath);
                Directory.GetFiles(outputPath).ForAll(File.Delete);
                Process unrealPak = Process.Start(new ProcessStartInfo(UAssetData.AppDataPath("UnrealPak.exe"),
                    [openDialog.FileName, "-extract", outputPath]) { CreateNoWindow = true })!;
                unrealPak.WaitForExit();
                if (unrealPak.ExitCode != 0)
                    return "UnrealPak failed to extract the file.";
                string[] files = Directory.GetFiles(outputPath, "BP_PalSpawner_Sheets_*.uasset").Select(Path.GetFileName).ToArray()!;
                if (files.Length == 0)
                    return "No valid uasset files were found.";
                List<AreaData> areaList = Data.AreaDataCopy();
                foreach (AreaData area in areaList)
                {
                    if (files.Contains(area.filename))
                    {
                        area.uAsset = UAssetData.LoadAsset($"Extract-Pak\\{area.filename}");
                        area.spawnExportData = PalSpawn.ReadAsset(area.uAsset, Data.AreaData[area.filename].spawnExportData.header.Length);
                        area.modified = true;
                    }
                }
                areaList.Sort((x, y) =>
                {
                    if (x.modified == y.modified)
                        return string.Compare(x.filename, y.filename);
                    return (y.modified ? 1 : 0) - (x.modified ? 1 : 0);
                });
                PalSpawnPage.Instance.areaList.ItemsSource = areaList;
            }
            else
            {
                return "Cancel";
            }
            return null;
        }
        public static void SaveCSV(List<AreaData> areaList)
        {
            SaveFileDialog saveDialog = new()
            {
                FileName = "PalworldSpawns",
                DefaultExt = ".csv",
                Filter = "Comma-separated values|*.csv|All files|*.*"
            };
            if (saveDialog.ShowDialog() == true)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("Area Name,Group Index,Weight,Night Only,Character Name,Is Boss,Min. Level,Max. Level,Min. Count,Max. Count");
                foreach (AreaData area in areaList)
                {
                    if (area.SpawnEntries.Count == 0)
                    {
                        stringBuilder.Append(area.SimpleName);
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
                            stringBuilder.AppendJoin(',', [area.SimpleName, i, (j == 0 ? entry.Weight : ""), (j == 0 ? entry.NightOnly : ""), spawnData.SimpleName, spawnData.IsBoss,
                                spawnData.MinLevel, spawnData.MaxLevel, spawnData.MinGroupSize, spawnData.MaxGroupSize]);
                            stringBuilder.AppendLine();
                        }
                    }
                }
                File.WriteAllText(saveDialog.FileName, stringBuilder.ToString(), Encoding.UTF8);
            }
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
                string[] fileLines = File.ReadAllLines(openDialog.FileName, Encoding.UTF8)[1..];
                try
                {
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
                        spawnEntry.SpawnList.Add(new()
                        {
                            Name = Data.SimpleName[list[4]],
                            IsPal = Data.PalData[Data.SimpleName[list[4]]].IsPal,
                            IsBoss = bool.Parse(list[5]),
                            MinLevel = uint.Parse(list[6]),
                            MaxLevel = uint.Parse(list[7]),
                            MinGroupSize = uint.Parse(list[8]),
                            MaxGroupSize = uint.Parse(list[9])
                        });
                    }
                    List<AreaData> areaList = [.. areaDict.Values];
                    areaList.ForEach(area => area.SpawnEntries.RemoveAll(entry => entry.SpawnList.Count == 0));
                    Data.AreaForEachIfDiff(areaList, area => area.modified = true);
                    areaList.Sort((x, y) =>
                    {
                        if (x.modified == y.modified)
                            return string.Compare(x.filename, y.filename);
                        return (y.modified ? 1 : 0) - (x.modified ? 1 : 0);
                    });
                    PalSpawnPage.Instance.areaList.ItemsSource = areaList;
                }
                catch (Exception e)
                {
                    return e.ToString();
                }
            }
            else
                return "Cancel";
            return null;
        }
    }
}
