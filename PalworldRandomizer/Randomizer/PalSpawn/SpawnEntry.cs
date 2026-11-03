using System.Text;

namespace PalworldRandomizer.Randomizer.PalSpawn;

/// <summary>
/// A spawn group containing meta data and a list of character spawn data
/// </summary>
public class SpawnEntry {
    /// <summary>The relative selection weight of this spawn group.</summary>
    public int Weight { get; set; } = 10;

    /// <summary>Whether this spawn group is restricted to nighttime.</summary>
    public bool NightOnly { get; set; } = false;

    /// <summary>The characters spawned together when this group is selected.</summary>
    public List<SpawnData> SpawnList { get; set; } = [];

    /// <summary>Creates a deep copy of the group and its individual character entries.</summary>
    public SpawnEntry Clone() =>
        new() {
            Weight = Weight,
            NightOnly = NightOnly,
            SpawnList = SpawnList.ConvertAll(spawnData => spawnData.Clone()),
        };

    /// <summary>Appends the group metadata and its characters to a text log.</summary>
    public void Print(StringBuilder stringBuilder) {
        PrintInfo(stringBuilder);
        PrintEntries(stringBuilder);
    }

    /// <summary>Appends the group weight and nighttime restriction to a text log.</summary>
    public void PrintInfo(StringBuilder stringBuilder) {
        stringBuilder.AppendJoin(null, ["Weight: ", Weight, (NightOnly ? " (Night)" : "")]);
        stringBuilder.AppendLine();
    }

    /// <summary>Appends the character entries in this group to a text log.</summary>
    public void PrintEntries(StringBuilder stringBuilder) =>
        SpawnList.ForEach(spawnData => spawnData.Print(stringBuilder));

    /// <inheritdoc/>
    public override string? ToString() => string.Join(",", SpawnList);
}