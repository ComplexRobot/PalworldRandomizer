using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalworldRandomizer.Models;
using PalworldRandomizer.Randomizer.PalSpawn;
using PalworldRandomizer.Serializer;

// Run against a previously built application assembly; no WPF window or game data is needed.
(string Name, Action Run)[] tests =
[
    ("Configuration is available independently of the WPF namespace", PortableConfig),
    ("Configuration exposes documented properties instead of public fields", ConfigProperties),
    ("Configuration default JSON remains compatible", ConfigDefaults),
    ("Existing configuration JSON round-trips without losing values", ConfigRoundTrip),
    ("Missing configuration members retain their defaults", ConfigMissingMembers),
    ("Form settings construct and round-trip without a WPF page", PortableFormRoundTrip),
    ("All 137 original form property names and types are preserved", FormContract),
    ("All original control and mode defaults are preserved", FormDefaults),
    ("Every nondefault form setting round-trips through legacy JSON", FormAllValuesRoundTrip),
    ("Missing form members use portable defaults", FormMissingMembers),
    ("Partial templates retain captured settings instead of DTO defaults", FormPopulate),
    ("Template population preserves full decimal precision", FormPopulatePrecision),
    ("Unknown numeric template members outside decimal range are ignored", FormIgnoredLargeNumber),
    ("Unknown nested template members outside decimal range are ignored", FormIgnoredNestedNumber),
    ("Null templates do not capture or reset current settings", FormNullTemplate),
    ("Template arrays replace captured weights without appending", FormArrayReplacement),
    ("Each settings instance owns an independent weights array", FormArrayIndependence),
    ("Explicit zero false fractional and null template values survive", FormExplicitValues),
    ("Egg respawn arithmetic and negative component clamping are unchanged", FormEggRespawn),
    ("All original mode names and values are preserved", ModeValues),
    ("Generation accepts the portable form settings type", GenerationSettingsContract),
    ("PalSchema export uses explicit settings without a WPF page", PortableSchemaExport),
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
        Console.Error.WriteLine($"FAIL {name}: {exception}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} regression checks passed.");
return failures == 0 ? 0 : 1;

// Requires the configuration DTO at the portable boundary and exercises its default constructor.
static void PortableConfig() {
    var type = typeof(ConfigData).Assembly.GetType("PalworldRandomizer.Models.ConfigData");
    Require(type != null, "UI-independent Models.ConfigData is missing.");
    Require(Activator.CreateInstance(type!) != null, "Portable configuration construction failed.");
}

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
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length == names.Length
            && names.All(name => type.GetProperty(name) is { CanRead: true, CanWrite: true }),
        "Every persisted configuration member must remain publicly readable and writable."
    );
    foreach (string name in names) {
        var expectedType = name switch {
            "InstallationDirectory" or "GameVersion" => typeof(string),
            "AssetVersion" => typeof(float),
            _ => typeof(bool),
        };
        Require(type.GetProperty(name)!.PropertyType == expectedType, $"Configuration type changed for {name}.");
    }
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

// Exercises the real compiled settings type without loading a Windows Desktop runtime or active page.
static void PortableFormRoundTrip() {
    var type = typeof(ConfigData).Assembly.GetType("PalworldRandomizer.Models.FormData");
    Require(type != null, "UI-independent Models.FormData is missing.");
    object settings = Activator.CreateInstance(type!)!;
    type!.GetProperty("RandomSeed")!.SetValue(settings, 8675309);
    type.GetProperty("PredatorDamageCap")!.SetValue(settings, 2.75m);
    var expected = JObject.FromObject(settings);
    object restored = JsonConvert.DeserializeObject(expected.ToString(), type)!;
    Require(JToken.DeepEquals(expected, JObject.FromObject(restored)), "Portable form JSON failed to round-trip.");
}

// Loads the pinned contract captured from the original XAML and constructor, not from the new model.
static JObject OriginalFormFixture() {
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
        "Regression.Fixtures.FormData.original.json"
    )!;
    using StreamReader reader = new(stream);
    return JObject.Parse(reader.ReadToEnd());
}

// Pins the exact persisted member set, CLR types and public read/write access.
static void FormContract() {
    var properties = typeof(FormData).GetProperties(BindingFlags.Public | BindingFlags.Instance);
    var fixture = (JArray)OriginalFormFixture()["properties"]!;
    Require(fixture.Count == 137 && properties.Length == fixture.Count, "The 137-property form contract changed.");
    Require(typeof(FormData).GetFields(BindingFlags.Public | BindingFlags.Instance).Length == 0, "Public form fields.");
    foreach (var entry in fixture) {
        string name = (string)entry["name"]!;
        var property = properties.SingleOrDefault(candidate => candidate.Name == name);
        Require(
            property is { CanRead: true, CanWrite: true } && property.PropertyType.FullName == (string)entry["type"]!,
            $"The persisted contract changed for {name}."
        );
    }
}

// Compares all defaults, including collapsed controls, unchecked radios and unused rarity slots.
static void FormDefaults() {
    var actual = JObject.FromObject(new FormData());
    foreach (var entry in OriginalFormFixture()["properties"]!) {
        string name = (string)entry["name"]!;
        Require(JToken.DeepEquals(entry["default"], actual[name]), $"Original default changed for {name}.");
    }
}

// Builds a fully populated legacy-shaped template with a changed value for every persisted property.
static JObject NondefaultFormJson() {
    JObject result = [];
    foreach (var entry in OriginalFormFixture()["properties"]!) {
        string name = (string)entry["name"]!;
        JToken value = entry["default"]!;
        result[name] = (string)entry["type"]! switch {
            "System.Boolean" => new JValue(!value.Value<bool>()),
            "System.Int32" => new JValue(value.Value<int>() + 111),
            "System.Decimal" => new JValue(value.Value<decimal>() + 0.125m),
            "System.Int32[]" => new JArray(Enumerable.Range(200, 21)),
            "System.String" => new JValue(name switch {
                "WeightCustomMode" => "Weight Sum",
                "OverflowFixMode" => "Scale All",
                "LevelScaleMode" => "Random",
                _ => throw new InvalidOperationException($"Unexpected selector {name}."),
            }),
            _ => throw new InvalidOperationException($"Unexpected property type for {name}."),
        };
    }

    return result;
}

// Exercises nondefault values for every property, including all array slots and hidden settings.
static void FormAllValuesRoundTrip() {
    var expected = NondefaultFormJson();
    var settings = JsonConvert.DeserializeObject<FormData>(expected.ToString())!;
    Require(JToken.DeepEquals(expected, JObject.FromObject(settings)), "A nondefault form value was lost.");
}

// Older portable templates use original defaults for omitted properties and ignore unknown future members.
static void FormMissingMembers() {
    var settings = JsonConvert.DeserializeObject<FormData>(
        """{"RandomSeed":77,"RandomizeField":false,"FutureSetting":"ignored"}"""
    )!;
    var expected = JObject.FromObject(new FormData());
    expected["RandomSeed"] = 77;
    expected["RandomizeField"] = false;
    Require(JToken.DeepEquals(expected, JObject.FromObject(settings)), "Missing form defaults changed.");
}

// Exercises the same Populate path used by the WPF boundary after capturing its current controls.
static void FormPopulate() {
    var expected = NondefaultFormJson();
    var captured = expected.ToObject<FormData>()!;
    Require(
        ReferenceEquals(captured, TemplateJson.Load("""{"RandomSeed":77,"FutureSetting":true}""", () => captured)),
        "Template population did not use the captured settings."
    );
    expected["RandomSeed"] = 77;
    Require(JToken.DeepEquals(expected, JObject.FromObject(captured)), "Partial template reset a captured value.");
}

// Guards against a token-buffering step rounding a template decimal through a double.
static void FormPopulatePrecision() {
    const string json = """{"HumanDamageCap":0.1234567890123456789012345678}""";
    var expected = JsonConvert.DeserializeObject<FormData>(json)!;
    UnderTemplateCultures(() => {
        FormData captured = new();
        TemplateJson.Load(json, () => captured);
        Require(captured.HumanDamageCap == expected.HumanDamageCap, "Template population rounded a decimal setting.");
    });
}

// Checks numeric JSON under decimal-point and decimal-comma cultures without leaking process state.
static void UnderTemplateCultures(Action check) {
    var original = CultureInfo.CurrentCulture;
    try {
        foreach (string culture in new[] { "en-US", "de-DE", "fr-FR" }) {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            check();
        }
    } finally {
        CultureInfo.CurrentCulture = original;
    }
}

// Pins the old target-typed loading behavior while requiring omitted values to retain the current capture.
static void RequireIgnoredTemplateMember(string json) =>
    UnderTemplateCultures(() => {
        var original = JsonConvert.DeserializeObject<FormData>(json)!;
        Require(original.RandomSeed == 77, "The original target-typed path rejected an ignored future setting.");
        var expected = NondefaultFormJson();
        var captured = expected.ToObject<FormData>()!;
        TemplateJson.Load(json, () => captured);
        expected["RandomSeed"] = 77;
        Require(JToken.DeepEquals(expected, JObject.FromObject(captured)), "Ignored future setting changed a capture.");
    });

// Unknown values need not fit the decimal type used by known settings.
static void FormIgnoredLargeNumber() =>
    RequireIgnoredTemplateMember("""{"RandomSeed":77,"FutureSetting":1e100}""");

// Skipping an unknown object must also skip numbers in its nested objects and arrays.
static void FormIgnoredNestedNumber() =>
    RequireIgnoredTemplateMember("""{"RandomSeed":77,"FutureSetting":{"nested":1e100,"array":[-1e100]}}""");

// A JSON null must remain a failed load and must not access controls merely to produce that result.
static void FormNullTemplate() {
    Require(JsonConvert.DeserializeObject<FormData>("null") == null, "The original null-template result changed.");
    Require(
        TemplateJson.Load(
            "null",
            () => throw new InvalidOperationException("A null template captured controls.")
        ) == null,
        "A null template should not become default settings."
    );
}

// Arrays were replaced during deserialization; populating a captured DTO must preserve that behavior.
static void FormArrayReplacement() {
    var captured = NondefaultFormJson().ToObject<FormData>()!;
    TemplateJson.Load("""{"WeightCustom":[9,8,7]}""", () => captured);
    Require(captured.WeightCustom.SequenceEqual([9, 8, 7]), "Template weights appended to the captured array.");
}

// Default construction and empty deserialization must allocate separate mutable arrays.
static void FormArrayIndependence() {
    FormData first = new();
    FormData second = new();
    var loaded = JsonConvert.DeserializeObject<FormData>("{}")!;
    first.WeightCustom[1] = -1;
    loaded.WeightCustom[2] = -2;
    Require(
        second.WeightCustom[1] == 60 && second.WeightCustom[2] == 60 && loaded.WeightCustom[1] == 60,
        "Settings instances share the default rarity weights."
    );
}

// Keeps explicit values distinct from omitted settings, including legacy nulls and shortened arrays.
static void FormExplicitValues() {
    var settings = JsonConvert.DeserializeObject<FormData>(
        """
        {"PredatorChance":0,"RandomizeField":false,"HumanDamageCap":0.125,"WeightCustom":null,"LevelScaleMode":null}
        """
    )!;
    var json = JObject.FromObject(settings);
    Require(
        settings.PredatorChance == 0 && !settings.RandomizeField && settings.HumanDamageCap == 0.125m
            && json["WeightCustom"]!.Type == JTokenType.Null && json["LevelScaleMode"]!.Type == JTokenType.Null,
        "Explicit zero, false, fraction or null values were replaced with defaults."
    );
}

// Pins the existing arithmetic, including clamping each negative component separately.
static void FormEggRespawn() {
    Require(new FormData().EggRespawnTime() == 180f, "The default egg respawn time changed.");
    FormData settings = new() { EggRespawnHours = 1, EggRespawnMinutes = 2, EggRespawnSeconds = 30 };
    Require(settings.EggRespawnTime() == 62.5f, "Egg respawn units changed.");
    settings.EggRespawnHours = -1;
    settings.EggRespawnMinutes = -2;
    Require(settings.EggRespawnTime() == 0.5f, "Negative components are not clamped independently.");
    settings.EggRespawnSeconds = -30;
    Require(settings.EggRespawnTime() == 0, "Negative egg timers are not clamped.");
}

// Mode labels are persisted strings; pin every original public field name and value.
static void ModeValues() {
    foreach (var mode in (JObject)OriginalFormFixture()["modes"]!) {
        var type = typeof(FormData).Assembly.GetType($"PalworldRandomizer.Window.{mode.Key}")!;
        var actual = new JObject(type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(
            field => new JProperty(field.Name, field.GetValue(null))
        ));
        Require(JToken.DeepEquals(mode.Value, actual), $"Persisted mode values changed for {mode.Key}.");
    }
}

// Prevents generation from continuing to consume the removed control-dependent snapshot type.
static void GenerationSettingsContract() {
    var method = typeof(PalworldRandomizer.Randomizer.Randomize).GetMethod("GeneratePalSpawns")!;
    Require(method.GetParameters().Single().ParameterType == typeof(FormData), "Generation uses the old UI model.");
}

// Verifies that export receives the captured settings instead of constructing a control-dependent snapshot.
static void PortableSchemaExport() {
    var method = typeof(FileModify).GetMethod("GeneratePalSchema", [typeof(List<AreaData>), typeof(FormData)]);
    Require(method != null, "PalSchema export cannot accept UI-independent settings.");
    FormData settings = new() { EggRespawnHours = 1, EggRespawnMinutes = 2, EggRespawnSeconds = 30 };
    List<AreaData> areas = [new([], "egg.uasset") { AreaType = AreaType.Egg }];
    var result = (List<FileModify.PalSchemaJson>)method!.Invoke(null, [areas, settings])!;
    var egg = JObject.Parse(result.Single().JsonData)["egg_C"]!;
    Require((float)egg["RespawnTimeMinutesObtained"]! == 62.5f, "Export ignored the supplied egg timer.");
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