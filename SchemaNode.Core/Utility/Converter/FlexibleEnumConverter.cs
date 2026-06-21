using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchemaNode.Utility;

public class FlexibleEnumConverter<T> : JsonConverter<T> where T : struct, System.Enum
{       
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (System.Enum.TryParse<T>(str, ignoreCase: true, out var result))
                return result;
            throw new JsonException($"Invalid enum value '{str}' for {typeof(T)}");
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            var val = reader.GetInt64();
            return (T)System.Enum.ToObject(typeof(T), val);
        }

        throw new JsonException($"Unexpected token {reader.TokenType} when parsing enum {typeof(T)}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var isFlags = typeof(T).IsDefined(typeof(FlagsAttribute), false);
        if (isFlags && string.IsNullOrEmpty(value.ToString()))
        {
            writer.WriteNumberValue(Convert.ToInt64(value));
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
