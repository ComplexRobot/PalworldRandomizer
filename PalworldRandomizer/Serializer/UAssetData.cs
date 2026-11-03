using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CUE4Parse.Compression;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.Utils;
using IniParser.Model;
using IniParser.Parser;
using PalworldRandomizer.Resources;
using PalworldRandomizer.Window;

namespace PalworldRandomizer.Serializer;

/// <summary>Initializes game-archive access and manages extracted application resources.</summary>
public static partial class UAssetData {
    /// <summary>The version used to determine whether extracted application resources need updating.</summary>
    public const float ASSET_VERSION = 11;

    /// <summary>The configured Palworld installation directory.</summary>
    public static string InstallationDirectory { set; get; } =
        @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";

    /// <summary>The path of the game archive used to load assets.</summary>
    public static string ArchivePath { set; get; } =
        @"C:\Program Files (x86)\Steam\steamapps\common\Palworld\Pal\Content\Paks\Pal-Windows.pak";

    /// <summary>The last version read from the game configuration inside the archive.</summary>
    public static string GameVersion { set; get; } = "0.0.0.0";
    private static string? appDataPath;

    [GeneratedRegex(
        @"^Pal/Content/Others/InventoryItemIcon/Texture/T_itemicon_Weapon_"
            + @"(AssaultRifle_Default1|HandGun_Default|PumpActionShotgun|Launcher_Default|Bat|FragGrenade"
            + @"|FlameThrower_Default|GatlingGun|BowGun|LaserRifle|GuidedMissileLauncher"
            + @"|GrenadeLauncher|Katana)\.uasset$",
        RegexOptions.ExplicitCapture
    )]
    private static partial Regex WeaponIconRegex();

    [GeneratedRegex("skyisland")]
    private static partial Regex TestRegex();

    [GeneratedRegex(@"^Pal/Content/Pal/(Blueprint|DataTable)/.+?\.uasset$", RegexOptions.ExplicitCapture)]
    private static partial Regex TestBlueprintRegex();

    /// <summary>Verifies the configured archive path and opens setup when it is missing.</summary>
    public static bool VerifyInstallationFolder(AppWindow settingsWindow) {
        appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!appDataPath.EndsWith('\\')) {
            appDataPath += '\\';
        }

        appDataPath += @"Palworld-Randomizer\";
        Directory.CreateDirectory(appDataPath);
        ConfigData config = SharedWindow.GetConfig();
        SettingsPage.Instance.installationFolderTextbox.Text = InstallationDirectory = config.InstallationDirectory;
        ArchivePath = InstallationDirectory + @"\Pal\Content\Paks\Pal-Windows.pak";
        if (!File.Exists(ArchivePath)) {
            settingsWindow.ShowClean();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Updates and copies resources from the Pal-Windows.pak and Resource.resx to the user's system.
    /// </summary>
    /// <returns>A <see cref="VfsFileProvider"/> with files created from Pal-Windows.pak.</returns>
    public static async Task<VfsFileProvider> Initialize() {
        ConfigData config = SharedWindow.GetConfig();
        SettingsPage.Instance.installationFolderTextbox.Text = InstallationDirectory = config.InstallationDirectory;
        GameVersion = config.GameVersion;
        ArchivePath = InstallationDirectory + @"\Pal\Content\Paks\Pal-Windows.pak";
        bool replaceAssets = config.AssetVersion < ASSET_VERSION && config.AutoReplaceOldFiles;
        config.AssetVersion = ASSET_VERSION;
        XmlDocument xmlDoc = new();
        xmlDoc.LoadXml(Resource.Resource_resx);
        foreach (XmlNode resource in xmlDoc.DocumentElement!.SelectNodes("data")!.Cast<XmlNode>()) {
            string name = resource.Attributes!["name"]!.InnerText;
            if (name == "Resource.resx") {
                continue;
            }

            string filename = AppDataPath(
                resource.SelectSingleNode("value")!.InnerText[
                    ..resource.SelectSingleNode("value")!.InnerText.IndexOf(';')
                ]
            );
            Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
            if (replaceAssets || !File.Exists(filename)) {
                File.WriteAllBytes(filename, (byte[])Resource.ResourceManager.GetObject(name)!);
            }
        }

        OodleHelper.Initialize(AppDataPath("oo2core_9_win64.dll"));
        ZlibHelper.Initialize(AppDataPath("zlib-ng2.dll"));

        await using VfsFileProvider fileProvider = new()
        {
            MappingsContainer = new FileUsmapTypeMappingsProvider(AppDataPath("Mappings.usmap")),
        };

        fileProvider.RegisterVfs(ArchivePath);
        fileProvider.Initialize();
        fileProvider.Mount();
        string gameVersion = GameVersion;
        if (fileProvider.TrySaveAsset("Pal/Config/DefaultGame.ini", out byte[]? gameIni)) {
            IniData iniData = new IniDataParser(new() { AllowDuplicateKeys = true }).Parse(
                Encoding.ASCII.GetString(gameIni)
            );
            gameVersion = iniData["/Script/EngineSettings.GeneralProjectSettings"]["ProjectVersion"];
        }

        bool gameUpdated = fileProvider.GameVersionUpdated = gameVersion != GameVersion;
        config.GameVersion = GameVersion = gameVersion;

        Directory.CreateDirectory(PalIconPath());
        Directory.CreateDirectory(PalIconPath("Human"));
        Directory.CreateDirectory(NpcIconPath());
        Directory.CreateDirectory(WeaponIconPath());

        var threadLock = new Lock();

        //ConcurrentDictionary<string, byte> palEggSpawnsReferenced = new();
        Parallel.ForEach(
            fileProvider.Files,
            keyValuePair => {
                (string key, var value) = keyValuePair;

                if (WeaponIconRegex().IsMatch(key)) {
                    lock (threadLock) {
                        fileProvider.SaveTexturePng(key, WeaponIconPath(), gameUpdated);
                    }
                }
                //else if (TestBlueprintRegex().IsMatch(key)
                //    && TestRegex().IsMatch(JsonConvert.SerializeObject(fileProvider.LoadAsset(key)))) {
                //    palEggSpawnsReferenced.TryAdd(key, 0);
                //}
            }
        );

        //foreach ((string found, _) in palEggSpawnsReferenced) {
        //    Console.WriteLine(found);
        //}

        SharedWindow.SaveConfig(config);
        return fileProvider;
    }

    /// <summary>Builds a path relative to the application local-data directory.</summary>
    public static string AppDataPath(string path = null!) => appDataPath + path;

    /// <summary>Prefixes a nonempty relative path with the existing Windows path separator.</summary>
    public static string? PathSeparator(string path) => path.Length > 0 ? '\\' + path : null;

    /// <summary>Builds a path inside the extracted-assets directory.</summary>
    public static string AssetsPath(string path = "") => AppDataPath(@"Assets" + PathSeparator(path));

    //public static string PalSpawnerPath(string path = "") => AssetsPath(@"PalSpawner" + PathSeparator(path));
    /// <summary>Builds a path for extracted Pal spawn assets.</summary>
    public static string PalSpawnerPath(string path = "") => AssetsPath(path);

    /// <summary>Builds a path inside the extracted egg-assets directory.</summary>
    public static string PalEggPath(string path = "") => AssetsPath(@"PalEgg" + PathSeparator(path));

    /// <summary>Builds a path inside the application data directory.</summary>
    public static string DataPath(string path = "") => AppDataPath(@"Data" + PathSeparator(path));

    /// <summary>Builds a path inside the extracted-images directory.</summary>
    public static string ImagesPath(string path = "") => AppDataPath(@"Images" + PathSeparator(path));

    /// <summary>Builds a path inside the extracted Pal-icons directory.</summary>
    public static string PalIconPath(string path = "") => ImagesPath(@"PalIcon" + PathSeparator(path));

    /// <summary>Builds a path inside the extracted NPC-icons directory.</summary>
    public static string NpcIconPath(string path = "") => ImagesPath(@"NPC" + PathSeparator(path));

    /// <summary>Builds a path inside the extracted weapon-icons directory.</summary>
    public static string WeaponIconPath(string path = "") => ImagesPath(@"InventoryItemIcon" + PathSeparator(path));

    /// <summary>Builds a path inside the application imports directory.</summary>
    public static string ImportsPath(string path = "") => AppDataPath(@"Imports" + PathSeparator(path));

#if DEBUG
    // Example: PrintClassDefinition("CharacterData", "DT_PalMonsterParameter.uasset");
    /// <summary>Prints C# property declarations inferred from a game data table for development.</summary>
    public static void PrintClassDefinition(string name, string path, VfsFileProvider fileProvider) {
        Console.WriteLine("// Auto-generated with function PrintClassDefinition");
        Console.WriteLine($"public class {name}(List<FPropertyTag> properties) : StructData\n{{");
        Dictionary<FName, FStructFallback> rowMap = fileProvider.LoadDataTable(path);
        foreach (FStructFallback structData in rowMap.Values) {
            for (int i = 0; i < structData.Properties.Count; ++i) {
                FPropertyTag propertyData = structData.Properties[i];
                switch (propertyData.PropertyType.Text) {
                    case "NameProperty":
                    case "EnumProperty":
                    case "StrProperty":
                        Console.Write($"    public string?");
                        break;
                    case "BoolProperty":
                        Console.Write("    public bool");
                        break;
                    case "IntProperty":
                        Console.Write("    public int");
                        break;
                    case "FloatProperty":
                        Console.Write("    public float");
                        break;
                    // Skipping structs
                    case "StructProperty":
                        continue;
                    default:
                        throw new Exception($"Unknown data type '{propertyData.PropertyType.Text}'.");
                }

                Console.Write($" {propertyData.Name.Text} {{ get; set; }} = ");
                switch (propertyData.PropertyType.Text) {
                    case "NameProperty":
                    case "EnumProperty":
                        Console.Write($"NullCheck(");
                        break;
                }

                Console.Write(
                    $"(({propertyData.PropertyType.Text})FindProp(properties, \"{propertyData.Name.Text}\").Tag!).Value"
                );
                switch (propertyData.PropertyType.Text) {
                    case "NameProperty":
                        Console.WriteLine($");");
                        break;
                    case "EnumProperty":
                        Console.WriteLine($")?.SubstringAfterLast(':');");
                        break;
                    case "StrProperty":
                    case "BoolProperty":
                    case "IntProperty":
                    case "FloatProperty":
                        Console.WriteLine($";");
                        break;
                    default:
                        throw new Exception($"Unknown data type '{propertyData.PropertyType.Text}'.");
                }
            }

            break;
        }

        Console.WriteLine("}");
    }
#endif
}

/// <summary>Common helpers for reading named properties from game data-table structures.</summary>
public abstract class StructData {
    /// <summary>Finds a game property by name using case-insensitive comparison.</summary>
    protected static FPropertyTag FindProp(List<FPropertyTag> properties, string name) =>
        properties.Find(p => string.Equals(p.Name.Text, name, StringComparison.OrdinalIgnoreCase))!;

    /// <summary>Converts an Unreal name to text, using null for the None name.</summary>
    protected static string? NullCheck(FName fName) => fName.IsNone ? null : fName.Text;
}

// Auto-generated with function PrintClassDefinition
/// <summary>Typed character-table values loaded from the selected game archive.</summary>
/// <param name="properties">The properties read from a character data-table row.</param>
public class CharacterData(List<FPropertyTag> properties) : StructData {
    /// <summary>The game's OverrideNameTextID character-table value.</summary>
    public string? OverrideNameTextID { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "OverrideNameTextID").Tag!).Value);

    /// <summary>The game's NamePrefixID character-table value.</summary>
    public string? NamePrefixID { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "NamePrefixID").Tag!).Value);

    /// <summary>The game's OverridePartnerSkillNameTextID character-table value.</summary>
    public string? OverridePartnerSkillNameTextID { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "OverridePartnerSkillNameTextID").Tag!).Value);

    /// <summary>The game's OverridePartnerSkillDescTextID character-table value.</summary>
    public string? OverridePartnerSkillDescTextID { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "OverridePartnerSkillDescTextID").Tag!).Value);

    /// <summary>The game's IsPal character-table value.</summary>
    public bool IsPal { get; set; } = ((BoolProperty)FindProp(properties, "IsPal").Tag!).Value;

    /// <summary>The game's Tribe character-table value.</summary>
    public string? Tribe { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "Tribe").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's BPClass character-table value.</summary>
    public string? BPClass { get; set; } = NullCheck(((NameProperty)FindProp(properties, "BPClass").Tag!).Value);

    /// <summary>The game's ZukanIndex character-table value.</summary>
    public int ZukanIndex { get; set; } = ((IntProperty)FindProp(properties, "ZukanIndex").Tag!).Value;

    /// <summary>The game's ZukanIndexSuffix character-table value.</summary>
    public string? ZukanIndexSuffix { get; set; } =
        ((StrProperty)FindProp(properties, "ZukanIndexSuffix").Tag!).Value;

    /// <summary>The game's Size character-table value.</summary>
    public string? Size { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "Size").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's Rarity character-table value.</summary>
    public int Rarity { get; set; } = ((IntProperty)FindProp(properties, "Rarity").Tag!).Value;

    /// <summary>The game's ElementType1 character-table value.</summary>
    public string? ElementType1 { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "ElementType1").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's ElementType2 character-table value.</summary>
    public string? ElementType2 { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "ElementType2").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's GenusCategory character-table value.</summary>
    public string? GenusCategory { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "GenusCategory").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's Organization character-table value.</summary>
    public string? Organization { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "Organization").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's Weapon character-table value.</summary>
    public string? Weapon { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "Weapon").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's WeaponEquip character-table value.</summary>
    public bool WeaponEquip { get; set; } = ((BoolProperty)FindProp(properties, "WeaponEquip").Tag!).Value;

    /// <summary>The game's Hp character-table value.</summary>
    public int Hp { get; set; } = ((IntProperty)FindProp(properties, "Hp").Tag!).Value;

    /// <summary>The game's MeleeAttack character-table value.</summary>
    public int MeleeAttack { get; set; } = ((IntProperty)FindProp(properties, "MeleeAttack").Tag!).Value;

    /// <summary>The game's ShotAttack character-table value.</summary>
    public int ShotAttack { get; set; } = ((IntProperty)FindProp(properties, "ShotAttack").Tag!).Value;

    /// <summary>The game's Defense character-table value.</summary>
    public int Defense { get; set; } = ((IntProperty)FindProp(properties, "Defense").Tag!).Value;

    /// <summary>The game's Support character-table value.</summary>
    public int Support { get; set; } = ((IntProperty)FindProp(properties, "Support").Tag!).Value;

    /// <summary>The game's CraftSpeed character-table value.</summary>
    public int CraftSpeed { get; set; } = ((IntProperty)FindProp(properties, "CraftSpeed").Tag!).Value;

    /// <summary>The game's Friendship_HP character-table value.</summary>
    public float Friendship_HP { get; set; } = ((FloatProperty)FindProp(properties, "Friendship_HP").Tag!).Value;

    /// <summary>The game's Friendship_ShotAttack character-table value.</summary>
    public float Friendship_ShotAttack { get; set; } =
        ((FloatProperty)FindProp(properties, "Friendship_ShotAttack").Tag!).Value;

    /// <summary>The game's Friendship_Defense character-table value.</summary>
    public float Friendship_Defense { get; set; } =
        ((FloatProperty)FindProp(properties, "Friendship_Defense").Tag!).Value;

    /// <summary>The game's Friendship_CraftSpeed character-table value.</summary>
    public float Friendship_CraftSpeed { get; set; } =
        ((FloatProperty)FindProp(properties, "Friendship_CraftSpeed").Tag!).Value;

    /// <summary>The game's EnemyMaxHPRate character-table value.</summary>
    public float EnemyMaxHPRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyMaxHPRate").Tag!).Value;

    /// <summary>The game's EnemyReceiveDamageRate character-table value.</summary>
    public float EnemyReceiveDamageRate { get; set; } =
        ((FloatProperty)FindProp(properties, "EnemyReceiveDamageRate").Tag!).Value;

    /// <summary>The game's EnemyInflictDamageRate character-table value.</summary>
    public float EnemyInflictDamageRate { get; set; } =
        ((FloatProperty)FindProp(properties, "EnemyInflictDamageRate").Tag!).Value;

    /// <summary>The game's EnemyWazaCoolTimeRate character-table value.</summary>
    public float EnemyWazaCoolTimeRate { get; set; } =
        ((FloatProperty)FindProp(properties, "EnemyWazaCoolTimeRate").Tag!).Value;

    /// <summary>The game's CaptureRateCorrect character-table value.</summary>
    public float CaptureRateCorrect { get; set; } =
        ((FloatProperty)FindProp(properties, "CaptureRateCorrect").Tag!).Value;

    /// <summary>The game's ExpRatio character-table value.</summary>
    public float ExpRatio { get; set; } = ((FloatProperty)FindProp(properties, "ExpRatio").Tag!).Value;

    /// <summary>The game's Price character-table value.</summary>
    public float Price { get; set; } = ((FloatProperty)FindProp(properties, "Price").Tag!).Value;

    /// <summary>The game's StatusResistUpRate character-table value.</summary>
    public float StatusResistUpRate { get; set; } =
        ((FloatProperty)FindProp(properties, "StatusResistUpRate").Tag!).Value;

    /// <summary>The game's AIResponse character-table value.</summary>
    public string? AIResponse { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "AIResponse").Tag!).Value);

    /// <summary>The game's AISightResponse character-table value.</summary>
    public string? AISightResponse { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "AISightResponse").Tag!).Value);

    /// <summary>The game's SlowWalkSpeed character-table value.</summary>
    public int SlowWalkSpeed { get; set; } = ((IntProperty)FindProp(properties, "SlowWalkSpeed").Tag!).Value;

    /// <summary>The game's WalkSpeed character-table value.</summary>
    public int WalkSpeed { get; set; } = ((IntProperty)FindProp(properties, "WalkSpeed").Tag!).Value;

    /// <summary>The game's RunSpeed character-table value.</summary>
    public int RunSpeed { get; set; } = ((IntProperty)FindProp(properties, "RunSpeed").Tag!).Value;

    /// <summary>The game's RideSprintSpeed character-table value.</summary>
    public int RideSprintSpeed { get; set; } = ((IntProperty)FindProp(properties, "RideSprintSpeed").Tag!).Value;

    /// <summary>The game's TransportSpeed character-table value.</summary>
    public int TransportSpeed { get; set; } = ((IntProperty)FindProp(properties, "TransportSpeed").Tag!).Value;

    /// <summary>The game's SwimSpeed character-table value.</summary>
    public int SwimSpeed { get; set; } = ((IntProperty)FindProp(properties, "SwimSpeed").Tag!).Value;

    /// <summary>The game's SwimDashSpeed character-table value.</summary>
    public int SwimDashSpeed { get; set; } = ((IntProperty)FindProp(properties, "SwimDashSpeed").Tag!).Value;

    /// <summary>The game's IsBoss character-table value.</summary>
    public bool IsBoss { get; set; } = ((BoolProperty)FindProp(properties, "IsBoss").Tag!).Value;

    /// <summary>The game's IsTowerBoss character-table value.</summary>
    public bool IsTowerBoss { get; set; } = ((BoolProperty)FindProp(properties, "IsTowerBoss").Tag!).Value;

    /// <summary>The game's IsRaidBoss character-table value.</summary>
    public bool IsRaidBoss { get; set; } = ((BoolProperty)FindProp(properties, "IsRaidBoss").Tag!).Value;

    /// <summary>The game's UseBossHPGauge character-table value.</summary>
    public bool UseBossHPGauge { get; set; } = ((BoolProperty)FindProp(properties, "UseBossHPGauge").Tag!).Value;

    /// <summary>The game's BattleBGM character-table value.</summary>
    public string? BattleBGM { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "BattleBGM").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's IgnoreLeanBack character-table value.</summary>
    public bool IgnoreLeanBack { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreLeanBack").Tag!).Value;

    /// <summary>The game's IgnoreBlowAway character-table value.</summary>
    public bool IgnoreBlowAway { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreBlowAway").Tag!).Value;

    /// <summary>The game's IgnoreStun character-table value.</summary>
    public bool IgnoreStun { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreStun").Tag!).Value;

    /// <summary>The game's MaxFullStomach character-table value.</summary>
    public int MaxFullStomach { get; set; } = ((IntProperty)FindProp(properties, "MaxFullStomach").Tag!).Value;

    /// <summary>The game's FullStomachDecreaseRate character-table value.</summary>
    public float FullStomachDecreaseRate { get; set; } =
        ((FloatProperty)FindProp(properties, "FullStomachDecreaseRate").Tag!).Value;

    /// <summary>The game's FoodAmount character-table value.</summary>
    public int FoodAmount { get; set; } = ((IntProperty)FindProp(properties, "FoodAmount").Tag!).Value;

    /// <summary>The game's ViewingDistance character-table value.</summary>
    public int ViewingDistance { get; set; } = ((IntProperty)FindProp(properties, "ViewingDistance").Tag!).Value;

    /// <summary>The game's ViewingAngle character-table value.</summary>
    public int ViewingAngle { get; set; } = ((IntProperty)FindProp(properties, "ViewingAngle").Tag!).Value;

    /// <summary>The game's HearingRate character-table value.</summary>
    public float HearingRate { get; set; } = ((FloatProperty)FindProp(properties, "HearingRate").Tag!).Value;

    /// <summary>The game's NooseTrap character-table value.</summary>
    public bool NooseTrap { get; set; } = ((BoolProperty)FindProp(properties, "NooseTrap").Tag!).Value;

    /// <summary>The game's Nocturnal character-table value.</summary>
    public bool Nocturnal { get; set; } = ((BoolProperty)FindProp(properties, "Nocturnal").Tag!).Value;

    /// <summary>The game's BiologicalGrade character-table value.</summary>
    public int BiologicalGrade { get; set; } = ((IntProperty)FindProp(properties, "BiologicalGrade").Tag!).Value;

    /// <summary>The game's Predator character-table value.</summary>
    public bool Predator { get; set; } = ((BoolProperty)FindProp(properties, "Predator").Tag!).Value;

    /// <summary>The game's Edible character-table value.</summary>
    public bool Edible { get; set; } = ((BoolProperty)FindProp(properties, "Edible").Tag!).Value;

    /// <summary>The game's Stamina character-table value.</summary>
    public int Stamina { get; set; } = ((IntProperty)FindProp(properties, "Stamina").Tag!).Value;

    /// <summary>The game's MaleProbability character-table value.</summary>
    public int MaleProbability { get; set; } = ((IntProperty)FindProp(properties, "MaleProbability").Tag!).Value;

    /// <summary>The game's CombiRank character-table value.</summary>
    public int CombiRank { get; set; } = ((IntProperty)FindProp(properties, "CombiRank").Tag!).Value;

    /// <summary>The game's CombiDuplicatePriority character-table value.</summary>
    public int CombiDuplicatePriority { get; set; } =
        ((IntProperty)FindProp(properties, "CombiDuplicatePriority").Tag!).Value;

    /// <summary>The game's IgnoreCombi character-table value.</summary>
    public bool IgnoreCombi { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreCombi").Tag!).Value;

    /// <summary>The game's MeshCapsuleHalfHeight character-table value.</summary>
    public float MeshCapsuleHalfHeight { get; set; } =
        ((FloatProperty)FindProp(properties, "MeshCapsuleHalfHeight").Tag!).Value;

    /// <summary>The game's MeshCapsuleRadius character-table value.</summary>
    public float MeshCapsuleRadius { get; set; } =
        ((FloatProperty)FindProp(properties, "MeshCapsuleRadius").Tag!).Value;

    /// <summary>The game's BestWorkSuitability character-table value.</summary>
    public string? BestWorkSuitability { get; set; } =
        NullCheck(((EnumProperty)FindProp(properties, "BestWorkSuitability").Tag!).Value)?.SubstringAfterLast(':');

    /// <summary>The game's WorkSuitability_EmitFlame character-table value.</summary>
    public int WorkSuitability_EmitFlame { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_EmitFlame").Tag!).Value;

    /// <summary>The game's WorkSuitability_Watering character-table value.</summary>
    public int WorkSuitability_Watering { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Watering").Tag!).Value;

    /// <summary>The game's WorkSuitability_Seeding character-table value.</summary>
    public int WorkSuitability_Seeding { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Seeding").Tag!).Value;

    /// <summary>The game's WorkSuitability_GenerateElectricity character-table value.</summary>
    public int WorkSuitability_GenerateElectricity { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_GenerateElectricity").Tag!).Value;

    /// <summary>The game's WorkSuitability_Handcraft character-table value.</summary>
    public int WorkSuitability_Handcraft { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Handcraft").Tag!).Value;

    /// <summary>The game's WorkSuitability_Collection character-table value.</summary>
    public int WorkSuitability_Collection { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Collection").Tag!).Value;

    /// <summary>The game's WorkSuitability_Deforest character-table value.</summary>
    public int WorkSuitability_Deforest { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Deforest").Tag!).Value;

    /// <summary>The game's WorkSuitability_Mining character-table value.</summary>
    public int WorkSuitability_Mining { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Mining").Tag!).Value;

    /// <summary>The game's WorkSuitability_OilExtraction character-table value.</summary>
    public int WorkSuitability_OilExtraction { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_OilExtraction").Tag!).Value;

    /// <summary>The game's WorkSuitability_ProductMedicine character-table value.</summary>
    public int WorkSuitability_ProductMedicine { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_ProductMedicine").Tag!).Value;

    /// <summary>The game's WorkSuitability_Cool character-table value.</summary>
    public int WorkSuitability_Cool { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Cool").Tag!).Value;

    /// <summary>The game's WorkSuitability_Transport character-table value.</summary>
    public int WorkSuitability_Transport { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_Transport").Tag!).Value;

    /// <summary>The game's WorkSuitability_MonsterFarm character-table value.</summary>
    public int WorkSuitability_MonsterFarm { get; set; } =
        ((IntProperty)FindProp(properties, "WorkSuitability_MonsterFarm").Tag!).Value;

    /// <summary>The game's PassiveSkill1 character-table value.</summary>
    public string? PassiveSkill1 { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "PassiveSkill1").Tag!).Value);

    /// <summary>The game's PassiveSkill2 character-table value.</summary>
    public string? PassiveSkill2 { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "PassiveSkill2").Tag!).Value);

    /// <summary>The game's PassiveSkill3 character-table value.</summary>
    public string? PassiveSkill3 { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "PassiveSkill3").Tag!).Value);

    /// <summary>The game's PassiveSkill4 character-table value.</summary>
    public string? PassiveSkill4 { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "PassiveSkill4").Tag!).Value);

    /// <summary>The game's FirstDefeatRewardItemID character-table value.</summary>
    public string? FirstDefeatRewardItemID { get; set; } =
        NullCheck(((NameProperty)FindProp(properties, "FirstDefeatRewardItemID").Tag!).Value);
}