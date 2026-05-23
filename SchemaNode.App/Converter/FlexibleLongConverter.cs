using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Converter;


internal class FlexibleLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt64(out var l) => l,
            JsonTokenType.Number when reader.TryGetDouble(out var d) => Convert.ToInt64(d),
            JsonTokenType.Number when reader.TryGetDecimal(out var d) => Convert.ToInt64(d),
            JsonTokenType.String when long.TryParse(reader.GetString(), out var l) => l,
            _ => throw new JsonException($"Cannot convert {reader.GetString()} to long")
        };
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
