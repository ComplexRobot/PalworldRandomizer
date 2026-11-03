using Newtonsoft.Json;

namespace PalworldRandomizer.Serializer;

// Needed because the default json writer always adds a decimal point with a trailing zero otherwise
/// <summary>Writes template decimal values without an unnecessary trailing decimal zero.</summary>
public class JsonWriterDecimal : JsonConverter<decimal> {
    /// <summary>Decimal reading is not supported by this write-only template converter.</summary>
    public override decimal ReadJson(
        JsonReader reader,
        Type objectType,
        decimal existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    ) => throw new NotImplementedException();

    /// <summary>Writes a decimal directly without adding a trailing decimal zero.</summary>
    public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer) =>
        writer.WriteRawValue(value.ToString());
}