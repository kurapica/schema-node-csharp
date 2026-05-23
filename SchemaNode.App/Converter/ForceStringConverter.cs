using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Converter;

internal class ForceStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => null,
            JsonTokenType.EndObject => null,
            JsonTokenType.StartArray => null,
            JsonTokenType.EndArray => null,
            _ => reader.GetString()
        } ?? "";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
