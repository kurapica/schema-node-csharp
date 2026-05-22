using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using SchemaNode.App.Components;
using SchemaNode.App.Converter;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Utility.Converter;

namespace SchemaNode.App.Utility;

/// <summary>
/// App-side JSON serialization extensions that apply date/time format based on the current <see cref="Access"/> context item.
/// </summary>
public static class AppJsonExtension
{
    private static readonly ConcurrentDictionary<(DateFormatMode, string), JsonSerializerOptions> IndentOptions   = new();
    private static readonly ConcurrentDictionary<(DateFormatMode, string), JsonSerializerOptions> NoIndentOptions = new();

    /// <summary>Gets <see cref="JsonSerializerOptions"/> for the given format/timezone combination.</summary>
    public static JsonSerializerOptions GetJsonOptions(bool indent, DateFormatMode? dateFormat = null, TimeZoneInfo? timeZone = null)
    {
        var dfm = dateFormat ?? DateFormatMode.Iso8601;
        var tz  = timeZone  ?? AccessContextItemProviderExtensions.DefaultTimeZone;
        var dict = indent ? IndentOptions : NoIndentOptions;
        return dict.GetOrAdd((dfm, tz.Id), _ => new JsonSerializerOptions
        {
            WriteIndented = indent,
            Converters =
            {
                new UniversalFlexibleEnumConverter(),
                new ForceStringConverter(),
                new FlexibleLongConverter(),
                new JsonDateTimeConverter(dfm, tz),
                new JsonDateTimeOffsetConverter(dfm, tz),
                new JsonNodeDateFormatConverter(dfm, tz),
            },
            PropertyNamingPolicy         = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition       = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    /// <summary>Serializes <paramref name="value"/> to JSON using the access context date format.</summary>
    public static string ToJson<T>(this SchemaContext context, T value, bool indent = false)
    {
        Access? access = context.GetContextItem<Access>();
        return value.ToAppJson(indent, access?.DateFormatMode, access?.TimeZone);
    }

    /// <summary>Creates an <see cref="IResult"/> containing JSON-serialized <paramref name="value"/>.</summary>
    public static IResult ToJsonResult<T>(this SchemaContext context, T value, bool indent = false)
    {
        Access? access = context.GetContextItem<Access>();
        return Results.Json(value, GetJsonOptions(indent, access?.DateFormatMode, access?.TimeZone));
    }

    /// <summary>Deserializes <paramref name="value"/> from JSON using the access context date format.</summary>
    public static T? FromJson<T>(this SchemaContext context, string value)
    {
        Access? access = context.GetContextItem<Access>();
        return value.FromAppJson<T>(access?.DateFormatMode);
    }

    // ---- plain static helpers ----

    /// <summary>Serializes <paramref name="value"/> to JSON.</summary>
    public static string ToAppJson<T>(this T value, bool indent = false, DateFormatMode? mode = null, TimeZoneInfo? timeZone = null)
    {
        if (value is JsonNode json) return json.ToString();
        return JsonSerializer.Serialize(value, GetJsonOptions(indent, mode, timeZone));
    }

    /// <summary>Deserializes a JSON string to <typeparamref name="T"/>.</summary>
    public static T? FromAppJson<T>(this string value, DateFormatMode? mode = null, TimeZoneInfo? timeZone = null)
        => (T?)value.FromAppJson(typeof(T), mode, timeZone);

    internal static object? FromAppJson(this string value, Type type, DateFormatMode? mode = null, TimeZoneInfo? timeZone = null)
    {
        if (type == typeof(string))            return value;
        if (type == typeof(DateTimeOffset))    return DateTimeOffset.Parse(value);
        if (type == typeof(DateTime))          return DateTime.Parse(value);
        return JsonSerializer.Deserialize(value, type, GetJsonOptions(false, mode, timeZone));
    }
}
