using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using UAssetAPI.ExportTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using System.Text;
using System.IO;
using System.Xml;
using PalworldRandomizer.Resources;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace PalworldRandomizer
{
    public class ConfigData
    {
        public float AssetVersion = 0;
        public bool AutoReplaceOldFiles = true;
    }

    public static class UAssetData
    {
        public const float ASSET_VERSION = 1;
        public const string GLOBAL_GONFIG_FILENAME = @"Config\GlobalConfig.json";
        private static string? appDataPath;
        private static Usmap? usmap;

        public static void Initialize()
        {
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!appDataPath.EndsWith('\\'))
            {
                appDataPath += '\\';
            }
            appDataPath += @"Palworld-Randomizer\";
            Directory.CreateDirectory(appDataPath);
            Directory.CreateDirectory(AppDataPath("Config"));
            string configFilePath = AppDataPath(GLOBAL_GONFIG_FILENAME);
            ConfigData config = ((Func<ConfigData>) (() =>
            {
                if (File.Exists(configFilePath))
                {
                    ConfigData? config = JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(configFilePath));
                    if (config != null)
                    {
                        return config;
                    }
                }
                return new();
            }))();
            bool replaceAssets = config.AssetVersion < ASSET_VERSION && config.AutoReplaceOldFiles;
            config.AssetVersion = ASSET_VERSION;
            File.WriteAllText(configFilePath, JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented));
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
        }

        public static string AppDataPath(string path = null!) => appDataPath + path;
        public static UAsset LoadAsset(string filepath) => new(AppDataPath(filepath), EngineVersion.VER_UE5_1, usmap);
        public static UAsset LoadAssetLocal(string filepath) => new(filepath, EngineVersion.VER_UE5_1, usmap);

        public static UAsset PalDataAsset { get; private set; } = null!;
        public static UAsset HumanDataAsset { get; private set; } = null!;
        public static Dictionary<string, CharacterData> CreatePalData()
        {
            PalDataAsset = LoadAsset(@"Data\DT_PalMonsterParameter.uasset");
            HumanDataAsset = LoadAsset(@"Data\DT_PalHumanParameter.uasset");
            return new Collection<KeyValuePair<string, CharacterData>>
                ([.. CreateReferencePairs(PalDataAsset, (asset, dataTable) => new(asset, dataTable)),
                .. CreateReferencePairs(HumanDataAsset, (asset, dataTable) => new(asset, dataTable))]).ToDictionary();
            static List<KeyValuePair<string, CharacterData>> CreateReferencePairs(UAsset uAsset, Func<UAsset, StructPropertyData, CharacterData> createFunc) =>
                ((DataTableExport) uAsset.Exports[0]).Table.Data
                .ConvertAll(structPropertyData => new KeyValuePair<string, CharacterData>($"{structPropertyData.Name}", createFunc(uAsset, structPropertyData)));
        }

#if DEBUG
        public static void PrintClassDefinition(string name, string filepath)
        {
            Console.WriteLine("#pragma warning disable IDE1006");
            Console.WriteLine($"public class {name}(UAsset asset, StructPropertyData dataTable)\n{{");
            Console.WriteLine("    private readonly UAsset uAsset = asset;");
            Console.WriteLine("    private readonly StructPropertyData structPropertyData = dataTable;");
            UAsset uAsset = LoadAssetLocal(filepath);
            DataTableExport dataTableExport = (DataTableExport) uAsset.Exports[0];
            foreach (StructPropertyData structPropertyData in dataTableExport.Table.Data)
            {
                for (int i = 0; i < structPropertyData.Value.Count; ++i)
                {
                    PropertyData propertyData = structPropertyData.Value[i];
                    switch (propertyData.PropertyType.Value)
                    {
                    case "NameProperty":
                    case "EnumProperty":
                    case "StrProperty":
                        Console.WriteLine($"    public string? {propertyData.Name.Value.Value}\n    {{");
                        break;
                    case "BoolProperty":
                        Console.WriteLine($"    public bool {propertyData.Name.Value.Value}\n    {{");
                        break;
                    case "IntProperty":
                        Console.WriteLine($"    public int {propertyData.Name.Value.Value}\n    {{");
                        break;
                    case "FloatProperty":
                        Console.WriteLine($"    public float {propertyData.Name.Value.Value}\n    {{");
                        break;
                    }
                    switch (propertyData.PropertyType.Value)
                    {
                    case "NameProperty":
                    case "EnumProperty":
                    case "StrProperty":
                        Console.WriteLine($"        get => (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value?.ToString();");
                        break;
                    case "BoolProperty":
                    case "IntProperty":
                    case "FloatProperty":
                        Console.WriteLine($"        get => (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value;");
                        break;
                    }
                    Console.Write($"        set => (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value = ");
                    switch (propertyData.PropertyType.Value)
                    {
                    case "NameProperty":
                        Console.WriteLine("value == null ? null : new FName(uAsset, value);");
                        break;
                    case "EnumProperty":
                        Console.WriteLine("value == null ? null : FName.DefineDummy(uAsset, value);");
                        break;
                    case "StrProperty":
                        Console.WriteLine("value == null ? null : new FString(value, Encoding.ASCII);");
                        break;
                    case "BoolProperty":
                    case "IntProperty":
                    case "FloatProperty":
                        Console.WriteLine("value;");
                        break;
                    }
                    Console.WriteLine("    }");
                }
                break;
            }
            Console.WriteLine("}");
            Console.Write("#pragma warning restore IDE1006");
        }
#endif
    }

#pragma warning disable IDE1006
    public class CharacterData(UAsset asset, StructPropertyData dataTable)
    {
        private readonly UAsset uAsset = asset;
        private readonly StructPropertyData structPropertyData = dataTable;
        public string? OverrideNameTextID
        {
            get => ((NamePropertyData) structPropertyData.Value[0]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[0]).Value = value == null ? null : new FName(uAsset, value);
        }
        public string? NamePrefixID
        {
            get => ((NamePropertyData) structPropertyData.Value[1]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[1]).Value = value == null ? null : new FName(uAsset, value);
        }
        public string? OverridePartnerSkillTextID
        {
            get => ((NamePropertyData) structPropertyData.Value[2]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[2]).Value = value == null ? null : new FName(uAsset, value);
        }
        public bool IsPal
        {
            get => ((BoolPropertyData) structPropertyData.Value[3]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[3]).Value = value;
        }
        public string? Tribe
        {
            get => ((EnumPropertyData) structPropertyData.Value[4]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[4]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public string? BPClass
        {
            get => ((NamePropertyData) structPropertyData.Value[5]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[5]).Value = value == null ? null : new FName(uAsset, value);
        }
        public int ZukanIndex
        {
            get => ((IntPropertyData) structPropertyData.Value[6]).Value;
            set => ((IntPropertyData) structPropertyData.Value[6]).Value = value;
        }
        public string? ZukanIndexSuffix
        {
            get => ((StrPropertyData) structPropertyData.Value[7]).Value?.ToString();
            set => ((StrPropertyData) structPropertyData.Value[7]).Value = value == null ? null : new FString(value, Encoding.ASCII);
        }
        public string? Size
        {
            get => ((EnumPropertyData) structPropertyData.Value[8]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[8]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public int Rarity
        {
            get => ((IntPropertyData) structPropertyData.Value[9]).Value;
            set => ((IntPropertyData) structPropertyData.Value[9]).Value = value;
        }
        public string? ElementType1
        {
            get => ((EnumPropertyData) structPropertyData.Value[10]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[10]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public string? ElementType2
        {
            get => ((EnumPropertyData) structPropertyData.Value[11]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[11]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public string? GenusCategory
        {
            get => ((EnumPropertyData) structPropertyData.Value[12]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[12]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public string? Organization
        {
            get => ((EnumPropertyData) structPropertyData.Value[13]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[13]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public string? weapon
        {
            get => ((EnumPropertyData) structPropertyData.Value[14]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[14]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public bool WeaponEquip
        {
            get => ((BoolPropertyData) structPropertyData.Value[15]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[15]).Value = value;
        }
        public int HP
        {
            get => ((IntPropertyData) structPropertyData.Value[16]).Value;
            set => ((IntPropertyData) structPropertyData.Value[16]).Value = value;
        }
        public int MeleeAttack
        {
            get => ((IntPropertyData) structPropertyData.Value[17]).Value;
            set => ((IntPropertyData) structPropertyData.Value[17]).Value = value;
        }
        public int ShotAttack
        {
            get => ((IntPropertyData) structPropertyData.Value[18]).Value;
            set => ((IntPropertyData) structPropertyData.Value[18]).Value = value;
        }
        public int Defense
        {
            get => ((IntPropertyData) structPropertyData.Value[19]).Value;
            set => ((IntPropertyData) structPropertyData.Value[19]).Value = value;
        }
        public int Support
        {
            get => ((IntPropertyData) structPropertyData.Value[20]).Value;
            set => ((IntPropertyData) structPropertyData.Value[20]).Value = value;
        }
        public int CraftSpeed
        {
            get => ((IntPropertyData) structPropertyData.Value[21]).Value;
            set => ((IntPropertyData) structPropertyData.Value[21]).Value = value;
        }
        public float EnemyMaxHPRate
        {
            get => ((FloatPropertyData) structPropertyData.Value[22]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[22]).Value = value;
        }
        public float EnemyReceiveDamageRate
        {
            get => ((FloatPropertyData) structPropertyData.Value[23]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[23]).Value = value;
        }
        public float EnemyInflictDamageRate
        {
            get => ((FloatPropertyData) structPropertyData.Value[24]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[24]).Value = value;
        }
        public float CaptureRateCorrect
        {
            get => ((FloatPropertyData) structPropertyData.Value[25]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[25]).Value = value;
        }
        public float ExpRatio
        {
            get => ((FloatPropertyData) structPropertyData.Value[26]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[26]).Value = value;
        }
        public float Price
        {
            get => ((FloatPropertyData) structPropertyData.Value[27]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[27]).Value = value;
        }
        public float StatusResistUpRate
        {
            get => ((FloatPropertyData) structPropertyData.Value[28]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[28]).Value = value;
        }
        public string? AIResponse
        {
            get => ((NamePropertyData) structPropertyData.Value[29]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[29]).Value = value == null ? null : new FName(uAsset, value);
        }
        public string? AISightResponse
        {
            get => ((NamePropertyData) structPropertyData.Value[30]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[30]).Value = value == null ? null : new FName(uAsset, value);
        }
        public int SlowWalkSpeed
        {
            get => ((IntPropertyData) structPropertyData.Value[31]).Value;
            set => ((IntPropertyData) structPropertyData.Value[31]).Value = value;
        }
        public int WalkSpeed
        {
            get => ((IntPropertyData) structPropertyData.Value[32]).Value;
            set => ((IntPropertyData) structPropertyData.Value[32]).Value = value;
        }
        public int RunSpeed
        {
            get => ((IntPropertyData) structPropertyData.Value[33]).Value;
            set => ((IntPropertyData) structPropertyData.Value[33]).Value = value;
        }
        public int RideSprintSpeed
        {
            get => ((IntPropertyData) structPropertyData.Value[34]).Value;
            set => ((IntPropertyData) structPropertyData.Value[34]).Value = value;
        }
        public int TransportSpeed
        {
            get => ((IntPropertyData) structPropertyData.Value[35]).Value;
            set => ((IntPropertyData) structPropertyData.Value[35]).Value = value;
        }
        public bool IsBoss
        {
            get => ((BoolPropertyData) structPropertyData.Value[36]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[36]).Value = value;
        }
        public bool IsTowerBoss
        {
            get => ((BoolPropertyData) structPropertyData.Value[37]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[37]).Value = value;
        }
        public bool IsRaidBoss
        {
            get => ((BoolPropertyData) structPropertyData.Value[38]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[38]).Value = value;
        }
        public bool UseBossHPGauge
        {
            get => ((BoolPropertyData) structPropertyData.Value[39]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[39]).Value = value;
        }
        public string? BattleBGM
        {
            get => ((EnumPropertyData) structPropertyData.Value[40]).Value?.ToString();
            set => ((EnumPropertyData) structPropertyData.Value[40]).Value = value == null ? null : FName.DefineDummy(uAsset, value);
        }
        public bool IgnoreLeanBack
        {
            get => ((BoolPropertyData) structPropertyData.Value[41]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[41]).Value = value;
        }
        public bool IgnoreBlowAway
        {
            get => ((BoolPropertyData) structPropertyData.Value[42]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[42]).Value = value;
        }
        public int MaxFullStomach
        {
            get => ((IntPropertyData) structPropertyData.Value[43]).Value;
            set => ((IntPropertyData) structPropertyData.Value[43]).Value = value;
        }
        public float FullStomachDecreaseRate
        {
            get => ((FloatPropertyData) structPropertyData.Value[44]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[44]).Value = value;
        }
        public int FoodAmount
        {
            get => ((IntPropertyData) structPropertyData.Value[45]).Value;
            set => ((IntPropertyData) structPropertyData.Value[45]).Value = value;
        }
        public int ViewingDistance
        {
            get => ((IntPropertyData) structPropertyData.Value[46]).Value;
            set => ((IntPropertyData) structPropertyData.Value[46]).Value = value;
        }
        public int ViewingAngle
        {
            get => ((IntPropertyData) structPropertyData.Value[47]).Value;
            set => ((IntPropertyData) structPropertyData.Value[47]).Value = value;
        }
        public float HearingRate
        {
            get => ((FloatPropertyData) structPropertyData.Value[48]).Value;
            set => ((FloatPropertyData) structPropertyData.Value[48]).Value = value;
        }
        public bool NooseTrap
        {
            get => ((BoolPropertyData) structPropertyData.Value[49]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[49]).Value = value;
        }
        public bool Nocturnal
        {
            get => ((BoolPropertyData) structPropertyData.Value[50]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[50]).Value = value;
        }
        public int BiologicalGrade
        {
            get => ((IntPropertyData) structPropertyData.Value[51]).Value;
            set => ((IntPropertyData) structPropertyData.Value[51]).Value = value;
        }
        public bool Predator
        {
            get => ((BoolPropertyData) structPropertyData.Value[52]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[52]).Value = value;
        }
        public bool Edible
        {
            get => ((BoolPropertyData) structPropertyData.Value[53]).Value;
            set => ((BoolPropertyData) structPropertyData.Value[53]).Value = value;
        }
        public int Stamina
        {
            get => ((IntPropertyData) structPropertyData.Value[54]).Value;
            set => ((IntPropertyData) structPropertyData.Value[54]).Value = value;
        }
        public int MaleProbability
        {
            get => ((IntPropertyData) structPropertyData.Value[55]).Value;
            set => ((IntPropertyData) structPropertyData.Value[55]).Value = value;
        }
        public int CombiRank
        {
            get => ((IntPropertyData) structPropertyData.Value[56]).Value;
            set => ((IntPropertyData) structPropertyData.Value[56]).Value = value;
        }
        public int WorkSuitability_EmitFlame
        {
            get => ((IntPropertyData) structPropertyData.Value[57]).Value;
            set => ((IntPropertyData) structPropertyData.Value[57]).Value = value;
        }
        public int WorkSuitability_Watering
        {
            get => ((IntPropertyData) structPropertyData.Value[58]).Value;
            set => ((IntPropertyData) structPropertyData.Value[58]).Value = value;
        }
        public int WorkSuitability_Seeding
        {
            get => ((IntPropertyData) structPropertyData.Value[59]).Value;
            set => ((IntPropertyData) structPropertyData.Value[59]).Value = value;
        }
        public int WorkSuitability_GenerateElectricity
        {
            get => ((IntPropertyData) structPropertyData.Value[60]).Value;
            set => ((IntPropertyData) structPropertyData.Value[60]).Value = value;
        }
        public int WorkSuitability_Handcraft
        {
            get => ((IntPropertyData) structPropertyData.Value[61]).Value;
            set => ((IntPropertyData) structPropertyData.Value[61]).Value = value;
        }
        public int WorkSuitability_Collection
        {
            get => ((IntPropertyData) structPropertyData.Value[62]).Value;
            set => ((IntPropertyData) structPropertyData.Value[62]).Value = value;
        }
        public int WorkSuitability_Deforest
        {
            get => ((IntPropertyData) structPropertyData.Value[63]).Value;
            set => ((IntPropertyData) structPropertyData.Value[63]).Value = value;
        }
        public int WorkSuitability_Mining
        {
            get => ((IntPropertyData) structPropertyData.Value[64]).Value;
            set => ((IntPropertyData) structPropertyData.Value[64]).Value = value;
        }
        public int WorkSuitability_OilExtraction
        {
            get => ((IntPropertyData) structPropertyData.Value[65]).Value;
            set => ((IntPropertyData) structPropertyData.Value[65]).Value = value;
        }
        public int WorkSuitability_ProductMedicine
        {
            get => ((IntPropertyData) structPropertyData.Value[66]).Value;
            set => ((IntPropertyData) structPropertyData.Value[66]).Value = value;
        }
        public int WorkSuitability_Cool
        {
            get => ((IntPropertyData) structPropertyData.Value[67]).Value;
            set => ((IntPropertyData) structPropertyData.Value[67]).Value = value;
        }
        public int WorkSuitability_Transport
        {
            get => ((IntPropertyData) structPropertyData.Value[68]).Value;
            set => ((IntPropertyData) structPropertyData.Value[68]).Value = value;
        }
        public int WorkSuitability_MonsterFarm
        {
            get => ((IntPropertyData) structPropertyData.Value[69]).Value;
            set => ((IntPropertyData) structPropertyData.Value[69]).Value = value;
        }
        public string? PassiveSkill1
        {
            get => ((NamePropertyData) structPropertyData.Value[70]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[70]).Value = value == null ? null : new FName(uAsset, value);
        }
        public string? PassiveSkill2
        {
            get => ((NamePropertyData) structPropertyData.Value[71]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[71]).Value = value == null ? null : new FName(uAsset, value);
        }
        public string? PassiveSkill3
        {
            get => ((NamePropertyData) structPropertyData.Value[72]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[72]).Value = value == null ? null : new FName(uAsset, value);
        }
        public string? PassiveSkill4
        {
            get => ((NamePropertyData) structPropertyData.Value[73]).Value?.ToString();
            set => ((NamePropertyData) structPropertyData.Value[73]).Value = value == null ? null : new FName(uAsset, value);
        }
    }
#pragma warning restore IDE1006
}
