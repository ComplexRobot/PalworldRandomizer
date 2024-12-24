using System.Text;
using System.IO;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;

namespace PalworldRandomizer
{
    public static class PalSpawn
    {
        private static readonly byte[] dataSignature = [ 0, 0, 0, 0x80 ];
        // Read in a SheetsVariant/PalSpawner uasset file to parse the binary and store spawn data.
        public static SpawnExportData ReadAsset(UAsset uAsset, int? dataStart = null)
        {
            SpawnExportData spawnExportData = new();
            RawExport rawExport = (RawExport) uAsset.Exports.Find(export => export is RawExport)!;
            int startOffset;
            if (dataStart == null)
            {
                startOffset = rawExport.Data.AsSpan(1).IndexOf(dataSignature);
                if (startOffset == -1)
                {
                    throw new Exception($"{Path.GetFileNameWithoutExtension(uAsset.FilePath)}: Data signature not found.");
                }
            }
            else
            {
                startOffset = dataStart.Value;
            }
            byte[] header = rawExport.Data[..startOffset];
            int count = BitConverter.ToInt32(rawExport.Data, startOffset);
            int position = startOffset + 4;
            int end = position;
            List<SpawnEntry> spawnEntries = [];
            for (int i = 0; i < count;)
            {
                bool skipMinGroupSize = false;
                bool skipMaxGroupSize = false;
                if (rawExport.Data[position] == 0x80)
                {
                    ushort bitFlags = BitConverter.ToUInt16(rawExport.Data, position + 1);
                    if (bitFlags == 0x0409 || bitFlags == 0x130D)
                    {
                        SpawnEntry spawnEntry = new();
                        spawnEntries.Add(spawnEntry);
                        spawnEntry.Weight = BitConverter.ToInt32(rawExport.Data, position + 3);
                        spawnEntry.NightOnly = true;
                        position += 14;
                    }
                    else if (bitFlags == 0x0509 || bitFlags == 0x170D)
                    {
                        SpawnEntry spawnEntry = new();
                        spawnEntries.Add(spawnEntry);
                        spawnEntry.Weight = 0;
                        spawnEntry.NightOnly = true;
                        position += 10;
                    }
                    else if (bitFlags == 0x0609 || bitFlags == 0x1B0D)
                    {
                        SpawnEntry spawnEntry = new();
                        spawnEntries.Add(spawnEntry);
                        spawnEntry.Weight = BitConverter.ToInt32(rawExport.Data, position + 3);
                        position += 13;
                    }
                    else if (bitFlags == 0x0709 || bitFlags == 0x1F0D)
                    {
                        SpawnEntry spawnEntry = new();
                        spawnEntries.Add(spawnEntry);
                        spawnEntry.Weight = 0;
                        position += 9;
                    }
                    else if (bitFlags == 0x100D)
                    {
                        skipMinGroupSize = true;
                        position += 3;
                    }
                    else if (bitFlags == 0x200D)
                    {
                        skipMaxGroupSize = true;
                        position += 3;
                    }
                    else
                    {
                        throw new Exception($"{Path.GetFileNameWithoutExtension(uAsset.FilePath)}: "
                            + $"Unknown value 0x{Convert.ToHexString([.. rawExport.Data[(position + 1)..(position + 3)].Reverse()])} "
                            + $"at offset 0x{Convert.ToHexString([.. BitConverter.GetBytes(position + 1).Reverse()])}.");
                    }
                }
                else
                {
                    position += 2;
                }
                // TODO: Read in length of spawn entry to close the loop
                if (position >= rawExport.Data.Length || (rawExport.Data[position - 1] != 0x0D && !(skipMinGroupSize || skipMaxGroupSize)))
                {
                    break;
                }
                SpawnData spawnData = new();
                spawnEntries[^1].SpawnList.Add(spawnData);
                int nameIndex = 0;
                if (rawExport.Data[position] == 0x00)
                {
                    nameIndex = BitConverter.ToUInt16(rawExport.Data, position + 2);
                }
                else
                {
                    spawnData.IsPal = false;
                    nameIndex = BitConverter.ToUInt16(rawExport.Data, position + 5);
                }
                spawnData.Name = uAsset.GetNameReference(nameIndex).Value;
                position += 13;
                spawnData.MinLevel = BitConverter.ToInt32(rawExport.Data, position);
                position += 4;
                spawnData.MaxLevel = BitConverter.ToInt32(rawExport.Data, position);
                position += 4;
                if (skipMinGroupSize)
                {
                    spawnData.MinCount = 0;
                }
                else
                {
                    spawnData.MinCount = BitConverter.ToInt32(rawExport.Data, position);
                    position += 4;
                }
                if (skipMaxGroupSize)
                {
                    spawnData.MaxCount = 0;
                }
                else
                {
                    spawnData.MaxCount = BitConverter.ToInt32(rawExport.Data, position);
                    position += 4;
                }
                if (position + 1 >= rawExport.Data.Length || (rawExport.Data[position] == 0x80 && rawExport.Data[position + 1] == 0x09))
                {
                    ++i;
                }
                end = position;
            }
            byte[] footer = rawExport.Data[end..];
            spawnExportData.spawnEntries = spawnEntries;
            spawnExportData.header = header;
            spawnExportData.footer = footer;
            return spawnExportData;
        }

        // Convert spawn data into binary data for saving as a uasset using the original uasset file header and footer.
        public static void MutateAsset(UAsset uAsset, SpawnExportData spawnExportData)
        {
            List<byte> bytes = [.. spawnExportData.header, .. BitConverter.GetBytes(spawnExportData.spawnEntries.Count)];
            foreach (SpawnEntry spawnEntry in spawnExportData.spawnEntries)
            {
                bytes.AddRange([0x80, 0x0D]);
                if (spawnEntry.NightOnly && spawnEntry.Weight != 0)
                {
                    bytes.Add(0x13);
                    bytes.AddRange(BitConverter.GetBytes(spawnEntry.Weight));
                    bytes.Add(0x02);
                }
                else if (spawnEntry.NightOnly && spawnEntry.Weight == 0)
                {
                    bytes.Add(0x17);
                    bytes.Add(0x02);
                }
                else if (spawnEntry.Weight != 0)
                {
                    bytes.Add(0x1B);
                    bytes.AddRange(BitConverter.GetBytes(spawnEntry.Weight));
                }
                else if (spawnEntry.Weight == 0)
                {
                    bytes.Add(0x1F);
                }
                bytes.AddRange(BitConverter.GetBytes(spawnEntry.SpawnList.Count));
                foreach (SpawnData spawnData in spawnEntry.SpawnList)
                {
                    int nameIndex = uAsset.AddNameReference(new(spawnData.Name, Encoding.ASCII));
                    if (spawnData.MinCount == 0)
                    {
                        bytes.AddRange([0x80, 0x0D, 0x10]);
                    }
                    else if (spawnData.MaxCount == 0)
                    {
                        bytes.AddRange([0x80, 0x0D, 0x20]);
                    }
                    else
                    {
                        bytes.AddRange([0x00, 0x0D]);
                    }
                    if (spawnData.IsPal)
                    {
                        bytes.AddRange([0x00, 0x03]);
                        bytes.AddRange(BitConverter.GetBytes((ushort) nameIndex));
                        bytes.AddRange([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x03, 0x01]);
                    }
                    else
                    {
                        bytes.AddRange([0x80, 0x03, 0x01, 0x00, 0x03]);
                        bytes.AddRange(BitConverter.GetBytes((ushort) nameIndex));
                        bytes.AddRange([0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
                    }
                    bytes.AddRange(BitConverter.GetBytes(spawnData.MinLevel));
                    bytes.AddRange(BitConverter.GetBytes(spawnData.MaxLevel));
                    if (spawnData.MinCount != 0)
                    {
                        bytes.AddRange(BitConverter.GetBytes(spawnData.MinCount));
                    }
                    if (spawnData.MaxCount != 0 || spawnData.MinCount == 0)
                    {
                        bytes.AddRange(BitConverter.GetBytes(spawnData.MaxCount));
                    }
                }
            }
            bytes.AddRange(spawnExportData.footer);
            ((RawExport) uAsset.Exports.Find(export => export is RawExport)!).Data = [.. bytes];
        }
    }

    // A spawn group containing meta data and a list of character spawn data
    public class SpawnEntry
    {
        public int Weight { get; set; } = 10;
        public bool NightOnly { get; set; } = false;
        public List<SpawnData> SpawnList { get; set; } = [];
        public SpawnEntry Clone()
        {
            return new()
            {
                Weight = Weight,
                NightOnly = NightOnly,
                SpawnList = SpawnList.ConvertAll(spawnData => spawnData.Clone())
            };
        }
        public void Print(StringBuilder stringBuilder)
        {
            PrintInfo(stringBuilder);
            PrintEntries(stringBuilder);
        }
        public void PrintInfo(StringBuilder stringBuilder)
        {
            stringBuilder.AppendJoin(null, ["Weight: ", Weight, (NightOnly ? " (Night)" : "")]);
            stringBuilder.AppendLine();
        }
        public void PrintEntries(StringBuilder stringBuilder)
        {
            SpawnList.ForEach(spawnData => spawnData.Print(stringBuilder));
        }
    }

    public partial class SpawnData : INotifyPropertyChanged
    {
        public bool IsPal { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public int MinLevel { get; set; } = 1;
        public int MaxLevel { get; set; } = 1;
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; } = 1;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged(string name) => PropertyChanged?.Invoke(this, new(name));
        public void Print(StringBuilder stringBuilder)
        {
            object[] nameAppend = IsPal ? [(IsBoss ? " {BOSS}" : "")] : [" (", Name, ")"];
            object[] levelStrings = MinLevel == MaxLevel ? [MinLevel] : [MinLevel, "-", MaxLevel];
            object[] countStrings = MinCount == MaxCount ? [MinCount] : [MinCount, "-", MaxCount];
            stringBuilder.AppendJoin(null,
                ["  ", ResolvedName, .. nameAppend, " <> Lv. ", .. levelStrings, ", Count: ", .. countStrings]);
            stringBuilder.AppendLine();
        }
        public SpawnData() { }
        public SpawnData Clone()
        {
            return new()
            {
                Name = Name,
                IsPal = IsPal,
                MinLevel = MinLevel,
                MaxLevel = MaxLevel,
                MinCount = MinCount,
                MaxCount = MaxCount
            };
        }
        public SpawnData(string characterName, int minSize, int maxSize)
        {
            Name = characterName;
            MinCount = minSize;
            MaxCount = maxSize;
            MaxLevel = 4;
        }

        public SpawnData(string characterName)
        {
            Name = characterName;
            MaxLevel = 4;
        }

        [GeneratedRegex("^(?<prefix>(RAID|PREDATOR|SUMMON)_)?.+?(_(?<suffix>[0-9]+(_.+)?|MAX|Oilrig))?$", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
        private static partial Regex nameSuffixRegex();
        
        public string ResolvedName
        {
            get
            {
                if (Name.EndsWith("_Flower"))
                    return $"{Data.PalName[Name]}🌺";
                Match match = nameSuffixRegex().Match(Name);
                if (match.Groups["prefix"].Value.Length != 0 || match.Groups["suffix"].Value.Length != 0)
                {
                    return $"{Data.PalName[Name]} ({CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                    (
                        match.Groups["prefix"].Value + match.Groups["suffix"].Value
                    ).Trim('_').Replace('_', ' ').ToLower()).Replace(' ', '-')})";
                }
                return Data.PalName[Name];
            }
        }
        public string SimpleName
        {
            get => IsPal ? ResolvedName : Name;
            set
            {
                if (value == null)
                    return;
                bool wasBoss = IsBoss;
                Name = Data.SimpleName[value];
                IsPal = Data.PalData[Name].IsPal;
                IsBoss = (IsBoss || wasBoss) && IsPal;
                if (IsBoss != wasBoss)
                    NotifyPropertyChanged(nameof(IsBoss));
                NotifyPropertyChanged(nameof(BossChangeable));
            }
        }
        public bool IsBoss
        {
            get => Data.PalData[Name].IsBoss;
            set
            {
                if (!Data.PalData[Name].IsPal)
                    return;
                if (value != IsBoss)
                {
                    if (value)
                    {
                        if (Data.BossName.TryGetValue(Name, out var name))
                            Name = name;
                    }
                    else if (Data.PalData.ContainsKey(Name[(Name.IndexOf('_') + 1)..]))
                        Name = Name[(Name.IndexOf('_') + 1)..];
                }
            }
        }
        public string IconPath => Data.PalIcon[Name];
        public bool BossChangeable => Data.PalData[Name].IsPal && !Name.StartsWith("GYM_", StringComparison.InvariantCultureIgnoreCase)
             && !Name.StartsWith("RAID_", StringComparison.InvariantCultureIgnoreCase) && !Name.StartsWith("PREDATOR_", StringComparison.InvariantCultureIgnoreCase)
            && (IsBoss && Data.PalData.ContainsKey(Name[(Name.IndexOf('_') + 1)..]) || !IsBoss && Data.BossName.ContainsKey(Name));

        [GeneratedRegex("^((BOSS|GYM|RAID|PREDATOR|SUMMON)_)?(.+?)(_([0-9]+(_.+)?|MAX|Oilrig))?$", RegexOptions.IgnoreCase)]
        private static partial Regex baseNameRegex();

        public string BaseName => baseNameRegex().Match(Name).Groups[3].Value;
    }

    public class SpawnExportData
    {
        public List<SpawnEntry> spawnEntries = [];
        public byte[] header = [];
        public byte[] footer = [];
    }

    public class ObservableList<T> : ObservableCollection<T>
    {
        public List<T> List => (List<T>) Items;
    }

    public class AreaData(UAsset asset, SpawnExportData exportData, string name)
    {
        public UAsset uAsset = asset;
        public SpawnExportData spawnExportData = exportData;
        public string filename = name;
        public int minLevel = 0;
        public int maxLevel = 0;
        public int minLevelNight = 0;
        public int maxLevelNight = 0;
        public bool modified = false;
        public bool isFieldBoss = false;
        public bool isDungeonBoss = false;
        public bool isDungeon = false;
        public bool isField = false;
        public bool isBoss = false;
        public bool isInDungeon = false;
        public bool isPredator = false;
        private readonly ObservableList<SpawnEntry> virtualEntries = [];

        public AreaData Clone()
        {
            return new(new(), new(), filename)
            {
                minLevel = minLevel,
                maxLevel = maxLevel,
                minLevelNight = minLevelNight,
                maxLevelNight = maxLevelNight,
                modified = modified,
                isFieldBoss = isFieldBoss,
                isDungeonBoss = isDungeonBoss,
                isDungeon = isDungeon,
                isField = isField,
                isBoss = isBoss,
                isInDungeon = isInDungeon,
                isPredator = isPredator,
                uAsset = UAssetData.LoadAsset($"Assets\\{filename}"),
                spawnExportData =
                new()
                {
                    header = [.. spawnExportData.header],
                    footer = [.. spawnExportData.footer],
                    spawnEntries = SpawnEntries.ConvertAll(entry => entry.Clone())
                }
            };
        }

        public int EntriesToShow
        {
            get => virtualEntries.Count;
            set
            {
                if (value == 0)
                {
                    virtualEntries.Clear();
                }
                else if (value > virtualEntries.Count)
                {
                    foreach (SpawnEntry entry in CollectionsMarshal.AsSpan(SpawnEntries)
                        .Slice(virtualEntries.Count, Math.Min(value - virtualEntries.Count, SpawnEntries.Count - virtualEntries.Count)))
                    {
                        virtualEntries.Add(entry);
                    }
                }
                else if (value < virtualEntries.Count)
                {
                    while (virtualEntries.Count > value)
                    {
                        virtualEntries.RemoveAt(virtualEntries.Count - 1);
                    }
                }
            }
        }
        public void Insert(int index, SpawnEntry spawnEntry)
        {
            SpawnEntries.Insert(index, spawnEntry);
            if (EntriesToShow >= index)
            {
                virtualEntries.Insert(index, spawnEntry);
            }
        }
        public void RemoveAt(int index)
        {
            SpawnEntries.RemoveAt(index);
            if (EntriesToShow > index)
            {
                virtualEntries.RemoveAt(index);
            }
        }
        public void Clear()
        {
            SpawnEntries.Clear();
            virtualEntries.Clear();
        }
        public int VirtualCapacity
        {
            get => virtualEntries.List.Capacity;
            set
            {
                if (VirtualCapacity < value)
                    virtualEntries.List.Capacity = value;
            }
        }
        public int Count => SpawnEntries.Count;
        public ObservableCollection<SpawnEntry> SpawnEntriesView => virtualEntries;
        public List<SpawnEntry> SpawnEntries { get => spawnExportData.spawnEntries; set => spawnExportData.spawnEntries = value; }
        public string Name => SimpleName + (modified ? "*" : "");
        public string SimpleName => Path.GetFileNameWithoutExtension(filename)["BP_PalSpawner_Sheets_".Length..];
        public override string ToString() => Name;
    }
}