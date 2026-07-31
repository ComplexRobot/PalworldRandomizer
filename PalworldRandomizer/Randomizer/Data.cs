using System.IO;
using System.Text.RegularExpressions;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using PalworldRandomizer.Randomizer.PalSpawn;
using PalworldRandomizer.Serializer;

namespace PalworldRandomizer.Randomizer;

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
    /// <summary>List of PIDF Rider pals.</summary>
    public static List<string> PolicePalNames { get; private set; } = [];
    public static Dictionary<string, List<SpawnEntry>> SoloEntries { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, List<SpawnEntry>> BossEntries { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static List<SpawnEntry> GroupEntries { get; private set; } = [];
    public static Dictionary<string, AreaData> AreaData { get; private set; } = [];
    public static List<string> TerrariaMonsters { get; private set; } = [];
    public static List<string> TerrariaMonstersBosses { get; private set; } = [];
    public static List<string> TowerHumanNames { get; private set; } = [];
    public static string FirstCage { get; private set; } = null!;
    public static string FirstEgg { get; private set; } = null!;
    /// <summary>Filename of the first boss spawn table.</summary>
    public static string FirstBoss { get; private set; } = null!;

    public static readonly string[] humanNames = [
        "Believer_Bat",
        "Believer_CrossBow",
        "Believer_Fat_Cane",
        "Believer_Fat_GatlingGun",
        "FireCult_FlameThrower",
        "FireCult_Rifle",
        "FireCult_GrenadeLauncher",
        "FireCult_RocketLauncher",
        "FireCult_MissileLauncher",
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
        "Hunter_LaserRifle",
        "Hunter_Katana",
        "Male_Scientist01_LaserRifle",
        "Scientist_FlameThrower",
        "Scientist_GrenadeLauncher",
        "Scientist_MissileLauncher",
        "Male_Soldier01_EnemyGroup",
        "Male_Soldier02_EnemyGroup",
        "Male_Soldier02_Invader",
        "Female_Soldier03_Invader",
        "Female_Soldier04_Invader",
        "Male_Ninja01",
        "Male_NinjaElite01",
        "Ninja_Grenade",
        "NinjaElite_Grenade",
        "Ninja_Bowgun",
        "NinjaElite_Bowgun",
        "Viking",
        "Viking_Elite",
        "Viking_Melee",
        "Viking_GrenadeLauncher",
        "Viking_RocketLauncher",
        "Police_Invader_Katana",
        "Police_Invader_LaserRifle",
        "Police_Invader_GrenadeLauncher",
        "Police_Invader_FlameThrower",
        "Police_Invader_RocketLauncher",
        "Police_Invader_MissileLauncher",
        "Police_Invader_CrossBow",
        "Police_Invader_Handgun",
        "Police_Invader_Shotgun",
        "Police_Invader_Rifle",
        "Female_Kunoichi01",
        "Female_SurveyGirl04",
    ];

    public static readonly string[] policeNames = [
        "Police_Handgun",
        "Police_Rifle",
        "Police_Shotgun",
        "Police_BowGun",
    ];

    public static readonly string[] guardNames = [
        "Guard_Rifle",
        "Guard_Shotgun",
        "Male_DarkTrader01",
        "Male_DarkTrader02",
        "Yamishima_guide5",
        "Escort_PalTamer01",
        "Escort_Warrior01",
        "Reward_BossDefeat",
        "Reward_Paldex",
        "Reward_Paldex_v02",
        "Reward_PalCaptureCount",
        "Reward_PalCaptureCount_v02",
        "Reward_Food",
    ];

    public static readonly string[] traderNames = [
        "SalesPerson",
        "SalesPerson_Desert",
        "SalesPerson_Desert2",
        "SalesPerson_Volcano",
        "SalesPerson_Volcano2",
        "SalesPerson_Wander",
        "CaravanLeader01",
        "CaravanLeader02",
        "CaravanLeader03",
        "Male_Trader01_v04",
        "Male_Trader01_v05",
        "Male_Trader01_v06",
        "Male_Trader01_v07",
        "Male_Trader01_v08",
        "Male_Trader01_v09",
        "Male_Trader01_v10",
        "Male_Trader01_v11",
        "Male_Trader01_v12",
        "Male_Trader01_v13",
        "Male_Trader01_v14",
        "Male_Trader01_v15",
        "Male_Trader01_v16",
        "Male_Trader01_v17",
        "Male_Trader01_v18",
        "Male_Trader01_v19",
        "Male_Trader01_v20",
        "Male_Trader01_v21",
        "Male_Trader01_v22",
        "Male_Trader01_v23",
        "Male_Trader01_v24",
        "Male_Trader01_v25",
        "NPC_Dungeon_Shop",
    ];

    public static readonly string[] palTraderNames = [
        "PalDealer",
        "PalDealer_Desert",
        "PalDealer_Volcano",
        "RandomEventShop",
    ];

    public static readonly string[] specialNames = [
        "PalPassive_Doctor",
        //"Visitor_Recruiter", // broken
    ];

    /// <summary>Names of various humans that don't do anything special.</summary>
    public static readonly string[] UselessHumanNames = [
        "Male_Kigurumi01_v01",
        "Male_Police_old",
        "Female_Presenter01",
        "Visitor_Hunter_Rifle",
        "Female_Nomad01_v01",
        "Female_Nomad01_v02",
        "Female_Nomad01_v03",
        "Female_Nomad01_v04",
        "Female_Nomad01_v05",
        "Female_Farmer01_v01",
        "Female_Farmer01_v02",
        "Female_Farmer01_v03",
        "Female_Farmer01_v04",
        "Female_Farmer01_v05",
        "Female_Ranger01_v01",
        "Female_Ranger01_v02",
        "Female_Ranger01_v03",
        "Female_Ranger01_v04",
        "Female_Ranger01_v05",
        "Male_Scholar01_v01",
        "Male_Scholar01_v02",
        "Male_Scholar01_v03",
        "Male_Scholar01_v04",
        "Male_Scholar01_v05",
        "Male_Breeder01_v01",
        "Male_Breeder01_v02",
        "Male_Breeder01_v03",
        "Male_Breeder01_v04",
        "Male_Breeder01_v05",
        "Female_SurveyGirl01",
        "Female_SurveyGirl02",
        "Female_SurveyGirl03",
        "Female_SurveyWoman01",
        "Female_SurveyWoman02",
        "Male_SurveyMan01",
        "Male_SurveyMan02",
        "Male_SurveyMan03",
        "Male_StrongOldMan01",
        "Male_StrongOldMan02",
        "Help01",
        "Help02",
        "Help03",
        "Help04",
        "Female_Soldier01",
        "Female_Soldier02",
        "Female_Soldier03",
        "Female_Soldier04",
        "Male_Soldier01",
        "Male_Soldier02",
        "Male_Soldier03",
        "Male_Soldier04",
    ];

    /// <summary>Names of generic villager NPCs.</summary>
    public static readonly string[] VillagerNames = [
        "Female_DesertPeople02",
        "Male_DesertPeople01",
        "Female_DTairikuPeople01_v01",
        "Male_DTairikuPeople01_v01",
        "Female_People02",
        "Male_People02",
        "Female_People03",
        "Male_People03",
        "Female_SakurajimaPeople01",
        "Male_SakurajimaPeople01",
        "Female_SnowPeople01",
        "Male_SnowPeople01",
        "Female_SorajimaPeople01",
        "Male_SorajimaPeople01",
        "Female_TenrakuPeople01",
        "Male_TenrakuPeople01",
        "Female_WorldTreePeople01",
        "Male_WorldTreePeople01",
        "MobuCitizen",
        "MobuCitizen_Male",
        "MobuVillager",
    ];

    [GeneratedRegex("^(Quest(_[^_]+)?_)?(?<name>.+?)(_[0-9]+(_.+)?|_Flower|_MAX|_Oilrig)?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex resourceKeyRegex();

    [GeneratedRegex(@"^Pal/Content/Pal/Blueprint/MapObject/Spawner/bp_palmapobjectspawner_palegg_.+?\.uasset$")]
    private static partial Regex PalEggSpawnSheetsRegex();

    [GeneratedRegex("^(.+?_)?(HawkBird|Eagle|BirdDragon|ThunderBird|RedArmorBird|HadesBird|Suzaku|Horus|"
        + "YakushimaMonster002|YakushimaMonster003|YakushimaBoss001|GhostDragon|BlueSkyDragon)(_[^_]+)?$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex FlyingPalNameRegex();

    public static void Initialize(VfsFileProvider fileProvider)
    {
        var palDataAsset = fileProvider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
        var humanDataAsset = fileProvider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalHumanParameter.uasset");

        PalData = ((IEnumerable<KeyValuePair<string, CharacterData>>)
            [.. CreateReferencePairs(palDataAsset), .. CreateReferencePairs(humanDataAsset)]).ToDictionary(StringComparer.OrdinalIgnoreCase);

        PalData["RowName"] = new CharacterData(palDataAsset.First().Value.Properties)
        {
            IsPal = true,
            ZukanIndex = -1,
            OverrideNameTextID = null,
            IsBoss = false
        };

        static IEnumerable<KeyValuePair<string, CharacterData>> CreateReferencePairs(Dictionary<FName, FStructFallback> rowMap) =>
            rowMap.Select(keyValuePair => new KeyValuePair<string, CharacterData>($"{keyValuePair.Key.Text}", new(keyValuePair.Value.Properties)));

        var palNames = fileProvider.LoadDataTableText("Pal/Content/L10N/en/Pal/DataTable/Text/DT_PalNameText_Common.uasset");
        var humanNames = fileProvider.LoadDataTableText("Pal/Content/L10N/en/Pal/DataTable/Text/DT_HumanNameText_Common.uasset");
        var bossNPCIcons = fileProvider.LoadDataTableSoftObject("Pal/Content/Pal/DataTable/Character/DT_PalBossNPCIcon.uasset");
        var palIcons = fileProvider.LoadDataTableSoftObject("Pal/Content/Pal/DataTable/Character/DT_PalCharacterIconDataTable.uasset");

        string unknownIconPath = fileProvider.SaveTexturePng(
            "Pal/Content/Pal/Texture/UI/Main_Menu/T_icon_unknown.uasset", UAssetData.PalIconPath(),
            fileProvider.GameVersionUpdated);

        string commonHumanIconPath = fileProvider.SaveTexturePng(
            "Pal/Content/Pal/Texture/PalIcon/Normal/T_CommonHuman_icon_normal.uasset", UAssetData.PalIconPath("Human"),
            fileProvider.GameVersionUpdated);

        var weapons = new Dictionary<string, string> {
            { "AssaultRifle", UAssetData.WeaponIconPath("T_itemicon_Weapon_AssaultRifle_Default1.png") },
            { "Handgun", UAssetData.WeaponIconPath("T_itemicon_Weapon_HandGun_Default.png") },
            { "Shotgun", UAssetData.WeaponIconPath("T_itemicon_Weapon_PumpActionShotgun.png") },
            { "RocketLauncher", UAssetData.WeaponIconPath("T_itemicon_Weapon_Launcher_Default.png") },
            { "MeleeWeapon", UAssetData.WeaponIconPath("T_itemicon_Weapon_Bat.png") },
            { "ThrowObject", UAssetData.WeaponIconPath("T_itemicon_Weapon_FragGrenade.png") },
            { "FlameThrower", UAssetData.WeaponIconPath("T_itemicon_Weapon_FlameThrower_Default.png") },
            { "GatlingGun", UAssetData.WeaponIconPath("T_itemicon_Weapon_GatlingGun.png") },
            { "BowGun", UAssetData.WeaponIconPath("T_itemicon_Weapon_BowGun.png") },
            { "LaserRifle", UAssetData.WeaponIconPath("T_itemicon_Weapon_LaserRifle.png") },
            { "MissileLauncher", UAssetData.WeaponIconPath("T_itemicon_Weapon_GuidedMissileLauncher.png") },
            { "GrenadeLauncher", UAssetData.WeaponIconPath("T_itemicon_Weapon_GrenadeLauncher.png") },
            { "Katana", UAssetData.WeaponIconPath("T_itemicon_Weapon_Katana.png") },
            { "GiantClub", UAssetData.WeaponIconPath("T_itemicon_Weapon_Bat.png") },
        };

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
        foreach ((string key, var value) in PalData)
        {
            if (value.IsPal)
            {
                bool isTowerBoss = key.StartsWith("GYM_", StringComparison.OrdinalIgnoreCase);
                bool isRaidBoss = key.StartsWith("RAID_", StringComparison.OrdinalIgnoreCase);
                bool isPredator = key.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase);
                bool isBoss = key.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase) || isTowerBoss || isRaidBoss || isPredator;
                bool isSummon = key.StartsWith("SUMMON_", StringComparison.OrdinalIgnoreCase);
                bool isOilrig = key.EndsWith("_Oilrig", StringComparison.OrdinalIgnoreCase);
                bool isQuest = key.StartsWith("Quest_", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_Quest", StringComparison.OrdinalIgnoreCase)
                        || key.EndsWith("_Quest_Friend", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_Quest_Enemy", StringComparison.OrdinalIgnoreCase);
                bool isTower = key.EndsWith("_Tower", StringComparison.OrdinalIgnoreCase);
                bool isPolice = key.StartsWith("POLICE_", StringComparison.OrdinalIgnoreCase);

                string nameString;

                if (value.OverrideNameTextID is string textId && palNames.TryGetValue(textId, out string? name) && name is not null) {
                    nameString = name;
                } else if (palNames.TryGetValue($"PAL_NAME_{key}", out string? nameFallback) && nameFallback is not null) {
                    nameString = nameFallback;
                } else {
                    nameString = "en_text";
                }

                if (nameString is "-" or "Unidentified Pal")
                {
                    nameString = "en_text";
                }
                while (nameString.Contains("  "))
                {
                    nameString = nameString.Replace("  ", " ");
                }
                PalName.Add(key, (nameString == "en_text" ? (isBoss ? key[(key.IndexOf('_') + 1)..] : key) : nameString).Trim());
                if (value.ZukanIndex > 0 && !isSummon && !isOilrig && !isQuest && !isTower
                    && key != "WorldTreeDragon")
                {
                    PalList.Add(key);
                }
                else if (!isRaidBoss && key.Contains("Yakushima"))
                {
                    if (isBoss)
                    {
                        TerrariaMonstersBosses.Add(key);
                    }
                    else
                    {
                        TerrariaMonsters.Add(key);
                    }
                }
                else if (key.EndsWith("_Otomo", StringComparison.OrdinalIgnoreCase) && !isBoss)
                {
                    TowerNonBossNames.Add(key);
                } else if (isPolice) {
                    PolicePalNames.Add(key);
                }

                if (!isBoss || isTowerBoss || isRaidBoss || isPredator)
                {
                    if (!isQuest) {
                        if (!isBoss && key != "WorldTreeDragon")
                        {
                            try
                            {
                                BossName.Add(key, PalData.Keys.First(k => k.Equals($"BOSS_{key}", StringComparison.OrdinalIgnoreCase)));
                            }
                            catch
                            {
                            }
                        }
                        else if (isTowerBoss)
                        {
                            // TODO: Change to regex
                            if (!key.EndsWith("_2") && !key.EndsWith("_2_Avatar") && !key.EndsWith("_2_Servant") && !key.EndsWith("_Otomo"))
                            {
                                TowerBossNames.Add(key);
                            }
                        }
                        else if (isPredator)
                        {
                            PredatorNames.Add(key);
                        }
                        else
                        {
                            // Moon Lord and True Eye of Cthulhu do not work
                            if (!key.EndsWith("_2") && !key.StartsWith("RAID_YakushimaBoss002") && key != "RAID_YakushimaBoss001_Green")
                            {
                                RaidBossNames.Add(key);
                            }
                        }
                    }

                    SimpleName.Add(new SpawnData(key).SimpleName, key);
                }
                PalIconCheck((isBoss || isSummon) && !key.EndsWith("_Otomo"));
                if (FlyingPalNameRegex().IsMatch(key))
                {
                    FlyingNames.Add(key);
                }
            }
            else
            {
                SimpleName.Add(key, key);

                if (value.OverrideNameTextID is string textId && humanNames.TryGetValue(textId, out string? name) && name is not null)
                {
                    PalName.Add(key, name.Trim());
                }
                else
                {
                    if (humanNameFixes.TryGetValue(key, out string? nameFix))
                    {
                        PalName.Add(key, nameFix);
                    }
                    else
                    {
                        PalName.Add(key, "-");
                    }
                }
                if (key.StartsWith("BOSS_", StringComparison.OrdinalIgnoreCase))
                {
                    HumanBossNames.Add(key);

                    if (bossNPCIcons.TryGetValue(key, out string? foundPath) && foundPath != null) {
                        string resourcePath = fileProvider.SaveTexturePng(
                            VfsFileProvider.SoftPathToHardPath(foundPath), UAssetData.NpcIconPath(),
                            fileProvider.GameVersionUpdated);
                        PalIcon.Add(key, resourcePath);
                    }
                }
                else
                {
                    PalIconCheck();
                }
                if (!PalIcon.ContainsKey(key))
                {
                    if (PalData[key].Weapon != null && weapons.TryGetValue(PalData[key].Weapon!, out string? weaponName))
                    {
                        PalIcon.Add(key, weaponName);
                    }
                    else
                    {
                        PalIcon.Add(key, commonHumanIconPath);
                    }
                }
                if (key.EndsWith("Boss"))
                {
                    TowerHumanNames.Add(key);
                }
            }
            void PalIconCheck(bool skipPrefix = false)
            {
                string resourceKey = resourceKeyRegex().Match(key).Groups[1].Value;
                resourceKey = skipPrefix ? resourceKey[(resourceKey.IndexOf('_') + 1)..] : resourceKey;

                if (palIcons.TryGetValue(resourceKey, out string? foundPath) && foundPath != null
                    && foundPath != "/Game/Pal/Texture/PalIcon/Normal/T_dummy_icon.T_dummy_icon") {
                    string hardPath = VfsFileProvider.SoftPathToHardPath(foundPath);

                    if (fileProvider.Files.ContainsKey(hardPath)) {
                        string resourcePath = fileProvider.SaveTexturePng(hardPath,
                            UAssetData.PalIconPath(value.IsPal ? "" : "Human"),
                            fileProvider.GameVersionUpdated);
                        PalIcon.Add(key, resourcePath);
                    } else {
                        string resourcePath = UAssetData.PalIconPath(Path.GetFileName(hardPath));

                        if (File.Exists(resourcePath)) {
                            PalIcon.Add(key, resourcePath);
                        } else {
                            PalIcon.Add(key, unknownIconPath);
                        }
                    }
                } else if (value.IsPal) {
                    PalIcon.Add(key, unknownIconPath);
                }
            }
        }
        PalName["RowName"] = "<None>";
        SimpleName["<None>"] = "RowName";
        SimpleName.Remove("RowName");
        SimpleNameValues = [.. SimpleName.Keys];
        SimpleNameValues.Sort();

        var spawnerList = ((IEnumerable<string?>)
            [.. fileProvider.LoadDataTableSoftObject("Pal/Content/Pal/DataTable/Spawner/DT_PalSpawnerPlacement.uasset").Values,
            .. fileProvider.LoadDataTableSoftObject("Pal/Content/Pal/DataTable/Dungeon/DT_DungeonEnemySpawnDataTable.uasset").Values])
            .Distinct()
            .Select(value => value is string path ? VfsFileProvider.SoftPathToHardPath(path) : null)
            .Order();

        foreach (string? path in spawnerList)
        {
            if (path is null) {
                continue;
            }

            string filename = Path.GetFileName(path);

            if (!filename.StartsWith("BP_PalSpawner_")) {
                continue;
            }

            var spawnEntries = new GameStruct(fileProvider, path).PalSpawnsToSpawnEntries();

            float averageLevel = 0;
            float levelRange = 0;
            float weightSum = 0;
            float nightAverageLevel = 0;
            float nightLevelRange = 0;
            float nightWeightSum = 0;
            foreach (SpawnEntry spawnEntry in spawnEntries)
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

            var area = new AreaData([.. spawnEntries], filename);

            AreaData.Add(filename, area);

            if (weightSum == 0) {
                averageLevel = nightAverageLevel;
                levelRange = nightLevelRange;
                weightSum = nightWeightSum;
            }

            area.minLevel = Convert.ToInt32(averageLevel / weightSum - levelRange / 2.0f / weightSum);
            area.maxLevel = Convert.ToInt32(averageLevel / weightSum + levelRange / 2.0f / weightSum);

            if (nightWeightSum != 0) {
                area.minLevelNight = Convert.ToInt32(nightAverageLevel / nightWeightSum - nightLevelRange / 2.0f / nightWeightSum);
                area.maxLevelNight = Convert.ToInt32(nightAverageLevel / nightWeightSum + nightLevelRange / 2.0f / nightWeightSum);
            }

            area.isBoss = filename.Contains("boss", StringComparison.OrdinalIgnoreCase) || area.SpawnEntries[0].SpawnList[0].IsBoss;
            area.isInDungeon = filename.Contains("dungeon", StringComparison.OrdinalIgnoreCase);
            area.isPredator = filename.Contains("PreBOSS", StringComparison.OrdinalIgnoreCase);
            area.isMimic = Path.GetFileNameWithoutExtension(filename).EndsWith("_mimic", StringComparison.OrdinalIgnoreCase);
            //area.isMonsterOnly = Path.GetFileNameWithoutExtension(filename).EndsWith("_monsteronly", StringComparison.OrdinalIgnoreCase);
            area.isFieldBoss = area.isBoss && !area.isInDungeon && !area.isPredator;
            area.isDungeonBoss = area.isBoss && area.isInDungeon;
            area.isDungeon = !area.isBoss && area.isInDungeon && !area.isMimic;
            area.isField = !area.isBoss && !area.isInDungeon;
            area.isQuest = area.SimpleName.StartsWith("Quest_", StringComparison.OrdinalIgnoreCase);
            area.IsAllArea = filename.Contains("allarea", StringComparison.OrdinalIgnoreCase);
            area.IsOnlyHumans = !area.SpawnEntries
                .Exists(x => x.SpawnList.Exists(y => PalData[y.Name].IsPal && y.Name != "RowName"));
            area.IsSingleSpawn = !area.isBoss
                && (area.SpawnEntries.Count(x => x.SpawnList[0].Name != "RowName") == 1
                || area.SpawnEntries
                .SelectMany(x => x.SpawnList.Select(y => y.Name).Where(z => z != "RowName"))
                .Distinct()
                .Count() == 1);
        }
        string firstAreaName = "BP_PalSpawner_Sheets_green_A.uasset";
        AreaData[firstAreaName].minLevel = AreaData[firstAreaName].SpawnEntries[0].SpawnList[0].MinLevel;
        AreaData[firstAreaName].maxLevel = AreaData[firstAreaName].SpawnEntries[0].SpawnList[0].MaxLevel;

        FirstBoss = AreaData.Values.Where(x => x.isBoss).Min(x => x.filename)!;

        var cageData = FileModify.ReadCageData(
            new GameStruct(fileProvider, "Pal/Content/Pal/DataTable/Character/DT_CapturedCagePal.uasset")
            .DataTable.Values);
        FirstCage = cageData.Values.Min(x => x.filename)!;
        foreach (var keyPair in cageData)
        {
            AreaData.Add(keyPair.Key, keyPair.Value);
        }

        var eggSpawnerList = fileProvider.Files.Keys.Where(x => PalEggSpawnSheetsRegex().IsMatch(x)).Order();

        FirstEgg = $"PalEgg\\{Path.GetFileName(eggSpawnerList.First())}";
        foreach (string path in eggSpawnerList)
        {
            string filename = Path.GetFileName(path);

            var spawnData = new GameStruct(fileProvider, path);

            AreaData.Add($"PalEgg\\{filename}", FileModify.ReadEggData(filename, spawnData));
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