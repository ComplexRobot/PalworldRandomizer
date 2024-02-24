using Microsoft.Win32;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows.Controls;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;

namespace PalworldRandomizer
{
    public static class Data
    {
        public static Dictionary<string, CharacterData> palData { get; private set; } = [];
        public static Dictionary<string, string> palName { get; private set; } = [];
        public static Dictionary<string, string> simpleName { get; private set; } = [];
        public static Dictionary<string, string> palIcon { get; private set; } = [];
        public static List<string> palList { get; private set; } = [];
        public static Dictionary<string, string> bossName { get; private set; } = [];
        public static List<string> towerBossNames { get; private set; } = [];
        public static Dictionary<string, List<SpawnEntry>> soloEntries { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);
        public static Dictionary<string, List<SpawnEntry>> bossEntries { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);
        public static List<SpawnEntry> groupEntries { get; private set; } = [];
        public static Dictionary<string, AreaData> areaData { get; private set; } = [];
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

        public static void Initialize()
        {
            palData = UAssetData.CreatePalData();
            UAsset palNames = UAssetData.LoadAsset("Data\\DT_PalNameText.uasset");
            UAsset humanNames = UAssetData.LoadAsset("Data\\DT_HumanNameText.uasset");
            ResourceManager resourceManager = new(Assembly.GetExecutingAssembly().GetName().Name + ".g", Assembly.GetExecutingAssembly());
            HashSet<string> resourceNames = new(resourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true)!.Cast<DictionaryEntry>()
                .Select(x => (string) x.Key), StringComparer.InvariantCultureIgnoreCase);
            resourceManager.ReleaseAllResources();
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
            foreach (KeyValuePair<string, CharacterData> keyPair in palData)
            {
                if (keyPair.Value.IsPal)
                {
                    palName.Add(keyPair.Key,
                        ((TextPropertyData) ((DataTableExport) palNames.Exports[0]).Table.Data.Find(property =>
                        (palData[keyPair.Key].OverrideNameTextID != "" &&
                        string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"{palData[keyPair.Key].OverrideNameTextID}_TextData", true) == 0)
                        || string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"PAL_NAME_{keyPair.Key}_TextData", true) == 0)!.Value[0])
                        .CultureInvariantString.Value.Trim());
                    bool isTowerBoss = keyPair.Key.StartsWith("GYM_", StringComparison.InvariantCultureIgnoreCase);
                    bool isBoss = keyPair.Key.StartsWith("BOSS_", StringComparison.InvariantCultureIgnoreCase) || isTowerBoss;
                    if (keyPair.Value.ZukanIndex > 0)
                    {
                        palList.Add(keyPair.Key);
                        bossName.Add(keyPair.Key, palData.Keys.ToList().Find(key => string.Compare(key, $"BOSS_{keyPair.Key}", true) == 0)!);
                        simpleName.Add(new SpawnData(keyPair.Key).simpleName, keyPair.Key);
                    }
                    if (isTowerBoss)
                    {
                        towerBossNames.Add(keyPair.Key);
                        simpleName.Add(new SpawnData(keyPair.Key).simpleName, keyPair.Key);
                    }
                    if (keyPair.Value.ZukanIndex > 0 || isBoss)
                    {
                        string resourceKey = keyPair.Key.EndsWith("_Flower") ? keyPair.Key[..keyPair.Key.LastIndexOf('_')] : keyPair.Key;
                        resourceKey = isBoss ? resourceKey[(resourceKey.IndexOf('_') + 1)..] : resourceKey;
                        string resourceName = $"Resources/Images/PalIcon/T_{resourceKey}_icon_normal.png";
                        if (palData[resourceKey].ZukanIndex > 0 && resourceNames.Contains(resourceName))
                        {
                            palIcon.Add(keyPair.Key, $"/{resourceName}");
                        }
                    }
                }
                else
                {
                    StructPropertyData? property = ((DataTableExport) humanNames.Exports[0]).Table.Data.Find(property =>
                        ((TextPropertyData) property.Value[0]).Value.Value == $"{palData[keyPair.Key].OverrideNameTextID}_TextData");
                    if (property != null)
                    {
                        palName.Add(keyPair.Key,
                        ((TextPropertyData) property.Value[0]).CultureInvariantString.Value.Trim());
                        simpleName.Add(new SpawnData(keyPair.Key) { isPal = false }.simpleName, keyPair.Key);
                        if (weapons.TryGetValue(palData[keyPair.Key].weapon, out string? value))
                        {
                            palIcon.Add(keyPair.Key, value);
                        }
                        else
                        {
                            palIcon.Add(keyPair.Key, $"/Resources/Images/PalIcon/T_CommonHuman_icon_normal.png");
                        }
                    }
                }
            }
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
                    if (spawnEntry.spawnList.Count == 1 && !spawnEntry.spawnList[0].name.StartsWith("boss_", true, null))
                    {
                        if (!soloEntries.TryGetValue(spawnEntry.spawnList[0].name, out List<SpawnEntry>? value))
                        {
                            value = [];
                            soloEntries.Add(spawnEntry.spawnList[0].name, value);
                        }
                        value.Add(spawnEntry);
                    }
                    else if (spawnEntry.spawnList[0].name.StartsWith("boss_", true, null))
                    {
                        if (!bossEntries.TryGetValue(spawnEntry.spawnList[0].name, out List<SpawnEntry>? value))
                        {
                            value = [];
                            bossEntries.Add(spawnEntry.spawnList[0].name, value);
                        }
                        value.Add(spawnEntry);
                    }
                    else
                    {
                        groupEntries.Add(spawnEntry);
                    }
                    int weightUsed = (spawnEntry.weight == 0 ? 1 : spawnEntry.weight);
                    if (spawnEntry.nightOnly)
                    {
                        nightAverageLevel += (float) (spawnEntry.spawnList[0].minLevel + spawnEntry.spawnList[0].maxLevel) * weightUsed / 2.0f;
                        nightLevelRange += (float) (spawnEntry.spawnList[0].maxLevel - spawnEntry.spawnList[0].minLevel) * weightUsed;
                        nightWeightSum += weightUsed;
                    }
                    else
                    {
                        averageLevel += (float) (spawnEntry.spawnList[0].minLevel + spawnEntry.spawnList[0].maxLevel) * weightUsed / 2.0f;
                        levelRange += (float) (spawnEntry.spawnList[0].maxLevel - spawnEntry.spawnList[0].minLevel) * weightUsed;
                        weightSum += weightUsed;
                    }
                }
                areaData.Add(filename, new(uAsset, spawnExportData, filename));
                areaData[filename].minLevel = (int) Math.Round(averageLevel / weightSum - levelRange / 2.0f / weightSum);
                areaData[filename].maxLevel = (int) Math.Round(averageLevel / weightSum + levelRange / 2.0f / weightSum);
                if (nightWeightSum != 0)
                {
                    areaData[filename].minLevelNight = (int) Math.Round(nightAverageLevel / nightWeightSum - nightLevelRange / 2.0f / nightWeightSum);
                    areaData[filename].maxLevelNight = (int) Math.Round(nightAverageLevel / nightWeightSum + nightLevelRange / 2.0f / nightWeightSum);
                }
            }
            areaData["BP_PalSpawner_Sheets_1_1_plain_begginer.uasset"].minLevel = 1;
            areaData["BP_PalSpawner_Sheets_1_1_plain_begginer.uasset"].maxLevel = 3;
        }
        public static List<AreaData> AreaDataCopy()
        {
            return [..areaData.Values.Select(area =>
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
                    spawnEntries = area.spawnExportData.spawnEntries.ConvertAll(entry =>
                    new SpawnEntry
                    {
                        weight = entry.weight,
                        nightOnly = entry.nightOnly,
                        spawnList = entry.spawnList.ConvertAll(spawnData =>
                        new SpawnData
                        {
                            name = spawnData.name,
                            isPal = spawnData.isPal,
                            minLevel = spawnData.minLevel,
                            maxLevel = spawnData.maxLevel,
                            minGroupSize = spawnData.minGroupSize,
                            maxGroupSize = spawnData.maxGroupSize
                        })
                    })
                }
            })];
        }
    }

    public static class Randomize
    {
        public static List<AreaData> generatedAreaList { get; private set; } = [];
        private static Dictionary<string, SpawnEntry> basicSpawns = [];
        private static Dictionary<string, SpawnEntry> bossSpawns = [];
        private static List<SpawnEntry> humanSpawns = [];

        public static void Initialize()
        {
            generatedAreaList = Data.AreaDataCopy();
        }
        private static void GenerateSpawnLists()
        {
            MainWindow window = MainWindow.Instance;
            basicSpawns = [];
            bossSpawns = [];
            humanSpawns = [];
            if (window.groupVanilla.IsChecked == true)
            {
                foreach (string key in Data.palList)
                {
                    if (window.spawnPals.IsChecked == true)
                    {
                        SpawnEntry basicEntry = new();
                        basicSpawns.Add(key, basicEntry);
                        if (Data.soloEntries.TryGetValue(key, out List<SpawnEntry>? soloValue))
                        {
                            SpawnData spawnData = new() { name = key };
                            basicEntry.spawnList.Add(spawnData);
                            int minCount = 1;
                            int maxCount = 1;
                            foreach (SpawnEntry spawnEntry in soloValue)
                            {
                                minCount = (int) Math.Max(minCount, spawnEntry.spawnList[0].minGroupSize);
                                maxCount = (int) Math.Max(maxCount, spawnEntry.spawnList[0].maxGroupSize);
                            }
                            spawnData.minGroupSize = (uint) minCount;
                            spawnData.maxGroupSize = (uint) maxCount;
                        }
                        else if (Data.groupEntries.Exists(entry => entry.spawnList.Exists(spawnData => spawnData.name == key)))
                        {
                            foreach (SpawnEntry spawnEntry in Data.groupEntries)
                            {
                                int palCount = 0;
                                if (spawnEntry.spawnList[0].name == key)
                                {
                                    int currentCount = 0;
                                    foreach (SpawnData currentData in spawnEntry.spawnList)
                                    {
                                        currentCount += (int) currentData.minGroupSize + (int) currentData.maxGroupSize;
                                    }
                                    if (currentCount > palCount)
                                    {
                                        basicEntry.spawnList = spawnEntry.spawnList; // shallow copy
                                        palCount = currentCount;
                                    }
                                }
                            }
                        }
                        else
                        {
                            SpawnData spawnData = new() { name = key };
                            basicEntry.spawnList.Add(spawnData);
                            if (key.Contains('_'))
                            {
                                string baseName = key[..key.IndexOf('_')];
                                if (Data.soloEntries.TryGetValue(baseName, out List<SpawnEntry>? soloValue2))
                                {
                                    spawnData.minGroupSize = soloValue2[0].spawnList[0].minGroupSize;
                                    spawnData.maxGroupSize = soloValue2[0].spawnList[0].maxGroupSize;
                                }
                            }
                        }
                    }
                    if (key == "BlackCentaur")
                    {
                        continue;
                    }
                    string bossKey = Data.bossName[key];
                    SpawnEntry bossEntry = new();
                    bossSpawns.Add(bossKey, bossEntry);
                    if (Data.bossEntries.TryGetValue(bossKey, out List<SpawnEntry>? value))
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
                                foreach (SpawnData currentData in spawnEntry.spawnList)
                                {
                                    currentCount += (int) currentData.minGroupSize + (int) currentData.maxGroupSize;
                                }
                                if (currentCount > palCount)
                                {
                                    bossEntry.spawnList = spawnEntry.spawnList; // shallow copy
                                    palCount = currentCount;
                                }
                            }
                        }
                    }
                    else
                    {
                        SpawnData bossData = new() { name = bossKey };
                        bossEntry.spawnList.Add(bossData);
                        if (bossKey["BOSS_".Length..].Contains('_'))
                        {
                            SpawnEntry baseEntry = bossSpawns[bossKey[..bossKey.LastIndexOf('_')]];
                            if (baseEntry.spawnList.Count > 1)
                            {
                                bossEntry.spawnList.Add(new()
                                {
                                    name = key,
                                    minLevel = baseEntry.spawnList[1].minLevel,
                                    maxLevel = baseEntry.spawnList[1].maxLevel,
                                    minGroupSize = baseEntry.spawnList[1].minGroupSize,
                                    maxGroupSize = baseEntry.spawnList[1].maxGroupSize
                                });
                                bossData.minLevel = baseEntry.spawnList[0].minLevel;
                                bossData.maxLevel = baseEntry.spawnList[0].maxLevel;
                            }
                        }
                        if (bossEntry.spawnList.Count == 1 && basicSpawns[key].spawnList[0].maxGroupSize > 1)
                        {
                            bossEntry.spawnList.Add(new()
                            {
                                name = key,
                                minGroupSize = Math.Max(2, basicSpawns[key].spawnList[0].minGroupSize),
                                maxGroupSize = basicSpawns[key].spawnList[0].maxGroupSize,
                                minLevel = 2,
                                maxLevel = 6
                            });
                            bossData.minLevel = 4;
                            bossData.maxLevel = 8;
                        }
                    }
                }
                foreach (KeyValuePair<string, List<SpawnEntry>> keyPair in Data.soloEntries)
                {
                    if (!Data.palData[keyPair.Key].IsPal &&
                        ((window.spawnHumans.IsChecked == true && Data.humanNames.Contains(keyPair.Key))
                        || (window.spawnPolice.IsChecked == true && Data.policeNames.Contains(keyPair.Key)))
                        )
                    {
                        SpawnData spawnData = new() { name = keyPair.Key, isPal = false };
                        humanSpawns.Add(new() { spawnList = [spawnData] });
                        int minCount = 1;
                        int maxCount = 1;
                        foreach (SpawnEntry spawnEntry in keyPair.Value)
                        {
                            spawnData.minGroupSize = (uint) Math.Max(minCount, spawnEntry.spawnList[0].minGroupSize);
                            spawnData.maxGroupSize = (uint) Math.Max(maxCount, spawnEntry.spawnList[0].maxGroupSize);
                        }
                    }
                }
                List<SpawnEntry> groupEntriesCopy = [.. Data.groupEntries];
                for (int i = 0; i < groupEntriesCopy.Count; ++i)
                {
                    SpawnEntry spawnEntry = groupEntriesCopy[i];
                    if (!spawnEntry.spawnList[0].isPal &&
                        ((window.spawnHumans.IsChecked == true && Data.humanNames.Contains(spawnEntry.spawnList[0].name))
                        || (window.spawnPolice.IsChecked == true && Data.policeNames.Contains(spawnEntry.spawnList[0].name)))
                        )
                    {
                        humanSpawns.Add(spawnEntry); // shallow copy
                        int humanCount = 0;
                        foreach (SpawnData spawnData in spawnEntry.spawnList)
                        {
                            humanCount += (int) (spawnData.minGroupSize + spawnData.maxGroupSize);
                        }
                        for (int j = i + 1; j < groupEntriesCopy.Count;)
                        {
                            SpawnEntry spawnEntry2 = groupEntriesCopy[j];
                            if (spawnEntry2.spawnList.Count != spawnEntry.spawnList.Count)
                            {
                                ++j;
                                continue;
                            }
                            int currentCount = 0;
                            if (((Func<bool>) (() =>
                            {
                                for (int k = 0; k < spawnEntry.spawnList.Count; ++k)
                                {
                                    if (spawnEntry.spawnList[k].name != spawnEntry2.spawnList[k].name)
                                    {
                                        return false;
                                    }
                                    currentCount += (int) (spawnEntry2.spawnList[k].minGroupSize + spawnEntry2.spawnList[k].maxGroupSize);
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
                if (window.spawnPolice.IsChecked == true)
                {
                    humanSpawns.Add(new() { spawnList = [new("Police_Handgun", 1, 2)] });
                }
                if (window.spawnGuards.IsChecked == true)
                {
                    humanSpawns.Add(new() { spawnList = [new("Guard_Rifle", 1, 2), new("Guard_Shotgun", 1, 2)] });
                }
                if (window.spawnHumans.IsChecked == true)
                {
                    humanSpawns.Add(new() { spawnList = [new("Hunter_FlameThrower", 1, 2)] });
                    humanSpawns.Add(new() { spawnList = [new("Scientist_FlameThrower", 1, 2)] });
                }
                if (window.spawnTraders.IsChecked == true)
                {
                    humanSpawns.AddRange(Data.traderNames.ToList().ConvertAll(name => new SpawnEntry { spawnList = [new(name)] }));
                }
                if (window.spawnPalTraders.IsChecked == true)
                {
                    humanSpawns.AddRange(Data.palTraderNames.ToList().ConvertAll(name => new SpawnEntry { spawnList = [new(name)] }));
                }
            }
            else if (window.groupRandom.IsChecked == true)
            {
                if (window.spawnPals.IsChecked == true)
                {
                    Data.palList.ForEach(name => basicSpawns.Add(name, new() { spawnList = [new(name)] }));
                }
                Data.bossName.Values.ToList().ForEach(name => bossSpawns.Add(name, new() { spawnList = [new(name)] }));
                humanSpawns.AddRange(new List<string>
                ([
                    .. (window.spawnHumans.IsChecked == true ? Data.humanNames : []),
                    .. (window.spawnPolice.IsChecked == true ? Data.policeNames : []),
                    .. (window.spawnGuards.IsChecked == true ? Data.guardNames : []),
                    .. (window.spawnTraders.IsChecked == true ? Data.traderNames : []),
                    .. (window.spawnPalTraders.IsChecked == true ? Data.palTraderNames : [])
                ]).ConvertAll(name => new SpawnEntry { spawnList = [new(name)] }));
            }
            if (window.spawnTowerBosses.IsChecked == true)
            {
                Data.towerBossNames.ForEach(name => bossSpawns.Add(name, new() { spawnList = [new(name)] }));
            }
        }
        private static ICollection<string> GetAllowedNames()
        {
            MainWindow window = MainWindow.Instance;
            return
            [
                .. (window.spawnPals.IsChecked == true ? Data.palList : []),
                .. Data.bossName.Values,
                .. (window.spawnTowerBosses.IsChecked == true ? Data.towerBossNames : []),
                .. (window.spawnHumans.IsChecked == true ? Data.humanNames : []),
                .. (window.spawnPolice.IsChecked == true ? Data.policeNames : []),
                .. (window.spawnGuards.IsChecked == true ? Data.guardNames : []),
                .. (window.spawnTraders.IsChecked == true ? Data.traderNames : []),
                .. (window.spawnPalTraders.IsChecked == true ? Data.palTraderNames : [])
            ];
        }
        private static void RandomizeAndSaveAssets()
        {
            MainWindow window = MainWindow.Instance;
            string outputLog = "";
            static void ValidateNumericText(TextBox textBox, int minValue, int? defaultValue = null)
            {
                defaultValue ??= minValue;
                try
                {
                    if (textBox.Text.Length == 0)
                    {
                        textBox.Text = $"{defaultValue}";
                    }
                    else if (int.Parse(textBox.Text) < minValue)
                    {
                        textBox.Text = $"{minValue}";
                    }
                }
                catch (Exception)
                {
                    textBox.Text = $"{defaultValue}";
                }
            }
            ValidateNumericText(window.groupMin, 1);
            ValidateNumericText(window.groupMax, int.Parse(window.groupMin.Text));
            ValidateNumericText(window.spawnListSize, 1);
            ValidateNumericText(window.fieldLevel, 0, 100);
            ValidateNumericText(window.dungeonLevel, 0, 100);
            ValidateNumericText(window.fieldBossLevel, 0, 100);
            ValidateNumericText(window.dungeonBossLevel, 0, 100);
            ValidateNumericText(window.levelCap, 1, 50);
            int minGroup = Math.Max(1, int.Parse(window.groupMin.Text));
            int maxGroup = Math.Max(minGroup, int.Parse(window.groupMax.Text));
            int spawnListSize = Math.Max(1, int.Parse(window.spawnListSize.Text));
            float fieldLevel = Math.Max(0, int.Parse(window.fieldLevel.Text)) / 100.0f;
            float dungeonLevel = Math.Max(0, int.Parse(window.dungeonLevel.Text)) / 100.0f;
            float fieldBossLevel = Math.Max(0, int.Parse(window.fieldBossLevel.Text)) / 100.0f;
            float dungeonBossLevel = Math.Max(0, int.Parse(window.dungeonBossLevel.Text)) / 100.0f;
            int levelCap = Math.Max(1, int.Parse(window.levelCap.Text));
            int totalSpeciesCount = 0;
            string outputPath = UAssetData.AppDataPath("SpawnRandomizer_P\\Pal\\Content\\Pal\\Blueprint\\Spawner\\SheetsVariant");
            Directory.CreateDirectory(outputPath);
            foreach (string filename in Directory.GetFiles(outputPath))
            {
                File.Delete(filename);
            }
            Dictionary<string, SpawnEntry> swapMap = [];
            List<SpawnEntry> basicSpawnsOriginal = [.. basicSpawns.Values, .. humanSpawns];
            List<SpawnEntry> bossSpawnsOriginal = [.. bossSpawns.Values];
            List<SpawnEntry> basicSpawnsCurrent = [.. basicSpawnsOriginal];
            List<SpawnEntry> bossSpawnsCurrent = [.. bossSpawnsOriginal];
            HashSet<string> allowedNames = [.. GetAllowedNames()];
            Random random = new();
            List<AreaData> areaList = Data.AreaDataCopy();
            areaList.Sort((x, y) =>
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
            foreach (AreaData area in areaList)
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
                if ((window.randomizeField.IsChecked != true && isField)
                    || (window.randomizeDungeons.IsChecked != true && isDungeon)
                    || (window.randomizeDungeonBosses.IsChecked != true && isDungeonBoss)
                    || (window.randomizeFieldBosses.IsChecked != true && isFieldBoss))
                {
                    continue;
                }
                bool nightOnly = (window.nightOnly.IsChecked == true && isField)
                    || (window.nightOnlyDungeons.IsChecked == true && isDungeon)
                    || (window.nightOnlyDungeonBosses.IsChecked == true && isDungeonBoss)
                    || (window.nightOnlyBosses.IsChecked == true && isFieldBoss);
                int changes = 1;
                float LevelMultiplier(SpawnData spawnData)
                {
                    if (isDungeon || isDungeonBoss)
                    {
                        return spawnData.isBoss ? dungeonBossLevel : dungeonLevel;
                    }
                    return spawnData.isBoss ? fieldBossLevel : fieldLevel;
                }
                if (window.methodNone.IsChecked != true)
                {
                    int speciesCount = 0;
                    List<SpawnEntry> spawnEntries = [];
                    List<SpawnEntry> spawnEntriesOriginal = area.spawnExportData.spawnEntries;
                    area.spawnExportData.spawnEntries = spawnEntries;
                    void AddEntry(SpawnEntry value)
                    {
                        SpawnEntry spawnEntry = new()
                        {
                            weight = 10,
                            spawnList = value.spawnList.ConvertAll(spawnData =>
                                new SpawnData(spawnData.name, spawnData.minGroupSize, spawnData.maxGroupSize) { isPal = Data.palData[spawnData.name].IsPal })
                        };
                        spawnEntries.Add(spawnEntry);
                        int minLevel = area.minLevel;
                        int maxLevel = area.maxLevel;
                        if (Data.palData[spawnEntry.spawnList[0].name].Nocturnal && nightOnly)
                        {
                            spawnEntry.nightOnly = true;
                            spawnEntry.weight = 1000;
                            if (area.minLevelNight != 0)
                            {
                                minLevel = area.minLevelNight;
                                maxLevel = area.maxLevelNight;
                            }
                        }
                        float range = maxLevel - minLevel;
                        float average = (maxLevel + minLevel) / 2.0f;
                        float levelMultiplier = LevelMultiplier(spawnEntry.spawnList[0]);
                        spawnEntry.spawnList[0].minLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average - range / 2.0f), 1, levelCap);
                        spawnEntry.spawnList[0].maxLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average + range / 2.0f), 1, levelCap);
                        if (!Data.palData[spawnEntry.spawnList[0].name].IsPal && Data.palData[spawnEntry.spawnList[0].name].AIResponse == "Kill_All" && window.groupVanilla.IsChecked == true)
                        {
                            spawnEntry.weight = 5;
                        }
                        if (spawnEntry.spawnList.Count > 1)
                        {
                            float firstRange = value.spawnList[0].maxLevel - value.spawnList[0].minLevel;
                            float firstAverage = (value.spawnList[0].maxLevel + value.spawnList[0].minLevel) / 2.0f;
                            for (int i = 1; i < spawnEntry.spawnList.Count; ++i)
                            {
                                float currentRange = value.spawnList[i].maxLevel - value.spawnList[i].minLevel;
                                float currentAverage = (value.spawnList[i].maxLevel + value.spawnList[i].minLevel) / 2.0f;
                                float newRange = (firstRange == 0 ? currentRange : range * currentRange / firstRange);
                                float newAverage = LevelMultiplier(spawnEntry.spawnList[i]) * average * currentAverage / firstAverage;
                                spawnEntry.spawnList[i].minLevel = (uint) Math.Clamp((int) Math.Round(newAverage - newRange / 2.0f), 1, levelCap);
                                spawnEntry.spawnList[i].maxLevel = (uint) Math.Clamp((int) Math.Round(newAverage + newRange / 2.0f), 1, levelCap);
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
                                    spawns.RemoveAll(entry => currentSpawns.Exists(spawnData => entry.spawnList[0].name == spawnData.name));
                                }
                            }
                            int i = random.Next(0, spawns.Count);
                            string name = spawns[i].spawnList[0].name;
                            spawns.RemoveAt(i);
                            return new(name) { isPal = Data.palData[name].IsPal };
                        }
                        int groupSize = random.Next(minGroup, maxGroup + 1);
                        SpawnEntry spawnEntry = new();
                        List<SpawnEntry> spawns = basicSpawnsCurrent;
                        List<SpawnEntry> original = basicSpawnsOriginal;
                        if (isBoss)
                        {
                            spawnEntry.spawnList.Add(NextSpecies(bossSpawnsCurrent, bossSpawnsOriginal));
                            if (window.multiBoss.IsChecked == true)
                            {
                                spawns = bossSpawnsCurrent;
                                original = bossSpawnsOriginal;
                            }
                        }
                        else
                        {
                            spawnEntry.spawnList.Add(NextSpecies(basicSpawnsCurrent, basicSpawnsOriginal));
                        }
                        if (original.Count != 0)
                        {
                            if (nightOnly)
                            {
                                List<SpawnEntry> diurnal = [.. spawns.Where(entry => !Data.palData[entry.spawnList[0].name].Nocturnal)];
                                List<SpawnEntry> nocturnal = [.. spawns.Where(entry => Data.palData[entry.spawnList[0].name].Nocturnal)];
                                List<SpawnEntry> diurnalOriginal = [.. original.Where(entry => !Data.palData[entry.spawnList[0].name].Nocturnal)];
                                List<SpawnEntry> nocturnalOriginal = [.. original.Where(entry => Data.palData[entry.spawnList[0].name].Nocturnal)];
                                List<SpawnEntry> spawnsUsed = Data.palData[spawnEntry.spawnList[0].name].Nocturnal ? nocturnal : diurnal;
                                List<SpawnEntry> originalsUsed = Data.palData[spawnEntry.spawnList[0].name].Nocturnal ? nocturnalOriginal : diurnalOriginal;
                                while (spawnEntry.spawnList.Count < groupSize)
                                {
                                    spawnEntry.spawnList.Add(NextSpecies(spawnsUsed, originalsUsed, spawnEntry.spawnList));
                                }
                                if (spawns.Count == 0)
                                    spawns.AddRange(original);
                                spawns.RemoveAll(entry => spawnEntry.spawnList.Exists(spawnData => entry.spawnList[0].name == spawnData.name));
                            }
                            else
                            {
                                while (spawnEntry.spawnList.Count < groupSize)
                                {
                                    spawnEntry.spawnList.Add(NextSpecies(spawns, original, spawnEntry.spawnList));
                                }
                            }
                        }
                        if (isBoss && spawnEntry.spawnList.Count > 1 && window.multiBoss.IsChecked != true)
                        {
                            spawnEntry.spawnList[0].minLevel = 4;
                            spawnEntry.spawnList[0].maxLevel = 8;
                            spawnEntry.spawnList[1..].ForEach(spawnData => { spawnData.minLevel = 2; spawnData.maxLevel = 6; });
                        }
                        return spawnEntry;
                    }
                    if (window.methodFull.IsChecked == true)
                    {
                        basicSpawnsCurrent = [.. basicSpawnsOriginal];
                        bossSpawnsCurrent = [.. bossSpawnsOriginal];
                        int maxSpecies = isFieldBoss && window.fieldBossExtended.IsChecked != true
                            ? 1
                            : Enumerable.Sum((isBoss ? bossSpawnsOriginal : basicSpawnsOriginal).ConvertAll(entry => entry.spawnList.Count));
                        if (window.groupVanilla.IsChecked == true)
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
                                speciesCount += spawns[i].spawnList.Count;
                                spawns.RemoveAt(i);
                            }
                        }
                        else if (window.groupRandom.IsChecked == true)
                        {
                            if (isBoss && window.multiBoss.IsChecked != true)
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
                                    spawnEntry.spawnList.ForEach(entry => speciesCount += speciesAdded.Add(entry.name) ? 1 : 0);
                                }
                            }
                        }
                    }
                    else
                    {
                        int entryCount = isFieldBoss && window.fieldBossExtended.IsChecked != true
                            ? 1
                            : (window.methodCustomSize.IsChecked == true ? spawnListSize : spawnEntriesOriginal.Count);
                        for (int i = 0; i < entryCount; ++i)
                        {
                            if (window.methodGlobalSwap.IsChecked == true)
                            {
                                SpawnEntry spawnEntry = spawnEntriesOriginal[i];
                                if (!swapMap.ContainsKey(spawnEntry.spawnList[0].name))
                                {
                                    if (window.groupVanilla.IsChecked == true)
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
                                        swapMap.Add(spawnEntry.spawnList[0].name, newValue);
                                    }
                                    else if (window.groupRandom.IsChecked == true)
                                    {
                                        swapMap.Add(spawnEntry.spawnList[0].name, GetRandomGroup());
                                    }
                                }
                                AddEntry(swapMap[spawnEntry.spawnList[0].name]);
                            }
                            else
                            {
                                if (window.groupVanilla.IsChecked == true)
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
                                else if (window.groupRandom.IsChecked == true)
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
                    foreach (SpawnEntry spawnEntry in area.spawnExportData.spawnEntries)
                    {
                        changes += spawnEntry.spawnList.RemoveAll(spawnData => !allowedNames.Contains(spawnData.name));
                    }
                    changes += area.spawnExportData.spawnEntries.RemoveAll(entry => entry.spawnList.Count == 0);
                    foreach (SpawnEntry spawnEntry in area.spawnExportData.spawnEntries)
                    {
                        if ((window.nightOnly.IsChecked != true && isField)
                            || (window.nightOnlyDungeons.IsChecked != true && isDungeon)
                            || (window.nightOnlyDungeonBosses.IsChecked != true && isDungeonBoss)
                            || (window.nightOnlyBosses.IsChecked != true && isFieldBoss))
                        {
                            changes += spawnEntry.nightOnly == true ? 1 : 0;
                            spawnEntry.nightOnly = false;
                        }
                        spawnEntry.spawnList.ForEach(spawnData =>
                        {
                            uint originalMin = spawnData.minLevel;
                            uint originalMax = spawnData.maxLevel;
                            float range = spawnData.maxLevel - spawnData.minLevel;
                            float average = (spawnData.maxLevel + spawnData.minLevel) / 2.0f;
                            float levelMultiplier = LevelMultiplier(spawnData);
                            spawnData.minLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average - range / 2.0f), 1, levelCap);
                            spawnData.maxLevel = (uint) Math.Clamp((int) Math.Round(levelMultiplier * average + range / 2.0f), 1, levelCap);
                            changes += (spawnData.minLevel != originalMin ? 1 : 0) + (spawnData.maxLevel != originalMax ? 1 : 0);
                        });
                    }
                }
                if (window.methodNone.IsChecked != true)
                {
                    area.spawnExportData.spawnEntries.Sort((x, y) => (x.nightOnly ? 1 : 0) - (y.nightOnly ? 1 : 0));
                }
                string areaName = Path.GetFileNameWithoutExtension(area.filename)["BP_PalSpawner_Sheets_".Length..];
                outputLog += areaName + '\n';
                Console.WriteLine(areaName);
                area.spawnExportData.spawnEntries.ForEach(entry => { outputLog += entry.Print(); totalSpeciesCount += entry.spawnList.Count; });
                Console.WriteLine();
                outputLog += '\n';
                if (changes == 0)
                    continue;
                area.modified = true;
                PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                area.uAsset.Write($"{outputPath}\\{area.filename}");
            }
            Console.WriteLine($"{totalSpeciesCount} Total Entries\n");
            outputLog += $"{totalSpeciesCount} Total Entries\n\n";
            areaList.Sort((x, y) => string.Compare(x.filename, y.filename));
            generatedAreaList = areaList;
            PalSpawnWindow.Instance.areaList.ItemsSource = generatedAreaList;
            if (window.outputLog.IsChecked == true)
            {
                try
                {
                    File.WriteAllText($"{Assembly.GetExecutingAssembly().GetName().Name!.Replace(' ', '-')}-Log.txt", outputLog);
                }
                catch (Exception)
                {
                }
            }
        }
        public static void AreaForEachIfDiff(List<AreaData> areaList, Action<AreaData> func)
        {
            foreach (AreaData area in areaList)
            {
                List<SpawnEntry> baseEntries = Data.areaData[area.filename].spawnExportData.spawnEntries;
                List<SpawnEntry> newEntries = area.spawnExportData.spawnEntries;
                if (baseEntries.Count != newEntries.Count
                    || ((Func<bool>) (() =>
                    {
                        for (int i = 0; i < baseEntries.Count; ++i)
                        {
                            List<SpawnData> baseList = baseEntries[i].spawnList;
                            List<SpawnData> newList = newEntries[i].spawnList;
                            if (baseList.Count != newList.Count || baseEntries[i].weight != newEntries[i].weight || baseEntries[i].nightOnly != newEntries[i].nightOnly)
                            {
                                return true;
                            }
                            if (((Func<bool>) (() =>
                            {
                                for (int j = 0; j < baseList.Count; ++j)
                                {
                                    if (baseList[j].name != newList[j].name
                                        || baseList[j].isPal != newList[j].isPal
                                        || baseList[j].minLevel != newList[j].minLevel
                                        || baseList[j].maxLevel != newList[j].maxLevel
                                        || baseList[j].minGroupSize != newList[j].minGroupSize
                                        || baseList[j].maxGroupSize != newList[j].maxGroupSize)
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
                    func(area);
                }
            }
        }
        public static bool SaveAreaList(List<AreaData> areaList)
        {
            string outputPath = UAssetData.AppDataPath("SpawnRandomizer_P\\Pal\\Content\\Pal\\Blueprint\\Spawner\\SheetsVariant");
            Directory.CreateDirectory(outputPath);
            foreach (string filename in Directory.GetFiles(outputPath))
            {
                File.Delete(filename);
            }
            bool changesDetected = false;
            AreaForEachIfDiff(areaList, area =>
            { 
                changesDetected = true;
                PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                area.uAsset.Write($"{outputPath}\\{area.filename}");
            });
            return changesDetected;
        }
        public static bool GenerateAndSavePak()
        {
            string outputPath = UAssetData.AppDataPath("SpawnRandomizer_P\\Pal\\Content\\Pal\\Blueprint\\Spawner\\SheetsVariant");
            if (Directory.GetFiles(outputPath).Length == 0)
            {
                return false;
            }
            File.WriteAllText(UAssetData.AppDataPath("filelist.txt"), $"\"{UAssetData.AppDataPath("SpawnRandomizer_P\\*.*")}\" \"..\\..\\..\\*.*\" \n");
            Process unrealPak = Process.Start(UAssetData.AppDataPath("UnrealPak.exe"),
                $"\"{UAssetData.AppDataPath("SpawnRandomizer_P.pak")}\" \"-create={UAssetData.AppDataPath("filelist.txt")}\" -compress");
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
                string outputPath = UAssetData.AppDataPath("Extract");
                Directory.CreateDirectory(outputPath);
                foreach (string filename in Directory.GetFiles(outputPath))
                {
                    File.Delete(filename);
                }
                Process unrealPak = Process.Start(UAssetData.AppDataPath("UnrealPak.exe"),
                    $"\"{openDialog.FileName}\" -extract \"{outputPath}\"");
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
                        area.uAsset = UAssetData.LoadAsset($"Extract\\{area.filename}");
                        area.spawnExportData = PalSpawn.ReadAsset(area.uAsset, Data.areaData[area.filename].spawnExportData.header.Length);
                        area.modified = true;
                    }
                }
                PalSpawnWindow.Instance.areaList.ItemsSource = areaList;
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
                string outputText = "Area Name,Group Index,Weight,Night Only,Character Name,Is Boss,Min. Level,Max. Level,Min. Count,Max. Count\n";
                foreach (AreaData area in areaList)
                {
                    if (area.spawnExportData.spawnEntries.Count == 0)
                    {
                        outputText += area.simpleName + new string(',', 9) + '\n';
                        continue;
                    }
                    for (int i = 0; i < area.spawnExportData.spawnEntries.Count; ++i)
                    {
                        SpawnEntry entry = area.spawnExportData.spawnEntries[i];
                        for (int j = 0; j < entry.spawnList.Count; ++j)
                        {
                            SpawnData spawnData = entry.spawnList[j];
                            outputText += $"{area.simpleName},{i},{(j == 0 ? entry.weight : "")},{(j == 0 ? entry.nightOnly : "")},{spawnData.simpleName},{spawnData.isBoss},"
                                + $"{spawnData.minLevel},{spawnData.maxLevel},{spawnData.minGroupSize},{spawnData.maxGroupSize}\n";
                        }
                    }
                }
                File.WriteAllText(saveDialog.FileName, outputText/*[..^1]*/, Encoding.UTF8);
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
                    List<string[]> csvData = [.. fileLines.Select(x => x.Split(',').Select(x => x.Trim()).ToArray())];
                    Dictionary<string, AreaData> areaDict = Data.AreaDataCopy().ToDictionary(x => x.simpleName, x => x);
                    HashSet<string> addedNames = [];
                    foreach (string[] list in csvData)
                    {
                        AreaData area = areaDict[list[0]];
                        if (addedNames.Add(list[0]))
                        {
                            area.spawnEntries = [];
                        }
                        if (list[1].Length == 0)
                            continue;
                        int spawnIndex = int.Parse(list[1]);
                        while (area.spawnEntries.Count < spawnIndex + 1)
                        {
                            area.spawnEntries.Add(new());
                        }
                        SpawnEntry spawnEntry = area.spawnEntries[spawnIndex];
                        if (list[2].Length != 0)
                            spawnEntry.weight = int.Parse(list[2]);
                        if (list[3].Length != 0)
                            spawnEntry.nightOnly = bool.Parse(list[3]);
                        spawnEntry.spawnList.Add(new()
                        {
                            name = Data.simpleName[list[4]],
                            isPal = Data.palData[Data.simpleName[list[4]]].IsPal,
                            isBoss = bool.Parse(list[5]),
                            minLevel = uint.Parse(list[6]),
                            maxLevel = uint.Parse(list[7]),
                            minGroupSize = uint.Parse(list[8]),
                            maxGroupSize = uint.Parse(list[9])
                        });
                    }
                    List<AreaData> areaList = [.. areaDict.Values];
                    areaList.ForEach(area => area.spawnExportData.spawnEntries.RemoveAll(entry => entry.spawnList.Count == 0));
                    areaList.Sort((x, y) => string.Compare(x.filename, y.filename));
                    AreaForEachIfDiff(areaList, area => area.modified = true);
                    PalSpawnWindow.Instance.areaList.ItemsSource = areaList;
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
        public static bool GeneratePalSpawns()
        {
            GenerateSpawnLists();
            RandomizeAndSaveAssets();
            return GenerateAndSavePak();
        }
        public static string GetRandomPal()
        {
            return Data.palList[new Random().Next(0, Data.palList.Count)];
        }
    }
}
