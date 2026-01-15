using SchemaNode.Node;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using Microsoft.AspNetCore.Http;

namespace SchemaNode.Utility;

public static class Extension
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

    public static string? ToLiteral(this object input)
    {
        return input switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd hh:mm:ss.fff"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd hh:mm:ss.fff"),
            _ => input.ToString()
        };
    }

    /// <summary>
    /// Split the type path
    /// </summary>
    internal static string[] SplitTypeName(this string name)
    {
        List<string> paths = name.ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries).Where(f => !string.IsNullOrEmpty(f)).ToList();
        while (paths.Count > 1 && paths.Last().EndsWith(">") && !paths.Last().Contains("<"))
        {
            string last = paths.Last();
            paths.RemoveAt(paths.Count - 1);

            string secondLast = paths.Last();
            paths.RemoveAt(paths.Count - 1);

            paths.Add(secondLast + "." + last);
        }
        return paths.ToArray();
    }

    /// <summary>
    /// Gets the base type
    /// </summary>
    internal static string GetBaseType(this string name)
    {
        return name.Contains("<") ? name[..name.IndexOf('<')] : name;
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
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString() ?? "", null, DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(Format));
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

    internal class JsonDateTimeOffsetIsoConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString() ?? "", null, DateTimeStyles.RoundtripKind);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToUniversalTime().ToString(Format));
        }
    }

    internal class FlexibleEnumConverter<T> : JsonConverter<T> where T : struct, System.Enum
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
            if (isFlags)
            {
                writer.WriteNumberValue(Convert.ToInt64(value));
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }

    public class UniversalFlexibleEnumConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return FlagsEnums.GetOrAdd(typeToConvert, t =>
            {
                var isFlags = t.IsDefined(typeof(FlagsAttribute), false);
                if (isFlags)
                {
                    var converterType = typeof(FlexibleEnumConverter<>).MakeGenericType(t);
                    return (JsonConverter)Activator.CreateInstance(converterType)!;
                }
                else
                {
                    return NormalEnumConverter.CreateConverter(t, options);
                }
            });
        }

        static readonly JsonStringEnumConverter NormalEnumConverter = new (JsonNamingPolicy.CamelCase);
        private static readonly ConcurrentDictionary<Type, JsonConverter> FlagsEnums = [];
    }

    /// <summary>
    /// Serializes a .NET value to JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="indent">use indent</param>
    public static string ToJson<T>(this T value, bool indent = false)
    {
        if (value is JsonNode json) return json.ToString();
        
        // Generate the JSON string.
        return JsonSerializer.Serialize(value, indent ? IndentJsonOption : NoIndentJsonOption);
    }

    /// <summary>
    /// To http result
    /// </summary>
    public static IResult ToResult<T>(this T value)
    {
        return Results.Json(value, NoIndentJsonOption);
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
    
    internal static JsonNode? ToJsonNode<T>(this T? value, bool noError = false)
    {
        try
        {
            if (value == null) return null;
            if (typeof(T).IsAssignableTo(typeof(JsonNode))) return (JsonNode?)(object)value;
            return JsonSerializer.SerializeToNode(value, NoIndentJsonOption);
        }
        catch 
        {
            // not able to convert
            if (!noError) throw;
            return null;
        }
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
                else if (val.TryGetValue(out decimal _))
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
            JsonValue v => v.ToJsonString() == "null" || string.IsNullOrWhiteSpace(v.ToString()),
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

    internal static readonly JsonSerializerOptions IndentJsonOption = new()
    {
        WriteIndented = true,
        Converters =
        {
            new UniversalFlexibleEnumConverter(),
            new JsonDateTimeIsoConverter(),
            new JsonDateTimeOffsetIsoConverter(),
            new ForceStringConverter(),
            new FlexibleLongConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    
    internal static readonly JsonSerializerOptions NoIndentJsonOption = new()
    {
        WriteIndented = false,
        Converters =
        {
            new UniversalFlexibleEnumConverter(),
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
    internal static bool IsArrayType(this Type type) => type != typeof(string) && type != typeof(ArrayTypeNode) && 
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

    /// <summary>
    /// Get summary contents from XML document.
    /// </summary>
    internal static string? GetSummaryFromXmlDoc(this Type type, PropertyInfo? prop = null)
    {
        string prefix = prop == null ? "T:" : "P:";
        string memberName = $"{prefix}{type.FullName!.Replace('+', '.')}";
        if (prop != null)
            memberName += $".{prop.Name}";

        return GetSummaryFromXmlDocInternal(type.Assembly, memberName);
    }

    /// <summary>
    /// Get summary contents of enum field from XML doc.
    /// </summary>
    internal static string? GetSummaryFromXmlDoc(this Type type, FieldInfo field)
    {
        if (!type.IsEnum) return null;

        string memberName = $"F:{type.FullName!.Replace('+', '.')}.{field.Name}";
        return GetSummaryFromXmlDocInternal(type.Assembly, memberName);
    }

    /// <summary>
    /// Get summary contents of method from XML doc.
    /// </summary>
    internal static string? GetSummaryFromXmlDoc(this MethodInfo method)
    {
        const string prefix = "M:";
        var type = method.DeclaringType!;

        string typeName = type.FullName!.Replace('+', '.');
        string methodName = method.Name;

        // Generic method: Method``1
        if (method.IsGenericMethodDefinition)
            methodName += $"``{method.GetGenericArguments().Length}";

        // Parameters
        string paramList = string.Join(",", method.GetParameters()
            .Select(p => GetXmlDocTypeName(p.ParameterType)));

        string memberName = $"{prefix}{typeName}.{methodName}";
        if (!string.IsNullOrEmpty(paramList))
            memberName += $"({paramList})";

        return GetSummaryFromXmlDocInternal(type.Assembly, memberName, "summary");
    }

    /// <summary>
    /// Get summary contents of method from XML doc.
    /// </summary>
    internal static string? GetSummaryFromXmlDoc(this MethodInfo method, ParameterInfo parameter)
    {
        const string prefix = "M:";
        var type = method.DeclaringType!;

        string typeName = type.FullName!.Replace('+', '.');
        string methodName = method.Name;

        // Generic method: Method``1
        if (method.IsGenericMethodDefinition)
            methodName += $"``{method.GetGenericArguments().Length}";

        // Parameters
        string paramList = string.Join(",", method.GetParameters()
            .Select(p => GetXmlDocTypeName(p.ParameterType)));

        string memberName = $"{prefix}{typeName}.{methodName}";
        if (!string.IsNullOrEmpty(paramList))
            memberName += $"({paramList})";

        return GetSummaryFromXmlDocInternal(type.Assembly, memberName, $"param[@name='{parameter.Name}']");
    }
    
    #region ---------- Shared Internal Helpers ----------

    /// <summary>
    /// Load XML once and query the node by name.
    /// </summary>
    private static string? GetSummaryFromXmlDocInternal(Assembly asm, string memberName, string? subPath = null)
    {
        string xmlPath = asm.Location.Replace(".dll", ".xml");
        XmlDocument? doc = LoadXml(xmlPath);
        if (doc == null) return null;

        XmlNode? members = doc.SelectSingleNode("/doc/members");
        if (members == null) return null;

        foreach (XmlElement node in members)
        {
            string? name = node.Attributes?[0]?.Value;
            if (name == memberName)
            {
                switch (string.IsNullOrEmpty(subPath))
                {
                    case false:
                    {
                        XmlNode? subNode = node.SelectSingleNode(subPath);
                        if (subNode != null)
                            return CleanupSummary(subNode.InnerText);
                        else
                            return null;
                    }
                    default:
                        return CleanupSummary(node.InnerText);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Load xml with caching
    /// </summary>
    private static XmlDocument? LoadXml(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            return null;

        if (!XmlFiles.TryGetValue(xmlPath, out XmlDocument? doc))
        {
            doc = new XmlDocument();
            doc.Load(xmlPath);
            XmlFiles[xmlPath] = doc;
        }

        return doc;
    }

    /// <summary>
    /// Cleanup whitespace of xml summary.
    /// </summary>
    private static string? CleanupSummary(string content)
    {
        string cleaned = string.Join("\n",
            content.Split('\n', '\r')
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim()));

        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    /// <summary>
    /// Get XML doc formatted type name.
    /// </summary>
    private static string GetXmlDocTypeName(Type type)
    {
        if (type.IsGenericParameter)
            return $"``{type.GenericParameterPosition}";

        if (type.IsArray)
        {
            string elementName = GetXmlDocTypeName(type.GetElementType()!);
            int rank = type.GetArrayRank();

            if (rank == 1)
                return elementName + "[]";

            return elementName + "[" + new string(',', rank - 1) + "]";
        }

        if (!type.IsGenericType)
            return type.FullName!.Replace('+', '.');

        string typeName = type.GetGenericTypeDefinition().FullName!;
        typeName = typeName[..typeName.IndexOf('`')].Replace('+', '.');

        string genericArgs = string.Join(",", type.GetGenericArguments().Select(GetXmlDocTypeName));
        return $"{typeName}{{{genericArgs}}}";
    }

    static readonly ConcurrentDictionary<string, XmlDocument> XmlFiles = [];

    #endregion


    #endregion

    #region Array

    internal static Array SliceArray(this Array source, int count)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        int len = Math.Min(count, source.Length);
        Type elementType = source.GetType().GetElementType()!;

        Array result = Array.CreateInstance(elementType, len);
        Array.Copy(source, result, len);

        return result;
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