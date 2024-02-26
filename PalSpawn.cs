using System.Text;
using System.IO;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PalworldRandomizer
{
    public static class PalSpawn
    {
        public static SpawnExportData ReadAsset(UAsset uAsset, int? dataStart = null)
        {
            SpawnExportData spawnExportData = new();
            RawExport rawExport = (RawExport) uAsset.Exports.Find(export => export is RawExport)!;
            int startOffset = 0;
            if (dataStart == null)
            {
                byte[] dataSignature = [ 0, 0, 0, 0x80 ];
                for (int i = 1; i < rawExport.Data.Length; ++i)
                {
                    if (Enumerable.SequenceEqual(rawExport.Data[i..(i + 4)], dataSignature))
                    {
                        startOffset = i - 1;
                        break;
                    }
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
                    if (rawExport.Data[position + 1] == 0x09)
                    {
                        SpawnEntry spawnEntry = new();
                        spawnEntries.Add(spawnEntry);
                        if (rawExport.Data[position + 2] == 0x04)
                        {
                            spawnEntry.Weight = BitConverter.ToInt32(rawExport.Data, position + 3);
                            spawnEntry.NightOnly = true;
                            position += 14;
                        }
                        else if (rawExport.Data[position + 2] == 0x05)
                        {
                            spawnEntry.Weight = 0;
                            spawnEntry.NightOnly = true;
                            position += 10;
                        }
                        else if (rawExport.Data[position + 2] == 0x06)
                        {
                            spawnEntry.Weight = BitConverter.ToInt32(rawExport.Data, position + 3);
                            position += 13;
                        }
                        else if (rawExport.Data[position + 2] == 0x07)
                        {
                            spawnEntry.Weight = 0;
                            position += 9;
                        }
                        else
                        {
                            throw new Exception($"{Path.GetFileNameWithoutExtension(uAsset.FilePath)}: "
                                + $"Unknown value 0x{Convert.ToHexString(rawExport.Data[(position + 2)..(position + 3)])} "
                                + $"at offset 0x{Convert.ToHexString(BitConverter.GetBytes(position + 2))}.");
                        }
                    }
                    else if (rawExport.Data[position + 1] == 0x0D)
                    {
                        if (rawExport.Data[position + 2] == 0x10)
                        {
                            skipMinGroupSize = true;
                        }
                        else if (rawExport.Data[position + 2] == 0x20)
                        {
                            skipMaxGroupSize = true;
                        }
                        else
                        {
                            throw new Exception($"{Path.GetFileNameWithoutExtension(uAsset.FilePath)}: "
                                + $"Unknown value 0x{Convert.ToHexString(rawExport.Data[(position + 2)..(position + 3)])} "
                                + $"at offset 0x{Convert.ToHexString(BitConverter.GetBytes(position + 2))}.");
                        }
                        position += 3;
                    }
                    else
                    {
                        throw new Exception($"{Path.GetFileNameWithoutExtension(uAsset.FilePath)}: "
                                + $"Unknown value 0x{Convert.ToHexString(rawExport.Data[(position + 1)..(position + 2)])} "
                                + $"at offset 0x{Convert.ToHexString(BitConverter.GetBytes(position + 1))}.");
                    }
                }
                else
                {
                    position += 2;
                }
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
                spawnData.MinLevel = BitConverter.ToUInt32(rawExport.Data, position);
                position += 4;
                spawnData.MaxLevel = BitConverter.ToUInt32(rawExport.Data, position);
                position += 4;
                if (skipMinGroupSize)
                {
                    spawnData.MinGroupSize = 0;
                }
                else
                {
                    spawnData.MinGroupSize = BitConverter.ToUInt32(rawExport.Data, position);
                    position += 4;
                }
                if (skipMaxGroupSize)
                {
                    spawnData.MaxGroupSize = 0;
                }
                else
                {
                    spawnData.MaxGroupSize = BitConverter.ToUInt32(rawExport.Data, position);
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

        public static void MutateAsset(UAsset uAsset, SpawnExportData spawnExportData)
        {
            List<byte> bytes = [.. spawnExportData.header, .. BitConverter.GetBytes(spawnExportData.spawnEntries.Count)];
            foreach (SpawnEntry spawnEntry in spawnExportData.spawnEntries)
            {
                bytes.AddRange([0x80, 0x09]);
                if (spawnEntry.NightOnly && spawnEntry.Weight != 0)
                {
                    bytes.Add(0x04);
                    bytes.AddRange(BitConverter.GetBytes(spawnEntry.Weight));
                    bytes.Add(0x02);
                }
                else if (spawnEntry.NightOnly && spawnEntry.Weight == 0)
                {
                    bytes.Add(0x05);
                    bytes.Add(0x02);
                }
                else if (spawnEntry.Weight != 0)
                {
                    bytes.Add(0x06);
                    bytes.AddRange(BitConverter.GetBytes(spawnEntry.Weight));
                }
                else if (spawnEntry.Weight == 0)
                {
                    bytes.Add(0x07);
                }
                bytes.AddRange(BitConverter.GetBytes(spawnEntry.SpawnList.Count));
                foreach (SpawnData spawnData in spawnEntry.SpawnList)
                {
                    FString name = new(spawnData.Name, Encoding.ASCII);
                    int nameIndex = 0;
                    if (uAsset.ContainsNameReference(name))
                    {
                        nameIndex = uAsset.SearchNameReference(name);
                    }
                    else
                    {
                        nameIndex = uAsset.GetNameMapIndexList().Count;
                        uAsset.AddNameReference(name);
                    }
                    if (spawnData.MinGroupSize == 0)
                    {
                        bytes.AddRange([0x80, 0x0D, 0x10]);
                    }
                    else if (spawnData.MaxGroupSize == 0)
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
                    if (spawnData.MinGroupSize != 0)
                    {
                        bytes.AddRange(BitConverter.GetBytes(spawnData.MinGroupSize));
                    }
                    if (spawnData.MaxGroupSize != 0)
                    {
                        bytes.AddRange(BitConverter.GetBytes(spawnData.MaxGroupSize));
                    }
                }
            }
            bytes.AddRange(spawnExportData.footer);
            ((RawExport) uAsset.Exports.Find(export => export is RawExport)!).Data = [.. bytes];
        }
    }

    public class SpawnEntry
    {
        public int Weight { get; set; } = 10;
        public bool NightOnly { get; set; }
        public List<SpawnData> SpawnList { get; set; } = [];
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

    public class SpawnData : INotifyPropertyChanged
    {
        public bool IsPal { get; set; } = true;
        public string Name { get; set; } = string.Empty;
        public uint MinLevel { get; set; } = 1;
        public uint MaxLevel { get; set; } = 1;
        public uint MinGroupSize { get; set; } = 1;
        public uint MaxGroupSize { get; set; } = 1;
        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new(name));
        }
        public void Print(StringBuilder stringBuilder)
        {
            object[] nameAppend = IsPal ? [(IsBoss ? " {BOSS}" : "")] : [" (", Name, ")"];
            object[] levelStrings = MinLevel == MaxLevel ? [MinLevel] : [MinLevel, "-", MaxLevel];
            object[] groupStrings = MinGroupSize == MaxGroupSize ? [MinGroupSize] : [MinGroupSize, "-", MaxGroupSize];
            stringBuilder.AppendJoin(null,
                ["  ", Data.PalName[Name], (Name.EndsWith("_Flower") ? "🌺" : ""), .. nameAppend, " <> Lv. ", .. levelStrings, ", Count: ", .. groupStrings]);
            stringBuilder.AppendLine();
        }
        public SpawnData() {}
        public SpawnData(string characterName, uint minSize, uint maxSize)
        {
            Name = characterName;
            MinGroupSize = minSize;
            MaxGroupSize = maxSize;
            MaxLevel = 4;
        }

        public SpawnData(string characterName)
        {
            Name = characterName;
            MaxLevel = 4;
        }
        public string ResolvedName
        {
            get
            {
                return $"{Data.PalName[Name]}{(Name.EndsWith("_Flower") ? "🌺" : "")}";
            }
        }
        public string SimpleName
        {
            get
            {
                if (!IsPal)
                    return Name;
                return ResolvedName;
            }

            set
            {
                if (value == null)
                    return;
                bool wasBoss = IsBoss;
                Name = Data.SimpleName[value];
                IsPal = Data.PalData[Name].IsPal;
                IsBoss = (IsBoss || wasBoss) && IsPal;
                if (IsBoss != wasBoss)
                    OnPropertyChanged(nameof(IsBoss));
            }
        }
        public bool IsBoss
        {
            get
            {
                return Data.PalData[Name].IsBoss;
            }
            set
            {
                if (!Data.PalData[Name].IsPal)
                    return;
                if (value != IsBoss)
                {
                    if (value)
                        Name = Data.BossName[Name];
                    else
                        Name = Name[(Name.IndexOf('_') + 1)..];
                }
            }
        }
        public string IconPath
        {
            get
            {
                return Data.PalIcon[Name];
            }
        }
    }

    public class SpawnExportData
    {
        public List<SpawnEntry> spawnEntries = [];
        public byte[] header = [];
        public byte[] footer = [];
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
        private readonly ObservableCollection<SpawnEntry> virtualEntries = [];
        public int EntriesToShow
        {
            get
            {
                return virtualEntries.Count;
            }
            set
            {
                if (value == 0)
                {
                    virtualEntries.Clear();
                }
                else if (value > virtualEntries.Count)
                {
                    SpawnEntries[virtualEntries.Count..Math.Min(value, SpawnEntries.Count)].ForEach(x => virtualEntries.Add(x));
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
        public ObservableCollection<SpawnEntry> SpawnEntriesView
        {
            get { return virtualEntries; }
        }
        public List<SpawnEntry> SpawnEntries
        {
            get { return spawnExportData.spawnEntries; }
            set { spawnExportData.spawnEntries = value; }
        }
        public string Name
        {
            get { return Path.GetFileNameWithoutExtension(filename)["BP_PalSpawner_Sheets_".Length..] + (modified ? "*" : ""); }
        }
        public string SimpleName
        {
            get { return Path.GetFileNameWithoutExtension(filename)["BP_PalSpawner_Sheets_".Length..]; }
        }
        public override string ToString() { return Name; }
    }
}