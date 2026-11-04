using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PalworldRandomizer.Models;
using PalworldRandomizer.Serializer;

var assembly = typeof(FormData).Assembly;
int passed = 0;
try {
    string[] resources = assembly.GetManifestResourceNames();
    Check(resources.Contains("PalworldRandomizer.Metadata.RandomizerSettings.yaml")
        && resources.Contains("PalworldRandomizer.Metadata.Locales.en-US.Randomizer.yaml"),
        "catalog and English locale are compiled resources");
    var catalog = SettingsCatalog.LoadEmbedded();
    Check(catalog.Definitions.All(x => x.DescriptionOrigin is "Reference" or "Implementation")
        && catalog.Definitions.SelectMany(x => x.Options).All(x =>
            x.DescriptionOrigin is "Reference" or "Implementation"),
        "description provenance uses neutral source categories");
    Check(catalog.Definitions.Count == 137, "production loader covers all 137 original members");
    var properties = typeof(FormData).GetProperties();
    Check(properties.Select(x => x.Name).Order().SequenceEqual(catalog.Definitions.Select(x => x.Name).Order()),
        "canonical members have no missing names, extras or duplicates");
    var defaults = new FormData();
    foreach (var definition in catalog.Definitions) {
        var property = typeof(FormData).GetProperty(definition.Name)!;
        object? expected = property.GetValue(defaults);
        bool same = expected is int[] array
            ? definition.DefaultValue is IReadOnlyList<int> actual && actual.SequenceEqual(array)
            : Equals(expected, definition.DefaultValue);
        Require(definition.Type.ToString() == property.PropertyType.Name.Replace("[]", "Array") && same,
            $"canonical type/default: {definition.Name}");
    }

    Check(true, "all 137 original CLR types and cold defaults are unchanged");
    foreach (var definition in catalog.Definitions) {
        Require(catalog.English.ContainsKey(definition.LabelKey)
            && catalog.English.ContainsKey(definition.DescriptionKey), definition.Name);
    }

    Check(true, "all 137 labels and primary descriptions resolve from compiled English");
    using var fixtureStream = typeof(Program).Assembly.GetManifestResourceStream(
        "Metadata.Original.json")!;
    using var fixture = JsonDocument.Parse(fixtureStream);
    foreach (var expected in fixture.RootElement.EnumerateArray()) {
        string name = expected.GetProperty("Name").GetString()!;
        var definition = catalog.Definitions.Single(x => x.Name == name);
        string status = expected.GetProperty("OriginalDescriptionStatus").GetString()!;
        Require(definition.OriginalDescriptionStatus == status, name);
        Require(catalog.English[definition.LabelKey] == expected.GetProperty("Label").GetString(), name);
        string? hash = expected.GetProperty("ReferenceSha256").GetString();
        if (hash is not null) {
            var keys = definition.AdditionalDescriptionKeys.Prepend(definition.DescriptionKey);
            Require(keys.Any(key => Hash(catalog.English[key]) == hash), "exact Reference paragraph: " + name);
        }

        string origin = status is "verified" or "shared-range" ? "Reference" : "Implementation";
        Require(definition.DescriptionOrigin == origin && definition.DescriptionReferences.Count > 0, name);
    }

    Check(true, "111 verified/shared paragraphs are exact; all 26 additions have implementation provenance");
    Check(catalog.Definitions.Count(x => x.OriginalDescriptionStatus == "verified") == 67
        && catalog.Definitions.Count(x => x.OriginalDescriptionStatus == "shared-range") == 44
        && catalog.Definitions.Count(x => x.OriginalDescriptionStatus == "missing") == 20
        && catalog.Definitions.Count(x => x.OriginalDescriptionStatus == "ambiguous") == 6,
        "per-status original source coverage is 67/44/20/6");

    foreach (var definition in catalog.Definitions) {
        Require(definition.GetType().GetProperty("Controls")?.GetValue(definition)
            is System.Collections.ICollection { Count: > 0 }, "source control metadata: " + definition.Name);
        Require(definition.GetType().GetProperty("CategoryKey")?.GetValue(definition)
            is string key && catalog.English.ContainsKey(key), "localized category: " + definition.Name);
        var expected = fixture.RootElement.EnumerateArray().Single(x =>
            x.GetProperty("Name").GetString() == definition.Name);
        foreach (string property in new[] { "Category", "RadioGroup", "CompoundGroup", "Options", "Controls" }) {
            object? actual = definition.GetType().GetProperty(property)?.GetValue(definition);
            Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(actual), expected.GetProperty(property)),
                $"audited {property}: {definition.Name}");
        }
    }

    Require(catalog.GetType().GetProperty("RadioGroups")?.GetValue(catalog)
        is System.Collections.ICollection { Count: 3 }, "three original radio groups");
    Check(true, "original control constraints, relationships and categories are available");
    var referencedKeys = new HashSet<string>();
    foreach (var definition in catalog.Definitions) {
        referencedKeys.UnionWith([definition.LabelKey, definition.DescriptionKey, definition.CategoryKey]);
        referencedKeys.UnionWith(definition.AdditionalDescriptionKeys);
        foreach (var option in definition.Options) {
            referencedKeys.UnionWith([option.LabelKey, option.DescriptionKey]);
        }

        foreach (var control in definition.Controls) {
            referencedKeys.UnionWith(control.LabelKeys);
        }
    }

    Check(catalog.English.Count == 457 && referencedKeys.SetEquals(catalog.English.Keys)
        && catalog.English.Values.All(x => !string.IsNullOrWhiteSpace(x)),
        "all 457 English keys are nonempty and referenced, with no unresolved nested keys");
    foreach (var group in catalog.RadioGroups) {
        var members = catalog.Definitions.Where(x => x.RadioGroup == group.Id).ToArray();
        Require(group.Members.SequenceEqual(members.Select(x => x.Name)), group.Id);
        Require(members.All(x => x.Type == SettingValueType.Boolean)
            && members.Count(x => Equals(x.DefaultValue, true)) == 1
            && members.Single(x => Equals(x.DefaultValue, true)).Name == group.DefaultMember, group.Id);
    }

    Check(catalog.Definitions.Sum(x => x.Controls.Count) == 147,
        "147 original controls and three radio-group default contracts are consistent");
    RequireReadOnly(catalog.Definitions);
    RequireReadOnly(catalog.RadioGroups);
    Require(((IDictionary<string, string>)catalog.English).IsReadOnly, "English dictionary is read-only");
    try {
        ((IDictionary<string, string>)catalog.English).Add("test.mutation", "not allowed");
        throw new InvalidOperationException("English dictionary accepted mutation");
    } catch (NotSupportedException) {
        // Expected: callers cannot mutate compiled locale values.
    }

    foreach (var group in catalog.RadioGroups) {
        RequireReadOnly(group.Members);
    }

    foreach (var definition in catalog.Definitions) {
        RequireReadOnly(definition.AdditionalDescriptionKeys);
        RequireReadOnly(definition.DescriptionReferences);
        RequireReadOnly(definition.Options);
        RequireReadOnly(definition.Controls);
        if (definition.DefaultValue is IReadOnlyList<int> array) {
            RequireReadOnly(array);
        }

        foreach (var option in definition.Options) {
            RequireReadOnly(option.DescriptionReferences);
        }

        foreach (var control in definition.Controls) {
            RequireReadOnly(control.LabelKeys);
            RequireReadOnly(control.Visibility);
            foreach (var layer in control.Visibility) {
                RequireReadOnly(layer.Triggers);
                foreach (var trigger in layer.Triggers) {
                    RequireReadOnly(trigger.Conditions);
                }
            }
        }
    }

    Check(true, "catalog collections, nested evidence and default arrays reject caller mutation");
    var caller = new FormData { MethodFull = true, MethodNone = true };
    int[] callerArray = (int[])properties.First(x => x.PropertyType == typeof(int[])).GetValue(caller)!;
    callerArray[0] = 123456;
    string before = JsonSerializer.Serialize(caller);
    var reloaded = SettingsCatalog.LoadEmbedded();
    foreach (var definition in catalog.Definitions.Where(x => x.Type == SettingValueType.Int32Array)) {
        object other = reloaded.Definitions.Single(x => x.Name == definition.Name).DefaultValue;
        Require(!ReferenceEquals(definition.DefaultValue, other)
            && ((IReadOnlyList<int>)definition.DefaultValue).SequenceEqual((IReadOnlyList<int>)other), definition.Name);
    }

    Check(JsonSerializer.Serialize(caller) == before,
        "reloads have independent default arrays and do not repair or mutate caller settings");
    return 0;
} catch (Exception exception) {
    Console.Error.WriteLine("FAIL " + exception.Message);
    return 1;
} finally {
    Console.WriteLine($"{passed} metadata checks passed.");
}

void Check(bool condition, string message) {
    Require(condition, message);
    passed++;
    Console.WriteLine("PASS " + message);
}

static void Require(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
    }
}

static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

static void RequireReadOnly<T>(IReadOnlyList<T> values) {
    var list = (IList<T>)values;
    Require(list.IsReadOnly, "collection must be read-only");
    try {
        list.Add(default!);
        throw new InvalidOperationException("collection accepted mutation");
    } catch (NotSupportedException) {
        // Expected for empty and populated nested collections alike.
    }
}