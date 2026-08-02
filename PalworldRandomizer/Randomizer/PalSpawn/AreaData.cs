using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;

namespace PalworldRandomizer.Randomizer.PalSpawn;

public class AreaData(List<SpawnEntry> spawnEntries, string name) {
    public List<SpawnEntry> SpawnEntries { get; set; } = spawnEntries;
    /// <summary>The filename used for the area. (Usually BP_&lt;...&gt;.uasset)</summary>
    public string Filename { get; set; } = name;
    /// <summary>The average minimum level among all spawns in the area.</summary>
    public int MinLevel { get; set; } = 0;
    /// <summary>The average maximum level among all spawns in the area.</summary>
    public int MaxLevel { get; set; } = 0;
    /// <summary>The average minimum level among all nighttime spawns in the area.</summary>
    public int MinLevelNight { get; set; } = 0;
    /// <summary>The average maximum level among all nighttime spawns in the area.</summary>
    public int MaxLevelNight { get; set; } = 0;
    /// <summary><see langword="true"/> if the area has been changed from the original vanilla area.</summary>
    public bool Modified { get; set; } = false;
    /// <summary>The type of area. Pal spawner, egg, cage, etc.</summary>
    public AreaType AreaType { get; set; } = AreaType.Undefined;
    /// <summary>The area is an overworld boss spawn.</summary>
    public bool IsFieldBoss => IsBoss && !IsInDungeon;
    /// <summary>The area is a dungeon boss spawn.</summary>
    public bool IsDungeonBoss => IsBoss && IsInDungeon;
    /// <summary>The area is a dungeon non-boss spawn.</summary>
    public bool IsDungeon => !IsBoss && IsInDungeon;
    /// <summary>The area is an overworld non-boss spawn.</summary>
    public bool IsField => !IsBoss && !IsInDungeon;
    /// <summary>The area is a boss spawn of any kind.</summary>
    public bool IsBoss { get; set; } = false;
    /// <summary>The area is a dungeon spawn of any kind.</summary>
    public bool IsInDungeon { get; set; } = false;
    /// <summary>The area is a pal spawn of any kind.</summary>
    public bool IsPal => AreaType is AreaType.Pal;
    /// <summary>The area is a cage spawn in an enemy camp.</summary>
    public bool IsCage => AreaType is AreaType.Cage;
    /// <summary>The area is an overworld egg spawn.</summary>
    public bool IsEgg => AreaType is AreaType.Egg;
    /// <summary>The area is a human boss mono spawn.</summary>
    public bool IsHumanBossMono => AreaType is AreaType.HumanBossMono;
    /// <summary>The area is a human boss squad spawn.</summary>
    public bool IsHumanBossSquad => AreaType is AreaType.HumanBossSquad;
    /// <summary>Named "allarea" - contains spawn points all over the map.</summary>
    public bool IsAllArea { get; set; } = false;
    /// <summary>The spawn list contains only humans.</summary>
    public bool IsOnlyHumans { get; set; } = false;
    /// <summary>Contains only a single spawn group or species.</summary>
    public bool IsSingleSpawn { get; set; } = false;
    /// <summary>The respawn time for an egg spawn in minutes.</summary>
    public float EggRespawnTime { get; set; } = 0;
    /// <summary>Unknown.</summary>
    public float EggLotteryCooldown { get; set; } = 0;

    private readonly ObservableList<SpawnEntry> _virtualEntries = [];

    /// <summary>
    /// Make a deep-copy clone of the area.
    /// </summary>
    public AreaData Clone() => new([], Filename) {
        MinLevel = MinLevel,
        MaxLevel = MaxLevel,
        MinLevelNight = MinLevelNight,
        MaxLevelNight = MaxLevelNight,
        Modified = Modified,
        AreaType = AreaType,
        IsBoss = IsBoss,
        IsInDungeon = IsInDungeon,
        IsAllArea = IsAllArea,
        IsOnlyHumans = IsOnlyHumans,
        IsSingleSpawn = IsSingleSpawn,
        EggRespawnTime = EggRespawnTime,
        EggLotteryCooldown = EggLotteryCooldown,
        SpawnEntries = SpawnEntries.ConvertAll(entry => entry.Clone()),
    };

    public int EntriesToShow
    {
        get => _virtualEntries.Count;
        set
        {
            if (value == 0)
            {
                _virtualEntries.Clear();
            }
            else if (value > _virtualEntries.Count)
            {
                foreach (SpawnEntry entry in CollectionsMarshal.AsSpan(SpawnEntries)
                    .Slice(_virtualEntries.Count, Math.Min(value - _virtualEntries.Count, SpawnEntries.Count - _virtualEntries.Count)))
                {
                    _virtualEntries.Add(entry);
                }
            }
            else if (value < _virtualEntries.Count)
            {
                while (_virtualEntries.Count > value)
                {
                    _virtualEntries.RemoveAt(_virtualEntries.Count - 1);
                }
            }
        }
    }
    public void Insert(int index, SpawnEntry spawnEntry)
    {
        SpawnEntries.Insert(index, spawnEntry);
        if (EntriesToShow >= index)
        {
            _virtualEntries.Insert(index, spawnEntry);
        }
    }
    public void RemoveAt(int index)
    {
        SpawnEntries.RemoveAt(index);
        if (EntriesToShow > index)
        {
            _virtualEntries.RemoveAt(index);
        }
    }
    public void Clear()
    {
        SpawnEntries.Clear();
        _virtualEntries.Clear();
    }
    public int VirtualCapacity
    {
        get => _virtualEntries.List.Capacity;
        set
        {
            if (VirtualCapacity < value)
                _virtualEntries.List.Capacity = value;
        }
    }
    public int Count => SpawnEntries.Count;
    public ObservableCollection<SpawnEntry> SpawnEntriesView => _virtualEntries;
    public string Name => SimpleName + (Modified ? "*" : "");
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(Filename);
    /// <summary>Filename simplified for the UI.</summary>
    public string SimpleName => AreaType switch {
        AreaType.Cage => $"Cage:{Filename}",
        AreaType.Egg => FileNameWithoutExtension["bp_palmapobjectspawner_".Length..],
        AreaType.Pal => FileNameWithoutExtension["BP_PalSpawner_Sheets_".Length..],
        AreaType.HumanBossMono => $"BossMono:{FileNameWithoutExtension["BP_MonoNPCSpawnerBossBase_".Length..]}",
        AreaType.HumanBossSquad => $"BossSquad:{FileNameWithoutExtension["BP_SquadNPCSpawnerBossBase_".Length..]}",
        var x => throw new Exception($"Unhandled area type '{x}'"),
    };
    public override string ToString() => Name;
}

/// <summary>
/// The spawn type of the area.
/// </summary>
public enum AreaType {
    /// <summary>Unknown type.</summary>
    Undefined,
    /// <summary>Pal spawner.</summary>
    Pal,
    /// <summary>Cage in an enemy camp.</summary>
    Cage,
    /// <summary>Overworld egg spawner.</summary>
    Egg,
    /// <summary>Overworld human boss spawn with solo human and optionally a pal.</summary>
    HumanBossMono,
    /// <summary>Overworld human boss spawn with 3 humans (boss + 2 adds).</summary>
    HumanBossSquad,
}

public class ObservableList<T> : ObservableCollection<T> {
    public List<T> List => (List<T>) Items;
}