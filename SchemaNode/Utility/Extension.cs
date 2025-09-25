using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;

namespace SchemaNode.Utility;

public static class Extension
{
    #region Casing

    /// <summary>
    /// Returns the camel case of this string.
    /// </summary>
    /// <param name="s">This string.</param>
    /// <returns>The camel case of this string.</returns>
    public static string ToCamelCase(this string s)
    {
        string result = s;
        if (result.Length > 0)
        {
            result = string.Concat(result[..1].ToLowerInvariant(), result.AsSpan(1));
        }
        return result;
    }

    /// <summary>
    /// Returns the camel case of this string.
    /// </summary>
    /// <param name="s">This string.</param>
    /// <returns>The camel case of this string.</returns>
    public static string ToPascalCase(this string s)
    {
        string result = s;
        if (result.Length > 0)
        {
            result = string.Concat(result[..1].ToUpperInvariant(), result.AsSpan(1));
        }
        return result;
    }

    #endregion

    #region JSON

    public class JsonDateTimeIsoConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString() ?? "", null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(Format));
        }
    }
    public class ForceStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetDecimal().ToString(),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => reader.GetString()
            } ?? "";
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    public class JsonDateTimeOffetIsoConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString() ?? "", null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(Format));
        }
    }

    /// <summary>
    /// Serializes a .NET value to JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="indent">Whether use indent</param>
    public static string ToJson<T>(this T value, bool indent = false)
    {
        // Generate the JSON string.
        return JsonSerializer.Serialize<T>(value, indent ? IndentJsonOption : NoIndentJsonOption);
    }

    /// <summary>
    /// Deserializes a JSON string to a .NET value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    public static T? FromJson<T>(this string value)
    {
        return JsonSerializer.Deserialize<T>(value, NoIndentJsonOption);
    }

    /// <summary>
    /// Deserializes a JSON string to a .NET value.
    /// </summary>
    public static object? FromJson(this string value, Type type)
    {
        if (type == typeof(string))
            return value;
        if (type == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(value);
        if (type == typeof(DateTime))
            return DateTime.Parse(value);

        return JsonSerializer.Deserialize(value, type, NoIndentJsonOption);
    }

    /// <summary>
    /// Whether the json node is empty
    /// </summary>
    public static bool IsEmpty(this JsonNode? node)
    {
        if (node == null) return true;
        return node switch
        {
            JsonArray a => a.Count == 0,
            JsonObject o => o.Count == 0,
            JsonValue v => v.ToJsonString() == "null",
            _ => true
        };
    }

    /// <summary>
    /// Add range
    /// </summary>
    public static void AddRange(this JsonArray a, JsonArray b)
    {
        foreach (var item in b)
        {
            a.Add(item);
        }
    }

    private static JsonSerializerOptions IndentJsonOption = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new JsonDateTimeIsoConverter(),
            new JsonDateTimeOffetIsoConverter(),
            new ForceStringConverter()
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
    private static JsonSerializerOptions NoIndentJsonOption = new()
    {
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new JsonDateTimeIsoConverter(),
            new JsonDateTimeOffetIsoConverter(),
            new ForceStringConverter()
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
    #endregion
    
    #region Generics
    
    public static bool IsPrimitiveLike(this Type type)
    {
        if (type.IsEnum) return true;
        if (type == typeof(Guid) || type == typeof(DateTimeOffset)) return true; // no type code
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean or TypeCode.Char or TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single
                or TypeCode.Double or TypeCode.Decimal or TypeCode.String or TypeCode.DateTime => true,
            _ => false
        };
    }

    // Checks if the type is nullable
    public static bool IsNullable(this Type type) => type.IsSubclassOfGenericType(typeof(Nullable<>));

    /// <summary>
    /// Gets a specific generic base type.
    /// </summary>
    public static Type? GetGenericBaseType(this Type type, Type genericType)
    {
        // Initialize.
        if (!genericType.IsGenericType || genericType.GetGenericTypeDefinition() != genericType)
        {
            return null;
        }

        // Check inheritance chain.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericType)
        {
            return type;
        }
        if (genericType.IsInterface)
        {
            foreach (Type interfaceType in type.GetInterfaces())
            {
                Type? result = GetGenericBaseType(interfaceType, genericType);
                if (result != null) return result;
            }
        }
        else if (type.BaseType != null)
        {
            Type? result = GetGenericBaseType(type.BaseType, genericType);
            if (result != null) return result;
        }

        // Finish.
        return null;
    }

    /// <summary>
    /// Gets a specific generic base type.
    /// </summary>
    public static Type? GetGenericBaseType<T>(this Type type) => GetGenericBaseType(type, typeof(T));

    /// <summary>
    /// Checks whether a type is a subclass of a specific generic type.
    /// </summary>
    public static bool IsSubclassOfGenericType(this Type type, Type genericType) => GetGenericBaseType(type, genericType) != null;

    /// <summary>
    /// Checks whether a type is a subclass of a specific generic type.
    /// </summary>
    public static bool IsSubclassOfGenericType<T>(this Type type) => IsSubclassOfGenericType(type, typeof(T));

    /// <summary>
    /// Unpack the nullable type
    /// </summary>
    public static (Type type, bool nullable) UnpackNullable(this Type type) => type.IsSubclassOfGenericType(typeof(Nullable<>)) ? (type.GetGenericArguments()[0], true) : (type, false);

    /// <summary>
    /// Gets the not null type
    /// </summary>
    public static Type GetNotNullType(this Type type) => type.IsSubclassOfGenericType(typeof(Nullable<>)) ? type.GetGenericArguments()[0] : type;

    /// <summary>
    /// Gets the nullable type
    /// </summary>
    public static Type GetNullableType(this Type type) => type.IsSubclassOfGenericType(typeof(Nullable<>)) ? type : typeof(Nullable<>).MakeGenericType(type);

    /// <summary>
    /// The type is simple array type
    /// </summary>
    public static bool IsArrayType(this Type type) => type.IsSZArray || type.IsSubclassOfGenericType(typeof(List<>));

    #endregion

    #region Exception

    /// <summary>
    /// Gets the innermost exception.
    /// </summary>
    public static Exception GetInnermostException(this Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return exception;
    }

    #endregion
}