using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Function;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// Provide the system schema
/// </summary>
internal static class Schema
{
    #region Schema <-> CSharp Type

    /// <summary>
    /// Gets the system node schema
    /// </summary>
    internal static NodeSchema? GetSystemNodeSchema(string schemaName)
    {
        schemaName = schemaName.ToLowerInvariant();
        NodeSchema? node = _root;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            node = node.Schemas?.FirstOrDefault(x => x.Name == fullPath);
            if (node == null) return null;
        }
        return node;
    }

    /// <summary>
    /// Save the node schema as system, should only be used to save all system define function
    /// </summary>
    internal static void SaveSystemNodeSchema(NodeSchema schema, Type? type = null)
    {
        schema.LoadState = SchemaLoadState.System;

        string schemaName = schema.Name.ToLowerInvariant();
        NodeSchema root = _root;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+").Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            NodeSchema? node = root.Schemas!.FirstOrDefault(x => x.Name == fullPath);
            if (node == null)
            {
                if (schemaName == fullPath)
                {
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([schema]).ToArray() : [schema];
                }
                else
                {
                    node = new NodeSchema
                    {
                        Name = fullPath,
                        Type = SchemaType.Namespace,
                        LoadState = SchemaLoadState.System,
                        Schemas = []
                    };
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([node]).ToArray() : [node];
                    root = node;
                    root.Schemas ??= [];
                }
            }
            else if (schemaName != fullPath)
            {
                root = node;
                root.Schemas ??= [];
            }
        }

        Console.WriteLine($"System schema: {schemaName}(${schema.Type}) - saved");

        // Register the type map
        if (type != null && schema.Type is SchemaType.Enum or SchemaType.Struct or SchemaType.Array)
        {
            if (schema.Type != SchemaType.Array)
            {
                _systemTypes[schemaName] = type;
                _typeNames[type] = schemaName;
            }
            else
            {
                _typeArrNames[type] = schemaName;
            }
        }
    }

    /// <summary>
    /// Try get the schema name of a assembly type, with auto register
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="autoConv">Whether auto convert the type no matter the attribute existed</param>
    /// <returns>The schema name be registered</returns>
    internal static string? GetSchemaType(this Type type, bool autoConv = false)
    {
        return type.GetSchemaTypeInfo()?.GetSchemaType(autoConv);
    }

    /// <summary>
    /// Try get the schema name of a assembly type, with auto register
    /// </summary>
    internal static string? GetSchemaType(this SchemaParamTypeInfo typeInfo, bool autoConv = false)
    {
        if (typeInfo.Complex) return NS_SYSTEM_JSON; // Complex type, use JsonNode
        if (typeInfo.BaseType == null) return null; // Generic, no schema type
        
        // array & list check
        bool isArray = typeInfo.AnyArray;
        Type type = typeInfo.BaseType;

        // Already registered
        if (isArray ? _typeArrNames.TryGetValue(type, out var typeName) : _typeNames.TryGetValue(type, out typeName)) return typeName;
        
        // Common
        if (type == typeof(JsonArray) || type == typeof(ArrayTypeNode))
        {
            return NS_SYSTEM_ARRAY;
        }
        else if (type == typeof(JsonObject) || type == typeof(StructTypeNode))
        {
            return NS_SYSTEM_STRUCT;
        }
        else if (type == typeof(JsonNode))
        {
            return NS_SYSTEM_JSON;
        }

        // Basic value check
        if (!type.IsEnum)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    typeName = NS_SYSTEM_BOOL;
                    break;
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return isArray ? NS_SYSTEM_INTS : NS_SYSTEM_INT;
                case TypeCode.Single:
                    return isArray ? NS_SYSTEM_NUMBERS : NS_SYSTEM_FLOAT;
                case TypeCode.Double:
                    return isArray ? NS_SYSTEM_NUMBERS : NS_SYSTEM_DOUBLE;
                case TypeCode.Decimal:
                    return isArray ? NS_SYSTEM_NUMBERS : NS_SYSTEM_NUMBER;
                case TypeCode.DateTime:
                    typeName = NS_SYSTEM_DATE;
                    break;
                case TypeCode.Char:
                case TypeCode.String:
                    return isArray ? NS_SYSTEM_STRINGS : NS_SYSTEM_STRING;
            }
        }

        if (type == typeof(Guid))
        {
            typeName = NS_SYSTEM_GUID;
        }
        else if (type == typeof(DateTimeOffset))
        {
            typeName = NS_SYSTEM_DATE;
        }

        if (string.IsNullOrWhiteSpace(typeName) && !_typeNames.TryGetValue(type, out typeName))
        {
            // try generate
            NodeSchema[]? schemas = null;
            bool shouldConv = autoConv || type.GetCustomAttribute<SchemaTypeAttribute>() != null || type.GetCustomAttribute<SchemaAppAttribute>() != null;
            if (type.IsClass)
            {
                // static class as api container
                if (type.IsAbstract)
                {
                    if (type.IsSealed)
                    {
                        // static class as method container
                        SchemaTypeAttribute? funcNsAttr = type.GetCustomAttribute<SchemaTypeAttribute>();
                        if (funcNsAttr != null)
                        {
                            List<NodeSchema>? funcNs = null;
                            foreach (MethodInfo info in type.GetMethods().Where(m => m.IsStatic && m.GetCustomAttribute<SchemaTypeAttribute>() != null))
                            {
                                NodeSchema? func = FunctionType.GenerateSystemFunction(info, funcNsAttr.Name);
                                if (func == null) continue;
                                funcNs ??= [];
                                funcNs.Add(func);
                            }
                            if (funcNs != null) schemas = funcNs.ToArray();
                        }
                    }
                }
                else
                {
                    if (shouldConv) 
                        schemas = StructType.GenerateSystemStruct(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaTypeAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaTypeAttribute>()?.Name);
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                {
                    if (shouldConv) 
                        schemas = EnumType.GenerateSystemEnum(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaTypeAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaTypeAttribute>()?.Name);
                }
                else if (!type.IsPrimitiveLike())
                {
                    // struct
                    if (shouldConv) 
                        schemas = StructType.GenerateSystemStruct(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaTypeAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaTypeAttribute>()?.Name);
                }
            }

            if (schemas is not { Length: > 0 }) return null;
            foreach (NodeSchema schema in schemas)
            {
                schema.LoadState = SchemaLoadState.System;
                SaveSystemNodeSchema(schema, type);
            }
            typeName = schemas[0].Name;
        }
        
        if (!isArray) return typeName;
        
        // auto-build array schema
        NodeSchema? arraySchema = GetSystemNodeSchema($"{typeName}s");
        if (arraySchema != null) return arraySchema.Name;
        arraySchema = new NodeSchema
        {
            Name = $"{typeName}s",
            Type = SchemaType.Array,
            LoadState = SchemaLoadState.System,
            Display = $"[Array]{typeName}",
            Array = new ArraySchema
            {
                Element = typeName
            }
        };
        SaveSystemNodeSchema(arraySchema, type);
        return arraySchema.Name;
    }

    /// <summary>
    /// Gets the parameter type info in the schema system
    /// </summary>
    internal static SchemaParamTypeInfo? GetSchemaTypeInfo(this Type? input, bool autoConv = false)
    {
        if (input == null) return null;

        SchemaParamTypeInfo? result;
        
        if (input.IsGenericType) // IList<T>, IList<int>
        {
            Type[] args = input.GetGenericArguments();
            if (args.Length != 1)
            {
                return new SchemaParamTypeInfo
                {
                    Kind = ParameterTypeKind.GenericType | ParameterTypeKind.Complex,
                    Type = typeof(JsonNode),
                    BaseType = typeof(JsonNode),
                    SchemaType = NS_SYSTEM_JSON,
                };
            };
            
            result = GetSchemaTypeInfo(args[0]);
            if (result == null) return null;

            Type genType = input.GetGenericTypeDefinition();
            
            // T?
            if (genType == typeof(Nullable<>))
            {
                result.Kind |= ParameterTypeKind.Nullable;
            }
            
            // IList<T>, List<T>
            else if (genType == typeof(IList<>) || genType == typeof(List<>))
            {
                result.Kind |= ParameterTypeKind.List;
            }
            
            // IEnumerable<T>
            else if (genType == typeof(IEnumerable<>))
            {
                result.Kind |= ParameterTypeKind.Enumerable;
            }

            // Task<T>
            else if (genType == typeof(Task<>))
            {
                result.Kind |= ParameterTypeKind.Task;
            }
            
            // Like Dict<TK, TV>
            else
            {
                return new SchemaParamTypeInfo
                {
                    Kind = ParameterTypeKind.GenericType | ParameterTypeKind.Complex,
                    Type = typeof(JsonNode),
                    BaseType = typeof(JsonNode),
                    SchemaType = NS_SYSTEM_JSON,
                };
            }

            result.Kind |= ParameterTypeKind.GenericType;
        }
        else if (input.IsGenericParameter) // T where T: INumber<T>
        {
            // Only check INumber<T>, IFloatPoint<T>, don't cover full constraints
            var constraints = input.GetGenericParameterConstraints();
            bool isNumber = false;
            bool isFloat = false;

            foreach (Type constraint in constraints)
            {
                if (!constraint.IsGenericType) continue;
                if (constraint.GetGenericTypeDefinition() == typeof(INumber<>))
                {
                    isNumber = true;
                }
                else if (constraint.GetGenericTypeDefinition() == typeof(IFloatingPoint<>))
                {
                    isFloat = true;
                }
            }
            return new SchemaParamTypeInfo
            {
                Generic = input,
                Kind = ParameterTypeKind.GenericParameter | 
                       (isNumber ? ParameterTypeKind.Number : isFloat ? ParameterTypeKind.Float : ParameterTypeKind.Normal)
            };
        }
        else if (input.IsArray)
        {
            // only allow one-level array
            if (!input.IsSZArray) return null;
            result = GetSchemaTypeInfo(input.GetElementType());
            if (result == null) return null;
            result.Kind |= ParameterTypeKind.Array;
        }
        else
        {
            result = new SchemaParamTypeInfo
            {
                BaseType = input,
            };
        }

        // auto conv type to schema type
        result.Type = input;
        if (autoConv && result.BaseType != null) result.SchemaType = GetSchemaType(result, true);
        return result;
    }

    /// <summary>
    /// Gets the schema type info from any schema node
    /// </summary>
    internal static SchemaParamTypeInfo? GetSchemaTypeInfo(this AnySchemeType node)
    {
        return node switch
        {
            ScalarType or EnumType or StructType or ArrayType => new SchemaParamTypeInfo
            {
                Type = node.ToCSharpType(), 
                SchemaType = node.Name
            },
            JsonType => new SchemaParamTypeInfo
            {
                Type = typeof(JsonNode), 
                SchemaType = NS_SYSTEM_JSON
            },
            _ => null
        };
    }

    /// <summary>
    /// Gets the C# type by schema name
    /// </summary>
    internal static Type ToCSharpType(this AnySchemeType node, bool? nullable = false)
    {
        bool isArray = false;
        Type? type = null;
        
        // json
        if (node is JsonType)
            return typeof(JsonNode);
        
        // array
        if (node is ArrayType array)
        {
            if (array.ElementSchemaType == null)
            {
                return typeof(ArrayTypeNode);
            }
            else
            {
                isArray = true;
                node = array.ElementSchemaType;
            }
        }
        if (type is null && !_systemTypes.TryGetValue(node.Name.ToLowerInvariant(), out type))
        {
            if (node is EnumType enumNode)
            {
                type = enumNode.ValueType == EnumValueType.String ? typeof(string) : typeof(Int64);
            }
            else if (node is ScalarType scalar)
            {
                if (scalar.IsBool)
                {
                    type = typeof(bool);
                }
                else if (scalar.IsInt)
                {
                    type = typeof(long);
                }
                else if(scalar.IsSingle)
                {
                    type = typeof(float);
                }
                else if(scalar.IsDouble)
                {
                    type = typeof(double);
                }
                else if(scalar.IsNumber)
                {
                    type = typeof(decimal);
                }
                else if (scalar.IsString)
                {
                    type = typeof(string);
                }
                else if (scalar.IsDate)
                {
                    type = typeof(DateTime);
                }
                else
                {
                    type = isArray ? typeof(ArrayTypeNode) : typeof(ScalarTypeNode);
                    isArray = false;
                }
            }
            else if(node is StructType)
            {
                type = isArray ? typeof(ArrayTypeNode) : typeof(StructTypeNode);
                isArray = false;
            }
        }

        // cover all
        if (type == null)
        {
            type ??= isArray ? typeof(ArrayTypeNode) : typeof(StructTypeNode);
        }
        else if (isArray)
        {
            type = typeof(List<>).MakeGenericType(type);
        }
        if (nullable ?? false)
        {
            type = typeof(Nullable<>).MakeGenericType(type);
        }
        return type;
    }

    #endregion

    #region Group Join

    /// <summary>
    /// Join to scalar
    /// </summary>
    internal static AnySchemaNode? GroupJoin(AnySchemaNode? value, DataCombineType method)
    {
        return method switch
        {
            DataCombineType.Assign => value is ArrayTypeNode arr ? arr.LastOrDefault() : value,
            DataCombineType.Init => value is ArrayTypeNode arr ? arr.FirstOrDefault() : value,
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Join to scalar
    /// </summary>
    internal static AnySchemaNode? GroupJoin(ScalarType node, AnySchemaNode? value, DataCombineType method)
    {
        return method switch
        {
            DataCombineType.Assign => value is ArrayTypeNode arr ? arr.LastOrDefault() : value,
            DataCombineType.Init => value is ArrayTypeNode arr ? arr.FirstOrDefault() : value,
            DataCombineType.Sum => new ScalarTypeNode(node, value is ArrayTypeNode arr ? arr.Select(a => a.ToValue<decimal>()).Sum() : (value?.Value ?? 0m)),
            DataCombineType.Count => new ScalarTypeNode(node, value is ArrayTypeNode arr ? arr.Count : 0),
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Join to struct
    /// </summary>
    internal static AnySchemaNode? GroupJoin(StructType node, AnySchemaNode? value, IReadOnlyDictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty || node.Fields.Length == 0) return null;
        switch (value)
        {
            case StructTypeNode @struct:
                {
                    // count field
                    foreach ((string field, DataCombineType method) in joinMethodMap)
                    {
                        if (method == DataCombineType.Count && node.Fields.FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) is { TypeNode: ScalarType { IsNumber: true } })
                        {
                            @struct[field] = 1;
                        }
                    }
                    return @struct;
                }
            case ArrayTypeNode { Count: > 0 } array:
                {
                    // Join
                    StructTypeNode result = new(node);
                    foreach (StructFieldConfig field in node.Fields)
                    {
                        switch (joinMethodMap.GetValueOrDefault(field.Name, DataCombineType.Assign))
                        {
                            case DataCombineType.Assign:
                                {
                                    StructTypeNode? last = (StructTypeNode?)array.LastOrDefault(p => p is StructTypeNode obj && !obj.GetField(field.Name)!.IsEmpty);
                                    if (last != null) result[field.Name] = last[field.Name];
                                    break;
                                }
                            case DataCombineType.Init:
                                {
                                    StructTypeNode? first = (StructTypeNode?)array.FirstOrDefault(p => p is StructTypeNode obj && !obj.GetField(field.Name)!.IsEmpty);
                                    if (first != null) result[field.Name] = first[field.Name];
                                    break;
                                }
                            case DataCombineType.Sum:
                                result[field.Name] = field.TypeNode is ScalarType { IsNumber: true } ? array.Sum(p => p is StructTypeNode obj && obj[field.Name] is ScalarTypeNode val && !val.IsEmpty ? val.ToValue<decimal>() : 0) : null;
                                break;
                            case DataCombineType.Count:
                                result[field.Name] = field.TypeNode is ScalarType { IsNumber: true } ? array.Count : null;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    return value;
                }
        }
        return null;
    }

    /// <summary>
    /// Join to array
    /// </summary>
    internal static Dictionary<string, StructTypeNode> GroupJoinObjectMap(ArrayType node, AnySchemaNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (value == null || value.IsEmpty) return new();

        // Gets field type
        StructType @struct = (StructType)node.ElementSchemaType!;
        string[] valueFields = (from fieldType in @struct.Fields where !node.Primary!.Contains(fieldType.Name) select fieldType.Name).ToArray();

        // The element struct type
        switch (value)
        {
            // Check by value
            case StructTypeNode { IsEmpty: false } o:
                {
                    // Check the primary key
                    string? key = node.GetPrimaryKey(o);
                    if (string.IsNullOrWhiteSpace(key)) return new();

                    // Return single element array
                    return new() { { key, o } };
                }
            case ArrayTypeNode array:
                {
                    // The return list with order
                    Dictionary<string, StructTypeNode> keyMap = new();
                    Dictionary<string, int> keyCount = new();
                    foreach (var token in array)
                    {
                        if (token is not StructTypeNode obj) continue;

                        // Gets the key
                        string? key = node.GetPrimaryKey(obj);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (keyMap.TryGetValue(key, out StructTypeNode? total))
                        {
                            // Join the data fields
                            keyCount[key]++;
                            foreach (string s in valueFields)
                            {
                                switch (joinMethodMap.GetValueOrDefault(s, DataCombineType.Assign))
                                {
                                    case DataCombineType.Assign:
                                        {
                                            if (obj[s] is AnySchemaNode { IsEmpty: false } sp)
                                                total[s] = sp;
                                            break;
                                        }

                                    case DataCombineType.Init:
                                        if (!(total[s] is AnySchemaNode { IsEmpty: false }) && obj[s] is AnySchemaNode { IsEmpty: false } c)
                                            total[s] = c;
                                        break;

                                    case DataCombineType.Sum:
                                        total[s] = (total[s] is AnySchemaNode { IsEmpty: false } t ? t.ToValue<decimal>() : 0) +
                                                   (obj[s] is AnySchemaNode { IsEmpty: false } n ? n.ToValue<decimal>() : 0);
                                        break;

                                    case DataCombineType.Count:
                                        total[s] = (total[s] is AnySchemaNode { IsEmpty: false } d ? d.ToValue<int>() : 0) + 1;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                        else
                        {
                            // Add to order list
                            keyMap[key] = obj;
                            keyCount[key] = 1;

                            // Init Count
                            foreach ((string s, DataCombineType m) in joinMethodMap)
                                if (m == DataCombineType.Count)
                                    obj[s] = 1;
                        }
                    }

                    // Gen the result
                    return keyMap;
                }
        }
        return new();
    }

    /// <summary>
    /// Join to array
    /// </summary>
    internal static ArrayTypeNode? GroupJoin(ArrayType node, AnySchemaNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (node.ElementSchemaType is not StructType structNode || node.Primary == null) return null;
        Dictionary<string, AnySchemeType?> primaryNodes = structNode.Fields.Where(fieldType => node.Primary.Contains(fieldType.Name)).ToDictionary(fieldType => fieldType.Name, fieldType => fieldType.TypeNode);

        // Result
        Dictionary<string, StructTypeNode> resultMap = GroupJoinObjectMap(node, value, joinMethodMap);
        List<StructTypeNode> joinObjs = resultMap.Values.ToList();
        joinObjs.Sort((a, b) =>
        {
            foreach (string s in node.Primary)
            {
                switch (primaryNodes[s])
                {
                    case ScalarType { IsDate: true }:
                        {
                            DateTime ad = a.GetField(s)!.ToValue<DateTime>();
                            DateTime bd = b.GetField(s)!.ToValue<DateTime>();
                            if (!ad.Equal(bd))
                                return ad.LessThan(bd) ? -1 : 1;
                            break;
                        }
                    case ScalarType { IsNumber: true }:
                        {
                            decimal ad = a.GetField(s)!.ToValue<decimal>();
                            decimal bd = b.GetField(s)!.ToValue<decimal>();
                            if (ad != bd)
                                return ad < bd ? -1 : 1;
                            break;
                        }
                    default:
                        {
                            string ad = a[s]?.ToString() ?? string.Empty;
                            string bd = b[s]?.ToString() ?? string.Empty;
                            if (!ad.Equals(bd))
                                return string.Compare(ad, bd, StringComparison.OrdinalIgnoreCase);
                            break;
                        }
                }
            }
            return 0;
        });
        return new ArrayTypeNode(node, joinObjs);
    }

    #endregion

    #region Inner type

    /// <summary>
    /// The generic type info
    /// </summary>
    internal class SchemaParamTypeInfo
    {
        /// <summary>
        /// The generic type, only allow "T", "T1", "T2" and etc
        /// </summary>
        public Type? Generic { get; set; }
    
        /// <summary>
        /// The original type
        /// </summary>
        public Type? Type { get; set; }
        
        /// <summary>
        /// The base type
        /// </summary>
        public Type? BaseType { get; set; }
    
        /// <summary>
        /// The schema type
        /// </summary>
        public string? SchemaType { get; set; }
    
        /// <summary>
        /// The generic type kind
        /// </summary>
        public ParameterTypeKind Kind { get; set; } = ParameterTypeKind.Normal;

        #region State

        public bool Nullable => (Kind & ParameterTypeKind.Nullable) > 0;
        
        public bool List => (Kind & (ParameterTypeKind.List)) > 0;

        public bool Enumerable => (Kind & (ParameterTypeKind.Enumerable)) > 0;

        public bool Array => (Kind & ParameterTypeKind.Array) > 0;

        public bool AnyArray => (Kind & (ParameterTypeKind.List | ParameterTypeKind.Array | ParameterTypeKind.Enumerable)) > 0;
        
        public bool Task => (Kind & ParameterTypeKind.Task) > 0;
        
        public bool Number => (Kind & (ParameterTypeKind.Number | ParameterTypeKind.Float)) > 0;
        
        public bool OnlyFloat => (Kind & ParameterTypeKind.Float) > 0;
        
        public bool Complex => (Kind & ParameterTypeKind.Complex) > 0;
        
        #endregion
        
        #region Method

        /// <summary>
        /// Parse the value to get the real value, type and generic type
        /// </summary>
        public (object? value, Type? type, Type? generic) ParseValue(JsonNode? node, Type? generic = null)
        {
            if (node == null || node.IsEmpty()) return (null, Type, generic);
            if (Generic != null)
            {
                if (node is JsonArray arr)
                {
                    if (!AnyArray) return (null, Type, generic);
                    if (generic == null)
                    {
                        if (arr.Count == 0) return (arr, typeof(JsonArray), null); // unkown
                        var ele = arr[0];
                        if (ele is JsonObject)
                        {
                            return (arr, typeof(JsonArray), null);
                        }
                        else if (ele is JsonValue val)
                        {
                            (object? _, Type? type) = val.ParseValueAndType();
                            if (type == null) return (arr, typeof(JsonArray), null); // unkown
                            generic = type;                            
                        }
                        else
                        {
                            return (arr, typeof(JsonArray), null); // unkown
                        }
                    }

                    if (Enumerable)
                    {
                        try
                        {
                            MethodInfo method = _convToEnum.GetOrAdd(generic, t => typeof(Schema).GetMethod(nameof(ConvertToEnumerable), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
                            return (method.Invoke(null, [arr]), typeof(IEnumerable<>).MakeGenericType(generic), generic);
                        }
                        catch
                        {
                            // pass
                        }
                    }

                    Type arrType = Array ? generic.MakeArrayType() : typeof(List<>).MakeGenericType(generic);
                    try
                    {
                        return (node.FromJson(arrType), arrType, generic);
                    }
                    catch
                    {
                        // pass
                    }

                    try
                    {
                        if (Array)
                        {
                            MethodInfo method = _arrConv.GetOrAdd(generic, t => typeof(Schema).GetMethod(nameof(ConvertToArray), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
                            return (method.Invoke(null, [arr]), arrType, generic);
                        }
                        else
                        {
                            MethodInfo method = _lstConv.GetOrAdd(generic, t => typeof(Schema).GetMethod(nameof(ConvertToList), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
                            return (method.Invoke(null, [arr]), arrType, generic);
                        }
                    }
                    catch
                    {
                        // pass
                    }

                    return (null, arrType, generic);
                }
                
                // single
                if (AnyArray) return (null, Type, generic);
                if (node is JsonObject obj)
                {
                    return (obj, typeof(JsonObject), null);
                }
                else if (node is JsonValue val)
                {
                    (object? value, Type? type) = val.ParseValueAndType();
                    if (value == null) return (null, Type, generic);
                    if (generic != null)
                    {
                        try
                        {
                            value = generic.TryConvert(value);
                            return (value, generic, generic);
                        }
                        catch
                        {
                            return (null, Type, generic);
                        }
                    }
                    return (value, type, type);
                }
            }
            else if (Type != null)
            {
                // list JsonArray for IList
                if (Type.IsAssignableFrom(node.GetType())) return (node, Type, null);

                // not generic
                try
                {
                    return (node?.FromJson(Type), Type, null);
                }
                catch
                {
                    // pass
                }
            }

            return (null, Type, generic);
        }
        
        #endregion
    }

    /// <summary>
    /// The parameter type kind
    /// </summary>
    [Flags]
    internal enum ParameterTypeKind
    {
        Normal = 0,
        Nullable = 1 << 0,
        Number = 1 << 1,
        Float = 1 << 2,
        List = 1 << 3,
        Array = 1 << 4,
        Enumerable = 1 << 5,
        Task = 1 << 6,
        GenericType = 1 << 7,
        GenericParameter = 1 << 8,
        Complex = 1 << 9, // Dict<TK, TV> || JsonNode || other complex type
    }
    
    #endregion
    
    #region Utility

    static IEnumerable<T> ConvertToEnumerable<T>(JsonArray arr)
    {
        Type type = typeof(T);
        return arr.Select(a => (T)type.TryConvert(a)!);
    }

    static T[] ConvertToArray<T>(JsonArray arr)
    {
        Type type = typeof(T);
        return arr.Select(a => (T)type.TryConvert(a)!).ToArray();
    }

    static List<T> ConvertToList<T>(JsonArray arr)
    {
        Type type = typeof(T);
        return arr.Select(a => (T)type.TryConvert(a)!).ToList();
    }

    static ConcurrentDictionary<Type, MethodInfo> _convToEnum = [];
    static ConcurrentDictionary<Type, MethodInfo> _arrConv = [];
    static ConcurrentDictionary<Type, MethodInfo> _lstConv = [];

    // System type maps
    private static ConcurrentDictionary<string, Type> _systemTypes = [];
    private static ConcurrentDictionary<Type, string> _typeNames  = [];
    private static ConcurrentDictionary<Type, string> _typeArrNames = [];

    #endregion
    
    #region System Schema

    // The basic system schema
    static readonly NodeSchema _root = new NodeSchema
    {
        Name = "",
        Type = SchemaType.Namespace,
        LoadState = SchemaLoadState.System,
        Schemas =
        [
            // System types
            new NodeSchema
            {
                Name = NS_SYSTEM,
                Type = SchemaType.Namespace,
                LoadState = SchemaLoadState.System,
                Display = NS_SYSTEM,
                Schemas =
                [
                    #region base type
                    
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_ARRAY,
                        Type = SchemaType.Array,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_ARRAY,
                        Array = new ArraySchema
                        {
                            Element = "",
                        }
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_STRUCT,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_STRUCT,
                        Struct = new StructSchema
                        {
                            Fields = []
                        }
                    },
                    new NodeSchema()
                    {
                        Name = NS_SYSTEM_JSON,
                        Type = SchemaType.Json,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_JSON,
                    },
                    
                    #endregion

                    #region scalar
                    
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_BOOL,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_BOOL,
                        Scalar = new ScalarSchema
                        {
                            Error = "system.bool.error"
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_DATE,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_DATE,
                        Scalar = new ScalarSchema
                        {
                            Error = "system.date.error"
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_NUMBER,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_NUMBER,
                        Scalar = new ScalarSchema
                        {
                            Error = "system.number.error",
                            Regex = @"^(\\-|\\+)?\\d+(\\.\\d+)?(e\\-\\d+)?$",
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_DOUBLE,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_DOUBLE,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_NUMBER,
                            Error = "system.double.error",
                            Regex = @"^-?\\d+\\.?\\d+$",
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_FLOAT,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_FLOAT,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_DOUBLE,
                            Error = "system.float.error",
                            Regex = @"^\\d+(\\.\\d+)?$",
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_PERCENT,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_PERCENT,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_FLOAT,
                            Error = "system.percent.error",
                            Regex = @"^\\d+(\\.\\d+)?$",
                            UpLimit = 100,
                            LowLimit = 0
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_FULL_DATE,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_FULL_DATE,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_DATE,
                            Error = "system.fulldate.error",
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_INT,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_INT,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_NUMBER,
                            Error = "system.int.error",
                            Regex = @"^(\\-|\\+)?\\d+$",
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_STRING,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_STRING,
                        Scalar = new ScalarSchema(),
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_YEAR,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_YEAR,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_INT,
                            Unit = "system.year.unit",
                            LowLimit = 1900,
                            Regex = @"^\\d{4}$",
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_YEARMONTH,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_YEARMONTH,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_DATE,
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_GUID,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_GUID,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_STRING,
                            LowLimit = 36,
                            UpLimit = 36,
                            Regex = @"^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$",
                        }
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_LANGUAGE,
                        Type = SchemaType.Scalar,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_LANGUAGE,
                        Scalar = new ScalarSchema
                        {
                            Base = NS_SYSTEM_STRING,
                            LowLimit = 2,
                            UpLimit = 5,
                            Regex = @"^[a-z]{2}(-[A-Z]{2})?$", // en, en-US
                        }
                    },
                    
                    #endregion

                    #region struct
                    
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_RANGE_DATE,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGE_DATE,
                        Struct = new StructSchema
                        {
                            Fields =
                            [
                                new StructFieldConfig
                                {
                                    Name = "start",
                                    Require = true,
                                    Type = NS_SYSTEM_DATE,
                                    Display = "system.rangedate.start",
                                },
                                new StructFieldConfig
                                {
                                    Name = "stop",
                                    Require = true,
                                    Type = NS_SYSTEM_DATE,
                                    Display = "system.rangedate.stop",
                                }
                            ],
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_RANGE_FULL_DATE,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGE_FULL_DATE,
                        Struct = new StructSchema
                        {
                            Fields =
                            [
                                new StructFieldConfig
                                {
                                    Name = "start",
                                    Require = true,
                                    Type = NS_SYSTEM_FULL_DATE,
                                    Display = "system.rangedate.start",
                                },
                                new StructFieldConfig
                                {
                                    Name = "stop",
                                    Require = true,
                                    Type = NS_SYSTEM_FULL_DATE,
                                    Display = "system.rangedate.stop",
                                }
                            ],
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_RANGE_MONTH,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGE_MONTH,
                        Struct = new StructSchema
                        {
                            Fields =
                            [
                                new StructFieldConfig
                                {
                                    Name = "start",
                                    Require = true,
                                    Type = NS_SYSTEM_YEARMONTH,
                                    Display = "system.rangemonth.start",
                                },
                                new StructFieldConfig
                                {
                                    Name = "stop",
                                    Require = true,
                                    Type = NS_SYSTEM_YEARMONTH,
                                    Display = "system.rangemonth.stop",
                                }
                            ],
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_RANGE_YEAR,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGE_YEAR,
                        Struct = new StructSchema
                        {
                            Fields =
                            [
                                new StructFieldConfig
                                {
                                    Name = "start",
                                    Require = true,
                                    Type = NS_SYSTEM_YEAR,
                                    Display = "system.rangeyear.start",
                                },
                                new StructFieldConfig
                                {
                                    Name = "stop",
                                    Require = true,
                                    Type = NS_SYSTEM_YEAR,
                                    Display = "system.rangeyear.stop",
                                }
                            ],
                        },
                    },
                    
                    #endregion

                    #region array
                    
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_STRINGS,
                        Type = SchemaType.Array,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_STRINGS,
                        Array = new ArraySchema
                        {
                            Element = NS_SYSTEM_STRING,
                            Primary = [],
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_NUMBERS,
                        Type = SchemaType.Array,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_NUMBERS,
                        Array = new ArraySchema
                        {
                            Element = NS_SYSTEM_NUMBER,
                        },
                    },
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_INTS,
                        Type = SchemaType.Array,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_INTS,
                        Array = new ArraySchema
                        {
                            Element = NS_SYSTEM_INT
                        },
                    },
                    
                    #endregion

                    #region Schema

                    new NodeSchema
                    {
                        Name = NS_SYSTEM_SCHEMA,
                        Type = SchemaType.Namespace,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_SCHEMA,
                        Schemas = [
                            
                        ]
                    },

                    #endregion
                ]
            }
        ]
    };

    #endregion
}