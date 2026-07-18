using System.Text;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;

namespace PalworldRandomizer
{
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

        [GeneratedRegex("^(?<prefix>(RAID|PREDATOR|SUMMON|Quest(_[^_]+)?)_)?.+?(_(?<suffix>([0-9]+(_.+)?|MAX|Oilrig|Otomo|Hand_(Left|Right)|Head|Tower|Quest(_(Friend|Enemy))?)(_[0-9]+)?))?$",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
        private static partial Regex nameSuffixRegex();
        
        public string ResolvedName
        {
            get
            {
                if (Data.PalData[Name].IsPal)
                {
                    if (Name.EndsWith("_Flower")) {
                        return $"{Data.PalName[Name]}🌺";
                    }

                    if (Name.StartsWith("POLICE_", StringComparison.OrdinalIgnoreCase)) {
                        return $"{Data.PalName[Name]} ({Data.PalName[Name["POLICE_".Length..]]})";
                    }

                    Match match = nameSuffixRegex().Match(Name);
                    if (match.Groups["prefix"].Value.Length != 0 || match.Groups["suffix"].Value.Length != 0)
                    {
                        return $"{Data.PalName[Name]} ({CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                        (
                            match.Groups["prefix"].Value + match.Groups["suffix"].Value
                        ).Trim('_').Replace('_', ' ').ToLower()).Replace(' ', '-')})";
                    }
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
        public bool BossChangeable => Data.PalData[Name].IsPal && !Name.StartsWith("GYM_", StringComparison.OrdinalIgnoreCase)
             && !Name.StartsWith("RAID_", StringComparison.OrdinalIgnoreCase) && !Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase)
            && (IsBoss && Data.PalData.ContainsKey(Name[(Name.IndexOf('_') + 1)..]) || !IsBoss && Data.BossName.ContainsKey(Name));

        [GeneratedRegex("^((BOSS|GYM|RAID|PREDATOR|SUMMON)_)?(.+?)(_([0-9]+(_.+)?|MAX|Oilrig))?$", RegexOptions.IgnoreCase)]
        private static partial Regex baseNameRegex();

        public string BaseName => Name.EndsWith("_Otomo", StringComparison.OrdinalIgnoreCase) ? Name : baseNameRegex().Match(Name).Groups[3].Value;
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

    public class AreaData(SpawnExportData exportData, string name)
    {
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
        public bool isCage = false;
        public bool isEgg = false;
        public bool isQuest = false;
        public bool isMimic = false;
        public bool isMonsterOnly = false;
        public float eggRespawnTime = 0;
        public float eggLotteryCooldown = 0;
        private readonly ObservableList<SpawnEntry> virtualEntries = [];

        public AreaData Clone()
        {
            return new(new(), filename)
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
                isCage = isCage,
                isEgg = isEgg,
                isQuest = isQuest,
                isMimic = isMimic,
                isMonsterOnly = isMonsterOnly,
                eggRespawnTime = eggRespawnTime,
                eggLotteryCooldown = eggLotteryCooldown,
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
        public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(filename);
        public string SimpleName => isCage ? $"Cage:{filename}"
            : (isEgg ? FileNameWithoutExtension["bp_palmapobjectspawner_".Length..] : FileNameWithoutExtension["BP_PalSpawner_Sheets_".Length..]);
        public override string ToString() => Name;
    }
}