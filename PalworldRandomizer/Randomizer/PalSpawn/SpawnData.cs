using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PalworldRandomizer.Randomizer.PalSpawn;

/// <summary>An individual character entry with its spawn levels, counts and editor presentation.</summary>
public partial class SpawnData : INotifyPropertyChanged {
    /// <summary>Whether the character metadata identifies this entry as a Pal.</summary>
    public bool IsPal => Data.PalData[Name].IsPal;

    /// <summary>The internal character symbol used by the game.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The minimum level of this character spawn.</summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>The maximum level of this character spawn.</summary>
    public int MaxLevel { get; set; } = 1;

    /// <summary>The minimum number of this character spawned by the group.</summary>
    public int MinCount { get; set; } = 1;

    /// <summary>The maximum number of this character spawned by the group.</summary>
    public int MaxCount { get; set; } = 1;

    /// <summary>Raised when an editor-facing property needs to be refreshed.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Notifies editor bindings that the named property has changed.</summary>
    public void NotifyPropertyChanged(string name) => PropertyChanged?.Invoke(this, new(name));

    /// <summary>Appends a readable description of this character spawn to a text log.</summary>
    public void Print(StringBuilder stringBuilder) {
        object[] nameAppend = IsPal ? [(IsBoss ? " {BOSS}" : "")] : [" (", Name, ")"];
        object[] levelStrings = MinLevel == MaxLevel ? [MinLevel] : [MinLevel, "-", MaxLevel];
        object[] countStrings = MinCount == MaxCount ? [MinCount] : [MinCount, "-", MaxCount];
        stringBuilder.AppendJoin(
            null,
            ["  ", ResolvedName, .. nameAppend, " <> Lv. ", .. levelStrings, ", Count: ", .. countStrings]
        );
        stringBuilder.AppendLine();
    }

    /// <summary>Creates a character spawn with the supplied symbol and supported count defaults.</summary>
    public SpawnData() { }

    /// <summary>Creates an independent copy of this character spawn.</summary>
    public SpawnData Clone() =>
        new() {
            Name = Name,
            MinLevel = MinLevel,
            MaxLevel = MaxLevel,
            MinCount = MinCount,
            MaxCount = MaxCount,
        };

    /// <summary>Creates a character spawn with the supplied symbol and supported count defaults.</summary>
    public SpawnData(string characterName, int minSize, int maxSize) {
        Name = characterName;
        MinCount = minSize;
        MaxCount = maxSize;
        MaxLevel = 4;
    }

    /// <summary>Creates a character spawn with the supplied symbol and supported count defaults.</summary>
    public SpawnData(string characterName) {
        Name = characterName;
        MaxLevel = 4;
    }

    [GeneratedRegex(
        "^(?<prefix>(RAID|PREDATOR|SUMMON|Quest(_[^_]+)?)_)?.+?"
            + "(_(?<suffix>([0-9]+(_.+)?|MAX|Oilrig|Otomo|Hand_(Left|Right)|Head|Tower|Quest(_(Friend|Enemy))?"
            + "|BossRush)(_[0-9]+)?))?$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
    )]
    private static partial Regex nameSuffixRegex();

    /// <summary>The readable character name, including relevant variant markers.</summary>
    public string ResolvedName {
        get {
            if (Data.PalData[Name].IsPal) {
                if (Name.EndsWith("_Flower")) {
                    return $"{Data.PalName[Name]}🌺";
                }

                if (Name.StartsWith("POLICE_", StringComparison.OrdinalIgnoreCase)) {
                    return $"{Data.PalName[Name]} ({Data.PalName[Name["POLICE_".Length..]]})";
                }

                Match match = nameSuffixRegex().Match(Name);
                if (match.Groups["prefix"].Value.Length != 0 || match.Groups["suffix"].Value.Length != 0) {
                    return $"{Data.PalName[Name]} ({CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                    (
                        match.Groups["prefix"].Value + match.Groups["suffix"].Value
                    ).Trim('_').Replace('_', ' ').ToLower()).Replace(' ', '-')})";
                }
            }

            return Data.PalName[Name];
        }
    }

    /// <summary>The editor name used to display and select this character.</summary>
    public string SimpleName {
        get => IsPal ? ResolvedName : Name;
        set {
            if (value == null) {
                return;
            }

            bool wasBoss = IsBoss;
            Name = Data.SimpleName[value];
            IsBoss = (IsBoss || wasBoss) && IsPal;
            if (IsBoss != wasBoss) {
                NotifyPropertyChanged(nameof(IsBoss));
            }

            NotifyPropertyChanged(nameof(BossChangeable));
        }
    }

    /// <summary>Whether the character is a boss; setting this selects its corresponding variant.</summary>
    public bool IsBoss {
        get => Data.PalData[Name].IsBoss;
        set {
            if (!Data.PalData[Name].IsPal) {
                return;
            }

            if (value != IsBoss) {
                if (value) {
                    if (Data.BossName.TryGetValue(Name, out string? name)) {
                        Name = name;
                    }
                } else if (Data.PalData.ContainsKey(Name[(Name.IndexOf('_') + 1)..])) {
                    Name = Name[(Name.IndexOf('_') + 1)..];
                }
            }
        }
    }

    /// <summary>The extracted icon path associated with the character symbol.</summary>
    public string IconPath => Data.PalIcon[Name];

    /// <summary>Whether this character can switch between normal and boss variants.</summary>
    public bool BossChangeable =>
        Data.PalData[Name].IsPal
        && !Name.StartsWith("GYM_", StringComparison.OrdinalIgnoreCase)
        && !Name.StartsWith("RAID_", StringComparison.OrdinalIgnoreCase)
        && !Name.StartsWith("PREDATOR_", StringComparison.OrdinalIgnoreCase)
        && (
            IsBoss && Data.PalData.ContainsKey(Name[(Name.IndexOf('_') + 1)..])
            || !IsBoss && Data.BossName.ContainsKey(Name)
        );

    [GeneratedRegex(
        "^((BOSS|GYM|RAID|PREDATOR|SUMMON)_)?(.+?)(_([0-9]+(_.+)?|MAX|Oilrig))?$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex baseNameRegex();

    /// <summary>The base character symbol with supported spawn-variant markers removed.</summary>
    public string BaseName =>
        Name.EndsWith("_Otomo", StringComparison.OrdinalIgnoreCase)
            ? Name
            : baseNameRegex().Match(Name).Groups[3].Value;

    /// <inheritdoc/>
    public override string ToString() => SimpleName;
}