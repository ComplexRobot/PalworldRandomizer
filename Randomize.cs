using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stfu.Linq;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;

namespace PalworldRandomizer
{
    public static partial class Data
    {
        public static Dictionary<string, CharacterData> PalData { get; private set; } = [];
        public static Dictionary<string, string> PalName { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> SimpleName { get; private set; } = [];
        public static List<string> SimpleNameValues { get; private set; } = [];
        public static Dictionary<string, string> PalIcon { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public static List<string> PalList { get; private set; } = [];
        public static Dictionary<string, string> BossName { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public static List<string> TowerBossNames { get; private set; } = [];
        public static List<string> TowerNonBossNames { get; private set; } = [];
        public static List<string> RaidBossNames { get; private set; } = [];
        public static List<string> PredatorNames { get; private set; } = [];
        public static List<string> HumanBossNames { get; private set; } = [];
        public static HashSet<string> FlyingNames { get; private set; } = [];
        public static Dictionary<string, List<SpawnEntry>> SoloEntries { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, List<SpawnEntry>> BossEntries { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public static List<SpawnEntry> GroupEntries { get; private set; } = [];
        public static Dictionary<string, AreaData> AreaData { get; private set; } = [];
        public static List<string> TerrariaMonsters { get; private set; } = [];
        public static List<string> TerrariaMonstersBosses { get; private set; } = [];
        public static List<string> TowerHumanNames { get; private set; } = [];
        public static string FirstCage { get; private set; } = null!;
        public static string FirstEgg { get; private set; } = null!;
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
            "Hunter_MissileLauncher",
            "Hunter_GrenadeLauncher",
            "Hunter_BowGun_Oilrig",
            "Hunter_Katana_Oilrig",
            "Hunter_LaserRifle_Oilrig",
            "Male_Scientist01_LaserRifle",
            "Scientist_FlameThrower",
            "Male_Soldier01_EnemyGroup",
            "Male_Soldier02_EnemyGroup",
            "Male_Soldier02_Invader",
            "Female_Soldier03_Invader",
            "Female_Soldier04_Invader",
            "Male_Ninja01",
            "Male_NinjaElite01",
            "Viking",
            "Viking_Elite"
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
            "Guard_Shotgun",
            "Male_DarkTrader01",
            "Male_DarkTrader02",
            "Yamishima_guide5",
            "Escort_PalTamer01",
            "Escort_Warrior01"
        ];
        public static readonly string[] traderNames =
        [
            "SalesPerson",
            "SalesPerson_Desert",
            "SalesPerson_Desert2",
            "SalesPerson_Volcano",
            "SalesPerson_Volcano2",
            "SalesPerson_Wander",
            "CaravanLeader01",
            "CaravanLeader02",
            "CaravanLeader03"
        ];
        public static readonly string[] palTraderNames =
        [
            "PalDealer",
            "PalDealer_Desert",
            "PalDealer_Volcano",
            "RandomEventShop",
        ];
        public static readonly string[] specialNames =
        [
            "PalPassive_Doctor"
        ];

        [GeneratedRegex("^(Quest(_[^_]+)?_)?(?<name>.+?)(_[0-9]+(_.+)?|_Flower|_MAX|_Oilrig)?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
        private static partial Regex resourceKeyRegex();

        public static void Initialize()
        {
            PalData = UAssetData.CreatePalData();
            UAsset palNames = UAssetData.LoadAsset(@"Data\DT_PalNameText_Common.uasset");
            UAsset humanNames = UAssetData.LoadAsset(@"Data\DT_HumanNameText_Common.uasset");
            UAsset bossNPCIcons = UAssetData.LoadAsset(@"Data\DT_PalBossNPCIcon.uasset");
            Dictionary<CUE4Parse.UE4.Objects.UObject.FName, FStructFallback> palIcons
                = UAssetData.FileProvider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalCharacterIconDataTable.uasset");
            Dictionary<string, string> weapons = new()
            {
                { "AssaultRifle", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_AssaultRifle_Default1.png") },
                { "Handgun", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_HandGun_Default.png") },
                { "Shotgun", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_PumpActionShotgun.png") },
                { "RocketLauncher", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_Launcher_Default.png") },
                { "MeleeWeapon", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_Bat.png") },
                { "ThrowObject", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_FragGrenade.png") },
                { "FlameThrower", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_FlameThrower_Default.png") },
                { "GatlingGun", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_GatlingGun.png") },
                { "BowGun", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_BowGun.png") },
                { "LaserRifle", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_LaserRifle.png") },
                { "MissileLauncher", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_GuidedMissileLauncher.png") },
                { "GrenadeLauncher", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_GrenadeLauncher.png") },
                { "Katana", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_Katana.png") },
                { "GiantClub", UAssetData.AppDataPath(@"Images\InventoryItemIcon\T_itemicon_Weapon_Bat.png") },
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
                "Horus_Water",
                "YakushimaMonster002",
                "YakushimaMonster003",
                "YakushimaMonster003_Purple",
                "YakushimaBoss001",
                "YakushimaBoss001_Small",
            ];
            Dictionary<string, string> humanNameFixes = new()
            {
                //{ "GrassBoss", "Zoe" },
                { "ForestBoss", "Lily" },
                { "ElectricBoss", "Axel" },
                { "DesertBoss", "Marcus" },
                { "SnowBoss", "Victor" },
                { "SakurajimaBoss", "Saya" },
                { "VikingBoss", "Bjorn" },
            };
            foreach (KeyValuePair<string, CharacterData> keyPair in PalData)
            {
                if (keyPair.Value.IsPal)
                {
                    bool isTowerBoss = keyPair.Key.StartsWith("GYM_", StringComparison.OrdinalIgnoreCase);
                    bool isRaidBoss = keyPair.Key.StartsWith("RAID_", StringComparison.OrdinalIgnoreCase);
                    bool isPredator = keyPair.Key.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase);
                    bool isBoss = keyPair.Key.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase) || isTowerBoss || isRaidBoss || isPredator;
                    bool isSummon = keyPair.Key.StartsWith("SUMMON_", StringComparison.OrdinalIgnoreCase);
                    bool isOilrig = keyPair.Key.EndsWith("_Oilrig", StringComparison.OrdinalIgnoreCase);
                    bool isQuest = keyPair.Key.StartsWith("Quest_", StringComparison.OrdinalIgnoreCase);
                    StructPropertyData? nameData = ((DataTableExport) palNames.Exports[0]).Table.Data.Find(property =>
                        (PalData[keyPair.Key].OverrideNameTextID != null &&
                        string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"{PalData[keyPair.Key].OverrideNameTextID}_TextData", true) == 0)
                        || string.Compare(((TextPropertyData) property.Value[0]).Value.Value, $"PAL_NAME_{keyPair.Key}_TextData", true) == 0);
                    string nameString = nameData != null ? ((TextPropertyData) nameData.Value[0]).CultureInvariantString.Value.Trim() : "en_text";
                    if (nameString == "-")
                    {
                        nameString = "en_text";
                    }
                    while (nameString.Contains("  "))
                    {
                        nameString = nameString.Replace("  ", " ");
                    }
                    PalName.Add(keyPair.Key, nameString == "en_text" ? (isBoss ? keyPair.Key[(keyPair.Key.IndexOf('_') + 1)..] : keyPair.Key) : nameString);
                    if (keyPair.Value.ZukanIndex > 0 && !isSummon && !isOilrig && !isQuest)
                    {
                        PalList.Add(keyPair.Key);
                    }
                    else if (!isRaidBoss && keyPair.Key.Contains("Yakushima"))
                    {
                        if (isBoss)
                        {
                            TerrariaMonstersBosses.Add(keyPair.Key);
                        }
                        else
                        {
                            TerrariaMonsters.Add(keyPair.Key);
                        }
                    }
                    else if (keyPair.Key.EndsWith("_Otomo", StringComparison.OrdinalIgnoreCase))
                    {
                        TowerNonBossNames.Add(keyPair.Key);
                    }
                    if (!isBoss || isTowerBoss || isRaidBoss || isPredator)
                    {
                        if (!isBoss)
                        {
                            try
                            {
                                BossName.Add(keyPair.Key, PalData.Keys.First(key => string.Compare(key, $"BOSS_{keyPair.Key}", true) == 0));
                            }
                            catch
                            {
                            }
                        }
                        else if (isTowerBoss)
                        {
                            // TODO: Change to regex
                            if (!keyPair.Key.EndsWith("_2") && !keyPair.Key.EndsWith("_2_Avatar") && !keyPair.Key.EndsWith("_2_Servant") && !keyPair.Key.EndsWith("_Otomo"))
                            {
                                TowerBossNames.Add(keyPair.Key);
                            }
                        }
                        else if (isPredator)
                        {
                            PredatorNames.Add(keyPair.Key);
                        }
                        else
                        {
                            // Moon Lord and True Eye of Cthulhu do not work
                            if (!keyPair.Key.EndsWith("_2") && !keyPair.Key.StartsWith("RAID_YakushimaBoss002") && keyPair.Key != "RAID_YakushimaBoss001_Green")
                            {
                                RaidBossNames.Add(keyPair.Key);
                            }
                        }
                        SimpleName.Add(new SpawnData(keyPair.Key).SimpleName, keyPair.Key);
                    }
                    PalIconCheck((isBoss || isSummon) && !keyPair.Key.EndsWith("_Otomo"));
                    if (Array.Exists(flyingSuffixes, keyPair.Key.EndsWith))
                    {
                        FlyingNames.Add(keyPair.Key);
                    }
                }
                else
                {
                    StructPropertyData? property = ((DataTableExport) humanNames.Exports[0]).Table.Data.Find(property =>
                        ((TextPropertyData) property.Value[0]).Value.Value == $"{PalData[keyPair.Key].OverrideNameTextID}_TextData");
                    SimpleName.Add(keyPair.Key, keyPair.Key);
                    if (property != null)
                    {
                        PalName.Add(keyPair.Key, ((TextPropertyData)property.Value[0]).CultureInvariantString.Value.Trim());
                    }
                    else
                    {
                        if (humanNameFixes.TryGetValue(keyPair.Key, out string? value))
                        {
                            PalName.Add(keyPair.Key, value);
                        }
                        else
                        {
                            PalName.Add(keyPair.Key, "-");
                        }
                    }
                    if (keyPair.Key.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase))
                    {
                        HumanBossNames.Add(keyPair.Key);
                        StructPropertyData? bossProperty = ((DataTableExport)bossNPCIcons.Exports[0]).Table.Data.Find(property => property.Name.Value.Value == keyPair.Key);
                        if (bossProperty != null)
                        {
                            string resourceKey = ((SoftObjectPropertyData)bossProperty.Value[0]).Value.AssetPath.PackageName.Value.Value;
                            // null string check?
                            resourceKey = resourceKey[(resourceKey.LastIndexOf('/') + 1)..];
                            PalIcon.Add(keyPair.Key, UAssetData.AppDataPath($@"Images\NPC\{resourceKey}.png"));
                        }
                    }
                    else
                    {
                        PalIconCheck();
                    }
                    if (!PalIcon.ContainsKey(keyPair.Key))
                    {
                        if (PalData[keyPair.Key].Weapon != null && weapons.TryGetValue(PalData[keyPair.Key].Weapon!, out string? value))
                        {
                            PalIcon.Add(keyPair.Key, value);
                        }
                        else
                        {
                            PalIcon.Add(keyPair.Key, UAssetData.AppDataPath(@"Images\PalIcon\T_CommonHuman_icon_normal.png"));
                        }
                    }
                    if (keyPair.Key.EndsWith("Boss"))
                    {
                        TowerHumanNames.Add(keyPair.Key);
                    }
                }
                void PalIconCheck(bool skipPrefix = false)
                {
                    string resourceKey = resourceKeyRegex().Match(keyPair.Key).Groups[1].Value;
                    resourceKey = skipPrefix ? resourceKey[(resourceKey.IndexOf('_') + 1)..] : resourceKey;
                    FStructFallback resourceFound = palIcons.FirstOrDefault(kvp => string.Equals(kvp.Key.Text, resourceKey, StringComparison.OrdinalIgnoreCase)).Value;
                    CUE4Parse.UE4.Objects.UObject.FName? fName = ((SoftObjectProperty)resourceFound?.Properties[0].Tag!)?.Value.AssetPathName;
                    if (resourceFound != null && !((CUE4Parse.UE4.Objects.UObject.FName)fName!).IsNone)
                    {
                        string foundName = ((CUE4Parse.UE4.Objects.UObject.FName)fName).Text;
                        string resourceName = UAssetData.AppDataPath($@"Images\PalIcon\{foundName[(foundName.LastIndexOf('/') + 1)..foundName.LastIndexOf('.')]}.png");
                        PalIcon.Add(keyPair.Key, resourceName);
                    }
                    else if (keyPair.Value.IsPal)
                    {
                        PalIcon.Add(keyPair.Key, UAssetData.AppDataPath(@"Images\PalIcon\T_icon_unknown.png"));
                    }
                }
            }
            PalName["RowName"] = "<None>";
            SimpleName["<None>"] = "RowName";
            SimpleName.Remove("RowName");
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
                    if (spawnEntry.SpawnList[0].Name == "RowName")
                    {
                        continue;
                    }
                    if (spawnEntry.SpawnList.Count == 1 && !spawnEntry.SpawnList[0].IsBoss)
                    {
                        if (!SoloEntries.TryGetValue(spawnEntry.SpawnList[0].Name, out List<SpawnEntry>? value))
                        {
                            value = [];
                            SoloEntries.Add(spawnEntry.SpawnList[0].Name, value);
                        }
                        value.Add(spawnEntry);
                    }
                    else if (spawnEntry.SpawnList[0].IsBoss)
                    {
                        if (!spawnEntry.SpawnList[0].Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!BossEntries.TryGetValue(spawnEntry.SpawnList[0].Name, out List<SpawnEntry>? value))
                            {
                                value = [];
                                BossEntries.Add(spawnEntry.SpawnList[0].Name, value);
                            }
                            value.Add(spawnEntry);
                        }
                    }
                    else
                    {
                        GroupEntries.Add(spawnEntry);
                    }
                    if (spawnEntry.NightOnly)
                    {
                        nightAverageLevel += (float) (spawnEntry.SpawnList[0].MinLevel + spawnEntry.SpawnList[0].MaxLevel) * spawnEntry.Weight / 2.0f;
                        nightLevelRange += (float) (spawnEntry.SpawnList[0].MaxLevel - spawnEntry.SpawnList[0].MinLevel) * spawnEntry.Weight;
                        nightWeightSum += spawnEntry.Weight;
                    }
                    else
                    {
                        averageLevel += (float) (spawnEntry.SpawnList[0].MinLevel + spawnEntry.SpawnList[0].MaxLevel) * spawnEntry.Weight / 2.0f;
                        levelRange += (float) (spawnEntry.SpawnList[0].MaxLevel - spawnEntry.SpawnList[0].MinLevel) * spawnEntry.Weight;
                        weightSum += spawnEntry.Weight;
                    }
                }
                AreaData.Add(filename, new(uAsset, spawnExportData, filename));
                if (weightSum == 0)
                {
                    averageLevel = nightAverageLevel;
                    levelRange = nightLevelRange;
                    weightSum = nightWeightSum;
                }
                AreaData[filename].minLevel = Convert.ToInt32(averageLevel / weightSum - levelRange / 2.0f / weightSum);
                AreaData[filename].maxLevel = Convert.ToInt32(averageLevel / weightSum + levelRange / 2.0f / weightSum);
                if (nightWeightSum != 0)
                {
                    AreaData[filename].minLevelNight = Convert.ToInt32(nightAverageLevel / nightWeightSum - nightLevelRange / 2.0f / nightWeightSum);
                    AreaData[filename].maxLevelNight = Convert.ToInt32(nightAverageLevel / nightWeightSum + nightLevelRange / 2.0f / nightWeightSum);
                }
                AreaData[filename].isBoss = filename.Contains("boss", StringComparison.OrdinalIgnoreCase);
                AreaData[filename].isInDungeon = filename.Contains("dungeon", StringComparison.OrdinalIgnoreCase);
                AreaData[filename].isPredator = filename.Contains("PreBOSS", StringComparison.OrdinalIgnoreCase);
                AreaData[filename].isMimic = Path.GetFileNameWithoutExtension(filename).EndsWith("_mimic", StringComparison.OrdinalIgnoreCase);
                AreaData[filename].isMonsterOnly = Path.GetFileNameWithoutExtension(filename).EndsWith("_monsteronly", StringComparison.OrdinalIgnoreCase);
                AreaData[filename].isFieldBoss = AreaData[filename].isBoss && !AreaData[filename].isInDungeon && !AreaData[filename].isPredator;
                AreaData[filename].isDungeonBoss = AreaData[filename].isBoss && AreaData[filename].isInDungeon;
                AreaData[filename].isDungeon = !AreaData[filename].isBoss && AreaData[filename].isInDungeon && !AreaData[filename].isMimic;
                AreaData[filename].isField = !AreaData[filename].isBoss && !AreaData[filename].isInDungeon;
                AreaData[filename].isQuest = AreaData[filename].SimpleName.StartsWith("Quest_", StringComparison.OrdinalIgnoreCase);
            }
            string firstAreaName = "BP_PalSpawner_Sheets_1_1_plain_begginer.uasset";
            AreaData[firstAreaName].minLevel = AreaData[firstAreaName].SpawnEntries[0].SpawnList[0].MinLevel;
            AreaData[firstAreaName].maxLevel = AreaData[firstAreaName].SpawnEntries[0].SpawnList[0].MaxLevel;
            Dictionary<string, AreaData> cageData = FileModify.ReadCageData(UAssetData.AppDataPath(@"Data\DT_CapturedCagePal.uasset"));
            FirstCage = cageData.Values.Select(x => x.filename).Min()!;
            foreach (KeyValuePair<string, AreaData> keyPair in cageData)
            {
                AreaData.Add(keyPair.Key, keyPair.Value);
            }
            string[] eggFiles = Directory.GetFiles(UAssetData.AppDataPath(@"Assets\PalEgg"), "*.uasset").Select(Path.GetFileName).ToArray()!;
            Array.Sort(eggFiles);
            FirstEgg = $"PalEgg\\{eggFiles.First()}";
            foreach (string filename in eggFiles)
            {
                AreaData.Add($"PalEgg\\{filename}", FileModify.ReadEggData(UAssetData.AppDataPath($"Assets\\PalEgg\\{filename}")));
            }
        }
        public static List<AreaData> AreaDataCopy()
        {
            return [.. AreaData.Values.Select(area => area.Clone())];
        }
        public static void AreaForEachIfDiff(List<AreaData> areaList, Action<AreaData>? func, Action<AreaData>? elseFunc = null)
        {
            foreach (AreaData area in areaList)
            {
                List<SpawnEntry> baseEntries = AreaData[area.isCage ? $"Cage:{area.filename}" : area.filename].SpawnEntries;
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
                                    if (!string.Equals(baseList[j].Name, newList[j].Name, StringComparison.OrdinalIgnoreCase)
                                        || baseList[j].IsPal != newList[j].IsPal
                                        || baseList[j].MinLevel != newList[j].MinLevel
                                        || baseList[j].MaxLevel != newList[j].MaxLevel
                                        || baseList[j].MinCount != newList[j].MinCount
                                        || baseList[j].MaxCount != newList[j].MaxCount)
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
                    MainPage.Instance.savePak.IsEnabled = false;
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
                        if (key == "BlackCentaur") // Skip Necromus since it is included with Paladius
                        {
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
                        ((formData.SpawnHumans && Data.humanNames.Contains(keyPair.Key))
                        || (formData.SpawnPolice && Data.policeNames.Contains(keyPair.Key)))
                        )
                    {
                        SpawnData spawnData = new() { Name = keyPair.Key, IsPal = false };
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
                        ((formData.SpawnHumans && Data.humanNames.Contains(spawnEntry.SpawnList[0].Name))
                        || (formData.SpawnPolice && Data.policeNames.Contains(spawnEntry.SpawnList[0].Name)))
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
                if (formData.SpawnPolice)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Police_Handgun", 1, 2)] });
                }
                if (formData.SpawnGuards)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Guard_Rifle", 1, 2), new("Guard_Shotgun", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Male_DarkTrader01")] });
                    humanSpawns.Add(new() { SpawnList = [new("Male_DarkTrader02")] });
                    humanSpawns.Add(new() { SpawnList = [new("Yamishima_guide5", 2, 3)] });
                    humanSpawns.Add(new() { SpawnList = [new("Escort_PalTamer01", 1, 2), new("Escort_Warrior01", 1, 2)] });
                }
                if (formData.SpawnHumans)
                {
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_FlameThrower", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Scientist_FlameThrower", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_MissileLauncher", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_GrenadeLauncher", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_BowGun_Oilrig", 2, 3)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_Katana_Oilrig", 2, 3)] });
                    humanSpawns.Add(new() { SpawnList = [new("Hunter_LaserRifle_Oilrig", 2, 3)] });
                    humanSpawns.Add(new() { SpawnList = [new("Male_Soldier01_EnemyGroup", 1, 2), new("Male_Soldier02_EnemyGroup", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Male_Soldier02_Invader", 1, 2)] });
                    humanSpawns.Add(new() { SpawnList = [new("Female_Soldier03_Invader", 1, 2), new("Female_Soldier04_Invader", 1, 2)] });
                }
                if (formData.SpawnTraders)
                {
                    humanSpawns.AddRange(Data.traderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
                if (formData.SpawnPalTraders)
                {
                    humanSpawns.AddRange(Data.palTraderNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
                if (formData.SpawnSpecial)
                {
                    humanSpawns.AddRange(Data.specialNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
                }
                if (formData.SpawnTowerHumans)
                {
                    humanSpawns.AddRange(Data.TowerHumanNames.Select(name => new SpawnEntry { SpawnList = [new(name)] }));
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
                    .. formData.SpawnHumans ? Data.humanNames : [],
                    .. formData.SpawnPolice ? Data.policeNames : [],
                    .. formData.SpawnGuards ? Data.guardNames : [],
                    .. formData.SpawnTraders ? Data.traderNames : [],
                    .. formData.SpawnPalTraders ? Data.palTraderNames : [],
                    .. formData.SpawnSpecial ? Data.specialNames : [],
                    .. formData.SpawnTowerHumans ? Data.TowerHumanNames : [],
                ]).Select(name => new SpawnEntry { SpawnList = [new(name)] }));
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
        }
        private static ICollection<string> GetAllowedNames(FormData formData)
        {
            return
            [
                .. formData.SpawnPals ? Data.PalList : [],
                .. formData.SpawnAlphas ? Data.PalList.FindAll(Data.BossName.ContainsKey).ConvertAll(name => Data.BossName[name]) : [],
                .. formData.SpawnTowerBosses ? Data.TowerBossNames : [],
                .. formData.SpawnRaidBosses ? Data.RaidBossNames : [],
                .. formData.SpawnPredators ? Data.PredatorNames : [],
                .. formData.SpawnHumanBosses ? Data.HumanBossNames : [],
                .. formData.SpawnHumans ? Data.humanNames : [],
                .. formData.SpawnPolice ? Data.policeNames : [],
                .. formData.SpawnGuards ? Data.guardNames : [],
                .. formData.SpawnTraders ? Data.traderNames : [],
                .. formData.SpawnPalTraders ? Data.palTraderNames : [],
                .. formData.SpawnSpecial ? Data.specialNames : [],
                .. formData.SpawnTerraria ? Data.TerrariaMonsters : [],
                .. formData.SpawnTerrariaBosses ? Data.TerrariaMonstersBosses : [],
                .. formData.SpawnTowerHumans ? Data.TowerHumanNames : [],
                "RowName"
            ];
        }

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
            int maxGroupBoss = Math.Max(minGroup, formData.GroupMaxBoss);
            int spawnListSize = Math.Max(1, formData.SpawnListSize);
            double vanillaPlusChance = Math.Clamp(formData.VanillaPlusChance, 1, 99) / 100.0;
            float fieldLevel = Math.Max(0, formData.FieldLevel) / 100.0f;
            float dungeonLevel = Math.Max(0, formData.DungeonLevel) / 100.0f;
            float fieldBossLevel = Math.Max(0, formData.FieldBossLevel) / 100.0f;
            float dungeonBossLevel = Math.Max(0, formData.DungeonBossLevel) / 100.0f;
            float predatorLevel = Math.Max(0, formData.PredatorLevel) / 100.0f;
            float cageLevel = Math.Max(0, formData.CageLevel) / 100.0f;
            int levelCap = Math.Max(1, formData.LevelCap);
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
            float eggRespawnTime = Math.Max(0, formData.EggRespawnHours) * 60 + Math.Max(0, formData.EggRespawnMinutes) + Math.Max(0, formData.EggRespawnSeconds) / 60.0f;
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
                (formData.RandomizeField || !area.isField)
                && (formData.RandomizeDungeons || !area.isDungeon)
                && (formData.RandomizeDungeonBosses || !area.isDungeonBoss)
                && (formData.RandomizeFieldBosses || !area.isFieldBoss)
                && (formData.RandomizePredators || !area.isPredator)
                && (formData.RandomizeCages || !area.isCage)
                && (formData.RandomizeEggs || !area.isEgg)
                && (formData.RandomizeQuests || !area.isQuest)
                && (formData.RandomizeMimics || !area.isMimic));
            if (!formData.MethodNone)
            {
                List<AreaData> addedBosses = subList.FindAll(area => !area.isBoss && !area.isCage && !area.isMonsterOnly && BossesEverywhere(area)).ConvertAll(x => x.Clone());
                foreach (AreaData area in addedBosses)
                {
                    area.isBoss = true;
                    area.filename = $"~{area.filename}";
                }
                subList.AddRange(addedBosses);
            }
            subList.Sort((x, y) =>
            {
                if (x.isEgg != y.isEgg)
                    return (x.isEgg ? 1 : 0) - (y.isEgg ? 1 : 0);
                if (x.isCage != y.isCage)
                    return (x.isCage ? 1 : 0) - (y.isCage ? 1 : 0);
                if (x.isPredator != y.isPredator)
                    return (x.isPredator ? 1 : 0) - (y.isPredator ? 1 : 0);
                if (x.isBoss != y.isBoss)
                    return (x.isBoss ? 1 : 0) - (y.isBoss ? 1 : 0);
                if (x.isInDungeon != y.isInDungeon)
                    return (x.isInDungeon ? 1 : 0) - (y.isInDungeon ? 1 : 0);
                if (x.isMimic != y.isMimic)
                    return (x.isMimic ? 1 : 0) - (y.isMimic ? 1 : 0);
                bool nightOnlyX = NightOnly(x);
                bool nightOnlyY = NightOnly(y);
                if (nightOnlyX != nightOnlyY)
                    return (nightOnlyX ? 1 : 0) - (nightOnlyY ? 1 : 0);
                return string.Compare(x.filename, y.filename);
            });
            bool equalizeAreaRarity = formData.EqualizeAreaRarity && !formData.MethodNone && !formData.MethodFull && !formData.MethodGlobalSwap && !formData.VanillaRestrict;
            int progress = 0;
            int progressTotal = subList.Count;
            bool NightOnly(AreaData area, bool condition = true) => (formData.NightOnly == condition && area.isField)
                    || (formData.NightOnlyDungeons == condition && area.isDungeon)
                    || (formData.NightOnlyDungeonBosses == condition && area.isDungeonBoss)
                    || (formData.NightOnlyBosses == condition && area.isFieldBoss)
                    || (formData.NightOnlyPredators == condition && area.isPredator);
            bool BossesEverywhere(AreaData area) => area.isEgg ? formData.BossEggs : (area.isInDungeon ? formData.BossesEverywhereDungeons : formData.BossesEverywhere);
            double BossesEverywhereChance(AreaData area) => area.isEgg ? bossEggsChance : (area.isInDungeon ? bossesEverywhereDungeonsChance : bossesEverywhereChance);
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
                return Data.PalData[spawnData.Name["BOSS_".Length..]].Rarity;
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
                        ApplyLevelRange(spawnEntry.SpawnList[i], LevelMultiplier(spawnEntry.SpawnList[i]), average * currentAverage / firstAverage, newRange, !area.isCage && !area.isEgg);
                    }
                }
                ApplyLevelRange(spawnEntry.SpawnList[0], LevelMultiplier(spawnEntry.SpawnList[0]), average, range, !area.isCage && !area.isEgg);
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
                    return LevelMultiplierEx(spawnData, area.isInDungeon, area.isCage, area.isEgg);
                }
                float CountMultiplier(SpawnData spawnData)
                {
                    return CountMultiplierEx(spawnData, area.isInDungeon, area.isCage || area.isEgg);
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
                    if (area.isMonsterOnly)
                    {
                        continue;
                    }
                    List<SpawnEntry> spawnEntries = [];
                    List<SpawnEntry> spawnEntriesOriginal = area.SpawnEntries;
                    area.SpawnEntries = spawnEntries;
                    if (area.isPredator && predatorChance == 0)
                    {
                        continue;
                    }
                    if (area.isEgg)
                    {
                        area.eggRespawnTime = eggRespawnTime;
                    }
                    if (BossesEverywhere(area) && !area.isBoss && BossesEverywhereChance(area) == 1 && !area.isCage
                        && (!area.isEgg || area.filename != Data.FirstEgg))
                    {
                        continue;
                    }
                    string StripQuestPrefix(string name)
                    {
                        return questNameRegex().Match(name).Groups["name"].Value;
                    }
                    HashSet<string> vanillaNames = [.. spawnEntriesOriginal.FindAll(x => x.Weight != 0).ConvertAll(x => x.SpawnList.ConvertAll(y => StripQuestPrefix(y.Name)))
                        .SelectMany(x => x).Distinct()];
                    if (BossesEverywhere(area) && area.filename.StartsWith('~'))
                    {
                        vanillaNames.UnionWith(vanillaNames.ToList().FindAll(x => Data.PalData[x].IsPal && !Data.PalData[x].IsBoss)
                            .ConvertAll(x => Data.BossName.TryGetValue(x, out string? bossName) ? bossName : x));
                    }
                    long weightSum = 0;
                    if (formData.VanillaRestrict && !formData.MethodGlobalSwap)
                    {
                        List<SpawnEntry> bossBackupClone = bossSpawnsOriginalBackup.ConvertAll(x => x.Clone());
                        if (area.isPredator && formData.PredatorConstraint && !formData.SpawnPredators)
                        {
                            bossBackupClone.AddRange(Data.PredatorNames.ConvertAll(name => new SpawnEntry { SpawnList = [new(name)] }));
                        }
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
                    else if (formData.PredatorConstraint && area.isPredator)
                    {
                        basicSpawnsOriginal = [];
                        basicSpawnsCurrent = [];
                        bossSpawnsOriginal = Data.PredatorNames.ConvertAll(name => new SpawnEntry { SpawnList = [new(name)] });
                        bossSpawnsCurrent = FilterPredators(bossSpawnsCurrent.ConvertAll(x => x.Clone()));
                        List<SpawnEntry> FilterPredators(List<SpawnEntry> spawnList)
                        {
                            spawnList.ForEach(x => x.SpawnList.RemoveAll(y => !y.Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase)));
                            spawnList.RemoveAll(x => x.SpawnList.Count == 0 || !x.SpawnList.Exists(y => y.IsBoss));
                            return spawnList;
                        }
                    }
                    if (area.isCage && (area.filename == Data.FirstCage || (formData.VanillaRestrict && !formData.MethodGlobalSwap)))
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
                    if (area.isEgg && (area.filename == Data.FirstEgg || (formData.VanillaRestrict && !formData.MethodGlobalSwap)))
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
                    if (BossesEverywhere(area) && !area.isBoss && BossesEverywhereChance(area) == 1 && !area.isCage)
                    {
                        continue;
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
                        int maxSpecies = (area.isFieldBoss || area.isPredator) && !formData.FieldBossExtended
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
                        if (formData.MethodGlobalSwap)
                        {
                            if (area.isEgg)
                            {
                                spawnEntriesOriginal = spawnEntriesOriginal.FindAll(x => !x.SpawnList[0].IsBoss);
                            }
                            if (BossesEverywhere(area) && area.filename.StartsWith('~'))
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
                        int entryCount = (area.isFieldBoss || area.isPredator) && !formData.FieldBossExtended
                            ? 1
                            : (formData.MethodCustomSize ? spawnListSize : spawnEntriesOriginal.Count);
                        for (int i = 0; i < entryCount; ++i)
                        {
                            if (formData.MethodGlobalSwap)
                            {
                                SpawnEntry spawnEntry = spawnEntriesOriginal[i];
                                if (area.isEgg && swapMap.TryGetValue(spawnEntry.SpawnList[0].Name, out SpawnEntry? value) && !Data.PalData[value.SpawnList[0].Name].IsPal)
                                {
                                    swapMap.Remove(spawnEntry.SpawnList[0].Name);
                                }
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
                    void AddEntry(SpawnEntry value)
                    {
                        SpawnEntry spawnEntry = new()
                        {
                            Weight = formData.WeightTypeUniform ? random.Next(weightUniformMin, weightUniformMax + 1) : 10,
                            SpawnList = value.SpawnList.ConvertAll(spawnData =>
                                new SpawnData(spawnData.Name, area.isCage || area.isEgg ? 1 : spawnData.MinCount, area.isCage || area.isEgg ? 1 : spawnData.MaxCount)
                                {
                                    IsPal = Data.PalData[spawnData.Name].IsPal,
                                    MinLevel = spawnData.MinLevel,
                                    MaxLevel = spawnData.MaxLevel
                                })
                        };
                        if ((area.isCage || area.isEgg) && spawnEntry.SpawnList.Count > 1)
                        {
                            spawnEntry.SpawnList.RemoveRange(1, spawnEntry.SpawnList.Count - 1);
                        }
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
                                    CustomWeight(Rarity(spawnData), false, WeightScale(spawnData)))) / (float)spawnEntry.SpawnList.Count);
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
                                    spawnEntry.SpawnList.Sum(Rarity) / (float)spawnEntry.SpawnList.Count, false,
                                    spawnEntry.SpawnList.Sum(WeightScale) / spawnEntry.SpawnList.Count));
                            }
                            else if (formData.WeightCustomMode == GroupWeightMode.RarityAverageBlend)
                            {
                                weight = Convert.ToInt64(CustomWeight(
                                    spawnEntry.SpawnList.Sum(Rarity) / (float)spawnEntry.SpawnList.Count, true,
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
                        if (Data.PalData[spawnEntry.SpawnList[0].Name].Nocturnal && nightOnly)
                        {
                            spawnEntry.NightOnly = true;
                            if ((!formData.WeightTypeCustom || !formData.WeightAdjustProbability) && !formData.VanillaPlus && (!BossesEverywhere(area) || area.isBoss))
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
                        if (area.isCage || area.isEgg)
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
                            if (i == 0)
                            {
                                spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), countClampFirstMin, countClampFirstMax);
                                spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), countClampFirstMin, countClampFirstMax);
                            }
                            else
                            {
                                spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), countClampMin, countClampMax);
                                spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), countClampMin, countClampMax);
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
                        if (area.isCage || area.isEgg)
                        {
                            return spawnEntry;
                        }
                        if (original.Count != 0 && !(formData.Rarity8UpSolo && Rarity8Up(spawnEntry.SpawnList[0])))
                        {
                            int groupSize = area.isBoss ? random.Next(minGroupBoss, maxGroupBoss + 1) : random.Next(minGroup, maxGroup + 1);
                            if (nightOnly || !formData.MixHumanAndPal || formData.Rarity8UpSolo || formData.SeparateAggroHumans || formData.SeparateFlying)
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
                                        if (formData.SeparateAggroHumans && !separateAggroHumansApplied)
                                        {
                                            SeparateAggroHumanFilter();
                                        }
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
                            spawnEntry.SpawnList[0].MinLevel = minBossLevel;
                            spawnEntry.SpawnList[0].MaxLevel = maxBossLevel;
                            spawnEntry.SpawnList[1..].ForEach(spawnData => { spawnData.MinLevel = minAddLevel; spawnData.MaxLevel = maxAddLevel; });
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
                if (area.isPredator && predatorChance == 0)
                {
                    spawnEntries.Clear();
                    return true;
                }
                int changes = 0;
                foreach (SpawnEntry spawnEntry in spawnEntries)
                {
                    changes += spawnEntry.SpawnList.RemoveAll(spawnData => !allowedNames.Contains(spawnData.Name)
                        && (formData.MethodNone
                        || !area.isPredator || !formData.PredatorConstraint || !spawnData.Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase)));
                    if (area.isPredator && predatorChance == 1)
                    {
                        changes += spawnEntry.SpawnList.RemoveAll(spawnData => spawnData.Name == "RowName");
                    }
                }
                changes += spawnEntries.RemoveAll(entry => entry.SpawnList.Count == 0);
                if (area.isPredator && predatorChance != 1 && spawnEntries.Count == 2)
                {
                    int originalWeight0 = spawnEntries[0].Weight;
                    int originalWeight1 = spawnEntries[1].Weight;
                    spawnEntries[0].Weight = Convert.ToInt32(100 * predatorChance);
                    spawnEntries[1].Weight = 100 - spawnEntries[0].Weight;
                    changes += (spawnEntries[0].Weight != originalWeight0 ? 1 : 0) + (spawnEntries[1].Weight != originalWeight1 ? 1 : 0);
                }
                if (area.isEgg && area.eggRespawnTime != eggRespawnTime)
                {
                    ++changes;
                    area.eggRespawnTime = eggRespawnTime;
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
                        if (formData.ForceAddLevel && area.isBoss && i > 0 && !spawnData.IsBoss)
                        {
                            average = firstAverage * formData.BossAddLevel / 100;
                        }
                        else
                        {
                            average = (spawnData.MaxLevel + spawnData.MinLevel) / 2.0f;
                        }
                        ApplyLevelRange(spawnData, LevelMultiplierEx(spawnData, area.isInDungeon, area.isCage, area.isEgg), average, range, false);
                        float countMultiplier = CountMultiplierEx(spawnData, area.isInDungeon, area.isCage || area.isEgg);
                        if (i == 0)
                        {
                            firstAverage = average;
                            spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), countClampFirstMin, countClampFirstMax);
                            spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), countClampFirstMin, countClampFirstMax);
                        }
                        else
                        {
                            spawnData.MinCount = Math.Clamp(Convert.ToInt32(spawnData.MinCount * countMultiplier), countClampMin, countClampMax);
                            spawnData.MaxCount = Math.Clamp(Convert.ToInt32(spawnData.MaxCount * countMultiplier), countClampMin, countClampMax);
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
                    area.modified = true;
                    if (!area.isCage)
                    {
                        if (area.isEgg)
                        {
                            FileModify.WriteEggData(area, $"{eggOutputPath}\\{Path.GetFileName(area.filename)}");
                        }
                        else
                        {
                            PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                            area.uAsset.Write($"{outputPath}\\{area.filename}");
                        }
                    }
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
                List<AreaData> predatorList = [];
                List<AreaData> cageList = [];
                List<AreaData> eggList = [];
                List<AreaData> eggBossList = [];
                foreach (AreaData area in subList)
                {
                    if (area.isPredator)
                    {
                        predatorList.Add(area);
                    }
                    else if (area.isCage)
                    {
                        cageList.Add(area);
                    }
                    else if (area.isEgg)
                    {
                        if (area.isBoss)
                        {
                            eggBossList.Add(area);
                        }
                        else
                        {
                            eggList.Add(area);
                        }
                    }
                    else if (area.isBoss)
                    {
                        // Fix Bosses Everywhere mixing together lists with nocturnal separation and ones without
                        if (NightOnly(area))
                        {
                            if (area.isInDungeon)
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
                            if (area.isInDungeon)
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
                EqualizeSpawns(fieldBossNoctSplitList);
                EqualizeSpawns(dungeonBossNoctSplitList);
                EqualizeSpawns(predatorList);
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
                        GenerateLevels(spawnEntry, area, x => LevelMultiplierEx(x, area.isInDungeon, area.isCage, area.isEgg));
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
                if (formData.VanillaPlus && (!area.filename.StartsWith('~') || BossesEverywhereChance(area) == 1))
                {
                    vanillaSpawns = Data.AreaData[area.filename.StartsWith('~') ? area.filename[1..] : $"{(area.isCage ? "Cage:" : "")}{area.filename}"]
                        .SpawnEntries.ConvertAll(entry => entry.Clone());
                    if (formData.VanillaPlusFilter)
                    {
                        FilterVanillaSpawns(vanillaSpawns, area);
                    }
                    if (area.isPredator)
                    {
                        vanillaSpawns.RemoveAll(x => x.SpawnList.Exists(y => y.Name == "RowName"));
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
                if (formData.VanillaMerge && !area.isCage && !area.isEgg)
                {
                    List<SpawnEntry> mergedVanillaSpawns = Data.AreaData[area.filename.StartsWith('~') ? area.filename[1..] : area.filename].SpawnEntries.ConvertAll(x => x.Clone());
                    if (formData.VanillaMergeFilter)
                    {
                        FilterVanillaSpawns(mergedVanillaSpawns, area);
                    }
                    if (area.isPredator)
                    {
                        mergedVanillaSpawns.RemoveAll(x => x.SpawnList.Exists(y => y.Name == "RowName"));
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
                if (area.isPredator && predatorChance != 1)
                {
                    List<SpawnEntry> diurnalSpawns = spawnEntries.FindAll(x => !x.NightOnly);
                    int nullWeight = Convert.ToInt32(100 * (1 - predatorChance));
                    SpawnEntry? diurnalNull = null;
                    if (diurnalSpawns.Count != 0)
                    {
                        List<SpawnEntry> nullSpawn = [new() { Weight = nullWeight, SpawnList = [new("RowName") { MaxLevel = 1 }] }];
                        SelectiveScale(diurnalSpawns.Sum(x => (long)x.Weight), nullWeight, nullSpawn, spawnEntries, predatorChance);
                        spawnEntries.Insert(Math.Max(spawnEntries.IndexOf(diurnalSpawns[^1]) + 1, vanillaSpawns.Count), nullSpawn[0]);
                        diurnalNull = nullSpawn[0];
                    }
                    if (spawnEntries.Exists(x => x.NightOnly))
                    {
                        List<SpawnEntry> nullSpawn = [new() { NightOnly = true, Weight = nullWeight, SpawnList = [new("RowName") { MaxLevel = 1 }] }];
                        int diurnalNullWeight = diurnalNull != null ? diurnalNull.Weight : 0;
                        SelectiveScale(spawnEntries.Sum(x => (long)x.Weight) - diurnalNullWeight, nullWeight, nullSpawn, spawnEntries, predatorChance);
                        if (diurnalNull != null)
                        {
                            nullSpawn[0].Weight -= diurnalNull.Weight;
                        }
                        if (nullSpawn[0].Weight != 0)
                        {
                            spawnEntries.Add(nullSpawn[0]);
                        }
                    }
                    IntOverflowFix(spawnEntries, spawnEntries.Sum(x => (long)x.Weight), nightOnly);
                }
                if (!area.filename.StartsWith('~') && (formData.MethodNone || !BossesEverywhere(area) || area.isBoss || area.isCage))
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
                foreach (AreaData area in subList.FindAll(area => !area.isBoss && !area.isCage && !area.isMonsterOnly && BossesEverywhere(area)))
                {
                    AreaData addedBosses = subList.Find(x => x.filename == $"~{area.filename}")!;
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

                foreach (AreaData area in subList.FindAll(area => !area.filename.StartsWith('~') && !area.isCage && !area.isEgg && !area.isMonsterOnly))
                {
                    AreaData? monsterOnly = subList.Find(a => string.Equals(Path.GetFileNameWithoutExtension(a.filename),
                        Path.GetFileNameWithoutExtension(area.filename) + "_monsteronly", StringComparison.OrdinalIgnoreCase));
                    if (monsterOnly != null)
                    {
                        monsterOnly.SpawnEntries = area.SpawnEntries.ConvertAll(x => x.Clone());
                        monsterOnly.SpawnEntries.ForEach(entry => entry.SpawnList.RemoveAll(x => !Data.PalData[x.Name].IsPal));
                        monsterOnly.SpawnEntries.RemoveAll(x => x.SpawnList.Count == 0);
                        WriteAreaAsset(monsterOnly);
                    }
                }
            }

            if (formData.RandomizeCages)
            {
                FileModify.WriteCageData(subList.FindAll(x => x.isCage), basePath + @"\Pal\Content\Pal\DataTable\Character");
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
            MainPage.Instance.Dispatcher.Invoke(() => MainPage.Instance.statusBar.Text = "💾 Creating PAK...");
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

        public static bool SaveAreaList(List<AreaData> areaList)
        {
            MainPage.Instance.ValidateFormData();
            FormData formData = new();
            float eggRespawnTime = Math.Max(0, formData.EggRespawnHours) * 60 + Math.Max(0, formData.EggRespawnMinutes) + Math.Max(0, formData.EggRespawnSeconds) / 60.0f;
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
            bool changesDetected = false;
            bool cageChanges = false;
            Data.AreaForEachIfDiff(areaList, area =>
            {
                changesDetected = true;
                if (area.isCage)
                {
                    cageChanges = true;
                    return;
                }
                if (area.isEgg)
                {
                    area.eggRespawnTime = eggRespawnTime;
                    WriteEggData(area, $"{eggOutputPath}\\{Path.GetFileName(area.filename)}");
                }
                else
                {
                    PalSpawn.MutateAsset(area.uAsset, area.spawnExportData);
                    area.uAsset.Write($"{outputPath}\\{area.filename}");
                }
            });
            if (cageChanges)
            {
                string dataPath = UAssetData.AppDataPath(@"Create-Pak\Pal\Content\Pal\DataTable\Character");
                Directory.CreateDirectory(dataPath);
                WriteCageData(areaList.FindAll(x => x.isCage), dataPath);
            }
            return changesDetected;
        }

        public static Dictionary<string, AreaData> ReadCageData(string filepath)
        {
            UAsset uAsset = UAssetData.LoadAssetLocal(filepath);
            List<CagePalData> cagePalDataList = ((DataTableExport)uAsset.Exports[0]).Table.Data.ConvertAll(x => new CagePalData(uAsset, x));
            Dictionary<string, AreaData> cageList = [];
            foreach (CagePalData cagePalData in cagePalDataList)
            {
                if (!cageList.TryGetValue($"Cage:{cagePalData.FieldName}", out AreaData? areaData))
                {
                    areaData = new(new(), new(), cagePalData.FieldName!)
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

        public static void WriteCageData(List<AreaData> areaList, string savePath)
        {
            if (areaList.Count == 0)
            {
                return;
            }
            bool changes = false;
            Data.AreaForEachIfDiff(areaList, area =>
            {
                changes = true;
            });
            if (changes == false)
            {
                return;
            }
            Directory.CreateDirectory(savePath);
            UAsset uAsset = UAssetData.LoadAsset(@"Data\DT_CapturedCagePal.uasset");
            List<StructPropertyData> spawnList = ((DataTableExport)uAsset.Exports[0]).Table.Data;
            StructPropertyData baseStruct = spawnList[0];
            spawnList.Clear();
            int i = 0;
            foreach (AreaData area in areaList)
            {
                foreach (SpawnEntry spawnEntry in area.SpawnEntries)
                {
                    StructPropertyData newData = (StructPropertyData)baseStruct.Clone();
                    CagePalData cagePalData = new(uAsset, newData)
                    {
                        FieldName = area.filename,
                        PalID = spawnEntry.SpawnList[0].Name,
                        Weight = spawnEntry.Weight / 10.0f,
                        MinLevel = spawnEntry.SpawnList[0].MinLevel,
                        MaxLevel = spawnEntry.SpawnList[0].MaxLevel
                    };
                    newData.Name = new FName(uAsset, $"{++i}");
                    spawnList.Add(newData);
                }
            }
            uAsset.Write($"{savePath}\\DT_CapturedCagePal.uasset");
        }

        public static AreaData ReadEggData(string filename)
        {
            UAsset uAsset = UAssetData.LoadAssetLocal(filename);
            NormalExport export = (NormalExport)uAsset.Exports.Find(export => export.ObjectFlags.HasFlag(EObjectFlags.RF_ClassDefaultObject))!;
            List<StructPropertyData> spawnList = [.. Array.ConvertAll(((ArrayPropertyData)export.Data[0]).Value, x => (StructPropertyData)x)];
            return new(uAsset, new(), $"PalEgg\\{Path.GetFileName(filename)}")
            {
                isEgg = true,
                minLevel = 1,
                maxLevel = 1,
                SpawnEntries = spawnList.ConvertAll(x => new SpawnEntry
                {
                    SpawnList = [new() { Name = ((NamePropertyData)((StructPropertyData)((StructPropertyData)x.Value[0]).Value[0]).Value[0]).Value.Value.Value }],
                    Weight = Convert.ToInt32(((FloatPropertyData)x.Value[1]).Value * 40)
                }),
                eggRespawnTime = ((FloatPropertyData)export.Data[1]).Value,
                eggLotteryCooldown = ((FloatPropertyData)export.Data[2]).Value
            };
        }

        public static void WriteEggData(AreaData areaData, string filepath)
        {
            UAsset uAsset = UAssetData.LoadAsset($"Assets\\{areaData.filename}");
            NormalExport export = (NormalExport)uAsset.Exports.Find(export => export.ObjectFlags.HasFlag(EObjectFlags.RF_ClassDefaultObject))!;
            StructPropertyData baseStruct = (StructPropertyData)((ArrayPropertyData)export.Data[0]).Value[0];
            List<StructPropertyData> spawnList = [];
            foreach (SpawnEntry spawnEntry in areaData.SpawnEntries)
            {
                StructPropertyData newData = (StructPropertyData)baseStruct.Clone();
                ((NamePropertyData)((StructPropertyData)((StructPropertyData)newData.Value[0]).Value[0]).Value[0]).Value = new FName(uAsset, spawnEntry.SpawnList[0].Name);
                ((FloatPropertyData)newData.Value[1]).Value = spawnEntry.Weight / 40.0f;
                spawnList.Add(newData);
            }
            ((ArrayPropertyData)export.Data[0]).Value = [.. spawnList];
            ((FloatPropertyData)export.Data[1]).Value = areaData.eggRespawnTime;
            ((FloatPropertyData)export.Data[2]).Value = areaData.eggLotteryCooldown;
            uAsset.Write(filepath);
        }

        public static bool GenerateAndSavePak()
        {
            if (Directory.GetFiles(UAssetData.AppDataPath("Create-Pak"), "*.*", SearchOption.AllDirectories).Length == 0)
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
                Directory.GetDirectories(outputPath).ForAll(x => Directory.Delete(x, true));
                Process unrealPak = Process.Start(new ProcessStartInfo(UAssetData.AppDataPath("UnrealPak.exe"),
                    [openDialog.FileName, "-extract", outputPath]) { CreateNoWindow = true })!;
                unrealPak.WaitForExit();
                if (unrealPak.ExitCode != 0)
                    return "UnrealPak failed to extract the file.";
                string[] files = [.. Directory.GetFiles(outputPath, "BP_PalSpawner_Sheets_*.uasset", SearchOption.AllDirectories),
                    .. Directory.GetFiles(outputPath, "bp_palmapobjectspawner_palegg_*.uasset", SearchOption.AllDirectories)];
                string? cagePath;
                try
                {
                    cagePath = Directory.GetFiles(outputPath, "DT_CapturedCagePal.uasset", SearchOption.AllDirectories).First();
                }
                catch
                {
                    cagePath = null;
                }
                if (files.Length == 0 && cagePath == null)
                    return "No valid uasset files were found.";
                List<AreaData> areaList = Data.AreaDataCopy();
                List<AreaData> cages = [];
                if (cagePath != null)
                {
                    cages = [.. ReadCageData(cagePath).Values];
                }
                Data.AreaForEachIfDiff(cages, area =>
                {
                    AreaData found = areaList.Find(x => x.SimpleName == area.SimpleName)!;
                    found.SpawnEntries = area.SpawnEntries;
                    found.modified = true;
                });
                foreach (AreaData area in areaList)
                {
                    if (area.isCage)
                    {
                        continue;
                    }
                    string? path;
                    try
                    {
                        path = files.First(x => Path.GetFileName(x) == Path.GetFileName(area.filename));
                    }
                    catch
                    {
                        path = null;
                    }
                    if (path != null)
                    {
                        if (area.isEgg)
                        {
                            AreaData newData = ReadEggData(path);
                            area.uAsset = newData.uAsset;
                            area.SpawnEntries = newData.SpawnEntries;
                            area.eggRespawnTime = newData.eggRespawnTime;
                            area.eggLotteryCooldown = newData.eggLotteryCooldown;
                        }
                        else
                        {
                            area.uAsset = UAssetData.LoadAssetLocal(path);
                            area.spawnExportData = PalSpawn.ReadAsset(area.uAsset, Data.AreaData[area.filename].spawnExportData.header.Length);
                        }
                        area.modified = true;
                    }
                }
                areaList.Sort(AreaSortFunc);
                Randomize.SaveBackup();
                PalSpawnPage.Instance.areaList.ItemsSource = areaList;
                Randomize.AreaListChanged = true;
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
                    IsPal = Data.PalData[palName].IsPal,
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
            public string Key { get; set; } = string.Empty;
        }

        public class PalSpawnerOneTribeInfo
        {
            public PalMonsterData PalId { get; set; } = new();
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
            foreach (AreaData area in areaList.Where(x => !x.isCage))
            {
                if (area.isEgg)
                {
                    EggSchema.Add($"/Game/Pal/Blueprint/MapObject/Spawner/{area.FileNameWithoutExtension}.{area.FileNameWithoutExtension}_C",
                        new PalMapObject.SpawnerPalEgg
                        {
                            SpawnPalEggLotteryDataArray = [.. area.SpawnEntries.Select(entry =>
                                new PalMapObject.PickupItem.PalEggLotteryData
                                {
                                    PalEggData = new() { PalMonsterId = new() { Key = entry.SpawnList[0].Name } },
                                    Weight = entry.Weight / 40.0f
                                }
                            )],
                            RespawnTimeMinutesObtained = area.eggRespawnTime
                        }
                    );
                }
                else
                {
                    PalSpawnSchema.Add($"/Game/Pal/Blueprint/Spawner/SheetsVariant/{area.FileNameWithoutExtension}.{area.FileNameWithoutExtension}_C",
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
                                            PalId = new() { Key = spawn.Name },
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
                // TODO: Update when new PalSchema releases
                int originalCount = 107;// originalAreaList.Where(x => x.isCage).SelectMany(x => x.SpawnEntries).Count();
                foreach (AreaData area in cages)
                {
                    originalCageList.First(x => x.SimpleName == area.SimpleName).SpawnEntries = area.SpawnEntries;
                }

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

                for (int j = i + 1; j <= originalCount; ++j)
                {
                    cageSchema["DT_CapturedCagePal"].Add($"{j}", null);
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

        [GeneratedRegex("^/Game/Pal/Blueprint/(?<folder>Spawner/SheetsVariant|MapObject/Spawner)/(?<package>[^.]+)\\.(?<class>[^.]+?)_C$", RegexOptions.ExplicitCapture)]
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
                                    IsPal = Data.PalData[row.PalId].IsPal,
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
                    if (!regexMatch.Success || regexMatch.Groups["package"].Value != regexMatch.Groups["class"].Value)
                    {
                        continue;
                    }

                    AreaData? area = areaList.Find(x => x.FileNameWithoutExtension == regexMatch.Groups["package"].Value);
                    if (area == null)
                    {
                        continue;
                    }

                    if (regexMatch.Groups["folder"].Value == "Spawner/SheetsVariant")
                    {

                        PalSpawner? spawner = value.ToObject<PalSpawner>();
                        if (spawner == null)
                        {
                            continue;
                        }

                        area.SpawnEntries = [.. spawner.SpawnGroupList.Select(entry =>
                            new SpawnEntry
                            {
                                Weight = entry.Weight,
                                NightOnly = entry.OnlyTime == "Night" || entry.OnlyTime == "EPalOneDayTimeType::Night",
                                SpawnList = [.. entry.PalList.Select(spawn =>
                                    new SpawnData
                                    {
                                        IsPal = Data.PalData[spawn.PalId.Key].IsPal,
                                        Name = spawn.PalId.Key,
                                        MinLevel = spawn.Level,
                                        MaxLevel = spawn.Level_Max,
                                        MinCount = spawn.Num,
                                        MaxCount = spawn.Num_Max
                                    }
                                )]
                            }
                        )];

                    }
                    else if (regexMatch.Groups["folder"].Value == "MapObject/Spawner")
                    {
                        PalMapObject.SpawnerPalEgg? spawner = value.ToObject<PalMapObject.SpawnerPalEgg>();
                        if (spawner == null)
                        {
                            continue;
                        }

                        area.SpawnEntries = [.. spawner.SpawnPalEggLotteryDataArray.Select(entry =>
                            new SpawnEntry
                            {
                                Weight = Convert.ToInt32(entry.Weight * 40),
                                SpawnList =
                                [
                                    new SpawnData
                                    {
                                        IsPal = Data.PalData[entry.PalEggData.PalMonsterId.Key].IsPal,
                                        Name = entry.PalEggData.PalMonsterId.Key
                                    }
                                ]
                            }
                        )];
                    }
                }
            }
        }
    }
}
