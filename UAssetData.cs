using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CUE4Parse.Compression;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.MappingsProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Textures;
using IniParser.Model;
using IniParser.Parser;
using Newtonsoft.Json;
using PalworldRandomizer.Resources;
using static PalworldRandomizer.FileModify;

namespace PalworldRandomizer
{
    public class VfsFileProvider() : AbstractVfsFileProvider(new VersionContainer(EGame.GAME_UE5_1), StringComparer.OrdinalIgnoreCase) 
    {
        public override void Initialize() { }

        public void AddFile(string file, string mountPoint)
        {
            OsGameFile gameFile = new(new(Path.GetDirectoryName(file)!), new(file), mountPoint, new VersionContainer(EGame.GAME_UE5_1));
            Files.AddFiles(new Dictionary<string, GameFile> { { gameFile.Path, gameFile } });
        }

        public IEnumerable<UObject> LoadAsset(string path)
        {
            if (TryLoadPackage(path, out IPackage? package))
            {
                return package.GetExports();
            }
            throw new Exception($"Failed to load package '{path}'.");
        }

        public Dictionary<FName, FStructFallback> LoadDataTable(string path)
        {
            if (LoadAsset(path).First() is UDataTable dataTable)
            {
                return dataTable.RowMap;
            }
            throw new Exception($"'{path}' is not a data table.");
        }

        /// <summary>
        /// Loads a data table asset containing SoftObject data.<br/>
        /// i.e., name to asset path mappings.
        /// </summary>
        public Dictionary<string, string?> LoadDataTableSoftObject(string path) {
            if (LoadAsset(path).First() is not UDataTable dataTable) {
                throw new Exception($"'{path}' is not a data table.");
            }

            if (!dataTable.RowMap.First().Value.Properties.Exists(x => x.PropertyType.Text == "SoftObjectProperty")) {
                throw new Exception($"'{path}' is not a SoftObject data table.");
            }

            return dataTable.RowMap.Select(kvp =>
                new KeyValuePair<string, string?>(
                    kvp.Key.Text,
                    NoneCheck(((SoftObjectProperty)kvp.Value.Properties.First(x => x.PropertyType.Text == "SoftObjectProperty")
                        .Tag!).Value.AssetPathName)
                )
            ).ToDictionary(StringComparer.OrdinalIgnoreCase);

            static string? NoneCheck(FName fName) => fName.IsNone ? null : fName.Text;
        }

        /// <summary>
        /// Loads a data table asset containing text data.
        /// </summary>
        public Dictionary<string, string?> LoadDataTableText(string path) {
            if (LoadAsset(path).First() is not UDataTable dataTable) {
                throw new Exception($"'{path}' is not a data table.");
            }

            if (dataTable.RowMap.First().Value.Properties[0].PropertyType.Text != "TextProperty") {
                throw new Exception($"'{path}' is not a Text data table.");
            }

            return dataTable.RowMap.Select(kvp =>
                new KeyValuePair<string, string?>(
                    kvp.Key.Text,
                    ((TextProperty)kvp.Value.Properties[0].Tag!).Value?.Text
                )
            ).ToDictionary(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads a data table asset containing cage pal data.
        /// </summary>
        public IEnumerable<CagePalData> LoadDataTableCagePal(string path) {
            if (LoadAsset(path).First() is not UDataTable dataTable) {
                throw new Exception($"'{path}' is not a data table.");
            }

            return  dataTable.RowMap.Select(kvp => {
                    var properties = kvp.Value.Properties.Select(x =>
                        new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag))
                        .ToDictionary(StringComparer.OrdinalIgnoreCase);

                    return new CagePalData {
                        FieldName = ((NameProperty)properties["FieldName"]!).Value.Text,
                        PalID = ((NameProperty)properties["PalID"]!).Value.Text,
                        Weight = ((FloatProperty)properties["Weight"]!).Value,
                        MinLevel = ((IntProperty)properties["MinLevel"]!).Value,
                        MaxLevel = ((IntProperty)properties["MaxLevel"]!).Value
                    };
                }
            );
        }

        /// <summary>
        /// Loads a blueprint asset containing a pal spawner.
        /// </summary>
        public PalSpawner LoadBlueprintPalSpawner(string path) {
            var spawnClass = LoadAsset(path).FirstOrDefault(x => {
                string? className = x.Class is null || x.Class.Name.IsNone ? null : x.Class.Name.Text;
                return className is not null && className.StartsWith("BP_PalSpawner_") && className.EndsWith("_C");
            });

            if (spawnClass is null) {
                throw new Exception($"'{path}' does not contain a Pal Spawner Blueprint.");
            }

            var spawnData = ((ArrayProperty)spawnClass.Properties.First(x => {
                string? propertyName = x.Name.IsNone ? null : x.Name.Text;
                return propertyName == "SpawnGroupList";
            }).Tag!).Value!.Properties.Select(y => ((AbstractPropertyHolder)((StructProperty)y).Value!.StructType).Properties
                .Select(x => new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag))
                .ToDictionary(StringComparer.OrdinalIgnoreCase));

            return new PalSpawner {
                SpawnGroupList = [.. spawnData.Select(group =>
                    new PalSpawnerGroupInfo {
                        Weight = ((IntProperty)group["Weight"]!).Value,
                        OnlyTime = ((EnumProperty)group["OnlyTime"]!).Value.Text,
                        PalList = [.. ((ArrayProperty)group["PalList"]!).Value!.Properties.Select(palList => {
                                var properties = ((AbstractPropertyHolder)((StructProperty)palList).Value!.StructType).Properties.Select(x =>
                                new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag)).ToDictionary(StringComparer.OrdinalIgnoreCase);

                                var palId = ((NameProperty)((AbstractPropertyHolder)((StructProperty)properties["PalId"]!)
                                        .Value!.StructType).Properties[0].Tag!).Value;
                                var npcId = ((NameProperty)((AbstractPropertyHolder)((StructProperty)properties["NPCID"]!)
                                        .Value!.StructType).Properties[0].Tag!).Value;

                                return new PalSpawnerOneTribeInfo
                                {
                                    PalId = new() { Key = palId.IsNone ? null : palId.Text },
                                    NPCID = new() { Key = npcId.IsNone ? null : npcId.Text },
                                    Level = ((IntProperty)properties["Level"]!).Value,
                                    Level_Max = ((IntProperty)properties["Level_Max"]!).Value,
                                    Num = ((IntProperty)properties["Num"]!).Value,
                                    Num_Max = ((IntProperty)properties["Num_Max"]!).Value
                                };
                            }
                        )]
                    }
                )]
            };
        }

        /// <summary>
        /// Loads a blueprint asset containing a pal egg spawner.
        /// </summary>
        public PalMapObject.SpawnerPalEgg LoadBlueprintPalEggSpawner(string path) {
            var spawnClass = LoadAsset(path).FirstOrDefault(x => {
                string? className = x.Class is null || x.Class.Name.IsNone ? null : x.Class.Name.Text;
                return className is not null && className.StartsWith("bp_palmapobjectspawner_palegg_") && className.EndsWith("_C");
            });

            if (spawnClass is null) {
                throw new Exception($"'{path}' does not contain a Pal Egg Spawner Blueprint.");
            }

            var spawnData = ((ArrayProperty)spawnClass.Properties.First(x => x.Name.Text == "SpawnPalEggLotteryDataArray")
                .Tag!).Value!.Properties.Select(y => ((AbstractPropertyHolder)((StructProperty)y).Value!.StructType).Properties
                .Select(x => new KeyValuePair<string, FPropertyTagType?>(x.Name.Text, x.Tag))
                .ToDictionary(StringComparer.OrdinalIgnoreCase));

            float respawnTime = ((FloatProperty)spawnClass.Properties.First(x => x.Name.Text == "RespawnTimeMinutesObtained")
                .Tag!).Value;

            return new PalMapObject.SpawnerPalEgg {
                SpawnPalEggLotteryDataArray = [.. spawnData.Select(group =>
                    new PalMapObject.PickupItem.PalEggLotteryData {
                        PalEggData = new() { PalMonsterId = new() {
                            Key = ((NameProperty)((AbstractPropertyHolder)((StructProperty)((AbstractPropertyHolder)((StructProperty)group["PalEggData"]!)
                            .Value!.StructType).Properties[0].Tag!).Value!.StructType).Properties[0].Tag!).Value.Text }
                        },
                        Weight = ((FloatProperty)group["Weight"]!).Value,
                    }
                )],
                RespawnTimeMinutesObtained = respawnTime
            };
        }

        public string GetOsFileName(string path)
        {
            if (this[path] is OsGameFile gameFile)
            {
                return gameFile.ActualFile.FullName;
            }
            throw new Exception($"'{path}' is not located in the user's file system.");
        }
    }

    public static partial class UAssetData
    {
        public const float ASSET_VERSION = 10;
        public static string InstallationDirectory { set; get; } = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";
        public static string ArchivePath { set; get; } = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld\Pal\Content\Paks\Pal-Windows.pak";
        public static string GameVersion { set; get; } = "0.0.0.0";
        private static string? appDataPath;

        [GeneratedRegex(@"^Pal/Content/Pal/Texture/(PalIcon/Normal/(?!T_dummy_icon).+|UI/Main_Menu/T_icon_unknown)\.uasset$", RegexOptions.ExplicitCapture)]
        private static partial Regex PalIconRegex();

        [GeneratedRegex(@"^Pal/Content/Pal/Texture/PalIcon/NPC/.+\.uasset$")]
        private static partial Regex NPCIconRegex();

        [GeneratedRegex(@"^Pal/Content/Others/InventoryItemIcon/Texture/T_itemicon_Weapon_(AssaultRifle_Default1|HandGun_Default|PumpActionShotgun|Launcher_Default|Bat|FragGrenade"
            + @"|FlameThrower_Default|GatlingGun|BowGun|LaserRifle|GuidedMissileLauncher|GrenadeLauncher|Katana)\.uasset$", RegexOptions.ExplicitCapture)]
        private static partial Regex WeaponIconRegex();

        [GeneratedRegex("skyisland")]
        private static partial Regex TestRegex();

        [GeneratedRegex(@"^Pal/Content/Pal/(Blueprint|DataTable)/.+?\.uasset$", RegexOptions.ExplicitCapture)]
        private static partial Regex TestBlueprintRegex();

        public static bool VerifyInstallationFolder(AppWindow settingsWindow)
        {
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!appDataPath.EndsWith('\\'))
            {
                appDataPath += '\\';
            }
            appDataPath += @"Palworld-Randomizer\";
            Directory.CreateDirectory(appDataPath);
            ConfigData config = SharedWindow.GetConfig();
            SettingsPage.Instance.installationFolderTextbox.Text = InstallationDirectory = config.InstallationDirectory;
            ArchivePath = InstallationDirectory + @"\Pal\Content\Paks\Pal-Windows.pak";
            if (!File.Exists(ArchivePath))
            {
                settingsWindow.ShowClean();
                return false;
            }
            return true;
        }

        public static VfsFileProvider Initialize()
        {
            ConfigData config = SharedWindow.GetConfig();
            SettingsPage.Instance.installationFolderTextbox.Text = InstallationDirectory = config.InstallationDirectory;
            GameVersion = config.GameVersion;
            ArchivePath = InstallationDirectory + @"\Pal\Content\Paks\Pal-Windows.pak";
            bool replaceAssets = config.AssetVersion < ASSET_VERSION && config.AutoReplaceOldFiles;
            config.AssetVersion = ASSET_VERSION;
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(Resource.Resource_resx);
            foreach (XmlNode resource in xmlDoc.DocumentElement!.SelectNodes("data")!)
            {
                string name = resource.Attributes!["name"]!.InnerText;
                if (name == "Resource.resx")
                    continue;
                string filename = AppDataPath(resource.SelectSingleNode("value")!.InnerText[..resource.SelectSingleNode("value")!.InnerText.IndexOf(';')]);
                Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
                if (replaceAssets || !File.Exists(filename))
                {
                    File.WriteAllBytes(filename, (byte[]) Resource.ResourceManager.GetObject(name)!);
                }
            }
            OodleHelper.Initialize(AppDataPath("oo2core_9_win64.dll"));
            ZlibHelper.Initialize(AppDataPath("zlib-ng2.dll"));
            VfsFileProvider fileProvider = new() { MappingsContainer = new FileUsmapTypeMappingsProvider(AppDataPath("Mappings.usmap")) };
            fileProvider.RegisterVfs(ArchivePath);
            fileProvider.Initialize();
            fileProvider.Mount();
            string gameVersion = GameVersion;
            if (fileProvider.TrySaveAsset("Pal/Config/DefaultGame.ini", out byte[]? gameIni))
            {
                IniData iniData = new IniDataParser(new() { AllowDuplicateKeys = true }).Parse(Encoding.ASCII.GetString(gameIni));
                gameVersion = iniData["/Script/EngineSettings.GeneralProjectSettings"]["ProjectVersion"];
            }
            bool gameUpdated = gameVersion != GameVersion;
            config.GameVersion = GameVersion = gameVersion;

            string palIconFolder = PalIconPath();
            Directory.CreateDirectory(palIconFolder);
            string npcIconFolder = NpcIconPath();
            Directory.CreateDirectory(npcIconFolder);
            string weaponIconFolder = WeaponIconPath();
            Directory.CreateDirectory(weaponIconFolder);
            string importsFolder = ImportsPath();

            //ConcurrentDictionary<string, byte> palEggSpawnsReferenced = new();
            Parallel.ForEach(fileProvider.Files, keyValuePair => {
                if (PalIconRegex().IsMatch(keyValuePair.Key))
                {
                    SaveImage(palIconFolder);
                }
                else if (NPCIconRegex().IsMatch(keyValuePair.Key))
                {
                    SaveImage(npcIconFolder);
                }
                else if (WeaponIconRegex().IsMatch(keyValuePair.Key))
                {
                    SaveImage(weaponIconFolder);
                }
                //else if (TestBlueprintRegex().IsMatch(keyValuePair.Key)
                //    && TestRegex().IsMatch(JsonConvert.SerializeObject(fileProvider.LoadAsset(keyValuePair.Key)))) {
                //    palEggSpawnsReferenced.TryAdd(keyValuePair.Key, 0);
                //}

                void SaveImage(string folder)
                {
                    string filename = folder + '\\' + keyValuePair.Value.NameWithoutExtension + ".png";
                    if ((gameUpdated || !File.Exists(filename)) && fileProvider.TryLoadPackage(keyValuePair.Value, out IPackage? package))
                    {
                        foreach (UObject export in package.GetExports())
                        {
                            if (export is UTexture texture)
                            {
                                File.WriteAllBytes(filename, [.. texture.Decode()!.Encode(ETextureFormat.Png, false, out _)]);
                                break;
                            }
                        }
                    }
                }

            });

            //foreach ((string found, _) in palEggSpawnsReferenced) {
            //    Console.WriteLine(found);
            //}
            
            SharedWindow.SaveConfig(config);
            return fileProvider;
        }

        public static string AppDataPath(string path = null!) => appDataPath + path;
        public static string? PathSeparator(string path) => path.Length > 0 ? '\\' + path : null;
        public static string AssetsPath(string path = "") => AppDataPath(@"Assets" + PathSeparator(path));
        //public static string PalSpawnerPath(string path = "") => AssetsPath(@"PalSpawner" + PathSeparator(path));
        public static string PalSpawnerPath(string path = "") => AssetsPath(path);
        public static string PalEggPath(string path = "") => AssetsPath(@"PalEgg" + PathSeparator(path));
        public static string DataPath(string path = "") => AppDataPath(@"Data" + PathSeparator(path));
        public static string ImagesPath(string path = "") => AppDataPath(@"Images" + PathSeparator(path));
        public static string PalIconPath(string path = "") => ImagesPath(@"PalIcon" + PathSeparator(path));
        public static string NpcIconPath(string path = "") => ImagesPath(@"NPC" + PathSeparator(path));
        public static string WeaponIconPath(string path = "") => ImagesPath(@"InventoryItemIcon" + PathSeparator(path));
        public static string ImportsPath(string path = "") => AppDataPath(@"Imports" + PathSeparator(path));

#if DEBUG
        // Example: PrintClassDefinition("CharacterData", "DT_PalMonsterParameter.uasset");
        public static void PrintClassDefinition(string name, string path, VfsFileProvider fileProvider)
        {
            Console.WriteLine("// Auto-generated with function PrintClassDefinition");
            Console.WriteLine($"public class {name}(List<FPropertyTag> properties) : StructData\n{{");
            Dictionary<FName, FStructFallback> rowMap = fileProvider.LoadDataTable(path);
            foreach (FStructFallback structData in rowMap.Values)
            {
                for (int i = 0; i < structData.Properties.Count; ++i)
                {
                    FPropertyTag propertyData = structData.Properties[i];
                    switch (propertyData.PropertyType.Text)
                    {
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
                    switch (propertyData.PropertyType.Text)
                    {
                    case "NameProperty":
                    case "EnumProperty":
                        Console.Write($"NullCheck(");
                        break;
                    }
                    Console.Write($"(({propertyData.PropertyType.Text})FindProp(properties, \"{propertyData.Name.Text}\").Tag!).Value");
                    switch (propertyData.PropertyType.Text)
                    {
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

    public abstract class StructData
    {
        protected static FPropertyTag FindProp(List<FPropertyTag> properties, string name) => properties.Find(p => string.Equals(p.Name.Text, name, StringComparison.OrdinalIgnoreCase))!;

        protected static string? NullCheck(FName fName) => fName.IsNone ? null : fName.Text;
    }

    // Auto-generated with function PrintClassDefinition
    public class CharacterData(List<FPropertyTag> properties) : StructData
    {
        public string? OverrideNameTextID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "OverrideNameTextID").Tag!).Value);
        public string? NamePrefixID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "NamePrefixID").Tag!).Value);
        public string? OverridePartnerSkillNameTextID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "OverridePartnerSkillNameTextID").Tag!).Value);
        public string? OverridePartnerSkillDescTextID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "OverridePartnerSkillDescTextID").Tag!).Value);
        public bool IsPal { get; set; } = ((BoolProperty)FindProp(properties, "IsPal").Tag!).Value;
        public string? Tribe { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "Tribe").Tag!).Value)?.SubstringAfterLast(':');
        public string? BPClass { get; set; } = NullCheck(((NameProperty)FindProp(properties, "BPClass").Tag!).Value);
        public int ZukanIndex { get; set; } = ((IntProperty)FindProp(properties, "ZukanIndex").Tag!).Value;
        public string? ZukanIndexSuffix { get; set; } = ((StrProperty)FindProp(properties, "ZukanIndexSuffix").Tag!).Value;
        public string? Size { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "Size").Tag!).Value)?.SubstringAfterLast(':');
        public int Rarity { get; set; } = ((IntProperty)FindProp(properties, "Rarity").Tag!).Value;
        public string? ElementType1 { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "ElementType1").Tag!).Value)?.SubstringAfterLast(':');
        public string? ElementType2 { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "ElementType2").Tag!).Value)?.SubstringAfterLast(':');
        public string? GenusCategory { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "GenusCategory").Tag!).Value)?.SubstringAfterLast(':');
        public string? Organization { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "Organization").Tag!).Value)?.SubstringAfterLast(':');
        public string? Weapon { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "Weapon").Tag!).Value)?.SubstringAfterLast(':');
        public bool WeaponEquip { get; set; } = ((BoolProperty)FindProp(properties, "WeaponEquip").Tag!).Value;
        public int Hp { get; set; } = ((IntProperty)FindProp(properties, "Hp").Tag!).Value;
        public int MeleeAttack { get; set; } = ((IntProperty)FindProp(properties, "MeleeAttack").Tag!).Value;
        public int ShotAttack { get; set; } = ((IntProperty)FindProp(properties, "ShotAttack").Tag!).Value;
        public int Defense { get; set; } = ((IntProperty)FindProp(properties, "Defense").Tag!).Value;
        public int Support { get; set; } = ((IntProperty)FindProp(properties, "Support").Tag!).Value;
        public int CraftSpeed { get; set; } = ((IntProperty)FindProp(properties, "CraftSpeed").Tag!).Value;
        public float Friendship_HP { get; set; } = ((FloatProperty)FindProp(properties, "Friendship_HP").Tag!).Value;
        public float Friendship_ShotAttack { get; set; } = ((FloatProperty)FindProp(properties, "Friendship_ShotAttack").Tag!).Value;
        public float Friendship_Defense { get; set; } = ((FloatProperty)FindProp(properties, "Friendship_Defense").Tag!).Value;
        public float Friendship_CraftSpeed { get; set; } = ((FloatProperty)FindProp(properties, "Friendship_CraftSpeed").Tag!).Value;
        public float EnemyMaxHPRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyMaxHPRate").Tag!).Value;
        public float EnemyReceiveDamageRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyReceiveDamageRate").Tag!).Value;
        public float EnemyInflictDamageRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyInflictDamageRate").Tag!).Value;
        public float EnemyWazaCoolTimeRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyWazaCoolTimeRate").Tag!).Value;
        public float CaptureRateCorrect { get; set; } = ((FloatProperty)FindProp(properties, "CaptureRateCorrect").Tag!).Value;
        public float ExpRatio { get; set; } = ((FloatProperty)FindProp(properties, "ExpRatio").Tag!).Value;
        public float Price { get; set; } = ((FloatProperty)FindProp(properties, "Price").Tag!).Value;
        public float StatusResistUpRate { get; set; } = ((FloatProperty)FindProp(properties, "StatusResistUpRate").Tag!).Value;
        public string? AIResponse { get; set; } = NullCheck(((NameProperty)FindProp(properties, "AIResponse").Tag!).Value);
        public string? AISightResponse { get; set; } = NullCheck(((NameProperty)FindProp(properties, "AISightResponse").Tag!).Value);
        public int SlowWalkSpeed { get; set; } = ((IntProperty)FindProp(properties, "SlowWalkSpeed").Tag!).Value;
        public int WalkSpeed { get; set; } = ((IntProperty)FindProp(properties, "WalkSpeed").Tag!).Value;
        public int RunSpeed { get; set; } = ((IntProperty)FindProp(properties, "RunSpeed").Tag!).Value;
        public int RideSprintSpeed { get; set; } = ((IntProperty)FindProp(properties, "RideSprintSpeed").Tag!).Value;
        public int TransportSpeed { get; set; } = ((IntProperty)FindProp(properties, "TransportSpeed").Tag!).Value;
        public int SwimSpeed { get; set; } = ((IntProperty)FindProp(properties, "SwimSpeed").Tag!).Value;
        public int SwimDashSpeed { get; set; } = ((IntProperty)FindProp(properties, "SwimDashSpeed").Tag!).Value;
        public bool IsBoss { get; set; } = ((BoolProperty)FindProp(properties, "IsBoss").Tag!).Value;
        public bool IsTowerBoss { get; set; } = ((BoolProperty)FindProp(properties, "IsTowerBoss").Tag!).Value;
        public bool IsRaidBoss { get; set; } = ((BoolProperty)FindProp(properties, "IsRaidBoss").Tag!).Value;
        public bool UseBossHPGauge { get; set; } = ((BoolProperty)FindProp(properties, "UseBossHPGauge").Tag!).Value;
        public string? BattleBGM { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "BattleBGM").Tag!).Value)?.SubstringAfterLast(':');
        public bool IgnoreLeanBack { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreLeanBack").Tag!).Value;
        public bool IgnoreBlowAway { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreBlowAway").Tag!).Value;
        public bool IgnoreStun { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreStun").Tag!).Value;
        public int MaxFullStomach { get; set; } = ((IntProperty)FindProp(properties, "MaxFullStomach").Tag!).Value;
        public float FullStomachDecreaseRate { get; set; } = ((FloatProperty)FindProp(properties, "FullStomachDecreaseRate").Tag!).Value;
        public int FoodAmount { get; set; } = ((IntProperty)FindProp(properties, "FoodAmount").Tag!).Value;
        public int ViewingDistance { get; set; } = ((IntProperty)FindProp(properties, "ViewingDistance").Tag!).Value;
        public int ViewingAngle { get; set; } = ((IntProperty)FindProp(properties, "ViewingAngle").Tag!).Value;
        public float HearingRate { get; set; } = ((FloatProperty)FindProp(properties, "HearingRate").Tag!).Value;
        public bool NooseTrap { get; set; } = ((BoolProperty)FindProp(properties, "NooseTrap").Tag!).Value;
        public bool Nocturnal { get; set; } = ((BoolProperty)FindProp(properties, "Nocturnal").Tag!).Value;
        public int BiologicalGrade { get; set; } = ((IntProperty)FindProp(properties, "BiologicalGrade").Tag!).Value;
        public bool Predator { get; set; } = ((BoolProperty)FindProp(properties, "Predator").Tag!).Value;
        public bool Edible { get; set; } = ((BoolProperty)FindProp(properties, "Edible").Tag!).Value;
        public int Stamina { get; set; } = ((IntProperty)FindProp(properties, "Stamina").Tag!).Value;
        public int MaleProbability { get; set; } = ((IntProperty)FindProp(properties, "MaleProbability").Tag!).Value;
        public int CombiRank { get; set; } = ((IntProperty)FindProp(properties, "CombiRank").Tag!).Value;
        public int CombiDuplicatePriority { get; set; } = ((IntProperty)FindProp(properties, "CombiDuplicatePriority").Tag!).Value;
        public bool IgnoreCombi { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreCombi").Tag!).Value;
        public float MeshCapsuleHalfHeight { get; set; } = ((FloatProperty)FindProp(properties, "MeshCapsuleHalfHeight").Tag!).Value;
        public float MeshCapsuleRadius { get; set; } = ((FloatProperty)FindProp(properties, "MeshCapsuleRadius").Tag!).Value;
        public string? BestWorkSuitability { get; set; } = NullCheck(((EnumProperty)FindProp(properties, "BestWorkSuitability").Tag!).Value)?.SubstringAfterLast(':');
        public int WorkSuitability_EmitFlame { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_EmitFlame").Tag!).Value;
        public int WorkSuitability_Watering { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Watering").Tag!).Value;
        public int WorkSuitability_Seeding { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Seeding").Tag!).Value;
        public int WorkSuitability_GenerateElectricity { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_GenerateElectricity").Tag!).Value;
        public int WorkSuitability_Handcraft { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Handcraft").Tag!).Value;
        public int WorkSuitability_Collection { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Collection").Tag!).Value;
        public int WorkSuitability_Deforest { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Deforest").Tag!).Value;
        public int WorkSuitability_Mining { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Mining").Tag!).Value;
        public int WorkSuitability_OilExtraction { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_OilExtraction").Tag!).Value;
        public int WorkSuitability_ProductMedicine { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_ProductMedicine").Tag!).Value;
        public int WorkSuitability_Cool { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Cool").Tag!).Value;
        public int WorkSuitability_Transport { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_Transport").Tag!).Value;
        public int WorkSuitability_MonsterFarm { get; set; } = ((IntProperty)FindProp(properties, "WorkSuitability_MonsterFarm").Tag!).Value;
        public string? PassiveSkill1 { get; set; } = NullCheck(((NameProperty)FindProp(properties, "PassiveSkill1").Tag!).Value);
        public string? PassiveSkill2 { get; set; } = NullCheck(((NameProperty)FindProp(properties, "PassiveSkill2").Tag!).Value);
        public string? PassiveSkill3 { get; set; } = NullCheck(((NameProperty)FindProp(properties, "PassiveSkill3").Tag!).Value);
        public string? PassiveSkill4 { get; set; } = NullCheck(((NameProperty)FindProp(properties, "PassiveSkill4").Tag!).Value);
        public string? FirstDefeatRewardItemID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "FirstDefeatRewardItemID").Tag!).Value);
    }

    public class CagePalData {
        public string? FieldName { get; set; } = default;
        public string? PalID { get; set; } = default;
        public float Weight { get; set; } = default;
        public int MinLevel { get; set; } = default;
        public int MaxLevel { get; set; } = default;
    }
}