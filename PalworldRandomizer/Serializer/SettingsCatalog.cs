using System.Collections.ObjectModel;
using System.IO;
using PalworldRandomizer.Models;
using SharpYaml;
using SharpYaml.Serialization;

namespace PalworldRandomizer.Serializer;

/// <summary>Loads the bundled settings definitions and English text without a UI dependency.</summary>
public sealed class SettingsCatalog {
    /// <summary>The catalog definitions in original member order.</summary>
    public IReadOnlyList<SettingDefinition> Definitions { get; }

    /// <summary>The compiled English strings, addressed by stable localization keys.</summary>
    public IReadOnlyDictionary<string, string> English { get; }

    /// <summary>The original radio groups; imported model values are never normalized by this API.</summary>
    public IReadOnlyList<SettingRadioGroup> RadioGroups { get; }

    private SettingsCatalog(CatalogDocument catalog, LocaleDocument locale) {
        RadioGroups = catalog.RadioGroups.ConvertAll(x =>
            new SettingRadioGroup(x.Id, x.Members.ToList().AsReadOnly(), x.DefaultMember)).AsReadOnly();
        Definitions = catalog.Settings.ConvertAll(x => new SettingDefinition(x)).AsReadOnly();
        English = new ReadOnlyDictionary<string, string>(locale.Strings);
    }

    /// <summary>Reads the real embedded YAML using SharpYaml generated type metadata.</summary>
    public static SettingsCatalog LoadEmbedded() {
        var catalog = YamlSerializer.Deserialize(ReadResource("RandomizerSettings.yaml"),
            SettingsYamlContext.Default.CatalogDocument) ?? throw new InvalidDataException("Empty catalog.");
        var locale = YamlSerializer.Deserialize(ReadResource("Locales.en-US.Randomizer.yaml"),
            SettingsYamlContext.Default.LocaleDocument) ?? throw new InvalidDataException("Empty English locale.");
        return new(catalog, locale);
    }

    private static string ReadResource(string suffix) {
        string name = "PalworldRandomizer.Metadata." + suffix;
        using var stream = typeof(SettingsCatalog).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException($"Missing embedded resource '{name}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

[YamlSerializable(typeof(CatalogDocument))]
[YamlSerializable(typeof(LocaleDocument))]
internal partial class SettingsYamlContext : YamlSerializerContext;