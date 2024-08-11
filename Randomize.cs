using Microsoft.Win32;
using Newtonsoft.Json;
using Stfu.Linq;
using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace PalworldRandomizer
{
    public static partial class Data
    {
        public static Dictionary<string, CharacterData> PalData { get; private set; } = [];
        public static Dictionary<string, string> PalName { get; private set; } = [];
        public static Dictionary<string, string> SimpleName { get; private set; } = [];
        public static List<string> SimpleNameValues { get; private set; } = [];
        public static Dictionary<string, string> PalIcon { get; private set; } = [];
        public static List<string> PalList { get; private set; } = [];
        public static Dictionary<string, string> BossName { get; private set; } = [];
        public static List<string> TowerBossNames { get; private set; } = [];
        public static List<string> TowerBossNames2 { get; private set; } = [];
        public static List<string> RaidBossNames { get; private set; } = [];
        public static List<string> RaidBossNames2 { get; private set; } = [];
        public static HashSet<string> FlyingNames { get; private set; } = [];
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
            "Scientist_FlameThrower",
            "Hunter_MissileLauncher",
            "Hunter_GrenadeLauncher"
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
            "SalesPerson_Wander",
            "Male_DarkTrader02"
        ];
        public static readonly string[] palTraderNames =
        [
            "PalDealer",
            "PalDealer_Desert",
            "PalDealer_Volcano",
            "RandomEventShop",
            "Male_DarkTrader01"
        ];
        public static readonly string[] ninjaNames =
        [
            "Male_Ninja01",
            "Male_NinjaElite01"
        ];

        [GeneratedRegex("^(.+?)(_[0-9]+(_.+)?|_Flower)?$", RegexOptions.IgnoreCase)]
        private static partial Regex resourceKeyRegex();

        public static void Initialize(ResourceManager resourceManager)
        {
            PalData = UAssetData.CreatePalData();
            UAsset palNames = UAssetData.LoadAsset(@"Data\DT_PalNameText.uasset");
            UAsset humanNames = UAssetData.LoadAsset(@"Data\DT_HumanNameText.uasset");
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
                { "FlameThrower", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_FlameThrower_Default.png" },
                { "GatlingGun", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_GatlingGun.png" },
                { "BowGun", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_BowGun.png" },
                { "LaserRifle", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_LaserRifle.png" },
                { "MissileLauncher", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_GuidedMissileLauncher.png" },
                { "GrenadeLauncher", "/Resources/Images/InventoryItemIcon/T_itemicon_Weapon_GrenadeLauncher.png" }
                // Katana - No sprite available
            };
            string[] flyingSuffixes =
            [
                "HawkBird",
                "Eagle",
                "BirdDragon",
                "BirdDragon_Ice",
                "ThunderBird",
                "RedArmorBird",
                "HadesBird",
                "HadesBird_Electric",
                "Suzaku",
                "Suzaku_Water",
                "Horus",
                "Horus_2"
            ];
            foreach (KeyValuePair<string, CharacterData> keyPair in PalData)
            {
                if (keyPair.Value.IsPal)
                {
                    StructPropertyData? nameData = ((DataTableExport) palNames.Exports[0]).Table.Data.Find(property =>
                        (PalData[keyPair.Key].OverrideNameTextId != null &&
                        string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"{PalData[keyPair.Key].OverrideNameTextId}_TextData", true) == 0)
                        || string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"PAL_NAME_{keyPair.Key}_TextData", true) == 0);
                    string nameString = nameData != null ? ((TextPropertyData) nameData.Value[0]).CultureInvariantString.Value.Trim() : "en_text";
                    while (nameString.Contains("  "))
                    {
                        nameString = nameString.Replace("  ", " ");
                    }
                    bool isTowerBoss = keyPair.Key.StartsWith("GYM_", StringComparison.InvariantCultureIgnoreCase);
                    bool isRaidBoss = keyPair.Key.StartsWith("RAID_", StringComparison.InvariantCultureIgnoreCase);
                    bool isBoss = keyPair.Key.StartsWith("BOSS_", StringComparison.InvariantCultureIgnoreCase) || isTowerBoss || isRaidBoss;
                    PalName.Add(keyPair.Key, nameString == "en_text" ? (isBoss ? keyPair.Key[(keyPair.Key.IndexOf('_') + 1)..] : keyPair.Key) : nameString);
                    if (keyPair.Value.ZukanIndex > 0)
                    {
                        PalList.Add(keyPair.Key);
                    }
                    if (!isBoss || isTowerBoss || isRaidBoss)
                    {
                        if (!isBoss)
                        {
                            BossName.Add(keyPair.Key, PalData.Keys.First(key => string.Compare(key, $"BOSS_{keyPair.Key}", true) == 0));
                        }
                        else if (isTowerBoss)
                        {
                            // TODO: Change to regex
                            if (keyPair.Key.EndsWith("_2"))
                            {
                                TowerBossNames2.Add(keyPair.Key);
                            }
                            else if (!keyPair.Key.EndsWith("_2_Avatar") && !keyPair.Key.EndsWith("_2_Servant"))
                            {
                                TowerBossNames.Add(keyPair.Key);
                            }
                        }
                        else
                        {
                            if (keyPair.Key.EndsWith("_2"))
                            {
                                RaidBossNames2.Add(keyPair.Key);
                            }
                            else
                            {
                                RaidBossNames.Add(keyPair.Key);
                            }
                        }
                        SimpleName.Add(new SpawnData(keyPair.Key).SimpleName, keyPair.Key);
                    }
                    string resourceKey = resourceKeyRegex().Match(keyPair.Key).Groups[1].Value;
                    resourceKey = isBoss ? resourceKey[(resourceKey.IndexOf('_') + 1)..] : resourceKey;
                    string resourceName = $"Resources/Images/PalIcon/T_{resourceKey}_icon_normal.png";
                    if (resourceNames.Contains(resourceName))
                    {
                        PalIcon.Add(keyPair.Key, $"/{resourceName}");
                    }
                    else
                    {
                        PalIcon.Add(keyPair.Key, "/Resources/Images/PalIcon/T_icon_unknown.png");
                    }
                    if (Array.Exists(flyingSuffixes, keyPair.Key.EndsWith))
                    {
                        FlyingNames.Add(keyPair.Key);
                    }
                }
                else
                {
                    StructPropertyData? property = ((DataTableExport) humanNames.Exports[0]).Table.Data.Find(property =>
                        ((TextPropertyData) property.Value[0]).Value.Value == $"{PalData[keyPair.Key].OverrideNameTextId}_TextData");
                    SimpleName.Add(keyPair.Key, keyPair.Key);
                    if (property != null)
                    {
                        PalName.Add(keyPair.Key, ((TextPropertyData) property.Value[0]).CultureInvariantString.Value.Trim());
                    }
                    else
                    {
                        PalName.Add(keyPair.Key, "-");
                    }
                    if (PalData[keyPair.Key].Weapon != null && weapons.TryGetValue(PalData[keyPair.Key].Weapon!, out string? value))
                    {
                        PalIcon.Add(keyPair.Key, value);
                    }
                    else
                    {
                        PalIcon.Add(keyPair.Key, "/Resources/Images/PalIcon/T_CommonHuman_icon_normal.png");
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
                AreaData[filename].minLevel = Convert.ToInt32(averageLevel / weightSum - levelRange / 2.0f / weightSum);
                AreaData[filename].maxLevel = Convert.ToInt32(averageLevel / weightSum + levelRange / 2.0f / weightSum);
                if (nightWeightSum != 0)
                {
                    AreaData[filename].minLevelNight = Convert.ToInt32(nightAverageLevel / nightWeightSum - nightLevelRange / 2.0f / nightWeightSum);
                    AreaData[filename].maxLevelNight = Convert.ToInt32(nightAverageLevel / nightWeightSum + nightLevelRange / 2.0f / nightWeightSum);
                }
                AreaData[filename].isBoss = filename.Contains("boss", StringComparison.InvariantCultureIgnoreCase);
                AreaData[filename].isInDungeon = filename.Contains("dungeon", StringComparison.InvariantCultureIgnoreCase);
                AreaData[filename].isFieldBoss = AreaData[filename].isBoss && !AreaData[filename].isInDungeon;
                AreaData[filename].isDungeonBoss = AreaData[filename].isBoss && AreaData[filename].isInDungeon;
                AreaData[filename].isDungeon = !AreaData[filename].isBoss && AreaData[filename].isInDungeon;
                AreaData[filename].isField = !AreaData[filename].isBoss && !AreaData[filename].isInDungeon;
            }
            AreaData["BP_PalSpawner_Sheets_1_1_plain_begginer.uasset"].minLevel = 1;
            AreaData["BP_PalSpawner_Sheets_1_1_plain_begginer.uasset"].maxLevel = 3;
        }
        public static List<AreaData> AreaDataCopy()
        {
            return [.. AreaData.Values.Select(area => area.Clone())];
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
        public static bool AreaListChanged { get; set; } = false;

        public static void Initialize()
        {
            GeneratedAreaList = Data.AreaDataCopy();
        }
        private static void GenerateSpawnLists(FormData formData)
        {
            basicSpawns = [];
            bossSpawns = [];
            humanSpawns = [];
            if (formData.GroupVanilla)
            {
                foreach (string key in Data.PalList)
                {
                    if (formData.SpawnPals)
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
                        if (bossEntry.SpawnList.Count == 1 && formData.SpawnPals && basicSpawns[key].SpawnList[0].MaxGroupSize > 1)
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
                        ((formData.SpawnHumans && Data.humanNames.Contains(keyPair.Key))
                        || (formData.SpawnPolice && Data.policeNames.Contains(keyPair.Key))
                        || (formData.SpawnNinja && Data.ninjaNames.Contains(keyPair.Key)))
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
                        ((formData.SpawnHumans && Data.humanNames.Contains(spawnEntry.SpawnList[0].Name))
                        || (formData.SpawnPolice && Data.policeNames.Contains(spawnEntry.SpawnList[0].Name))
                        || (formData.SpawnNinja && Data.ninjaNames.Contains(spawnEntry.SpawnList[0].Name)))
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
                if (formData.SpawnPolice)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Police_Handgun", 1, 2)] });
                }
                if (formData.SpawnGuards)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Guard_Rifle", 1, 2), new("Guard_Shotgun", 1, 2)] });
                }
                if (formData.SpawnHumans)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_FlameThrower", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Scientist_FlameThrower", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_MissileLauncher", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_GrenadeLauncher", 1, 2)] });
                }
                if (formData.SpawnTraders)
                {
                    humanSpawns.AddRange(Data.traderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
                if (formData.SpawnPalTraders)
                {
                    humanSpawns.AddRange(Data.palTraderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
            }
            else if (formData.GroupRandom)
            {
                if (formData.SpawnPals)
                {
                    Data.PalList.ForEach(name => basicSpawns.Add(name, new() { SpawnList = [new(name)] }));
                }
                Data.PalList.ConvertAll(name => Data.BossName[name]).ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
                humanSpawns.AddRange(new Collection<string>
                ([
                    .. (formData.SpawnHumans ? Data.humanNames : []),
                    .. (formData.SpawnPolice ? Data.policeNames : []),
                    .. (formData.SpawnGuards ? Data.guardNames : []),
                    .. (formData.SpawnNinja ? Data.ninjaNames : []),
                    .. (formData.SpawnTraders ? Data.traderNames : []),
                    .. (formData.SpawnPalTraders ? Data.palTraderNames : [])
                ]).Select(name => new SpawnEntry { SpawnList = [new(name)] }));
            }
            if (formData.SpawnTowerBosses)
            {
                Data.TowerBossNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            if (formData.SpawnTowerBosses2)
            {
                Data.TowerBossNames2.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            if (formData.SpawnRaidBosses)
            {
                Data.RaidBossNames.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
            if (formData.SpawnRaidBosses2)
            {
                Data.RaidBossNames2.ForEach(name => bossSpawns.Add(name, new() { SpawnList = [new(name)] }));
            }
        }
        private static ICollection<string> GetAllowedNames(FormData formData)
        {
            return
            [
                .. (formData.SpawnPals ? Data.PalList : []),
                .. Data.PalList.ConvertAll(name => Data.BossName[name]),
                .. (formData.SpawnTowerBosses ? Data.TowerBossNames : []),
                .. (formData.SpawnTowerBosses2 ? Data.TowerBossNames2 : []),
                .. (formData.SpawnRaidBosses ? Data.RaidBossNames : []),
                .. (formData.SpawnRaidBosses2 ? Data.RaidBossNames2 : []),
                .. (formData.SpawnHumans ? Data.humanNames : []),
                .. (formData.SpawnPolice ? Data.policeNames : []),
                .. (formData.SpawnGuards ? Data.guardNames : []),
                .. (formData.SpawnNinja ? Data.ninjaNames : []),
                .. (formData.SpawnTraders ? Data.traderNames : []),
                .. (formData.SpawnPalTraders ? Data.palTraderNames : [])
            ];
        }

        public static void SaveBackup()
        {
            try
            {
                if (AreaListChanged)
                {
                    AreaListChanged = false;
                    List<AreaData> areaList = (List<AreaData>) PalSpawnPage.Instance.areaList.ItemsSource;
                    Directory.CreateDirectory(UAssetData.AppDataPath("Backups"));
                    File.WriteAllText(UAssetData.AppDataPath($"Backups\\{DateTime.Now:MM-dd-yy-HH-mm-ss}.csv"), FileModify.GenerateCSV(areaList), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        public static void RestoreBackup()
        {
            try
            {
                string[] backups = [.. ((IEnumerable<FileInfo>) [.. new DirectoryInfo(UAssetData.AppDataPath("Backups")).GetFiles("*.csv"),
                    .. new DirectoryInfo(UAssetData.AppDataPath("Log")).GetFiles("*.csv")]).OrderByDescending(x => x.LastWriteTime).Select(x => x.FullName)];
                if (backups.Length != 0)
                {
                    GeneratedAreaList = FileModify.ConvertCSV(backups[0]);
                }
            }
            catch
            {
            }
        }

        private static void RandomizeAndSaveAssets(FormData formData)
        {
            SaveBackup();
            StringBuilder outputLog = formData.OutputLog ? new($"Random Seed: {formData.RandomSeed}\n\n") : null!;
            int minGroup = Math.Max(1, formData.GroupMin);
            int maxGroup = Math.Max(minGroup, formData.GroupMax);
            int minGroupBoss = Math.Max(1, formData.GroupMinBoss);
            int maxGroupBoss = Math.Max(minGroup, formData.GroupMaxBoss);
            int spawnListSize = Math.Max(1, formData.SpawnListSize);
            double vanillaPlusChance = Math.Clamp(formData.VanillaPlusChance, 1, 99) / 100.0;
            float fieldLevel = Math.Max(0, formData.FieldLevel) / 100.0f;
            float dungeonLevel = Math.Max(0, formData.DungeonLevel) / 100.0f;
            float fieldBossLevel = Math.Max(0, formData.FieldBossLevel) / 100.0f;
            float dungeonBossLevel = Math.Max(0, formData.DungeonBossLevel) / 100.0f;
            int levelCap = Math.Max(1, formData.LevelCap);
            int rarity67MinLevel = Math.Clamp(formData.Rarity67MinLevel, 1, levelCap);
            int rarity8UpMinLevel = Math.Clamp(formData.Rarity8UpMinLevel, 1, levelCap);
            int weightUniformMin = Math.Max(1, formData.WeightUniformMin);
            int weightUniformMax = Math.Max(weightUniformMin, formData.WeightUniformMax);
            int humanRarity = Math.Clamp(formData.HumanRarity, 1, 20);
            float humanWeight = Math.Max(0, formData.HumanWeight) / 100.0f;
            float humanWeightAggro = Math.Max(0, formData.HumanWeightAggro) / 100.0f;
            double weightNightOnly = Math.Max(0, Convert.ToDouble(formData.WeightNightOnly));
            int baseCountMin = Math.Max(0, formData.BaseCountMin);
            int baseCountMax = Math.Max(Math.Max(1, baseCountMin), formData.BaseCountMax);
            float fieldCount = Math.Max(0, formData.FieldCount) / 100.0f;
            float dungeonCount = Math.Max(0, formData.DungeonCount) / 100.0f;
            float fieldBossCount = Math.Max(0, formData.FieldBossCount) / 100.0f;
            float dungeonBossCount = Math.Max(0, formData.DungeonBossCount) / 100.0f;
            int countClampMin = Math.Max(0, formData.CountClampMin);
            int countClampMax = Math.Max(Math.Max(1, countClampMin), formData.CountClampMax);
            int countClampFirstMin = Math.Max(0, formData.CountClampFirstMin);
            int countClampFirstMax = Math.Max(Math.Max(1, countClampFirstMin), formData.CountClampFirstMax);
            int totalSpeciesCount = 0;
            string outputPath = UAssetData.AppDataPath(@"Create-Pak\Pal\Content\Pal\Blueprint\Spawner\SheetsVariant");
            Directory.CreateDirectory(outputPath);
            Directory.GetFiles(outputPath).ForAll(File.Delete);
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
            }
            List<SpawnEntry> basicSpawnsCurrent = [.. basicSpawnsOriginal];
            List<SpawnEntry> bossSpawnsCurrent = [.. bossSpawnsOriginal];
            List<SpawnEntry> basicSpawnsOriginalBackup = [.. basicSpawnsOriginal];
            List<SpawnEntry> bossSpawnsOriginalBackup = [.. bossSpawnsOriginal];
            HashSet<string> allowedNames = [.. GetAllowedNames(formData)];
            Random random = new(formData.RandomSeed);
            List<AreaData> areaList = Data.AreaDataCopy();
            List<AreaData> subList = areaList.FindAll(area =>
            {
                return (formData.RandomizeField || !area.isField)
                    && (formData.RandomizeDungeons || !area.isDungeon)
                    && (formData.RandomizeDungeonBosses || !area.isDungeonBoss)
                    && (formData.RandomizeFieldBosses || !area.isFieldBoss);
            });
            subList.Sort((x, y) =>
            {
                if (x.isBoss != y.isBoss)
                    return (x.isBoss ? 1 : 0) - (y.isBoss ? 1 : 0);
                if (x.isInDungeon != y.isInDungeon)
                    return (x.isInDungeon ? 1 : 0) - (y.isInDungeon ? 1 : 0);
                return string.Compare(x.filename, y.filename);
            });
            bool equalizeAreaRarity = formData.EqualizeAreaRarity && !formData.MethodNone && !formData.MethodFull && !formData.MethodGlobalSwap && !formData.VanillaRestrict;
            int progress = 0;
            int progressTotal = subList.Count;
            bool NightOnly(AreaData area) => (formData.NightOnly && area.isField)
                    || (formData.NightOnlyDungeons && area.isDungeon)
                    || (formData.NightOnlyDungeonBosses && area.isDungeonBoss)
                    || (formData.NightOnlyBosses && area.isFieldBoss);
            int Rarity(SpawnData spawnData)
            {
                if (!Data.PalData[spawnData.Name].IsPal)
                {
                    return humanRarity;
                }
                if (!spawnData.IsBoss || !spawnData.Name.StartsWith("BOSS_", StringComparison.InvariantCultureIgnoreCase))
                {
                    return Data.PalData[spawnData.Name].Rarity;
                }
                return Data.PalData[spawnData.Name["BOSS_".Length..]].Rarity;
            }
            bool Rarity8Up(SpawnData spawnData, int min = 8)
            {
                return Rarity(spawnData) >= min && !spawnData.Name.EndsWith("PlantSlime_Flower", StringComparison.InvariantCultureIgnoreCase);
            }
            float LevelMultiplierEx(SpawnData spawnData, bool isDungeon)
            {
                if (isDungeon)
                {
                    return spawnData.IsBoss ? dungeonBossLevel : dungeonLevel;
                }
                return spawnData.IsBoss ? fieldBossLevel : fieldLevel;
            }
            float CountMultiplierEx(SpawnData spawnData, bool isDungeon)
            {
                if (isDungeon)
                {
                    return spawnData.IsBoss ? dungeonBossCount : dungeonCount;
                }
                return spawnData.IsBoss ? fieldBossCount : fieldCount;
            }
            void GenerateLevels(SpawnEntry spawnEntry, int[] oldMinLevels, int[] oldMaxLevels, int minLevel, int maxLevel, Func<SpawnData, float> LevelMultiplier)
            {
                float range = maxLevel - minLevel;
                float average = (maxLevel + minLevel) / 2.0f;
                float levelMultiplier = LevelMultiplier(spawnEntry.SpawnList[0]);
                spawnEntry.SpawnList[0].MinLevel = (uint) Math.Clamp(Convert.ToInt32(levelMultiplier * average - range / 2.0f), 1, levelCap);
                spawnEntry.SpawnList[0].MaxLevel = (uint) Math.Clamp(Convert.ToInt32(levelMultiplier * average + range / 2.0f), 1, levelCap);
                if (spawnEntry.SpawnList.Count > 1)
                {
                    float firstRange = oldMaxLevels[0] - oldMinLevels[0];
                    float firstAverage = (oldMaxLevels[0] + oldMinLevels[0]) / 2.0f;
                    for (int i = 1; i < spawnEntry.SpawnList.Count; ++i)
                    {
                        float currentRange = oldMaxLevels[i] - oldMinLevels[i];
                        float currentAverage = (oldMaxLevels[i] + oldMinLevels[i]) / 2.0f;
                        float newRange = (firstRange == 0 ? currentRange : range * currentRange / firstRange);
                        float newAverage = LevelMultiplier(spawnEntry.SpawnList[i]) * average * currentAverage / firstAverage;
                        spawnEntry.SpawnList[i].MinLevel = (uint) Math.Clamp(Convert.ToInt32(newAverage - newRange / 2.0f), 1, levelCap);
                        spawnEntry.SpawnList[i].MaxLevel = (uint) Math.Clamp(Convert.ToInt32(newAverage + newRange / 2.0f), 1, levelCap);
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
                    return LevelMultiplierEx(spawnData, area.isInDungeon);
                }
                float CountMultiplier(SpawnData spawnData)
                {
                    return CountMultiplierEx(spawnData, area.isInDungeon);
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
                float WeightScale(SpawnData spawnData)
                {
                    return spawnData.IsPal ? 1 : (Data.PalData[spawnData.Name].AIResponse == "Kill_All" ? humanWeightAggro : humanWeight);
                }
                // NOT No Randomization
                if (!formData.MethodNone)
                {
                    List<SpawnEntry> spawnEntries = [];
                    List<SpawnEntry> spawnEntriesOriginal = area.SpawnEntries;
                    HashSet<string> vanillaNames = [.. spawnEntriesOriginal.FindAll(x => x.Weight != 0).ConvertAll(x => x.SpawnList.ConvertAll(y => y.Name)).SelectMany(x => x).Distinct()];
                    area.SpawnEntries = spawnEntries;
                    long weightSum = 0;
                    void AddEntry(SpawnEntry value)
                    {
                        SpawnEntry spawnEntry = new()
                        {
                            Weight = formData.WeightTypeUniform ? random.Next(weightUniformMin, weightUniformMax + 1) : 10,
                            SpawnList = value.SpawnList.ConvertAll(spawnData =>
                                new SpawnData(spawnData.Name, spawnData.MinGroupSize, spawnData.MaxGroupSize)
                                {
                                    IsPal = Data.PalData[spawnData.Name].IsPal,
                                    MinLevel = spawnData.MinLevel,
                                    MaxLevel = spawnData.MaxLevel
                                })
                        };
                        spawnEntries.Add(spawnEntry);
                        long weight = spawnEntry.Weight;
                        if (formData.WeightTypeCustom)
                        {
                            if (formData.WeightCustomMode == GroupWeightMode.WeightSum)
                            {
                                weight = spawnEntry.SpawnList.Sum(spawnData => Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData))));
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.WeightAverage)
                            {
                                weight = Convert.ToInt64(spawnEntry.SpawnList.Sum(spawnData => Convert.ToInt64(
                                    CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))) / (float) spawnEntry.SpawnList.Count);
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.WeightMinimum)
                            {
                                weight = spawnEntry.SpawnList.Min(spawnData => Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData))));
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.WeightMaximum)
                            {
                                weight = spawnEntry.SpawnList.Max(spawnData => Convert.ToInt64(CustomWeight(Rarity(spawnData), false, WeightScale(spawnData))));
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.RarityAverageRounded)
                            {
                                weight = Convert.ToInt64(CustomWeight(
                                    spawnEntry.SpawnList.Sum(Rarity) / (float) spawnEntry.SpawnList.Count, false,
                                    spawnEntry.SpawnList.Sum(WeightScale) / spawnEntry.SpawnList.Count));
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.RarityAverageBlend)
                            {
                                weight = Convert.ToInt64(CustomWeight(
                                    spawnEntry.SpawnList.Sum(Rarity) / (float) spawnEntry.SpawnList.Count, true,
                                    spawnEntry.SpawnList.Sum(WeightScale) / spawnEntry.SpawnList.Count));
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.RarityAverageBlend10To20)
                            {
                                float rarity = spawnEntry.SpawnList.Sum(Rarity) / (float) spawnEntry.SpawnList.Count;
                                weight = Convert.ToInt64(CustomWeight(rarity, rarity >= 10,
                                    spawnEntry.SpawnList.Sum(WeightScale) / spawnEntry.SpawnList.Count));
                            }
                        }
                        else
                        {
                            weight = Convert.ToInt64(weight * spawnEntry.SpawnList.Sum(WeightScale) / spawnEntry.SpawnList.Count);
                        }
                        int minLevel = area.minLevel;
                        int maxLevel = area.maxLevel;
                        if (Data.PalData[spawnEntry.SpawnList[0].Name].Nocturnal && nightOnly)
                        {
                            spawnEntry.NightOnly = true;
                            if ((!formData.WeightTypeCustom || !formData.WeightAdjustProbability) && !formData.VanillaPlus)
                            {
                                weight = Convert.ToInt64(weight * weightNightOnly);
                            }
                            if (area.minLevelNight != 0)
                            {
                                minLevel = area.minLevelNight;
                                maxLevel = area.maxLevelNight;
                            }
                        }
                        try
                        {
                            spawnEntry.Weight = Convert.ToInt32(weight);
                        }
                        catch
                        {
                            spawnEntry.Weight = int.MaxValue;
                        }
                        spawnEntry.Weight = Math.Max(1, spawnEntry.Weight);
                        weightSum += spawnEntry.Weight;
                        if (!equalizeAreaRarity)
                        {
                            GenerateLevels(spawnEntry, [.. value.SpawnList.ConvertAll(x => (int) x.MinLevel)], [.. value.SpawnList.ConvertAll(x => (int) x.MaxLevel)],
                                minLevel, maxLevel, LevelMultiplier);
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
                                spawnData.MinGroupSize = (uint) baseCountMin;
                                spawnData.MaxGroupSize = (uint) baseCountMax;
                            }
                            if (i == 0)
                            {
                                spawnData.MinGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MinGroupSize * CountMultiplier(spawnData)), countClampFirstMin, countClampFirstMax);
                                spawnData.MaxGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MaxGroupSize * CountMultiplier(spawnData)), countClampFirstMin, countClampFirstMax);
                            }
                            else
                            {
                                spawnData.MinGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MinGroupSize * CountMultiplier(spawnData)), countClampMin, countClampMax);
                                spawnData.MaxGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MaxGroupSize * CountMultiplier(spawnData)), countClampMin, countClampMax);
                            }
                        }
                    }
                    // Generate a random group. Not used with Vanilla-Based mode
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
                        SpawnEntry spawnEntry = new();
                        List<SpawnEntry> spawns = basicSpawnsCurrent;
                        List<SpawnEntry> original = basicSpawnsOriginal;
                        if (area.isBoss)
                        {
                            spawnEntry.SpawnList.Add(NextSpecies(bossSpawnsCurrent, bossSpawnsOriginal));
                            if (formData.MultiBoss)
                            {
                                spawns = bossSpawnsCurrent;
                                original = bossSpawnsOriginal;
                            }
                        }
                        else
                        {
                            spawnEntry.SpawnList.Add(NextSpecies(basicSpawnsCurrent, basicSpawnsOriginal));
                        }
                        if (original.Count != 0 && !(formData.Rarity8UpSolo && Rarity8Up(spawnEntry.SpawnList[0])))
                        {
                            int groupSize = area.isBoss ? random.Next(minGroupBoss, maxGroupBoss + 1) : random.Next(minGroup, maxGroup + 1);
                            if (nightOnly || !formData.MixHumanAndPal || formData.Rarity8UpSolo || formData.SeparateAggroHumans)
                            {
                                List<SpawnEntry> spawnsUsed = spawns;
                                List<SpawnEntry> originalsUsed = original;
                                if (nightOnly)
                                {
                                    SeparateGroupsByCondition(entry => Data.PalData[entry.SpawnList[0].Name].Nocturnal);
                                }
                                if (!formData.MixHumanAndPal)
                                {
                                    SeparateGroupsByCondition(entry => Data.PalData[entry.SpawnList[0].Name].IsPal);
                                }
                                if (formData.SeparateFlying)
                                {
                                    SeparateGroupsByCondition(entry => Data.FlyingNames.Contains(entry.SpawnList[0].Name));
                                }
                                if (formData.SeparateAggroHumans)
                                {
                                    if (!Data.PalData[spawnEntry.SpawnList[0].Name].IsPal && Data.PalData[spawnEntry.SpawnList[0].Name].AIResponse != "Kill_All")
                                    {
                                        FilterGroupsByCondition(entry =>
                                            Data.PalData[entry.SpawnList[0].Name].AIResponse != "Kill_All"
                                            && Data.PalData[entry.SpawnList[0].Name].AIResponse != "Warlike"
                                            && Data.PalData[entry.SpawnList[0].Name].AIResponse != "Boss"
                                            && !entry.SpawnList[0].IsBoss);
                                    }
                                    else if (Data.PalData[spawnEntry.SpawnList[0].Name].AIResponse == "Kill_All"
                                            || Data.PalData[spawnEntry.SpawnList[0].Name].AIResponse == "Warlike"
                                            || Data.PalData[spawnEntry.SpawnList[0].Name].AIResponse == "Boss"
                                            || spawnEntry.SpawnList[0].IsBoss)
                                    {
                                        FilterGroupsByCondition(entry =>
                                            Data.PalData[entry.SpawnList[0].Name].IsPal || Data.PalData[entry.SpawnList[0].Name].AIResponse == "Kill_All");
                                    }
                                    else if (spawnsUsed.Exists(entry =>
                                            !Data.PalData[entry.SpawnList[0].Name].IsPal && Data.PalData[entry.SpawnList[0].Name].AIResponse != "Kill_All"))
                                    {
                                        FilterGroupsByCondition(entry =>
                                            Data.PalData[entry.SpawnList[0].Name].IsPal || Data.PalData[entry.SpawnList[0].Name].AIResponse != "Kill_All");
                                    }
                                }
                                if (formData.Rarity8UpSolo)
                                {
                                    FilterGroupsByCondition(entry => !entry.SpawnList.Exists(x => Rarity8Up(x)));
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
                                    }
                                    if (spawns.Count == 0)
                                        spawns.AddRange(original);
                                    spawns.RemoveAll(entry => spawnEntry.SpawnList.Exists(spawnData => entry.SpawnList[0].Name == spawnData.Name));
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
                        if (area.isBoss && spawnEntry.SpawnList.Count > 1 && !formData.MultiBoss)
                        {
                            spawnEntry.SpawnList[0].MinLevel = 4;
                            spawnEntry.SpawnList[0].MaxLevel = 8;
                            spawnEntry.SpawnList[1..].ForEach(spawnData => { spawnData.MinLevel = 2; spawnData.MaxLevel = 6; });
                        }
                        return spawnEntry;
                    }
                    if (formData.VanillaRestrict && !formData.MethodGlobalSwap)
                    {
                        basicSpawnsOriginal = FilterSpawnList(basicSpawnsOriginalBackup.ConvertAll(x => x.Clone()), false);
                        bossSpawnsOriginal = FilterSpawnList(bossSpawnsOriginalBackup.ConvertAll(x => x.Clone()), true);
                        basicSpawnsCurrent = [.. basicSpawnsOriginal];
                        bossSpawnsCurrent = [.. bossSpawnsOriginal];
                        List<SpawnEntry> FilterSpawnList(List<SpawnEntry> spawnList, bool bossList)
                        {
                            spawnList.ForEach(x => x.SpawnList.RemoveAll(y => !vanillaNames.Contains(y.Name)));
                            spawnList.RemoveAll(x => x.SpawnList.Count == 0 || (bossList && !x.SpawnList.Exists(y => y.IsBoss)));
                            return spawnList;
                        }
                    }
                    // All Species Everywhere
                    if (formData.MethodFull)
                    {
                        // area.filename check for first boss area to reset the lists when changing from non-boss to bosses
                        if (!area.isFieldBoss || formData.FieldBossExtended || area.filename == "BP_PalSpawner_Sheets_1_10_plain_F_Boss_BlueDragon.uasset")
                        {
                            basicSpawnsCurrent = [.. basicSpawnsOriginal];
                            bossSpawnsCurrent = [.. bossSpawnsOriginal];
                        }
                        int speciesCount = 0;
                        int maxSpecies = area.isFieldBoss && !formData.FieldBossExtended
                            ? 1
                            : (area.isBoss ? bossSpawnsOriginal : basicSpawnsOriginal).Sum(entry => entry.SpawnList.Count);
                        if (formData.GroupVanilla)
                        {
                            List<SpawnEntry> spawns = area.isBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                            List<SpawnEntry> original = area.isBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
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
                            if (area.isBoss && !formData.MultiBoss)
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
                        int entryCount = area.isFieldBoss && !formData.FieldBossExtended
                            ? 1
                            : (formData.MethodCustomSize ? spawnListSize : spawnEntriesOriginal.Count);
                        for (int i = 0; i < entryCount; ++i)
                        {
                            if (formData.MethodGlobalSwap)
                            {
                                SpawnEntry spawnEntry = spawnEntriesOriginal[i];
                                if (!swapMap.ContainsKey(spawnEntry.SpawnList[0].Name))
                                {
                                    if (formData.GroupVanilla)
                                    {
                                        List<SpawnEntry> spawns = area.isBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                                        List<SpawnEntry> original = area.isBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
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
                                    List<SpawnEntry> spawns = area.isBoss ? bossSpawnsCurrent : basicSpawnsCurrent;
                                    List<SpawnEntry> original = area.isBoss ? bossSpawnsOriginal : basicSpawnsOriginal;
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
                    changes += spawnEntry.SpawnList.RemoveAll(spawnData => !allowedNames.Contains(spawnData.Name));
                }
                changes += spawnEntries.RemoveAll(entry => entry.SpawnList.Count == 0);
                foreach (SpawnEntry spawnEntry in spawnEntries)
                {
                    if (NightOnly(area))
                    {
                        changes += spawnEntry.NightOnly == true ? 1 : 0;
                        spawnEntry.NightOnly = false;
                    }
                    for (int i = 0; i < spawnEntry.SpawnList.Count; ++i)
                    {
                        SpawnData spawnData = spawnEntry.SpawnList[i];
                        uint originalMin = spawnData.MinLevel;
                        uint originalMax = spawnData.MaxLevel;
                        uint originalGroupMin = spawnData.MinGroupSize;
                        uint originalGroupMax = spawnData.MaxGroupSize;
                        float range = spawnData.MaxLevel - spawnData.MinLevel;
                        float average = (spawnData.MaxLevel + spawnData.MinLevel) / 2.0f;
                        float levelMultiplier = LevelMultiplierEx(spawnData, area.isInDungeon);
                        spawnData.MinLevel = (uint) Math.Clamp(Convert.ToInt32(levelMultiplier * average - range / 2.0f), 1, levelCap);
                        spawnData.MaxLevel = (uint) Math.Clamp(Convert.ToInt32(levelMultiplier * average + range / 2.0f), 1, levelCap);
                        float countMultiplier = CountMultiplierEx(spawnData, area.isInDungeon);
                        if (i == 0)
                        {
                            spawnData.MinGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MinGroupSize * countMultiplier), countClampFirstMin, countClampFirstMax);
                            spawnData.MaxGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MaxGroupSize * countMultiplier), countClampFirstMin, countClampFirstMax);
                        }
                        else
                        {
                            spawnData.MinGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MinGroupSize * countMultiplier), countClampMin, countClampMax);
                            spawnData.MaxGroupSize = (uint) Math.Clamp(Convert.ToInt32(spawnData.MaxGroupSize * countMultiplier), countClampMin, countClampMax);
                        }
                        changes += (spawnData.MinLevel != originalMin ? 1 : 0) + (spawnData.MaxLevel != originalMax ? 1 : 0)
                            + (spawnData.MinGroupSize != originalGroupMin ? 1 : 0) + (spawnData.MaxGroupSize != originalGroupMax ? 1 : 0);
                    }
                }
                return changes != 0;
            }
            void WriteAreaAsset(AreaData area, bool saveData = true)
            {
                if (formData.OutputLog)
                {
                    outputLog.AppendLine(Path.GetFileNameWithoutExtension(area.filename)["BP_PalSpawner_Sheets_".Length..]);
                    area.SpawnEntries.ForEach(entry => { entry.Print(outputLog); totalSpeciesCount += entry.SpawnList.Count; });
                    outputLog.AppendLine();
                }
                if (saveData)
                {
                    area.modified = true;
                    PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                    area.uAsset.Write($"{outputPath}\\{area.filename}");
                }
            }
            if (equalizeAreaRarity)
            {
                List<AreaData> fieldList = [];
                List<AreaData> dungeonList = [];
                List<AreaData> fieldBossList = [];
                List<AreaData> dungeonBossList = [];
                foreach (AreaData area in subList)
                {
                    if (area.isBoss)
                    {
                        if (area.isInDungeon)
                        {
                            dungeonBossList.Add(area);
                        }
                        else
                        {
                            fieldBossList.Add(area);
                        }
                    }
                    else
                    {
                        if (area.isInDungeon)
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
                    double nightRatio = (double) nocturnalSpawns.Count / diurnalSpawns.Count;
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
                        int[] indices = Enumerable.Range(0, nightCounts.Count).ToArray();
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
                        int[] indices = Enumerable.Range(0, nightCounts.Count).ToArray();
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
                        int minLevel, maxLevel;
                        if (spawnEntry.NightOnly && area.minLevelNight != 0)
                        {
                            minLevel = area.minLevelNight;
                            maxLevel = area.maxLevelNight;
                        }
                        else
                        {
                            minLevel = area.minLevel;
                            maxLevel = area.maxLevel;
                        }
                        GenerateLevels(spawnEntry, [.. spawnEntry.SpawnList.ConvertAll(x => (int) x.MinLevel)], [.. spawnEntry.SpawnList.ConvertAll(x => (int) x.MaxLevel)],
                            minLevel, maxLevel, x => LevelMultiplierEx(x, area.isInDungeon));
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
                        WeightsToPercents(diurnalEntries, Math.Max(10 * spawnEntries.Count, 100));
                        WeightsToPercents(nocturnalEntries, Convert.ToInt32(Math.Max(10 * spawnEntries.Count, 100) * (formData.VanillaPlus ? 1.0 : weightNightOnly)));
                        weightSum = spawnEntries.Sum(x => (long) x.Weight);
                    }
                    void WeightsToPercents(List<SpawnEntry> entries, int totalSum)
                    {
                        if (entries.Count == 0)
                        {
                            return;
                        }
                        int[] breakpoints = [ 1, 5, 10, 25, 59 ];
                        int[] minWeights = [ 5, 10, 16, 31, int.MaxValue ];
                        int skippedPercent = 0;
                        int lastPercent = 0;
                        for (int i = 0; i < breakpoints.Length; ++i)
                        {
                            int percent = breakpoints[i];
                            int endIndex = entries.FindIndex(x => x.Weight >= minWeights[i]);
                            if (lastPercent == 0 && endIndex == -1)
                            {
                                lastPercent = percent;
                            }
                            if (endIndex == -1 || endIndex == 0)
                            {
                                if (i == breakpoints.Length - 1)
                                {
                                    endIndex = entries.Count;
                                }
                                else
                                {
                                    skippedPercent += percent;
                                    continue;
                                }
                            }
                            if (i == breakpoints.Length - 1)
                            {
                                percent += skippedPercent;
                                if (lastPercent < breakpoints.Last())
                                {
                                    percent = Math.Min(percent, Convert.ToInt32(lastPercent * (4.0f / 3)));
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
                if (formData.VanillaPlus)
                {
                    vanillaSpawns = Data.AreaData[area.filename].SpawnEntries.ConvertAll(entry => entry.Clone());
                    FilterVanillaSpawns(vanillaSpawns, area);
                    long vanillaWeightSum = vanillaSpawns.Sum(x => (long) x.Weight);
                    long vanillaNightSum = vanillaSpawns.FindAll(x => x.NightOnly).Sum(x => (long) x.Weight);
                    List<SpawnEntry> nightSpawns = spawnEntries.FindAll(x => x.NightOnly);
                    long nightSum = nightSpawns.Sum(x => (long) x.Weight);
                    bool rescaleNight = false;
                    if (nightSpawns.Count != 0)
                    {
                        if (vanillaNightSum != 0)
                        {
                            if (nightSum != weightSum)
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
                                SelectiveScale(weightSum, vanillaWeightSum, vanillaSpawns, spawnEntries);
                            }
                            else
                            {
                                SelectiveScale(weightSum, vanillaNightSum, vanillaSpawns, spawnEntries);
                            }
                        }
                        else
                        {
                            SelectiveScale(weightSum - nightSum, vanillaWeightSum, vanillaSpawns, spawnEntries.FindAll(x => !x.NightOnly));
                            rescaleNight = true;
                        }
                    }
                    else
                    {
                        if (vanillaNightSum != 0)
                        {
                            SelectiveScale(weightSum, vanillaWeightSum - vanillaNightSum, vanillaSpawns, spawnEntries);
                        }
                        else
                        {
                            SelectiveScale(weightSum, vanillaWeightSum, vanillaSpawns, spawnEntries);
                        }
                    }
                    spawnEntries.InsertRange(0, vanillaSpawns);
                    if (rescaleNight)
                    {
                        List<SpawnEntry> newDaySpawns = spawnEntries.FindAll(x => !x.NightOnly);
                        List<SpawnEntry> newNightSpawns = spawnEntries.FindAll(x => x.NightOnly);
                        SelectiveScale(newNightSpawns.Sum(x => (long) x.Weight), newDaySpawns.Sum(x => (long) x.Weight), newDaySpawns, newNightSpawns);
                    }
                    weightSum = spawnEntries.Sum(x => (long) x.Weight);
                    nightOnly = false;
                    void SelectiveScale(long originalSum, long vanillaSum, List<SpawnEntry> vanSpawns, List<SpawnEntry> origSpawns)
                    {
                        double vanillaScale = originalSum * (1 - vanillaPlusChance) / (vanillaSum * vanillaPlusChance);
                        if (vanillaScale >= 1)
                        {
                            MultiplyWeights(vanSpawns, vanillaScale);
                        }
                        else
                        {
                            MultiplyWeights(origSpawns, 1 / vanillaScale);
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
                            catch
                            {
                                spawnEntry.Weight = int.MaxValue;
                            }
                        }
                    }
                }
                // Int Overflow Fix
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
                if (formData.RarityLevelBoost)
                {
                    foreach (SpawnEntry spawnEntry in spawnEntries[vanillaSpawns.Count..])
                    {
                        foreach (SpawnData spawnData in spawnEntry.SpawnList)
                        {
                            if (!spawnData.IsPal)
                            {
                                continue;
                            }
                            int rarity = Rarity(spawnData);
                            if ((rarity == 6 || rarity == 7) && spawnData.MinLevel < rarity67MinLevel && !spawnData.Name.EndsWith("NightFox", StringComparison.InvariantCultureIgnoreCase))
                            {
                                uint levelDiff = (uint) rarity67MinLevel - spawnData.MinLevel;
                                spawnData.MinLevel = Math.Clamp(spawnData.MinLevel + levelDiff, 1, (uint) levelCap);
                                spawnData.MaxLevel = Math.Clamp(spawnData.MaxLevel + levelDiff, 1, (uint) levelCap);
                            }
                            else if (rarity >= 8 && spawnData.MinLevel < rarity8UpMinLevel && !spawnData.Name.EndsWith("PlantSlime_Flower", StringComparison.InvariantCultureIgnoreCase))
                            {
                                uint levelDiff = (uint) rarity8UpMinLevel - spawnData.MinLevel;
                                spawnData.MinLevel = Math.Clamp(spawnData.MinLevel + levelDiff, 1, (uint) levelCap);
                                spawnData.MaxLevel = Math.Clamp(spawnData.MaxLevel + levelDiff, 1, (uint) levelCap);
                            }
                        }
                    }
                }
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
                        bool bossX = x.SpawnList[0].Name.StartsWith("BOSS_", StringComparison.InvariantCultureIgnoreCase);
                        bool bossY = y.SpawnList[0].Name.StartsWith("BOSS_", StringComparison.InvariantCultureIgnoreCase);
                        if (bossX != bossY)
                            return (bossY ? 1 : 0) - (bossX ? 1 : 0);
                        string baseNameX = x.SpawnList[0].BaseName;
                        string baseNameY = y.SpawnList[0].BaseName;
                        if (Data.PalData[baseNameX].ZukanIndex != Data.PalData[baseNameY].ZukanIndex)
                            return Data.PalData[baseNameX].ZukanIndex - Data.PalData[baseNameY].ZukanIndex;
                        if (Data.PalData[baseNameX].ZukanIndexSuffix != Data.PalData[baseNameY].ZukanIndexSuffix)
                            return string.Compare(Data.PalData[baseNameX].ZukanIndexSuffix, Data.PalData[baseNameY].ZukanIndexSuffix);
                    }
                    return string.Compare(x.SpawnList[0].Name, y.SpawnList[0].Name);
                });
                if (formData.VanillaMerge)
                {
                    List<SpawnEntry> mergedVanillaSpawns = Data.AreaData[area.filename].SpawnEntries.ConvertAll(x => x.Clone());
                    FilterVanillaSpawns(mergedVanillaSpawns, area);
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
                WriteAreaAsset(area);
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
            if (formData.OutputLog)
            {
                outputLog.AppendJoin(' ', [totalSpeciesCount, "Total Entries"]);
                outputLog.AppendLine("\n");
                outputLog.AppendLine(JsonConvert.SerializeObject(formData, Formatting.Indented, new JsonSerializerSettings { Converters = [new JsonWriterDecimal()] }));
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
            try
            {
                string date = $"{DateTime.Now:MM-dd-yy-HH-mm-ss}";
                Directory.CreateDirectory(UAssetData.AppDataPath("Log"));
                File.WriteAllText(UAssetData.AppDataPath($"Log\\{date}.csv"), FileModify.GenerateCSV(areaList), Encoding.UTF8);
                File.WriteAllText(UAssetData.AppDataPath($"Log\\{date}.json"),
                    JsonConvert.SerializeObject(formData, Formatting.Indented, new JsonSerializerSettings { Converters = [new JsonWriterDecimal()] }));
            }
            catch
            {
            }
        }
        
        public static bool GeneratePalSpawns(FormData formData)
        {
            GenerateSpawnLists(formData);
            RandomizeAndSaveAssets(formData);
            MainPage.Instance.Dispatcher.Invoke(() => MainPage.Instance.statusBar.Text = "💾 Creating PAK...");
            return FileModify.GenerateAndSavePak();
        }
        public static string GetRandomPal() => Data.PalList[new Random().Next(Data.PalList.Count)];
    }

    public static class FileModify
    {
        public static bool SaveAreaList(List<AreaData> areaList)
        {
            string outputPath = UAssetData.AppDataPath(@"Create-Pak\Pal\Content\Pal\Blueprint\Spawner\SheetsVariant");
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
            string outputPath = UAssetData.AppDataPath(@"Create-Pak\Pal\Content\Pal\Blueprint\Spawner\SheetsVariant");
            if (Directory.GetFiles(outputPath).Length == 0)
            {
                return false;
            }
            File.WriteAllText(UAssetData.AppDataPath("create-pak.txt"), $"\"{UAssetData.AppDataPath("Create-Pak\\*.*")}\" \"..\\..\\..\\*.*\" \n");
            Process unrealPak = Process.Start(new ProcessStartInfo(UAssetData.AppDataPath("UnrealPak.exe"),
                [UAssetData.AppDataPath("SpawnRandomizer_P.pak"), $"-create={UAssetData.AppDataPath("create-pak.txt")}", "-compress"]) { CreateNoWindow = true })!;
            SaveFileDialog saveDialog = new()
            {
                FileName = "SpawnRandomizer_P",
                DefaultExt = ".pak",
                Filter = "PAK File|*.pak"
            };
            if (saveDialog.ShowDialog() == true)
            {
                unrealPak.WaitForExit();
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
                                spawnData.MinLevel, spawnData.MaxLevel, spawnData.MinGroupSize, spawnData.MaxGroupSize]);
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
                    return e.ToString();
                }
            }
            else
                return "Cancel";
            return null;
        }

        public static List<AreaData> ConvertCSV(string filename)
        {
            string[] fileLines = File.ReadAllLines(filename, Encoding.UTF8)[1..];
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
                    IsPal = Data.PalData[palName].IsPal,
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
            return areaList;
        }
    }
}
