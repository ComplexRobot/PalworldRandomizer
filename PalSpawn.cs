using System.Text;
using System.IO;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

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
                            spawnEntry.weight = BitConverter.ToInt32(rawExport.Data, position + 3);
                            spawnEntry.nightOnly = true;
                            position += 14;
                        }
                        else if (rawExport.Data[position + 2] == 0x05)
                        {
                            spawnEntry.weight = 0;
                            spawnEntry.nightOnly = true;
                            position += 10;
                        }
                        else if (rawExport.Data[position + 2] == 0x06)
                        {
                            spawnEntry.weight = BitConverter.ToInt32(rawExport.Data, position + 3);
                            position += 13;
                        }
                        else if (rawExport.Data[position + 2] == 0x07)
                        {
                            spawnEntry.weight = 0;
                            position += 9;
                        }
                        else
                        {
                            Console.WriteLine("***ERROR: unknown value 1");
                            break;
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
                            Console.WriteLine("***ERROR: unknown value 2");
                            break;
                        }
                        position += 3;
                    }
                    else
                    {
                        Console.WriteLine("***ERROR: unknown value 3");
                        break;
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
                spawnEntries.Last().spawnList.Add(spawnData);
                int nameIndex = 0;
                if (rawExport.Data[position] == 0x00)
                {
                    nameIndex = BitConverter.ToUInt16(rawExport.Data, position + 2);
                }
                else
                {
                    spawnData.isPal = false;
                    nameIndex = BitConverter.ToUInt16(rawExport.Data, position + 5);
                }
                spawnData.name = uAsset.GetNameReference(nameIndex).Value;
                position += 13;
                spawnData.minLevel = BitConverter.ToUInt32(rawExport.Data, position);
                position += 4;
                spawnData.maxLevel = BitConverter.ToUInt32(rawExport.Data, position);
                position += 4;
                if (skipMinGroupSize)
                {
                    spawnData.minGroupSize = 0;
                }
                else
                {
                    spawnData.minGroupSize = BitConverter.ToUInt32(rawExport.Data, position);
                    position += 4;
                }
                if (skipMaxGroupSize)
                {
                    spawnData.maxGroupSize = 0;
                }
                else
                {
                    spawnData.maxGroupSize = BitConverter.ToUInt32(rawExport.Data, position);
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
                if (spawnEntry.nightOnly && spawnEntry.weight != 0)
                {
                    bytes.Add(0x04);
                    bytes.AddRange(BitConverter.GetBytes(spawnEntry.weight));
                    bytes.Add(0x02);
                }
                else if (spawnEntry.nightOnly && spawnEntry.weight == 0)
                {
                    bytes.Add(0x05);
                    bytes.Add(0x02);
                }
                else if (spawnEntry.weight != 0)
                {
                    bytes.Add(0x06);
                    bytes.AddRange(BitConverter.GetBytes(spawnEntry.weight));
                }
                else if (spawnEntry.weight == 0)
                {
                    bytes.Add(0x07);
                }
                bytes.AddRange(BitConverter.GetBytes(spawnEntry.spawnList.Count));
                foreach (SpawnData spawnData in spawnEntry.spawnList)
                {
                    FString name = new(spawnData.name, Encoding.ASCII);
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
                    if (spawnData.minGroupSize == 0)
                    {
                        bytes.AddRange([0x80, 0x0D, 0x10]);
                    }
                    else if (spawnData.maxGroupSize == 0)
                    {
                        bytes.AddRange([0x80, 0x0D, 0x20]);
                    }
                    else
                    {
                        bytes.AddRange([0x00, 0x0D]);
                    }
                    if (spawnData.isPal)
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
                    bytes.AddRange(BitConverter.GetBytes(spawnData.minLevel));
                    bytes.AddRange(BitConverter.GetBytes(spawnData.maxLevel));
                    if (spawnData.minGroupSize != 0)
                    {
                        bytes.AddRange(BitConverter.GetBytes(spawnData.minGroupSize));
                    }
                    if (spawnData.maxGroupSize != 0)
                    {
                        bytes.AddRange(BitConverter.GetBytes(spawnData.maxGroupSize));
                    }
                }
            }
            bytes.AddRange(spawnExportData.footer);
            ((RawExport) uAsset.Exports.Find(export => export is RawExport)!).Data = [.. bytes];
        }
    }

    public class SpawnEntry
    {
        public int weight { get; set; } = 10;
        public bool nightOnly { get; set; } = false;
        public List<SpawnData> spawnList { get; set; } = [];
        public string Print()
        {
            return PrintInfo() + PrintEntries();
        }
        public string PrintInfo()
        {
            string text = $"Weight: {weight}{(nightOnly ? " (Night)" : "")}";
            Console.WriteLine(text);
            return text + '\n';
        }
        public string PrintEntries()
        {
            string text = "";
            foreach (SpawnData spawnData in spawnList)
            {
                text += spawnData.Print();
            }
            return text;
        }
        public string timeOfDay
        {
            get { return nightOnly ? "🌙" : ""; }
        }
    }

    public class SpawnData
    {
        public bool isPal { get; set; } = true;
        public string name { get; set; } = string.Empty;
        public uint minLevel { get; set; } = 1;
        public uint maxLevel { get; set; } = 1;
        public uint minGroupSize { get; set; } = 1;
        public uint maxGroupSize { get; set; } = 1;
        public string Print()
        {
            string text = $"  {Data.palName[name]}{(name.EndsWith("_Flower") ? "-Flower" : "")}"
                + $"{(isPal ? (isBoss ? " {BOSS}" : "") : $" ({name})")} <> Lv. "
                + (minLevel == maxLevel ? minLevel : $"{minLevel}-{maxLevel}")
                + ", Count: "
                + (minGroupSize == maxGroupSize ? minGroupSize : $"{minGroupSize}-{maxGroupSize}");
            Console.WriteLine(text);
            return text + '\n';
        }
        public SpawnData() {}
        public SpawnData(string characterName, uint minSize, uint maxSize)
        {
            name = characterName;
            minGroupSize = minSize;
            maxGroupSize = maxSize;
            maxLevel = 4;
        }

        public SpawnData(string characterName)
        {
            name = characterName;
            maxLevel = 4;
        }
        public string resolvedName
        {
            get
            {
                return $"{Data.palName[name]}{(name.EndsWith("_Flower") ? "🌺" : "")}{(isBoss ? "😈" : "")}";
            }
        }
        public string simpleName
        {
            get
            {
                if (!isPal)
                    return name;
                return $"{Data.palName[name]}{(name.EndsWith("_Flower") ? "🌺" : "")}";
            }
        }
        public bool isBoss
        {
            get
            {
                return Data.palData[name].IsBoss;
            }
            set
            {
                if (value != isBoss)
                {
                    if (value)
                        name = Data.bossName[name];
                    else
                        name = name[(name.IndexOf('_') + 1)..];
                }
            }
        }
        public string iconPath
        {
            get
            {
                return Data.palIcon[name];
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
        private ObservableCollection<SpawnEntry> virtualEntries = [];
        public int entriesToShow
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
                    spawnEntries[virtualEntries.Count..Math.Min(value, spawnEntries.Count)].ForEach(x => virtualEntries.Add(x));
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
        public ObservableCollection<SpawnEntry> spawnEntriesView
        {
            get { return virtualEntries; }
        }
        public List<SpawnEntry> spawnEntries
        {
            get { return spawnExportData.spawnEntries; }
            set { spawnExportData.spawnEntries = value; }
        }
        public string name
        {
            get { return Path.GetFileNameWithoutExtension(filename)["BP_PalSpawner_Sheets_".Length..] + (modified ? "*" : ""); }
        }
        public string simpleName
        {
            get { return Path.GetFileNameWithoutExtension(filename)["BP_PalSpawner_Sheets_".Length..]; }
        }
        public override string ToString() { return name; }
    }
}