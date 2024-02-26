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

namespace PalworldRandomizer
{
    public static class UAssetData
    {
        private static string? appDataPath;
        private static Usmap? usmap;

        public static void Initialize()
        {
            appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!appDataPath.EndsWith('\\'))
            {
                appDataPath += '\\';
            }
            appDataPath += "Palworld-Randomizer\\";
            Directory.CreateDirectory(appDataPath);
            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(Resource.Resource_resx);
            foreach (XmlNode resource in xmlDoc.DocumentElement!.SelectNodes("data")!)
            {
                string name = resource.Attributes!["name"]!.InnerText;
                if (name == "Resource.resx")
                    continue;
                string filename = AppDataPath(resource.SelectSingleNode("value")!.InnerText[..resource.SelectSingleNode("value")!.InnerText.IndexOf(';')]);
                Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
                if (!File.Exists(filename))
                {
                    File.WriteAllBytes(filename, (byte[]) Resource.ResourceManager.GetObject(name)!);
                }
            }
            usmap = new Usmap(AppDataPath("Mappings.usmap"));
        }

        public static string AppDataPath(string path)
        {
            return appDataPath + path;
        }

        public static UAsset LoadAsset(string filepath)
        {
            return new UAsset(AppDataPath(filepath), EngineVersion.VER_UE5_1, usmap);
        }

        public static UAsset LoadAssetLocal(string filepath)
        {
            return new UAsset(filepath, EngineVersion.VER_UE5_1, usmap);
        }

#if DEBUG
        public static void PrintClassDefinition(string name, string filepath)
        {
            Console.WriteLine($"public class {name}(UAsset asset, StructPropertyData dataTable)\n{{");
            Console.WriteLine("    private UAsset uAsset = asset;");
            Console.WriteLine("    private StructPropertyData structPropertyData = dataTable;");
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
                        Console.WriteLine($"    public string {propertyData.Name.Value.Value}\n    {{");
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
                        Console.WriteLine($"        get {{ return $\"{{(({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value}}\"; }}");
                        Console.WriteLine($"        set {{ (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value = new FName(uAsset, value); }}");
                        break;
                    case "EnumProperty":
                        Console.WriteLine($"        get {{ return $\"{{(({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value}}\"; }}");
                        Console.WriteLine($"        set {{ (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value = FName.DefineDummy(uAsset, value); }}");
                        break;
                    case "StrProperty":
                        Console.WriteLine($"        get {{ return $\"{{(({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value}}\"; }}");
                        Console.WriteLine($"        set {{ (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value = new FString(value, Encoding.ASCII); }}");
                        break;
                    case "BoolProperty":
                    case "IntProperty":
                    case "FloatProperty":
                        Console.WriteLine($"        get {{ return (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value; }}");
                        Console.WriteLine($"        set {{ (({propertyData.PropertyType.Value}Data) structPropertyData.Value[{i}]).Value = value; }}");
                        break;
                    }
                    Console.WriteLine("    }");
                }
                break;
            }
            Console.Write("}");
        }
#endif

        public static Dictionary<string, CharacterData> CreatePalData()
        {
            UAsset palData = LoadAsset("Data\\DT_PalMonsterParameter.uasset");
            UAsset humanData = LoadAsset("Data\\DT_PalHumanParameter.uasset");
            Dictionary<string, CharacterData> data = CreateReferenceDictionary(palData,
                (UAsset asset, StructPropertyData dataTable) => new CharacterData(asset, dataTable));
            foreach (KeyValuePair<string, CharacterData> keyPair in CreateReferenceDictionary(humanData,
                (UAsset asset, StructPropertyData dataTable) => new CharacterData(asset, dataTable)))
            {
                data.Add(keyPair.Key, keyPair.Value);
            }
            return data;
        }
        private static Dictionary<string, T> CreateReferenceDictionary<T>(UAsset uAsset, Func<UAsset, StructPropertyData, T> createFunc)
        {
            Dictionary<string, T> dictionary = [];
            DataTableExport dataTableExport = (DataTableExport) uAsset.Exports[0];
            foreach (StructPropertyData structPropertyData in dataTableExport.Table.Data)
            {
                dictionary.Add(structPropertyData.Name.Value.Value, createFunc(uAsset, structPropertyData));
            }
            return dictionary;
        }
    }

    public class CharacterData(UAsset asset, StructPropertyData dataTable)
    {
        private UAsset uAsset = asset;
        private StructPropertyData structPropertyData = dataTable;
        public string OverrideNameTextID
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[0]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[0]).Value = new FName(uAsset, value); }
        }
        public string NamePrefixID
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[1]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[1]).Value = new FName(uAsset, value); }
        }
        public string OverridePartnerSkillTextID
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[2]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[2]).Value = new FName(uAsset, value); }
        }
        public bool IsPal
        {
            get { return ((BoolPropertyData) structPropertyData.Value[3]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[3]).Value = value; }
        }
        public string Tribe
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[4]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[4]).Value = FName.DefineDummy(uAsset, value); }
        }
        public string BPClass
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[5]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[5]).Value = new FName(uAsset, value); }
        }
        public int ZukanIndex
        {
            get { return ((IntPropertyData) structPropertyData.Value[6]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[6]).Value = value; }
        }
        public string ZukanIndexSuffix
        {
            get { return $"{((StrPropertyData) structPropertyData.Value[7]).Value}"; }
            set { ((StrPropertyData) structPropertyData.Value[7]).Value = new FString(value, Encoding.ASCII); }
        }
        public string Size
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[8]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[8]).Value = FName.DefineDummy(uAsset, value); }
        }
        public int Rarity
        {
            get { return ((IntPropertyData) structPropertyData.Value[9]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[9]).Value = value; }
        }
        public string ElementType1
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[10]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[10]).Value = FName.DefineDummy(uAsset, value); }
        }
        public string ElementType2
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[11]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[11]).Value = FName.DefineDummy(uAsset, value); }
        }
        public string GenusCategory
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[12]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[12]).Value = FName.DefineDummy(uAsset, value); }
        }
        public string Organization
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[13]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[13]).Value = FName.DefineDummy(uAsset, value); }
        }
        public string weapon
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[14]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[14]).Value = FName.DefineDummy(uAsset, value); }
        }
        public bool WeaponEquip
        {
            get { return ((BoolPropertyData) structPropertyData.Value[15]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[15]).Value = value; }
        }
        public int HP
        {
            get { return ((IntPropertyData) structPropertyData.Value[16]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[16]).Value = value; }
        }
        public int MeleeAttack
        {
            get { return ((IntPropertyData) structPropertyData.Value[17]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[17]).Value = value; }
        }
        public int ShotAttack
        {
            get { return ((IntPropertyData) structPropertyData.Value[18]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[18]).Value = value; }
        }
        public int Defense
        {
            get { return ((IntPropertyData) structPropertyData.Value[19]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[19]).Value = value; }
        }
        public int Support
        {
            get { return ((IntPropertyData) structPropertyData.Value[20]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[20]).Value = value; }
        }
        public int CraftSpeed
        {
            get { return ((IntPropertyData) structPropertyData.Value[21]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[21]).Value = value; }
        }
        public float EnemyReceiveDamageRate
        {
            get { return ((FloatPropertyData) structPropertyData.Value[22]).Value; }
            set { ((FloatPropertyData) structPropertyData.Value[22]).Value = value; }
        }
        public float CaptureRateCorrect
        {
            get { return ((FloatPropertyData) structPropertyData.Value[23]).Value; }
            set { ((FloatPropertyData) structPropertyData.Value[23]).Value = value; }
        }
        public float ExpRatio
        {
            get { return ((FloatPropertyData) structPropertyData.Value[24]).Value; }
            set { ((FloatPropertyData) structPropertyData.Value[24]).Value = value; }
        }
        public float Price
        {
            get { return ((FloatPropertyData) structPropertyData.Value[25]).Value; }
            set { ((FloatPropertyData) structPropertyData.Value[25]).Value = value; }
        }
        public string AIResponse
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[26]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[26]).Value = new FName(uAsset, value); }
        }
        public string AISightResponse
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[27]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[27]).Value = new FName(uAsset, value); }
        }
        public int SlowWalkSpeed
        {
            get { return ((IntPropertyData) structPropertyData.Value[28]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[28]).Value = value; }
        }
        public int WalkSpeed
        {
            get { return ((IntPropertyData) structPropertyData.Value[29]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[29]).Value = value; }
        }
        public int RunSpeed
        {
            get { return ((IntPropertyData) structPropertyData.Value[30]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[30]).Value = value; }
        }
        public int RideSprintSpeed
        {
            get { return ((IntPropertyData) structPropertyData.Value[31]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[31]).Value = value; }
        }
        public int TransportSpeed
        {
            get { return ((IntPropertyData) structPropertyData.Value[32]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[32]).Value = value; }
        }
        public bool IsBoss
        {
            get { return ((BoolPropertyData) structPropertyData.Value[33]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[33]).Value = value; }
        }
        public bool IsTowerBoss
        {
            get { return ((BoolPropertyData) structPropertyData.Value[34]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[34]).Value = value; }
        }
        public string BattleBGM
        {
            get { return $"{((EnumPropertyData) structPropertyData.Value[35]).Value}"; }
            set { ((EnumPropertyData) structPropertyData.Value[35]).Value = FName.DefineDummy(uAsset, value); }
        }
        public bool IgnoreLeanBack
        {
            get { return ((BoolPropertyData) structPropertyData.Value[36]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[36]).Value = value; }
        }
        public bool IgnoreBlowAway
        {
            get { return ((BoolPropertyData) structPropertyData.Value[37]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[37]).Value = value; }
        }
        public int MaxFullStomach
        {
            get { return ((IntPropertyData) structPropertyData.Value[38]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[38]).Value = value; }
        }
        public float FullStomachDecreaseRate
        {
            get { return ((FloatPropertyData) structPropertyData.Value[39]).Value; }
            set { ((FloatPropertyData) structPropertyData.Value[39]).Value = value; }
        }
        public int FoodAmount
        {
            get { return ((IntPropertyData) structPropertyData.Value[40]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[40]).Value = value; }
        }
        public int ViewingDistance
        {
            get { return ((IntPropertyData) structPropertyData.Value[41]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[41]).Value = value; }
        }
        public int ViewingAngle
        {
            get { return ((IntPropertyData) structPropertyData.Value[42]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[42]).Value = value; }
        }
        public float HearingRate
        {
            get { return ((FloatPropertyData) structPropertyData.Value[43]).Value; }
            set { ((FloatPropertyData) structPropertyData.Value[43]).Value = value; }
        }
        public bool NooseTrap
        {
            get { return ((BoolPropertyData) structPropertyData.Value[44]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[44]).Value = value; }
        }
        public bool Nocturnal
        {
            get { return ((BoolPropertyData) structPropertyData.Value[45]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[45]).Value = value; }
        }
        public int BiologicalGrade
        {
            get { return ((IntPropertyData) structPropertyData.Value[46]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[46]).Value = value; }
        }
        public bool Predator
        {
            get { return ((BoolPropertyData) structPropertyData.Value[47]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[47]).Value = value; }
        }
        public bool Edible
        {
            get { return ((BoolPropertyData) structPropertyData.Value[48]).Value; }
            set { ((BoolPropertyData) structPropertyData.Value[48]).Value = value; }
        }
        public int Stamina
        {
            get { return ((IntPropertyData) structPropertyData.Value[49]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[49]).Value = value; }
        }
        public int MaleProbability
        {
            get { return ((IntPropertyData) structPropertyData.Value[50]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[50]).Value = value; }
        }
        public int CombiRank
        {
            get { return ((IntPropertyData) structPropertyData.Value[51]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[51]).Value = value; }
        }
        public int WorkSuitability_EmitFlame
        {
            get { return ((IntPropertyData) structPropertyData.Value[52]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[52]).Value = value; }
        }
        public int WorkSuitability_Watering
        {
            get { return ((IntPropertyData) structPropertyData.Value[53]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[53]).Value = value; }
        }
        public int WorkSuitability_Seeding
        {
            get { return ((IntPropertyData) structPropertyData.Value[54]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[54]).Value = value; }
        }
        public int WorkSuitability_GenerateElectricity
        {
            get { return ((IntPropertyData) structPropertyData.Value[55]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[55]).Value = value; }
        }
        public int WorkSuitability_Handcraft
        {
            get { return ((IntPropertyData) structPropertyData.Value[56]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[56]).Value = value; }
        }
        public int WorkSuitability_Collection
        {
            get { return ((IntPropertyData) structPropertyData.Value[57]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[57]).Value = value; }
        }
        public int WorkSuitability_Deforest
        {
            get { return ((IntPropertyData) structPropertyData.Value[58]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[58]).Value = value; }
        }
        public int WorkSuitability_Mining
        {
            get { return ((IntPropertyData) structPropertyData.Value[59]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[59]).Value = value; }
        }
        public int WorkSuitability_OilExtraction
        {
            get { return ((IntPropertyData) structPropertyData.Value[60]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[60]).Value = value; }
        }
        public int WorkSuitability_ProductMedicine
        {
            get { return ((IntPropertyData) structPropertyData.Value[61]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[61]).Value = value; }
        }
        public int WorkSuitability_Cool
        {
            get { return ((IntPropertyData) structPropertyData.Value[62]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[62]).Value = value; }
        }
        public int WorkSuitability_Transport
        {
            get { return ((IntPropertyData) structPropertyData.Value[63]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[63]).Value = value; }
        }
        public int WorkSuitability_MonsterFarm
        {
            get { return ((IntPropertyData) structPropertyData.Value[64]).Value; }
            set { ((IntPropertyData) structPropertyData.Value[64]).Value = value; }
        }
        public string PassiveSkill1
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[65]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[65]).Value = new FName(uAsset, value); }
        }
        public string PassiveSkill2
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[66]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[66]).Value = new FName(uAsset, value); }
        }
        public string PassiveSkill3
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[67]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[67]).Value = new FName(uAsset, value); }
        }
        public string PassiveSkill4
        {
            get { return $"{((NamePropertyData) structPropertyData.Value[68]).Value}"; }
            set { ((NamePropertyData) structPropertyData.Value[68]).Value = new FName(uAsset, value); }
        }
    }
}
