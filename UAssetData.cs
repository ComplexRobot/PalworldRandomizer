using CUE4Parse.Compression;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Textures;
using IniParser.Model;
using IniParser.Parser;
using PalworldRandomizer.Resources;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UAssetAPI;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

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

        public Dictionary<CUE4Parse.UE4.Objects.UObject.FName, FStructFallback> LoadDataTable(string path)
        {
            if (LoadAsset(path).First() is CUE4Parse.UE4.Assets.Exports.Engine.UDataTable dataTable)
            {
                return dataTable.RowMap;
            }
            throw new Exception($"'{path}' is not a data table.");
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
        public const float ASSET_VERSION = 9;
        public static string InstallationDirectory { set; get; } = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";
        public static string ArchivePath { set; get; } = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld\Pal\Content\Paks\Pal-Windows.pak";
        public static string GameVersion { set; get; } = "0.0.0.0";
        private static string? appDataPath;
        private static Usmap? usmap;
        public static VfsFileProvider FileProvider { get; private set; } = null!;

        [GeneratedRegex(@"^Pal/Content/Pal/Blueprint/Spawner/SheetsVariant/(?!C_Dummy).+$")]
        private static partial Regex SpawnSheetsRegex();

        [GeneratedRegex(@"^Pal/Content/Pal/Blueprint/MapObject/Spawner/bp_palmapobjectspawner_palegg_.+$")]
        private static partial Regex PalEggSpawnSheetsRegex();

        [GeneratedRegex(@"^Pal/Content/(Pal/DataTable/Character/DT_(CapturedCagePal|PalBossNPCIcon|PalHumanParameter|PalMonsterParameter|PalCharacterIconDataTable)"
            + @"|L10N/en/Pal/DataTable/Text/DT_(HumanNameText|PalNameText)_Common)\..+$", RegexOptions.ExplicitCapture)]
        private static partial Regex DataTableRegex();

        [GeneratedRegex(@"^Pal/Content/Pal/Texture/(PalIcon/Normal/(?!T_dummy_icon).+|UI/Main_Menu/T_icon_unknown)\.uasset$", RegexOptions.ExplicitCapture)]
        private static partial Regex PalIconRegex();

        [GeneratedRegex(@"^Pal/Content/Pal/Texture/PalIcon/NPC/.+\.uasset$")]
        private static partial Regex NPCIconRegex();

        [GeneratedRegex(@"^Pal/Content/Others/InventoryItemIcon/Texture/T_itemicon_Weapon_(AssaultRifle_Default1|HandGun_Default|PumpActionShotgun|Launcher_Default|Bat|FragGrenade"
            + @"|FlameThrower_Default|GatlingGun|BowGun|LaserRifle|GuidedMissileLauncher|GrenadeLauncher|Katana)\.uasset$", RegexOptions.ExplicitCapture)]
        private static partial Regex WeaponIconRegex();

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

        public static void Initialize()
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
            usmap = new Usmap(AppDataPath("Mappings.usmap"));
            OodleHelper.Initialize(AppDataPath("oo2core_9_win64.dll"));
            ZlibHelper.Initialize(AppDataPath("zlib-ng2.dll"));
            VfsFileProvider fileProvider = new() { MappingsContainer = new FileUsmapTypeMappingsProvider(AppDataPath("Mappings.usmap")) };
            fileProvider.RegisterVfs(ArchivePath);
            fileProvider.Initialize();
            fileProvider.Mount();
            //IPackage spawnExports = fileProvider.LoadPackage("Pal/Content/Pal/Blueprint/Spawner/SheetsVariant/BP_PalSpawner_Sheets_1_1_plain_begginer.uasset");
            string gameVersion = GameVersion;
            if (fileProvider.TrySaveAsset("Pal/Config/DefaultGame.ini", out byte[]? gameIni))
            {
                IniData iniData = new IniDataParser(new() { AllowDuplicateKeys = true }).Parse(Encoding.ASCII.GetString(gameIni));
                gameVersion = iniData["/Script/EngineSettings.GeneralProjectSettings"]["ProjectVersion"];
            }
            bool gameUpdated = gameVersion != GameVersion;
            config.GameVersion = GameVersion = gameVersion;
            string assetsFolder = AppDataPath("Assets");
            string palEggFolder = assetsFolder + @"\PalEgg";
            Directory.CreateDirectory(palEggFolder);
            string dataFolder = AppDataPath("Data");
            Directory.CreateDirectory(dataFolder);
            string imagesFolder = AppDataPath("Images");
            string palIconFolder = imagesFolder + @"\PalIcon";
            Directory.CreateDirectory(palIconFolder);
            string npcIconFolder = imagesFolder + @"\NPC";
            Directory.CreateDirectory(npcIconFolder);
            string weaponIconFolder = imagesFolder + @"\InventoryItemIcon";
            Directory.CreateDirectory(weaponIconFolder);
            string importsFolder = AppDataPath("Imports");
            ConcurrentDictionary<string, string> savedFilePaths = new();
            ConcurrentDictionary<string, byte> addedFiles = new();
            ConcurrentDictionary<string, FObjectImport> imports = new();
            Parallel.ForEach(fileProvider.Files, (KeyValuePair<string, GameFile> keyValuePair) =>
            {
                if (SpawnSheetsRegex().IsMatch(keyValuePair.Key))
                {
                    SaveAsset(assetsFolder);
                }
                else if (PalEggSpawnSheetsRegex().IsMatch(keyValuePair.Key))
                {
                    SaveAsset(palEggFolder);
                }
                else if (DataTableRegex().IsMatch(keyValuePair.Key))
                {
                    SaveAsset(dataFolder);
                }
                else if (PalIconRegex().IsMatch(keyValuePair.Key))
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

                void SaveAsset(string folder)
                {
                    string filename = folder + '\\' + keyValuePair.Value.Name;
                    if (gameUpdated || !File.Exists(filename))
                    {
                        File.WriteAllBytes(filename, keyValuePair.Value.Read());
                    }
                    if (fileProvider.TryLoadPackage(keyValuePair.Value, out IPackage? package))
                    {
                        ReadImports((Package)package);
                        addedFiles.TryAdd(keyValuePair.Value.Path, 0);
                    }
                    savedFilePaths.TryAdd(filename, keyValuePair.Value.Path[..(keyValuePair.Value.Path.LastIndexOf('/') + 1)]);
                }

                void SaveImage(string folder)
                {
                    string filename = folder + '\\' + keyValuePair.Value.NameWithoutExtension + ".png";
                    if ((gameUpdated || !File.Exists(filename)) && fileProvider.TryLoadPackage(keyValuePair.Value, out IPackage? package))
                    {
                        foreach (UObject export in package.GetExports())
                        {
                            if (export is UTexture texture)
                            {
                                File.WriteAllBytes(filename, texture.Decode()!.Encode(ETextureFormat.Png, 100).ToArray());
                                break;
                            }
                        }
                    }
                    addedFiles.TryAdd(keyValuePair.Value.Path, 0);
                    savedFilePaths.TryAdd(filename, keyValuePair.Value.Path[..(keyValuePair.Value.Path.LastIndexOf('/') + 1)]);
                }

                void ReadImports(Package package)
                {
                    foreach (FObjectImport import in package.ImportMap)
                    {
                        imports.TryAdd(import.OuterIndex!.Name, import);
                    }
                }
            });

            Parallel.ForEach(imports, (KeyValuePair<string, FObjectImport> keyValuePair) =>
            {
                if (keyValuePair.Key.StartsWith("/Game/"))
                {
                    string importPath = "Pal/Content" + keyValuePair.Key["/Game".Length..] + ".uasset";
                    if (fileProvider.Files.TryGetValue(importPath, out GameFile? gameFile) && !addedFiles.ContainsKey(gameFile.Path))
                    {
                        string filename = importsFolder + '\\' + gameFile.PathWithoutExtension.Replace('/', '\\');
                        string filenameUasset = filename + ".uasset";
                        string filenameUexp = filename + ".uexp";
                        Directory.CreateDirectory(filename[..filename.LastIndexOf('\\')]);
                        Parallel.Invoke(
                        [
                            () =>
                        {
                            if (gameUpdated || !File.Exists(filenameUasset))
                            {
                                File.WriteAllBytes(filenameUasset, gameFile.Read());
                            }
                        },
                        () =>
                        {
                            if (gameUpdated || !File.Exists(filenameUexp))
                            {
                                GameFile uexp = fileProvider.Files[gameFile.PathWithoutExtension + ".uexp"];
                                File.WriteAllBytes(filenameUexp, uexp.Read());
                            }
                        }
                    ]);

                        string mountPoint = gameFile.Path[..(gameFile.Path.LastIndexOf('/') + 1)];
                        savedFilePaths.TryAdd(filenameUasset, mountPoint);
                        savedFilePaths.TryAdd(filenameUexp, mountPoint);
                    }
                }
            });

            FileProvider = new() { MappingsContainer = new FileUsmapTypeMappingsProvider(AppDataPath("Mappings.usmap")) };
            FileProvider.Initialize();
            foreach (KeyValuePair<string, string> keyValuePair in savedFilePaths)
            {
                if (File.Exists(keyValuePair.Key))
                {
                    FileProvider.AddFile(keyValuePair.Key, keyValuePair.Value);
                }
            }
            fileProvider.PostMount();
            fileProvider.Dispose();
            SharedWindow.SaveConfig(config);
        }

        public static string AppDataPath(string path = null!) => appDataPath + path;
        public static UAsset LoadAsset(string filepath) => new(AppDataPath(filepath), EngineVersion.VER_UE5_1, usmap);
        public static UAsset LoadAssetLocal(string filepath) => new(filepath, EngineVersion.VER_UE5_1, usmap);

        public static Dictionary<string, CharacterData> CreatePalData()
        {
            Dictionary<CUE4Parse.UE4.Objects.UObject.FName, FStructFallback> palDataAsset = FileProvider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
            Dictionary<CUE4Parse.UE4.Objects.UObject.FName, FStructFallback> humanDataAsset = FileProvider.LoadDataTable("Pal/Content/Pal/DataTable/Character/DT_PalHumanParameter.uasset");
            Dictionary<string, CharacterData> palData = ((IEnumerable<KeyValuePair<string, CharacterData>>)
                [.. CreateReferencePairs(palDataAsset), .. CreateReferencePairs(humanDataAsset)]).ToDictionary(StringComparer.OrdinalIgnoreCase);
            palData["RowName"] = new CharacterData(palDataAsset.First().Value.Properties)
            {
                IsPal = true,
                ZukanIndex = -1,
                OverrideNameTextID = null,
                IsBoss = false
            };
            return palData;
            static IEnumerable<KeyValuePair<string, CharacterData>> CreateReferencePairs(Dictionary<CUE4Parse.UE4.Objects.UObject.FName, FStructFallback> rowMap) =>
                rowMap.Select(keyValuePair => new KeyValuePair<string, CharacterData>($"{keyValuePair.Key.Text}", new(keyValuePair.Value.Properties)));
        }

#if DEBUG
        // Example: PrintClassDefinition("CharacterData", "Pal/Content/Pal/DataTable/Character/DT_PalMonsterParameter.uasset");
        public static void PrintClassDefinition(string name, string path)
        {
            Console.WriteLine("// Auto-generated with function PrintClassDefinition");
            Console.WriteLine($"public class {name}(List<FPropertyTag> properties) : StructData\n{{");
            Dictionary<CUE4Parse.UE4.Objects.UObject.FName, FStructFallback> rowMap = FileProvider.LoadDataTable(path);
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

        protected static string? NullCheck(CUE4Parse.UE4.Objects.UObject.FName fName) => fName.IsNone ? null : fName.Text;
    }

    // Auto-generated with function PrintClassDefinition
    public class CharacterData(List<FPropertyTag> properties) : StructData
    {
        public string? OverrideNameTextID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "OverrideNameTextID").Tag!).Value);
        public string? NamePrefixID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "NamePrefixID").Tag!).Value);
        public string? OverridePartnerSkillTextID { get; set; } = NullCheck(((NameProperty)FindProp(properties, "OverridePartnerSkillTextID").Tag!).Value);
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
        public float EnemyMaxHPRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyMaxHPRate").Tag!).Value;
        public float EnemyReceiveDamageRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyReceiveDamageRate").Tag!).Value;
        public float EnemyInflictDamageRate { get; set; } = ((FloatProperty)FindProp(properties, "EnemyInflictDamageRate").Tag!).Value;
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
        public bool IgnoreCombi { get; set; } = ((BoolProperty)FindProp(properties, "IgnoreCombi").Tag!).Value;
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
        // More properties not added...
    }

    public class CagePalData(UAsset asset, StructPropertyData dataTable)
    {
        private readonly UAsset uAsset = asset;
        private readonly StructPropertyData structPropertyData = dataTable;
        public string? FieldName
        {
            get => ((NamePropertyData)structPropertyData.Value[0]).Value?.ToString();
            set => ((NamePropertyData)structPropertyData.Value[0]).Value = value == null ? null : new UAssetAPI.UnrealTypes.FName(uAsset, value);
        }
        public string? PalID
        {
            get => ((NamePropertyData)structPropertyData.Value[1]).Value?.ToString();
            set => ((NamePropertyData)structPropertyData.Value[1]).Value = value == null ? null : new UAssetAPI.UnrealTypes.FName(uAsset, value);
        }
        public float Weight
        {
            get => ((FloatPropertyData)structPropertyData.Value[2]).Value;
            set => ((FloatPropertyData)structPropertyData.Value[2]).Value = value;
        }
        public int MinLevel
        {
            get => ((IntPropertyData)structPropertyData.Value[3]).Value;
            set => ((IntPropertyData)structPropertyData.Value[3]).Value = value;
        }
        public int MaxLevel
        {
            get => ((IntPropertyData)structPropertyData.Value[4]).Value;
            set => ((IntPropertyData)structPropertyData.Value[4]).Value = value;
        }
    }
}
