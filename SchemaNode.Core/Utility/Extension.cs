using SchemaNode.Node;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Utility;

internal static class Extension
{
    #region Utility

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

    private static readonly string[] DateFormats =
    [
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
    ];

    #endregion

    #region Generic

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
    /// Serializes a .NET value to JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    internal static string ToJson<T>(this T value) =>value is JsonNode json ? json.ToString() : JsonSerializer.Serialize(value, DefaultJsonOptions);

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
    
    internal static T? TryConvertTo<T>(this object? value) => (T?)typeof(T).TryConvert(value);

    #endregion
    
    #region String
    
    /// <param name="value"></param>
    extension(string value)
    {
        internal bool TryParseDateTimeOffset(out DateTimeOffset? dateTime)
        {
            if (DateTimeOffset.TryParseExact(
                    value,
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
        /// Returns the camel case of this string.
        /// </summary>
        internal string ToCamelCase() => value.Length > 0 ? string.Concat(value[..1].ToLowerInvariant(), value.AsSpan(1)) : value;

        /// <summary>
        /// Gets the base type
        /// </summary>
        internal string GetBaseType() => value.Contains('<') ? value[..value.IndexOf('<')] : value;

        /// <summary>
        /// Gets the namespace
        /// </summary>
        internal string GetNamespace()
        {
            SpanReader reader = value;
            while (!reader.IsEnd)
                reader.NextNamespace();
            return reader.Previous.ToString().Trim('.');
        }

        /// <summary>
        /// Gets the schema name
        /// </summary>
        /// <returns></returns>
        internal string GetSchemaName()
        {
            SpanReader reader = value;
            while (!reader.IsEnd)
                reader.NextNamespace();
            return reader.Current.ToString().Trim('.');
        }

        /// <summary>
        /// Remove the ending part if existed
        /// </summary>
        internal string RemoveEnding(string ending) => value.EndsWith(ending, StringComparison.OrdinalIgnoreCase) ? value[..^ending.Length] : value;

        /// <summary>
        /// Remove the start part if existed
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        internal string RemoveStart(string start) => value.StartsWith(start, StringComparison.OrdinalIgnoreCase) ? value[start.Length..] : value;

        /// <summary>
        /// Deserializes a JSON string to a .NET value.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        internal T? FromJson<T>() => (T?)value.FromJson(typeof(T));

        /// <summary>
        /// Deserializes a JSON string to a .NET value.
        /// </summary>
        internal object? FromJson(Type type)
        {
            if (type == typeof(string))
                return value;
            if (type == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(value);
            if (type == typeof(DateTime))
                return DateTime.Parse(value);

            return JsonSerializer.Deserialize(value, type, DefaultJsonOptions);
        }
    }

    #endregion

    #region JSON

    extension(JsonNode? value)
    {
        /// <summary>
        /// Convert the JsonNode to the given type
        /// </summary>
        internal T? FromJson<T>() => (T?)value.FromJson(typeof(T));

        internal object? FromJson(Type type)
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
       
        internal T? ToValue<T>() => (T?)(typeof(T).TryConvert(value) ?? default(T?));
        
        /// <summary>
        /// Gets the value with paths
        /// </summary>
        internal JsonNode? GetValueByPaths(IEnumerable<string> paths)
        {
            JsonNode? token = value;
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
        internal JsonNode? GetValueByPaths(string paths) => value.GetValueByPaths(paths.Split('.', StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Whether the json node is empty
        /// </summary>
        internal bool IsEmpty()
        {
            return value switch
            {
                JsonArray a => a.Count == 0,
                JsonObject o => o.Count == 0,
                JsonValue v => v.ToJsonString() == "null" || string.IsNullOrWhiteSpace(v.ToString()),
                _ => true
            };
        }
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

                    if (s.TryParseDateTimeOffset(out var dto))
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

    #region Type

    extension(Type type)
    {
        /// <summary>
        /// Gets the property name from a property type, checking for Alias meta attribute first.
        /// </summary>
        internal string GetPropertyName()
            => type.GetMetaProperty<Alias>()?.Value ?? type.Name.RemoveEnding("Property").ToCamelCase();

        /// <summary>
        /// Gets the schema type from the type.
        /// </summary>
        internal string? GetSchemaType() => type.GetMetaProperty<SchemaType>()?.Value;

        internal bool IsPrimitiveLike()
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

        internal bool IsNullable() => type.IsSubclassOfGenericType(typeof(Nullable<>));

        /// <summary>
        /// Gets a specific generic base type.
        /// </summary>
        internal Type? GetGenericBaseType(Type genericType)
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
                    Type? result = interfaceType.GetGenericBaseType(genericType);
                    if (result != null) return result;
                }
            }
            else if (type.BaseType != null)
            {
                Type? result = type.BaseType.GetGenericBaseType(genericType);
                if (result != null) return result;
            }

            // Finish.
            return null;
        }

        /// <summary>
        /// Gets a specific generic base type.
        /// </summary>
        internal Type? GetGenericBaseType<T>() => type.GetGenericBaseType(typeof(T));

        /// <summary>
        /// Checks whether a type is a subclass of a specific generic type.
        /// </summary>
        internal bool IsSubclassOfGenericType(Type genericType) => type.GetGenericBaseType(genericType) != null;

        /// <summary>
        /// Checks whether a type is a subclass of a specific generic type.
        /// </summary>
        internal bool IsSubclassOfGenericType<T>() => type.IsSubclassOfGenericType(typeof(T));

        /// <summary>
        /// Gets the not null type
        /// </summary>
        internal Type GetNotNullType() => Nullable.GetUnderlyingType(type) ?? type;

        /// <summary>
        /// Gets the nullable type
        /// </summary>
        internal Type GetNullableType() => type.IsSubclassOfGenericType(typeof(Nullable<>)) ? type : typeof(Nullable<>).MakeGenericType(type);

        /// <summary>
        /// The type is simple array type
        /// </summary>
        internal bool IsArrayType() => type != typeof(string) && 
                                       type != typeof(ArrayNode) && 
                                       ( type.IsSZArray || type.IsSubclassOfGenericType(typeof(List<>)) || 
                                         type.IsSubclassOfGenericType(typeof(IEnumerable<>)));

        internal bool IsSafeConstantValue()
        { 
            if (type.IsValueType || type == typeof(string))
                return true;

            if (typeof(Type).IsAssignableFrom(type))
                return true;

            if (type == typeof(Uri) || type == typeof(Version))
                return true;

            return false;
        }
        /// <summary>
        /// Try to convert the value for the given type.
        /// </summary>
        internal object? TryConvert(object? value)
        {
            Type targetType = type.GetNotNullType();

            try
            {
                // value match
                if (value == null) return null;
                if (value.GetType().IsAssignableTo(targetType)) return value;

                // for schema node
                if (value is IDataNode node) return node.ToTypeValue(targetType);

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
                    if (value.GetType().IsAssignableTo(targetType)) return value;
                }

                if (value is (JsonArray or JsonObject))
                    return (value as JsonNode).Deserialize(targetType, DefaultJsonOptions);

                if (targetType == typeof(JsonArray))
                    return JsonSerializer.SerializeToNode(value, DefaultJsonOptions) as JsonArray;

                if (targetType == typeof(JsonObject))
                    return JsonSerializer.SerializeToNode(value, DefaultJsonOptions) as JsonObject;

                if (targetType == typeof(JsonValue))
                    return JsonValue.Create(value);

                if (targetType == typeof(JsonNode))
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
                    if (value.GetType().IsAssignableTo(targetType)) return value;
                }
                // for collections
                else if (value is Array arr)
                {
                    return ConvertToCollection(arr.Cast<object?>(), targetType);
                }
                else if (value is not string && value is IEnumerable iter)
                {
                    return ConvertToCollection(iter.Cast<object?>(), targetType);
                }

                // Enum convert
                if (targetType.IsEnum)
                {
                    return value is string s
                        ? System.Enum.Parse(targetType, s, ignoreCase: true)
                        : value.GetType().IsPrimitive
                            ? System.Enum.ToObject(targetType, value)
                            : null;
                }

                // Primitive
                switch (Type.GetTypeCode(targetType))
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
                        string? str = value.ToString();
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
                if (targetType == typeof(Guid))
                {
                    return Guid.TryParse(value.ToString(), out Guid result) ? result : null;
                }

                if (targetType == typeof(DateTimeOffset))
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

                return JsonSerializer.SerializeToNode(value, DefaultJsonOptions)?.Deserialize(targetType, DefaultJsonOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Cannot convert the value '{value}' to type '{targetType.FullName}'", ex);
            }
        }

        /// <summary>
        /// Get summary contents from XML document.
        /// </summary>
        internal string? GetSummaryFromXmlDoc(PropertyInfo? prop = null)
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
        internal string? GetSummaryFromXmlDoc(FieldInfo field)
        {
            if (!type.IsEnum) return null;

            string memberName = $"F:{type.FullName!.Replace('+', '.')}.{field.Name}";
            return GetSummaryFromXmlDocInternal(type.Assembly, memberName);
        }
    }

    internal static bool HasClosure(this Delegate method)
    {
        return method.Target != null && method.Target.GetType().FullName == "System.Runtime.CompilerServices.Closure";
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
                result.SetValue(eleType.TryConvert(items[i]), i);
            return result;
        }
        if (targetType.IsSubclassOfGenericType(typeof(List<>)))
        {
            Type eleType = targetType.GetGenericBaseType(typeof(List<>))!.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(eleType))!;
            foreach (var item in source)
                list.Add(eleType.TryConvert(item));
            return list;
        }
        if (targetType.IsSubclassOfGenericType(typeof(IEnumerable<>)))
        {
            Type eleType = targetType.GetGenericBaseType(typeof(IEnumerable<>))!.GetGenericArguments()[0];
            var items = source.ToArray();
            var result = Array.CreateInstance(eleType, items.Length);
            for (int i = 0; i < items.Length; i++)
                result.SetValue(eleType.TryConvert(items[i]), i);
            return result;
        }
        return null;
    }


    extension(MethodInfo method)
    {
        /// <summary>
        /// Get summary contents of method from XML doc.
        /// </summary>
        internal string? GetSummaryFromXmlDoc()
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
        internal string? GetSummaryFromXmlDoc(ParameterInfo parameter)
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
    }

    /// <summary>
    /// Load XML once and query the node by name.
    /// </summary>
    private static string? GetSummaryFromXmlDocInternal(Assembly asm, string memberName, string? subPath = null)
    {
        string xmlPath = asm.Location.Replace(".dll", ".xml");
        XmlDocument? doc = LoadXml(xmlPath);
        XmlNode? memberNode = doc?.SelectSingleNode($"/doc/members/member[@name='{memberName}']");
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