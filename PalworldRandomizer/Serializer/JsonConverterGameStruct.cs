using Newtonsoft.Json;

namespace PalworldRandomizer.Serializer;

/// <summary>
/// Converts and expands <see cref="GameStruct">GameStructs</see> to and from json.
/// </summary>
public class JsonConverterGameStruct : JsonConverter {
    /// <summary>
    /// Recursively reads and expands json into values and <see cref="GameStruct"/>'s
    /// <see cref="GameStruct.Properties">Properties</see>.
    /// </summary>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) {
        return ReadValue();

        object? ReadValue() {
            while (reader.TokenType is JsonToken.None or JsonToken.Comment) {
                if (!reader.Read()) {
                    throw new Exception("Unexpected end when reading object.");
                }
            }

            return reader.TokenType switch {
                JsonToken.StartObject => ReadObject(),
                JsonToken.StartArray => ReadList(),
                JsonToken.Integer => Convert.ToInt32(reader.Value),
                JsonToken.Float => Convert.ToSingle(reader.Value),
                JsonToken.String or JsonToken.Boolean or JsonToken.Undefined
                    or JsonToken.Null or JsonToken.Date or JsonToken.Bytes => reader.Value,
                var x => throw new Exception($"Unexpected token when converting object: '{x}'"),
            };
        }

        object ReadObject() {
            var gameStruct = new GameStruct();

            while (reader.Read()) {
                switch (reader.TokenType) {
                    case JsonToken.PropertyName:
                        string propertyName = (string)reader.Value!;

                        if (!reader.Read()) {
                            throw new Exception("Unexpected end when reading object.");
                        }

                        gameStruct.Properties.Add(propertyName, ReadValue());
                        break;

                    case JsonToken.Comment:
                        break;

                    case JsonToken.EndObject:
                        return gameStruct;
                }
            }

            throw new Exception("Unexpected end when reading object.");
        }

        object ReadList() {
            List<object?> list = [];

            while (reader.Read()) {
                switch (reader.TokenType) {
                    case JsonToken.Comment:
                        break;

                    default:
                        list.Add(ReadValue());
                        break;

                    case JsonToken.EndArray:
                        return list.Select(x => x);
                }
            }

            throw new Exception("Unexpected end when reading object.");
        }
    }

    /// <summary>
    /// Writes the <see cref="GameStruct"/>'s <see cref="GameStruct.Properties">Properties</see>.
    /// </summary>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
        serializer.Serialize(writer, ((GameStruct?)value)?.Properties);

    /// <summary>
    /// Check if this can convert a type.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if <see cref="GameStruct"/>, <see langword="false"/> otherwise.
    /// </returns>
    public override bool CanConvert(Type objectType) => objectType == typeof(GameStruct);
}