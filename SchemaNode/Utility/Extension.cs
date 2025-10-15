using SchemaNode.Node;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SchemaNode.Utility;

internal static class Extension
{
    #region Casing

    /// <summary>
    /// Returns the camel case of this string.
    /// </summary>
    /// <param name="s">This string.</param>
    /// <returns>The camel case of this string.</returns>
    internal static string ToCamelCase(this string s)
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
    internal static string ToPascalCase(this string s)
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

    internal class JsonDateTimeIsoConverter : JsonConverter<DateTime>
    {
        private const string FORMAT = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString() ?? "", null, DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(FORMAT));
        }
    }
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
                _ => reader.GetString()
            } ?? "";
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    internal class JsonDateTimeOffsetIsoConverter : JsonConverter<DateTimeOffset>
    {
        private const string FORMAT = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString() ?? "", null, DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(FORMAT));
        }
    }

    /// <summary>
    /// Serializes a .NET value to JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="indent">use indent</param>
    internal static string ToJson<T>(this T value, bool indent = false)
    {
        if (value is JsonNode json) return json.ToString();
        
        // Generate the JSON string.
        return JsonSerializer.Serialize(value, indent ? IndentJsonOption : NoIndentJsonOption);
    }

    /// <summary>
    /// Deserializes a JSON string to a .NET value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    internal static T? FromJson<T>(this string value)
    {
        return JsonSerializer.Deserialize<T>(value, NoIndentJsonOption);
    }

    /// <summary>
    /// Deserializes a JSON string to a .NET value.
    /// </summary>
    internal static object? FromJson(this string value, Type type)
    {
        if (type == typeof(string))
            return value;
        if (type == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(value);
        if (type == typeof(DateTime))
            return DateTime.Parse(value);

        return JsonSerializer.Deserialize(value, type, NoIndentJsonOption);
    }

    internal static T? FromJson<T>(this JsonNode value)
    {
        return value.Deserialize<T>(NoIndentJsonOption);
    }

    internal static object? FromJson(this JsonNode value, Type type)
    {
        if (type == typeof(JsonObject))
        {
            return value is JsonObject obj ? obj : throw new JsonException("The value is not an object");
        }
        else if (type == typeof(JsonArray))
        {
            return value is JsonArray arr ? arr : throw new JsonException("The value is not an array.");
        }
        else if (type == typeof(JsonValue))
        {
            return value is JsonValue val ? val : throw new JsonException("The value is not a valid JsonValue");
        }
        else if (type == typeof(JsonNode))
        {
            return value;
        }
        return value.Deserialize(type, NoIndentJsonOption);
    }
    
    internal static JsonNode? ToJsonNode<T>(this T? value)
    {
        if (value == null) return null;
        if (typeof(T).IsAssignableTo(typeof(JsonNode))) return (JsonNode?)(object)value;
        return JsonSerializer.SerializeToNode(value, NoIndentJsonOption);
    }

    internal static T? ToValue<T>(this JsonNode node)
    {
        return (T?)(TryConvert(typeof(T), node) ?? default);
    }

    /// <summary>
    /// Try parse the json value to value and type
    /// </summary>
    internal static (object? value, Type? type) ParseValueAndType(this JsonValue val)
    {
        switch ( val.GetValueKind() )
        {
            case JsonValueKind.String:
                if (val.TryGetValue(out string? s))
                {
                    if (DateTime.TryParse(s, out DateTime d))
                        return (d, typeof(DateTime));
                    return (s, typeof(string));
                }
                return (null, typeof(string));

            case JsonValueKind.Number:
                if (val.TryGetValue(out long l))
                    return (l, typeof(long));
                else if(val.TryGetValue(out int i))
                    return (i, typeof(int));
                else if (val.TryGetValue(out double db))
                    return (db, typeof(double));
                else if (val.TryGetValue(out float f))
                    return (f, typeof(float));
                else if (val.TryGetValue(out decimal dec))
                    return (val.GetValue<decimal>(), typeof(decimal));
                throw new InvalidCastException("Can't be converted to a number");
            case JsonValueKind.True:
                return (true, typeof(bool));
            case JsonValueKind.False:
                return (false, typeof(bool));
            case JsonValueKind.Null:
                return (null, null);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    /// <summary>
    /// Whether the json node is empty
    /// </summary>
    internal static bool IsEmpty(this JsonNode? node)
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
    internal static void AddRange(this JsonArray a, JsonArray b)
    {
        foreach (var item in b)
        {
            if (item != null)
                a.Add(item.DeepClone());
        }
    }

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    internal static JsonNode? GetValueByPaths(this JsonNode? token, IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (token is JsonObject obj && obj.ContainsKey(path))
            {
                token = obj[path];
            }
            else
            {
                token = null;
                break;
            }
        }
        return token;
    }

    /// <summary>
    /// Gets the value with paths
    /// </summary>
    public static JsonNode? GetValueByPaths(this JsonNode? token, string paths) => GetValueByPaths(token, paths.Split('.', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Try get the value with name
    /// </summary>
    internal static bool TryGetValue(this JsonObject? obj, string name, out JsonNode? value)
    {
        if (obj != null && obj.ContainsKey(name))
        {
            value = obj[name];
            return true;
        }
        value = null;
        return false;
    }
    
    private static readonly JsonSerializerOptions IndentJsonOption = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new JsonDateTimeIsoConverter(),
            new JsonDateTimeOffsetIsoConverter(),
            new ForceStringConverter(),
            new FlexibleLongConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
    private static readonly JsonSerializerOptions NoIndentJsonOption = new()
    {
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new JsonDateTimeIsoConverter(),
            new JsonDateTimeOffsetIsoConverter(),
            new ForceStringConverter(),
            new FlexibleLongConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #endregion

    #region Generics

    internal static bool IsPrimitiveLike(this Type type)
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
    internal static bool IsNullable(this Type type) => type.IsSubclassOfGenericType(typeof(Nullable<>));

    /// <summary>
    /// Gets a specific generic base type.
    /// </summary>
    internal static Type? GetGenericBaseType(this Type type, Type genericType)
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
    internal static Type? GetGenericBaseType<T>(this Type type) => GetGenericBaseType(type, typeof(T));

    /// <summary>
    /// Checks whether a type is a subclass of a specific generic type.
    /// </summary>
    internal static bool IsSubclassOfGenericType(this Type type, Type genericType) => GetGenericBaseType(type, genericType) != null;

    /// <summary>
    /// Checks whether a type is a subclass of a specific generic type.
    /// </summary>
    internal static bool IsSubclassOfGenericType<T>(this Type type) => IsSubclassOfGenericType(type, typeof(T));

    /// <summary>
    /// Gets the not null type
    /// </summary>
    internal static Type GetNotNullType(this Type type) => Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// Gets the nullable type
    /// </summary>
    internal static Type GetNullableType(this Type type) => type.IsSubclassOfGenericType(typeof(Nullable<>)) ? type : typeof(Nullable<>).MakeGenericType(type);

    /// <summary>
    /// The type is simple array type
    /// </summary>
    internal static bool IsArrayType(this Type type) => type != typeof(ArrayTypeNode) && 
        ( type.IsSZArray || type.IsSubclassOfGenericType(typeof(List<>)) || type.IsSubclassOfGenericType(typeof(IEnumerable<>)));
    
    internal static bool IsSafeConstantValue(this Type type)
    { 
        if (type.IsValueType || type == typeof(string))
            return true;

        if (typeof(Type).IsAssignableFrom(type))
            return true;

        if (type == typeof(Uri) || type == typeof(Version))
            return true;

        return false;
    }

    internal static bool HasClosure(this Delegate method)
    {
        return method.Target != null && method.Target.GetType().FullName == "System.Runtime.CompilerServices.Closure";
    }

    #endregion

    #region Type

    /// <summary>
    /// Try to convert the value for the given type, only for enum & primitive values
    /// </summary>
    internal static object? TryConvert(this Type type, object? value)
    {
        try
        {
            // value check
            if (value == null) return null;
            type = type.GetNotNullType();

            if (value.GetType().IsAssignableTo(type)) return value;
            if (value is AnySchemaNode node) return node.ToTypeValue(type);

            // json type
            if (value is JsonArray or JsonObject)
            {
                return (value as JsonNode)!.FromJson(type);
            }
            else if (type == typeof(JsonArray))
            {
                var result = JsonSerializer.SerializeToNode(value, NoIndentJsonOption);
                return result is JsonArray ? result : null;
            }
            else if (type == typeof(JsonObject))
            {
                var result = JsonSerializer.SerializeToNode(value, NoIndentJsonOption);
                return result is JsonObject ? result : null;
            }

            // none json type
            if (value is JsonValue v)
            {
                (value, _) = v.ParseValueAndType();
                if (value == null) return null;
                if (value.GetType().IsAssignableTo(type)) return value;
            }
            // for collections
            else if (value is Array arr)
            {
                if (type.IsSZArray)
                {
                    Type? eleType = type.GetElementType();
                    if (eleType == null) return null;
                    return arr.Cast<object>().Select(o => TryConvert(eleType, o)).ToArray();
                }
                else if (type.IsSubclassOfGenericType(typeof(List<>)))
                {
                    Type eleType = type.GetGenericArguments()[0];
                    return arr.Cast<object>().Select(o => TryConvert(eleType, o)).ToList();
                }
                else if (type.IsSubclassOfGenericType(typeof(IEnumerable<>)))
                {
                    Type? eleType = type.GetElementType();
                    if (eleType == null) return null;
                    return arr.Cast<object>().Select(o => TryConvert(eleType, o));
                }
                return null;
            }
            else if(value is not string && value is IEnumerable iter)
            {
                if (type.IsSZArray)
                {
                    Type? eleType = type.GetElementType();
                    if (eleType == null) return null;
                    return iter.Cast<object>().Select(o => TryConvert(eleType, o)).ToArray();
                }
                else if (type.IsSubclassOfGenericType(typeof(List<>)))
                {
                    Type eleType = type.GetGenericArguments()[0];
                    return iter.Cast<object>().Select(o => TryConvert(eleType, o)).ToList();
                }
                else if (type.IsSubclassOfGenericType(typeof(IEnumerable<>)))
                {
                    Type? eleType = type.GetElementType();
                    if (eleType == null) return null;
                    return iter.Cast<object>().Select(o => TryConvert(eleType, o));
                }
                return null;
            }

            // Enum convert
            if (type.IsEnum)
            {
                return value is string s
                    ? System.Enum.Parse(type, s, ignoreCase: true)
                    : value.GetType().IsPrimitive
                        ? System.Enum.ToObject(type, value)
                        : null;
            }

            // Primitive
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                    return null;
                case TypeCode.Object:
                    break;
                case TypeCode.Boolean:
                    return Convert.ToBoolean(value);
                case TypeCode.Char:
                    return Convert.ToChar(value);
                case TypeCode.SByte:
                    return Convert.ToSByte(value);
                case TypeCode.Byte:
                    return Convert.ToByte(value);
                case TypeCode.Int16:
                    return Convert.ToInt16(value);
                case TypeCode.UInt16:
                    return Convert.ToUInt16(value);
                case TypeCode.Int32:
                    return Convert.ToInt32(value);
                case TypeCode.UInt32:
                    return Convert.ToUInt32(value);
                case TypeCode.Int64:
                    return Convert.ToInt64(value);
                case TypeCode.UInt64:
                    return Convert.ToUInt64(value);
                case TypeCode.Single:
                    return Convert.ToSingle(value);
                case TypeCode.Double:
                    return Convert.ToDouble(value);
                case TypeCode.Decimal:
                    return Convert.ToDecimal(value);
                case TypeCode.DateTime:
                    return Convert.ToDateTime(value);
                case TypeCode.String:
                    return Convert.ToString(value);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Object
            if (type == typeof(Guid))
            {
                return Guid.TryParse(value.ToString(), out Guid result) ? result : null;
            }
            else if (type == typeof(DateTimeOffset))
            {
                return value switch
                {
                    string s => DateTimeOffset.Parse(s),
                    long or int => DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(value)),
                    _ => null
                };
            }
            else
            {
                return JsonSerializer.Deserialize(JsonSerializer.SerializeToNode(value, NoIndentJsonOption)!.ToJsonString(), type, NoIndentJsonOption);
            }
        }
        catch(Exception ex)
        {
            throw new InvalidCastException($"Cannot convert the value '{value}' to type '{type.FullName}'", ex);
        }
    }

    #endregion

    #region Exception

    /// <summary>
    /// Gets the innermost exception.
    /// </summary>
    internal static Exception GetInnermostException(this Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return exception;
    }

    #endregion
}