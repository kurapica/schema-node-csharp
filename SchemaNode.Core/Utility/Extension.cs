using SchemaNode.Converter;
using SchemaNode.Node;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;

namespace SchemaNode.Utility;

public static class Extension
{
    #region String

    /// <summary>
    /// Returns the camel case of this string.
    /// </summary>
    internal static string ToCamelCase(this string s) => s.Length > 0 ? string.Concat(s[..1].ToLowerInvariant(), s.AsSpan(1)) : s;

    internal static string? ToLiteral(this object input)
    {
        return input switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            _ => input.ToString()
        };
    }

    /// <summary>
    /// Split the type path
    /// </summary>
    internal static string[] SplitTypeName(this string name)
    {
        List<string> paths = [..name.ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries)];
        while (paths.Count > 1 && paths[^1].EndsWith(">") && !paths[^1].Contains("<"))
        {
            string last = paths[^1];
            paths.RemoveAt(paths.Count - 1);
            paths[^1] += "." + last;
        }
        return [..paths];
    }

    /// <summary>
    /// Gets the base type
    /// </summary>
    internal static string GetBaseType(this string name) =>name.Contains("<") ? name[..name.IndexOf('<')] : name;

    /// <summary>
    /// Remove the ending part if existed
    /// </summary>
    internal static string RemoveEnding(this string name, string ending)
    {
        if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            return name[..^ending.Length];
        return name;
    }

    /// <summary>
    /// Remove the start part if existed
    /// </summary>
    /// <param name="name"></param>
    /// <param name="start"></param>
    /// <returns></returns>
    internal static string RemoveStart(this string name, string start)
    {
        if (name.StartsWith(start, StringComparison.OrdinalIgnoreCase))
            return name[start.Length..];
        return name;
    }

    /// <summary>
    /// Gets the property kind from the name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    internal static string GetPropertyKind(this string name) => name.RemoveEnding("Property").RemoveEnding("Prop").RemoveStart("I").ToLower();

    /// <summary>
    /// Gets the schema kind from the name, which is the name without "Schema" suffix and in camel case.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    internal static string GetSchemaKind(this string name) => name.RemoveEnding("Schema").ToLower();

    /// <summary>
    /// Gets the property name
    /// </summary>
    internal static string GetPropertyName(this string name) => name.RemoveEnding("Property").RemoveEnding("Prop").ToCamelCase();

    #endregion

    #region JSON

    #region Json Options

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        Converters =
        {
            new UniversalFlexibleEnumConverter(),
            new ForceStringConverter(),
            new FlexibleLongConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    #endregion

    /// <summary>
    /// Serializes a .NET value to JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="indent">use indent</param>
    /// <param name="mode">The datetime format</param>
    internal static string ToJson<T>(this T value) =>value is JsonNode json ? json.ToString() : JsonSerializer.Serialize(value, DefaultJsonOptions);

    /// <summary>
    /// Deserializes a JSON string to a .NET value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="mode">The date format</param>
    internal static T? FromJson<T>(this string value) => (T?)value.FromJson(typeof(T));

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
            
        return JsonSerializer.Deserialize(value, type, DefaultJsonOptions);
    }

    /// <summary>
    /// Convert the JsonNode to the given type
    /// </summary>
    internal static T? FromJson<T>(this JsonNode value) => (T?)value.FromJson(typeof(T));

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
        return value.Deserialize(type, DefaultJsonOptions);
    }

    internal static JsonNode? ToJsonNode<T>(this T? value, bool noError = false)
    {
        try
        {
            if (value == null) return null;
            if (typeof(T).IsAssignableTo(typeof(JsonNode))) return (JsonNode?)(object)value;
            return JsonSerializer.SerializeToNode(value, DefaultJsonOptions);
        }
        catch 
        {
            // not able to convert
            if (!noError) throw;
            return null;
        }
    }

    internal static T? ToValue<T>(this JsonNode node) => (T?)(TryConvert(typeof(T), node) ?? default(T?));

    static readonly string[] DateFormats =
    {
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy/M/d H:mm:ss zzz",
         "yyyy/M/d H:mm:ss",
         "yyyyMMdd",
         "yyyyMMddHHmmss"
    };

    internal static bool TryParseDateTimeOffset(string str, out DateTimeOffset? dateTime)
    {
        if (DateTimeOffset.TryParseExact(
              str,
              DateFormats,
              CultureInfo.InvariantCulture,
              DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
              out var dto))
        {
            dateTime = dto;
            return true;
        }
        dateTime = null;
        return false;
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
                    s = s.Trim();

                    if (TryParseDateTimeOffset(s, out var dto))
                    {
                        return (dto, typeof(DateTimeOffset));
                    }

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
    internal static JsonNode? GetValueByPaths(this JsonNode? token, string paths) => GetValueByPaths(token, paths.Split('.', StringSplitOptions.RemoveEmptyEntries));

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
    internal static bool IsArrayType(this Type type) => type != typeof(string) && 
        type != typeof(ArrayTypeNode) && 
        ( type.IsSZArray || type.IsSubclassOfGenericType(typeof(List<>)) || 
        type.IsSubclassOfGenericType(typeof(IEnumerable<>)));
    
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

    #region Type Conversion

    internal static T? TryConvertTo<T>(this object? value) => (T?)TryConvert(typeof(T), value);

    /// <summary>
    /// Try to convert the value for the given type
    /// </summary>
    internal static object? TryConvert(this Type type, object? value)
    {
        try
        {
            // value match
            if (value == null) return null;
            type = type.GetNotNullType();
            if (value.GetType().IsAssignableTo(type)) return value;

            // for schema node
            if (value is AnySchemaNode node) return node.ToValue(type);

            // json type
            if (value is JsonElement ele)
            {
                value = ele.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => ele.GetString(),
                    JsonValueKind.Number => ele.TryGetInt64(out var l) ? l :
                                            ele.TryGetDouble(out var d) ? d : ele.GetDecimal(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Array => ele.EnumerateArray().Select(e => (object?)e).ToArray(),
                    JsonValueKind.Object => JsonNode.Parse(ele.GetRawText()),
                    _ => null
                };
                if (value == null) return null;
                if (value.GetType().IsAssignableTo(type)) return value;
            }

            if (value is (JsonArray or JsonObject))
                return (value as JsonNode).Deserialize(type, DefaultJsonOptions);
            
            if (type == typeof(JsonArray))
                return JsonSerializer.SerializeToNode(value, DefaultJsonOptions) as JsonArray;

            if (type == typeof(JsonObject))
                return JsonSerializer.SerializeToNode(value, DefaultJsonOptions) as JsonObject;

            if (type == typeof(JsonValue))
                return JsonValue.Create(value);

            if (type == typeof(JsonNode))
                return JsonSerializer.SerializeToNode(value, DefaultJsonOptions);

            // none json type
            if (value is JsonValue v)
            {
                switch (v.GetValueKind())
                {
                    case JsonValueKind.String:
                        if (v.TryGetValue(out string? s))
                        {
                            s = s.Trim();

                            if (TryParseDateTimeOffset(s, out var dto))
                                value = dto;
                            else
                                value = s;
                        }
                        else
                            value = null;
                        break;

                    case JsonValueKind.Number:
                        if (v.TryGetValue(out long l))
                            value = l;
                        else if (v.TryGetValue(out int i))
                            value = i;
                        else if (v.TryGetValue(out double db))
                            value = db;
                        else if (v.TryGetValue(out float f))
                            value = f;
                        else if (v.TryGetValue(out decimal d))
                            value = d;
                        else
                            value = null;
                        break;
                    case JsonValueKind.True:
                        value = true;
                        break;
                    case JsonValueKind.False:
                        value = false;
                        break;
                    default:
                        value = null;
                        break;
                }
                if (value == null) return null;
                if (value.GetType().IsAssignableTo(type)) return value;
            }
            // for collections
            else if (value is Array arr)
            {
                return ConvertToCollection(arr.Cast<object?>(), type);
            }
            else if (value is not string && value is IEnumerable iter)
            {
                return ConvertToCollection(iter.Cast<object?>(), type);
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
                {
                    string? str = value?.ToString();
                    if (DateTimeOffset.TryParseExact(
                            str,
                            DateFormats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out var dto))
                    {
                        return dto.DateTime;
                    }

                    if (DateTime.TryParseExact(
                            str,
                            DateFormats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var dt))
                    {
                        return dt;
                    }
                    return Convert.ToDateTime(value);
                }
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
                if (value is string s)
                {
                    if (TryParseDateTimeOffset(s, out var dto)) return dto;
                    return DateTimeOffset.Parse(s);
                }
                if (value is long or int)
                    return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(value));
                return null;
            }
            else
            {
                return JsonSerializer.SerializeToNode(value, DefaultJsonOptions)?.Deserialize(type, DefaultJsonOptions);
            }
        }
        catch(Exception ex)
        {
            throw new InvalidCastException($"Cannot convert the value '{value}' to type '{type.FullName}'", ex);
        }
    }

    /// <summary>
    /// Convert a flat object sequence into the target collection type with properly typed elements.
    /// </summary>
    private static object? ConvertToCollection(IEnumerable<object?> source, Type targetType)
    {
        if (targetType.IsSZArray)
        {
            Type? eleType = targetType.GetElementType();
            if (eleType == null) return null;
            var items = source.ToArray();
            var result = Array.CreateInstance(eleType, items.Length);
            for (int i = 0; i < items.Length; i++)
                result.SetValue(TryConvert(eleType, items[i]), i);
            return result;
        }
        if (targetType.IsSubclassOfGenericType(typeof(List<>)))
        {
            Type eleType = targetType.GetGenericBaseType(typeof(List<>))!.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(eleType))!;
            foreach (var item in source)
                list.Add(TryConvert(eleType, item));
            return list;
        }
        if (targetType.IsSubclassOfGenericType(typeof(IEnumerable<>)))
        {
            Type eleType = targetType.GetGenericBaseType(typeof(IEnumerable<>))!.GetGenericArguments()[0];
            var items = source.ToArray();
            var result = Array.CreateInstance(eleType, items.Length);
            for (int i = 0; i < items.Length; i++)
                result.SetValue(TryConvert(eleType, items[i]), i);
            return result;
        }
        return null;
    }

    #endregion

    #region XML Documentation

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

    /// <summary>
    /// Load XML once and query the node by name.
    /// </summary>
    private static string? GetSummaryFromXmlDocInternal(Assembly asm, string memberName, string? subPath = null)
    {
        string xmlPath = asm.Location.Replace(".dll", ".xml");
        XmlDocument? doc = LoadXml(xmlPath);
        if (doc == null) return null;

        XmlNode? memberNode = doc.SelectSingleNode($"/doc/members/member[@name='{memberName}']");
        if (memberNode == null) return null;

        if (!string.IsNullOrEmpty(subPath))
        {
            XmlNode? subNode = memberNode.SelectSingleNode(subPath!);
            return subNode != null ? CleanupSummary(subNode.InnerText) : null;
        }

        return CleanupSummary(memberNode.InnerText);
    }

    /// <summary>
    /// Load xml with caching
    /// </summary>
    private static XmlDocument? LoadXml(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            return null;

        return XmlFiles.GetOrAdd(xmlPath, static path =>
        {
            var doc = new XmlDocument();
            doc.Load(path);
            return doc;
        });
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