using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalworldRandomizer.Randomizer.PalSpawn;
using PalworldRandomizer.Window;

// Run against a previously built application assembly; no WPF window or game data is needed.
(string Name, Action Run)[] tests =
[
    ("Configuration exposes documented properties instead of public fields", ConfigProperties),
    ("Configuration default JSON remains compatible", ConfigDefaults),
    ("Existing configuration JSON round-trips without losing values", ConfigRoundTrip),
    ("Missing configuration members retain their defaults", ConfigMissingMembers),
    ("Spawn group cloning copies nested characters independently", SpawnClone),
    ("Area cloning preserves metadata and nested spawn data", AreaClone),
    ("Virtual spawn entries remain synchronized after edits", VirtualEntries),
];
int failures = 0;
foreach (var (name, run) in tests) {
    try {
        run();
        Console.WriteLine($"PASS {name}");
    } catch (Exception exception) {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} regression checks passed.");
return failures == 0 ? 0 : 1;

// Rejects the field declarations that trigger CA1051 while keeping the persisted member names.
static void ConfigProperties() {
    var type = typeof(ConfigData);
    string[] names =
    [
        "InstallationDirectory",
        "GameVersion",
        "AssetVersion",
        "AutoReplaceOldFiles",
        "AutoRestoreTemplate",
        "OutputLog",
        "AutoSaveRestoreBackups",
        "AutoSaveGenerationData",
    ];
    Require(
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length == 0,
        "ConfigData still contains public instance fields (CA1051)."
    );
    Require(
        names.All(name => type.GetProperty(name) is { CanRead: true, CanWrite: true }),
        "Every persisted configuration member must remain publicly readable and writable."
    );
}

// Compares the current serializer output with the original configuration field defaults.
static void ConfigDefaults() {
    var expected = JObject.Parse(
        """
        {
            "InstallationDirectory": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Palworld",
            "GameVersion": "0.0.0.0",
            "AssetVersion": 0.0,
            "AutoReplaceOldFiles": true,
            "AutoRestoreTemplate": true,
            "OutputLog": false,
            "AutoSaveRestoreBackups": true,
            "AutoSaveGenerationData": true
        }
        """
    );
    Require(
        JToken.DeepEquals(expected, JObject.FromObject(new ConfigData())),
        "Default configuration JSON changed."
    );
}

// Verifies that files written before the field-to-property change still load and save identically.
static void ConfigRoundTrip() {
    var expected = JObject.Parse(
        """
        {
            "InstallationDirectory": "D:\\Games\\Palworld",
            "GameVersion": "0.6.8",
            "AssetVersion": 11.5,
            "AutoReplaceOldFiles": false,
            "AutoRestoreTemplate": false,
            "OutputLog": true,
            "AutoSaveRestoreBackups": false,
            "AutoSaveGenerationData": false
        }
        """
    );
    var config = JsonConvert.DeserializeObject<ConfigData>(expected.ToString())!;
    Require(JToken.DeepEquals(expected, JObject.FromObject(config)), "Configuration values failed to round-trip.");
}

// Checks compatibility with older configuration files that omit newer settings.
static void ConfigMissingMembers() {
    var config = JsonConvert.DeserializeObject<ConfigData>("{\"GameVersion\":\"older\"}")!;
    Require(
        config.GameVersion == "older"
            && config.AutoReplaceOldFiles
            && config.AutoRestoreTemplate
            && config.AutoSaveRestoreBackups
            && config.AutoSaveGenerationData
            && !config.OutputLog,
        "Missing members no longer retain their original defaults."
    );
}

// Checks that editing a cloned group cannot change the source group's characters.
static void SpawnClone() {
    SpawnEntry original = new()
    {
        Weight = 37,
        NightOnly = true,
        SpawnList =
        [
            new()
            {
                Name = "SheepBall",
                MinLevel = 3,
                MaxLevel = 8,
                MinCount = 2,
                MaxCount = 5,
            },
        ],
    };
    var clone = original.Clone();
    Require(
        clone.Weight == 37 && clone.NightOnly && clone.SpawnList[0].MaxCount == 5,
        "Spawn metadata changed during cloning."
    );
    clone.SpawnList[0].MinLevel = 99;
    clone.SpawnList.Clear();
    Require(original.SpawnList is [{ MinLevel: 3 }], "Cloned groups share mutable character data.");
}

// Checks all area metadata and the deep-copy boundary used by generation backups.
static void AreaClone() {
    AreaData original = new([new() { Weight = 21, SpawnList = [new("SheepBall", 2, 4)] }], "area.uasset")
    {
        MinLevel = 2,
        MaxLevel = 9,
        MinLevelNight = 3,
        MaxLevelNight = 10,
        Modified = true,
        AreaType = AreaType.Egg,
        IsBoss = true,
        IsInDungeon = true,
        IsAllArea = true,
        IsOnlyHumans = true,
        IsSingleSpawn = true,
        EggRespawnTime = 180,
        EggLotteryCooldown = 12,
    };
    var clone = original.Clone();
    Require(
        clone.Filename == original.Filename
            && clone.MinLevel == 2
            && clone.MaxLevel == 9
            && clone.MinLevelNight == 3
            && clone.MaxLevelNight == 10
            && clone.Modified
            && clone.IsBoss
            && clone.IsInDungeon
            && clone.IsAllArea
            && clone.IsOnlyHumans
            && clone.IsSingleSpawn
            && clone.AreaType == AreaType.Egg
            && clone.EggRespawnTime == 180
            && clone.EggLotteryCooldown == 12,
        "Area metadata changed during cloning."
    );
    clone.SpawnEntries[0].SpawnList[0].MinCount = 99;
    Require(original.SpawnEntries[0].SpawnList[0].MinCount == 2, "Cloned areas share mutable spawn data.");
}

// Exercises the collection bookkeeping affected by brace and expression-body style fixes.
static void VirtualEntries() {
    AreaData area = new([new() { Weight = 10 }, new() { Weight = 20 }], "area.uasset") { EntriesToShow = 2 };
    area.Insert(1, new() { Weight = 15 });
    Require(area.Count == 3 && area.SpawnEntriesView[1].Weight == 15, "Insertion did not update the view.");
    area.RemoveAt(1);
    area.EntriesToShow = 1;
    Require(area.Count == 2 && area.SpawnEntriesView.Count == 1, "Removal or view shrinking regressed.");
    area.VirtualCapacity = 8;
    Require(area.VirtualCapacity >= 8, "Virtual capacity did not grow.");
    area.Clear();
    Require(area.Count == 0 && area.SpawnEntriesView.Count == 0, "Clearing left stale view entries.");
}

// Fails the current regression check with a diagnostic message.
static void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
    }
}