using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PalworldRandomizer.Randomizer.PalSpawn;

public partial class SpawnData : INotifyPropertyChanged {
    public bool IsPal => Data.PalData[Name].IsPal;
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
    public SpawnData Clone() => new() {
        Name = Name,
        MinLevel = MinLevel,
        MaxLevel = MaxLevel,
        MinCount = MinCount,
        MaxCount = MaxCount
    };
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

    [GeneratedRegex("^(?<prefix>(RAID|PREDATOR|SUMMON|Quest(_[^_]+)?)_)?.+?(_(?<suffix>([0-9]+(_.+)?|MAX|Oilrig|Otomo|Hand_(Left|Right)|Head|Tower|Quest(_(Friend|Enemy))?|BossRush)(_[0-9]+)?))?$",
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

    public override string ToString() => SimpleName;
}