using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Function;

namespace SchemaNode.Utility;

internal static class JsonOptions
{
    private static readonly ConcurrentDictionary<(DateFormatMode, string), JsonSerializerOptions> IndentJsonOptions = new();
    private static readonly ConcurrentDictionary<(DateFormatMode, string), JsonSerializerOptions> NoIndentJsonOptions = new();

    internal static JsonSerializerOptions GetJsonOptions(bool indent, DateFormatMode? dateFormat = null, TimeZoneInfo? timeZone = null)
    {
        var dfm = dateFormat ?? DateFormatMode.Iso8601;
        var tz = timeZone ?? SystemCalendar.DefaultTimeZone;
        var dict = indent ? IndentJsonOptions : NoIndentJsonOptions;
        return dict.GetOrAdd((dfm, tz.Id), _ =>
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indent,
                Converters =
                {
                    new UniversalFlexibleEnumConverter(),
                    new ForceStringConverter(),
                    new FlexibleLongConverter(),
                    new SchemaConverterFactory(),
                    new JsonDateTimeConverter(dfm, tz),
                    new JsonDateTimeOffsetConverter(dfm, tz),
                    new JsonNodeDateFormatConverter(dfm, tz),
                },
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            return options;
        });
    }
    
    /// <summary>
    /// From http request
    /// </summary>
    public static T? FromJsonRequest<T>(this SchemaContext context, string json, DateFormatMode? dateFormatmode = null)
    {
        return JsonSerializer.Deserialize<T>(json, GetJsonOptions(false, dateFormatmode, context.GetTimeZone()));
    }
    
    
    /// <summary>
    /// To http result
    /// </summary>
    public static IResult ToJsonResult<T>(this SchemaContext context, T value, bool indent = false, DateFormatMode? dateFormatmode = null)
    {
        return Results.Json(value, GetJsonOptions(indent, dateFormatmode, context.GetTimeZone()));
    }
}