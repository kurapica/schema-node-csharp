using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Enum;

namespace SchemaNode.Converter;

public class JsonNodeDateFormatConverter(DateFormatMode mode) : JsonConverter<JsonNode>
{
    public override JsonNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonNode.Parse(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonNode? value, JsonSerializerOptions options)
    {
        WriteNode(writer, value);
    }

    private void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (node is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (var kv in obj)
            {
                writer.WritePropertyName(kv.Key);
                WriteNode(writer, kv.Value);
            }
            writer.WriteEndObject();
            return;
        }

        if (node is JsonArray arr)
        {
            writer.WriteStartArray();
            foreach (var item in arr)
                WriteNode(writer, item);
            writer.WriteEndArray();
            return;
        }

        if (node is JsonValue val)
        {
            if (TryWriteAsDate(writer, val))
                return;

            val.WriteTo(writer); // 默认行为
        }
    }

    private bool TryWriteAsDate(Utf8JsonWriter writer, JsonValue val)
    {
        try
        {
            if (val.TryGetValue<DateTimeOffset>(out var dto))
            {
                DateFormatModeSerializer.Write(writer, dto, mode);
                return true;
            }

            if (val.TryGetValue<DateTime>(out var dt))
            {
                DateFormatModeSerializer.Write(writer, dt, mode);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
