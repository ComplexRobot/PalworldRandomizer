using System.Text;

namespace PalworldRandomizer.Randomizer.PalSpawn;

/// <summary>
/// A spawn group containing meta data and a list of character spawn data
/// </summary>
public class SpawnEntry {
    public int Weight { get; set; } = 10;
    public bool NightOnly { get; set; } = false;
    public List<SpawnData> SpawnList { get; set; } = [];
    public SpawnEntry Clone() => new() {
        Weight = Weight,
        NightOnly = NightOnly,
        SpawnList = SpawnList.ConvertAll(spawnData => spawnData.Clone())
    };
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

    public override string? ToString() => string.Join(",", SpawnList);
}