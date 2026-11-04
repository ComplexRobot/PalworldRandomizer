using System.Text.Json.Serialization;
using PalworldRandomizer.Models;
using SharpYaml;
using SharpYaml.Serialization;

namespace PalworldRandomizer.Serializer;

/// <summary>Serializes the complete FormData contract to the Default template YAML.</summary>
/// <remarks>
/// The generated context keeps the original CLR member names and value types. Unknown members are rejected so a
/// hand-edited or unsupported document cannot be silently replaced by defaults. This type never mutates FormData.
/// </remarks>
public static class TemplateYaml {
    /// <summary>Writes every FormData member, including array values, as one YAML mapping.</summary>
    public static string Serialize(FormData values) {
        ArgumentNullException.ThrowIfNull(values);
        return YamlSerializer.Serialize(values, TemplateYamlContext.Default.FormData);
    }

    /// <summary>Reads one complete YAML mapping into the original FormData contract.</summary>
    /// <exception cref="InvalidDataException">The document is empty or is not valid settings YAML.</exception>
    public static FormData Deserialize(string yaml) {
        if (string.IsNullOrWhiteSpace(yaml)) {
            throw new InvalidDataException("The Default template is empty.");
        }

        FormData? values;
        try {
            values = YamlSerializer.Deserialize(yaml, TemplateYamlContext.Default.FormData);
        } catch (Exception exception) {
            throw new InvalidDataException("The Default template is not valid settings YAML.", exception);
        }

        return values ?? throw new InvalidDataException("The Default template is empty.");
    }
}

[YamlSourceGenerationOptions(WriteIndented = true, IndentSize = 4,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[YamlSerializable(typeof(FormData))]
internal partial class TemplateYamlContext : YamlSerializerContext;