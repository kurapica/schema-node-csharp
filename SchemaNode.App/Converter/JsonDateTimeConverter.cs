using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SchemaNode.Enum;

namespace SchemaNode.App.Converter;

#region Serializer

/// <summary>
/// Serializes/deserializes DateTime and DateTimeOffset according to a <see cref="DateFormatMode"/>.
/// </summary>
internal static class DateFormatModeSerializer
{
    private const string IsoFormat          = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const string DateOnlyFormat     = "yyyy-MM-dd";
    private const string DateTimeFormat     = "yyyy-MM-dd HH:mm:ss";
    private const string CompactDateFormat  = "yyyyMMdd";
    private const string CompactDtFormat    = "yyyyMMddHHmmss";
    private const string SlashDateTimeFormat= "yyyy/MM/dd HH:mm:ss";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static DateTime ReadDateTime(ref Utf8JsonReader reader, DateFormatMode mode)
        => ReadDateTimeOffset(ref reader, mode).UtcDateTime;

    public static DateTimeOffset ReadDateTimeOffset(ref Utf8JsonReader reader, DateFormatMode mode)
        => mode switch
        {
            DateFormatMode.Iso8601         => DateTimeOffset.Parse(reader.GetString() ?? "", null, DateTimeStyles.RoundtripKind),
            DateFormatMode.DateOnly        => ParseExactAsOffset(ref reader, DateOnlyFormat),
            DateFormatMode.DateTime        => ParseExactAsOffset(ref reader, DateTimeFormat),
            DateFormatMode.Compact         => ParseCompact(ref reader),
            DateFormatMode.UnixSeconds     => DateTimeOffset.FromUnixTimeSeconds(GetInt64(ref reader)),
            DateFormatMode.UnixMilliseconds=> DateTimeOffset.FromUnixTimeMilliseconds(GetInt64(ref reader)),
            DateFormatMode.Ticks           => new DateTimeOffset(GetInt64(ref reader), TimeSpan.Zero),
            DateFormatMode.Rfc1123         => DateTimeOffset.ParseExact(GetRequiredString(ref reader), "r", Culture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            DateFormatMode.SlashDateTime   => ParseExactAsOffset(ref reader, SlashDateTimeFormat),
            _ => throw new NotSupportedException($"Unsupported {nameof(DateFormatMode)} '{mode}'.")
        };

    public static void Write(Utf8JsonWriter writer, DateTime value, DateFormatMode mode, TimeZoneInfo tz)
        => Write(writer, ToUtcOffset(value), mode, tz);

    public static void Write(Utf8JsonWriter writer, DateTimeOffset value, DateFormatMode mode, TimeZoneInfo tz)
    {
        var utc = value.ToUniversalTime();
        switch (mode)
        {
            case DateFormatMode.Iso8601:
                writer.WriteStringValue(value.ToString(IsoFormat, Culture));
                break;
            case DateFormatMode.DateOnly:
                writer.WriteStringValue(TimeZoneInfo.ConvertTimeFromUtc(utc.DateTime, tz).ToString(DateOnlyFormat, Culture));
                break;
            case DateFormatMode.DateTime:
                writer.WriteStringValue(TimeZoneInfo.ConvertTimeFromUtc(utc.DateTime, tz).ToString(DateTimeFormat, Culture));
                break;
            case DateFormatMode.Compact:
                writer.WriteStringValue(utc.ToString(utc.TimeOfDay == TimeSpan.Zero ? CompactDateFormat : CompactDtFormat, Culture));
                break;
            case DateFormatMode.UnixSeconds:
                writer.WriteNumberValue(utc.ToUnixTimeSeconds());
                break;
            case DateFormatMode.UnixMilliseconds:
                writer.WriteNumberValue(utc.ToUnixTimeMilliseconds());
                break;
            case DateFormatMode.Ticks:
                writer.WriteNumberValue(utc.UtcDateTime.Ticks);
                break;
            case DateFormatMode.Rfc1123:
                writer.WriteStringValue(utc.ToString("r", Culture));
                break;
            case DateFormatMode.SlashDateTime:
                writer.WriteStringValue(utc.ToString(SlashDateTimeFormat, Culture));
                break;
            default:
                throw new NotSupportedException($"Unsupported {nameof(DateFormatMode)} '{mode}'.");
        }
    }

    private static DateTimeOffset ParseExactAsOffset(ref Utf8JsonReader reader, string format)
    {
        string text = GetRequiredString(ref reader);
        var parsed = DateTime.ParseExact(text, format, Culture, DateTimeStyles.None);
        return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
    }

    private static DateTimeOffset ParseCompact(ref Utf8JsonReader reader)
    {
        string text = GetRequiredString(ref reader);
        return text.Length switch
        {
            8  => ParseExactText(text, CompactDateFormat),
            14 => ParseExactText(text, CompactDtFormat),
            _  => throw new JsonException($"Invalid compact date value '{text}'.")
        };
    }

    private static DateTimeOffset ParseExactText(string text, string format)
    {
        var parsed = DateTime.ParseExact(text, format, Culture, DateTimeStyles.None);
        return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
    }

    private static string GetRequiredString(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Unexpected token {reader.TokenType}, expected string.");
        string? value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new JsonException("Date value is null or empty.");
        return value!;
    }

    private static long GetInt64(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out long number))
            return number;
        if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), NumberStyles.Integer, Culture, out number))
            return number;
        throw new JsonException($"Unexpected token {reader.TokenType}, expected integer.");
    }

    private static DateTimeOffset ToUtcOffset(DateTime value)
    {
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc   => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _                  => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}

#endregion

#region DateTime Converters

internal class JsonDateTimeConverter(DateFormatMode mode, TimeZoneInfo tz) : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateFormatModeSerializer.ReadDateTime(ref reader, mode);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => DateFormatModeSerializer.Write(writer, value, mode, tz);
}

internal class JsonDateTimeOffsetConverter(DateFormatMode mode, TimeZoneInfo tz) : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateFormatModeSerializer.ReadDateTimeOffset(ref reader, mode);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => DateFormatModeSerializer.Write(writer, value, mode, tz);
}

internal class JsonNodeDateFormatConverter(DateFormatMode mode, TimeZoneInfo tz) : JsonConverter<JsonNode>
{
    public override JsonNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonNode.Parse(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonNode? value, JsonSerializerOptions options)
        => WriteNode(writer, value);

    private void WriteNode(Utf8JsonWriter writer, JsonNode? node)
    {
        if (node is null) { writer.WriteNullValue(); return; }

        if (node is JsonObject obj)
        {
            writer.WriteStartObject();
            foreach (var kv in obj) { writer.WritePropertyName(kv.Key); WriteNode(writer, kv.Value); }
            writer.WriteEndObject();
            return;
        }

        if (node is JsonArray arr)
        {
            writer.WriteStartArray();
            foreach (var item in arr) WriteNode(writer, item);
            writer.WriteEndArray();
            return;
        }

        if (node is JsonValue val)
        {
            if (TryWriteAsDate(writer, val)) return;
            val.WriteTo(writer);
        }
    }

    private bool TryWriteAsDate(Utf8JsonWriter writer, JsonValue val)
    {
        try
        {
            if (val.TryGetValue<DateTimeOffset>(out var dto)) { DateFormatModeSerializer.Write(writer, dto, mode, tz); return true; }
            if (val.TryGetValue<DateTime>(out var dt))        { DateFormatModeSerializer.Write(writer, dt,  mode, tz); return true; }
        }
        catch { return false; }
        return false;
    }
}

#endregion
