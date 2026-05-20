using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaNode.Components;
using static SchemaNode.Utility.Constant;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// Provide the system schema
/// </summary>
public static class Schema
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
        foreach (string path in schemaName.SplitTypeName())
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            node = node.Schemas?.FirstOrDefault(x => x.Name.Equals(fullPath));
            if (node == null) return null;
        }
        return node;
    }

    /// <summary>
    /// Save the node schema as system, should only be used to save all system define function
    /// </summary>
    public static void SaveSystemNodeSchema(NodeSchema schema, Type? type = null)
    {
        schema.LoadState = SchemaLoadState.System;

        string schemaName = schema.Name.ToLowerInvariant();
        NodeSchema root = _root;
        string fullPath = "";
        foreach (string path in schemaName.SplitTypeName())
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            if (root.Type != SchemaType.Namespace) throw new InvalidOperationException($"Cannot add schema node '{schema.Name}' under non-namespace node '{root.Name}'");
            
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
            else
            {
                // check locale string
                if ((node.Display == null || string.IsNullOrEmpty(node.Display.Key) || node.Display.Key == node.Name) 
                    && schema.Display != null && !string.IsNullOrEmpty(schema.Display.Key))
                    node.Display = schema.Display;
            }
        }

        Console.WriteLine($"System schema: {schemaName}(${schema.Type}) {schema.Display?.Key ?? ""} - saved");

        // Register the type map
        if (type != null && schema.Type is SchemaType.Enum or SchemaType.Struct or SchemaType.Array or SchemaType.Event or SchemaType.Workflow)
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
    /// <param name="defaultNs">The default namespace</param>
    /// <returns>The schema name be registered</returns>
    internal static string? GetSchemaType(this Type type, bool autoConv = false, string? defaultNs = null)
    {
        return type.GetSchemaTypeInfo()?.GetSchemaType(autoConv, defaultNs);
    }

    /// <summary>
    /// Try get the schema name of a assembly type, with auto register
    /// </summary>
    internal  static string? GetSchemaType(this SchemaParamTypeInfo typeInfo, bool autoConv = false, string? defaultNs = null)
    {
        if (typeInfo.Complex) return NS_SYSTEM_JSON; // Complex type, use JsonNode
        if (typeInfo.BaseType == null) return null; // Generic, no schema type
        
        // array & list check
        bool isArray = typeInfo.AnyArray;
        Type type = typeInfo.BaseType;

        // Already registered
        if (isArray ? _typeArrNames.TryGetValue(type, out var typeName) : _typeNames.TryGetValue(type, out typeName)) return typeName;
        
        // Common
        if (type == typeof(object)) return "T";
        if (type.IsAssignableTo(typeof(AnySchemaNode)))
        {
            if (type.IsAssignableTo(typeof(ArrayTypeNode)))
            {
                return NS_SYSTEM_ARRAY;
            }
            else if( type.IsAssignableTo(typeof(StructTypeNode)))
            {
                return NS_SYSTEM_STRUCT;
            }
            return "T"; // generic type
        }
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
                    typeName = NS_SYSTEM_CHAR;
                    break;
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
            bool shouldConv = autoConv || type.GetCustomAttribute<SchemaAttribute>() != null || type.GetCustomAttribute<SchemaAppAttribute>() != null;
            
            // common
            if (type.IsClass)
            {
                // static class as api container
                if (type.IsAbstract)
                {
                    if (type.IsSealed)
                    {
                        // static class as method container
                        SchemaAttribute? funcNsAttr = type.GetCustomAttribute<SchemaAttribute>();
                        if (funcNsAttr != null && !string.IsNullOrEmpty(funcNsAttr.Name))
                        {
                            List<NodeSchema> funcNs = [new NodeSchema
                            {
                                Name = funcNsAttr.Name,
                                Type = SchemaType.Namespace,
                                Display = funcNsAttr.Display ?? type.GetSummaryFromXmlDoc() ?? funcNsAttr.Name,
                                LoadState = SchemaLoadState.System,
                                Schemas = []
                            }];
                            foreach (MethodInfo info in type.GetMethods().Where(m => m.IsStatic && m.GetCustomAttribute<SchemaAttribute>() != null))
                            {
                                NodeSchema? func = FunctionType.GenerateSystemFunction(info, funcNsAttr.Name);
                                if (func == null) continue;
                                funcNs.Add(func);
                            }
                            schemas = funcNs.ToArray();
                        }
                    }
                }
                else if (type.IsAssignableTo(typeof(Event)))
                {
                    // system event
                    schemas = EventType.GenerateSystemEvent(type, defaultNs ?? type.Assembly.GetCustomAttribute<SchemaAttribute>()?.Name);
                }
                else if (type.IsAssignableTo(typeof(Workflow)))
                {
                    // system workflow
                    schemas = WorkflowType.GenerateSystemWorkflow(type, defaultNs ?? type.Assembly.GetCustomAttribute<SchemaAttribute>()?.Name);
                }
                else if (type.IsAssignableTo(typeof(IProperty)))
                {
                    // system property
                    schemas = PropertyType.GenerateSystemProperty(type, NS_SYSTEM_PROPERTY);
                }
                else
                {
                    if (shouldConv) 
                        schemas = StructType.GenerateSystemStruct(type, defaultNs ?? type.Assembly.GetCustomAttribute<SchemaAttribute>()?.Name);
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                {
                    if (shouldConv) 
                        schemas = EnumType.GenerateSystemEnum(type, defaultNs ?? type.Assembly.GetCustomAttribute<SchemaAttribute>()?.Name);
                }
                else if (!type.IsPrimitiveLike())
                {
                    // struct
                    if (shouldConv) 
                        schemas = StructType.GenerateSystemStruct(type, defaultNs ?? type.Assembly.GetCustomAttribute<SchemaAttribute>()?.Name);
                }
            }

            // No root node, that means type from not registered assembly
            schemas = schemas?.Where(s => s.Name.Contains(".")).ToArray(); 
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
            Display = $"{Locale.LIST_PREFIX}{{@{typeName}}}{Locale.LIST_SUFFIX}",
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
    internal static SchemaParamTypeInfo? GetSchemaTypeInfo(this Type? input, bool autoConv = false, string? defaultNs = null)
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
            }
            
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
        else if (input.IsArray && input != typeof(string))
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
        if (autoConv && result.BaseType != null) result.SchemaType = GetSchemaType(result, true, defaultNs);
        return result;
    }

    /// <summary>
    /// Gets the schema type info from any schema node
    /// </summary>
    internal static SchemaParamTypeInfo? GetSchemaTypeInfo(this AnySchemaType node)
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
    internal static Type ToCSharpType(this AnySchemaType schemaType, bool? nullable = false)
    {
        bool isArray = false;
        Type? cSharpType = null;
        
        // json
        if (schemaType is JsonType)
            return typeof(JsonNode);
        
        // array
        if (schemaType is ArrayType array)
        {
            if (array.ElementSchemaType == null)
            {
                return typeof(ArrayTypeNode);
            }
            else
            {
                isArray = true;
                schemaType = array.ElementSchemaType;
            }
        }

        // generic type check
        if (cSharpType is null && !_systemTypes.TryGetValue(schemaType.Name.GetBaseType().ToLower(), out cSharpType))
        {
            if (schemaType is EnumType enumNode)
            {
                cSharpType = enumNode.ValueType == EnumValueType.String ? typeof(string) : typeof(Int64);
            }
            else if (schemaType is ScalarType scalar)
            {
                if (scalar.IsBool)
                {
                    cSharpType = typeof(bool);
                }
                else if (scalar.IsInt)
                {
                    cSharpType = typeof(long);
                }
                else if(scalar.IsSingle)
                {
                    cSharpType = typeof(float);
                }
                else if(scalar.IsDouble)
                {
                    cSharpType = typeof(double);
                }
                else if(scalar.IsNumber)
                {
                    cSharpType = typeof(decimal);
                }
                else if (scalar.IsChar)
                {
                    cSharpType = typeof(char);
                }    
                else if (scalar.IsString)
                {
                    cSharpType = typeof(string);
                }
                else if (scalar.IsDate)
                {
                    cSharpType = typeof(DateTime);
                }
                else
                {
                    cSharpType = isArray ? typeof(ArrayTypeNode) : typeof(ScalarTypeNode);
                    isArray = false;
                }
            }
            else if(schemaType is StructType)
            {
                cSharpType = isArray ? typeof(ArrayTypeNode) : typeof(StructTypeNode);
                isArray = false;
            }
        }

        // cover all
        if (cSharpType == null)
        {
            cSharpType ??= isArray ? typeof(ArrayTypeNode) : typeof(StructTypeNode);
        }
        else if (isArray)
        {
            cSharpType = typeof(List<>).MakeGenericType(cSharpType);
        }
        if ((nullable ?? false) && cSharpType.IsValueType)
        {
            cSharpType = typeof(Nullable<>).MakeGenericType(cSharpType);
        }
        return cSharpType;
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
                        if (method == DataCombineType.Count && node.Fields.FirstOrDefault(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase)) is { SchemaType: ScalarType { IsNumber: true } })
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
                    foreach (StructFieldSchema field in node.Fields)
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
                                result[field.Name] = field.SchemaType is ScalarType { IsNumber: true } ? array.Sum(p => p is StructTypeNode obj && obj[field.Name] is ScalarTypeNode val && !val.IsEmpty ? val.ToValue<decimal>() : 0) : null;
                                break;
                            case DataCombineType.Count:
                                result[field.Name] = field.SchemaType is ScalarType { IsNumber: true } ? array.Count : null;
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

    internal static AppSchemaDataFilter? GetQueryFilter(this StructTypeNode node, ArrayType array)
    {
        if (array.Primary is not { Length: > 0 }) return null;
        AppSchemaDataFilter? filter = null;
        foreach (string primary in array.Primary)
        {
            if (node.GetField(primary) is not AnySchemaNode { IsEmpty: false } val) return null;
            var keyFilter = new AppSchemaDataFilterBinary(LogicType.Equal,
                new AppSchemaDataFilterField(primary.ToCamelCase()),
                new AppSchemaDataFilterValue(val));
            filter = filter == null ? keyFilter : filter.AndAlso(keyFilter);
        }
        return filter;

    }
    
    /// <summary>
    /// Join to array
    /// </summary>
    internal static ArrayTypeNode? GroupJoin(ArrayType node, AnySchemaNode? value, Dictionary<string, DataCombineType> joinMethodMap)
    {
        if (node.ElementSchemaType is not StructType structNode || node.Primary == null) return null;
        Dictionary<string, AnySchemaType?> primaryNodes = structNode.Fields.Where(fieldType => node.Primary.Contains(fieldType.Name)).ToDictionary(fieldType => fieldType.Name, fieldType => fieldType.SchemaType);

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
                            if (!ad.Equals(bd))
                                return ad.CompareTo(bd);
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
    public class SchemaParamTypeInfo
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

        public bool Params => (Kind & ParameterTypeKind.Params) > 0;

        public bool List => (Kind & (ParameterTypeKind.List)) > 0;

        public bool Enumerable => (Kind & (ParameterTypeKind.Enumerable)) > 0;

        public bool Array => (Kind & ParameterTypeKind.Array) > 0;

        public bool AnyArray => (Kind & (ParameterTypeKind.List | ParameterTypeKind.Array | ParameterTypeKind.Enumerable)) > 0 && !Params;
        
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
            Type? valueType = Type;
            if (Params)
                valueType = valueType?.GetElementType() ?? valueType;
            
            if (node == null || node.IsEmpty()) return (null, valueType, generic);
            if (Generic != null)
            {
                if (node is JsonArray arr)
                {
                    if (!AnyArray) return (null, valueType, generic);
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
                if (AnyArray) return (null, valueType, generic);
                if (node is JsonObject obj)
                {
                    return (obj, typeof(JsonObject), null);
                }
                else if (node is JsonValue val)
                {
                    (object? value, Type? type) = val.ParseValueAndType();
                    if (value == null) return (null, valueType, generic);
                    if (generic != null)
                    {
                        try
                        {
                            value = generic.TryConvert(value);
                            return (value, generic, generic);
                        }
                        catch
                        {
                            return (null, valueType, generic);
                        }
                    }
                    return (value, type, type);
                }
            }
            else if (valueType != null)
            {
                // list JsonArray for IList
                if (valueType.IsInstanceOfType(node)) return (node, valueType, null);

                // not generic
                try
                {
                    return (node.FromJson(valueType), valueType, null);
                }
                catch
                {
                    // pass
                }
            }

            return (null, valueType, generic);
        }
        
        #endregion
    }

    /// <summary>
    /// The parameter type kind
    /// </summary>
    [Flags]
    public enum ParameterTypeKind
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
        Params = 1 << 9, // params T[]
        Complex = 1 << 10, // Dict<TK, TV> || JsonNode || other complex type
    }
    
    #endregion
    
    #region Enumerable Convert

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

    private static readonly ConcurrentDictionary<Type, MethodInfo> _convToEnum = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> _arrConv = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> _lstConv = [];

    // System type maps
    private static readonly ConcurrentDictionary<string, Type> _systemTypes = [];
    private static readonly ConcurrentDictionary<Type, string> _typeNames  = [];
    private static readonly ConcurrentDictionary<Type, string> _typeArrNames = [];

    #endregion

    #region Locale

    /// <summary>
    /// Applies loaded locale translations to all statically defined system schemas in the root tree.
    /// Should be called after the full system preload so that dynamically registered schemas are also covered.
    /// </summary>
    internal static void ApplySystemLocales()
    {
        if (!SystemLocale.HasLocales) return;
        ApplySchemaLocales(_root);
    }

    private static void ApplySchemaLocales(NodeSchema schema)
    {
        // Type-level display: look up by schema.Name (the full dotted path)
        if (schema.Display != null && !string.IsNullOrEmpty(schema.Name))
            SystemLocale.Translate(schema.Display, schema.Name);

        // Struct: each field Display uses its own Key (e.g. "system.rangedate.start")
        if (schema.Struct != null)
        {
            foreach (StructFieldSchema field in schema.Struct.Fields)
                SystemLocale.Translate(field.Display);
        }

        // Enum: each value Name uses its own Key (e.g. "system.schema.workflowstatus.waiting")
        if (schema.Enum != null)
        {
            if (schema.Enum.Values != null)
            {
                foreach (EnumValueInfo value in schema.Enum.Values)
                    SystemLocale.Translate(value.Name);
            }
            if (schema.Enum.Cascade != null)
            {
                foreach (LocaleString cascade in schema.Enum.Cascade)
                    SystemLocale.Translate(cascade);
            }
        }

        // Recurse into sub-schemas
        if (schema.Schemas != null)
        {
            foreach (NodeSchema sub in schema.Schemas)
                ApplySchemaLocales(sub);
        }
    }

    #endregion

    #region Utility

    internal static NodeSchema NewSystemSchema(string name, SchemaType type = SchemaType.Namespace)
    {
        return new NodeSchema
        {
            Name = name,
            Type = type,
            LoadState = SchemaLoadState.System,
            Display = name,
        };
    }
    
    internal static NodeSchema NewSystemScalar(string name, string? baseType = null, bool enableError = false, Pattern[]? pattern = null, decimal? upLimit = null, decimal? lowLimit = null)
    {
        Dictionary<string, JsonElement>? extensions = null;
        if (lowLimit != null) { extensions ??= []; extensions[PROPERTY_LOWLIMIT] = JsonSerializer.SerializeToElement(lowLimit); }
        if (upLimit != null) { extensions ??= []; extensions[PROPERTY_UPLIMIT] = JsonSerializer.SerializeToElement(upLimit); }
        if (pattern != null) { extensions ??= []; extensions["pattern"] = JsonSerializer.SerializeToElement(pattern, Extension.GetJsonOptions(false)); }
        if (enableError) { extensions ??= []; extensions["error"] = JsonSerializer.SerializeToElement($"{name}.error"); }

        return new NodeSchema
        {
            Name = name,
            Type = SchemaType.Scalar,
            LoadState = SchemaLoadState.System,
            Display = name,
            Scalar = new ScalarSchema
            {
                Base = baseType,
                Extensions = extensions,
            },
        };
    }

    internal static NodeSchema NewSystemStruct(string name, (string name, string type, bool? require)[] fields)
    {
        return new NodeSchema
        {
            Name = name,
            Type = SchemaType.Struct,
            LoadState = SchemaLoadState.System,
            Display = name,
            Struct = new StructSchema
            {
                Fields = fields.Select(f =>
                {
                    var field = new StructFieldSchema
                    {
                        Name = f.name,
                        Type = f.type,
                        Display = $"{name}.{f.name}",
                    };
                    if (f.require == true)
                    {
                        field.Extensions = new Dictionary<string, JsonElement>
                        {
                            ["require"] = JsonSerializer.SerializeToElement(true)
                        };
                    }
                    return field;
                }).ToArray()
            },
        };
    }
    
    internal static NodeSchema NewSystemArray(string name, string? eleType = null, params string[] primary)
    {
        return new NodeSchema
        {
            Name = name,
            Type = SchemaType.Array,
            LoadState = SchemaLoadState.System,
            Display = eleType != null ? $"{Locale.LIST_PREFIX}{{@{eleType}}}{Locale.LIST_SUFFIX}" : name,
            Array = new ArraySchema
            {
                Element = eleType,
                Primary = primary.Length > 0 ? primary : null,
            },
        };
    }
    
    #endregion
    
    #region System Schema

    // The basic system schema
    static readonly NodeSchema _root = NewSystemSchema("").With([
        // System types
        NewSystemSchema(NS_SYSTEM).With([
            #region base type
            
            NewSystemScalar(NS_SYSTEM_OBJECT),
            NewSystemArray(NS_SYSTEM_ARRAY, ""),
            NewSystemArray(NS_SYSTEM_LIST, NS_GENERIC_TYPE),
            NewSystemStruct(NS_SYSTEM_STRUCT, []),
            NewSystemSchema(NS_SYSTEM_JSON, SchemaType.Json),
            
            #endregion

            #region scalar

            NewSystemScalar(NS_SYSTEM_BOOL, enableError: true),
            NewSystemScalar(NS_SYSTEM_DATE, enableError: true),
            // ^[+-]?\d+(\.\d+)?(e-?\d+)?$
            NewSystemScalar(NS_SYSTEM_NUMBER, enableError: true, pattern:
            [
                new() { Type = PatternType.CharSet, Chars = "+-", Min = 0, Max = 1 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
                [
                    new() { Type = PatternType.Literal, Text = "." },
                    new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                ]},
                new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
                [
                    new() { Type = PatternType.Literal, Text = "e" },
                    new() { Type = PatternType.Literal, Text = "-", Min = 0 },
                    new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                ]},
            ]),
            // ^[+-]?\d+\.?\d+$
            NewSystemScalar(NS_SYSTEM_DOUBLE, baseType:NS_SYSTEM_NUMBER, enableError: true, pattern:
            [
                new() { Type = PatternType.CharSet, Chars = "+-", Min = 0, Max = 1 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                new() { Type = PatternType.Literal, Text = ".", Min = 0 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            ]),
            // ^\d+(\.\d+)?$
            NewSystemScalar(NS_SYSTEM_FLOAT, baseType:NS_SYSTEM_DOUBLE, enableError:true, pattern:
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
                [
                    new() { Type = PatternType.Literal, Text = "." },
                    new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                ]},
            ]),
            // ^\d+(\.\d+)?$
            NewSystemScalar(NS_SYSTEM_PERCENT, baseType:NS_SYSTEM_FLOAT, enableError:true, upLimit:100, lowLimit:0, pattern:
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                new() { Type = PatternType.Group, Min = 0, Max = 1, Parts =
                [
                    new() { Type = PatternType.Literal, Text = "." },
                    new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
                ]},
            ]),
            NewSystemScalar(NS_SYSTEM_FULL_DATE, baseType:NS_SYSTEM_DATE, enableError:true),
            // ^[+-]?\d+$
            NewSystemScalar(NS_SYSTEM_INT, baseType:NS_SYSTEM_NUMBER, enableError:true, pattern:
            [
                new() { Type = PatternType.CharSet, Chars = "+-", Min = 0, Max = 1 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 1, Max = 0 },
            ]),
            NewSystemScalar(NS_SYSTEM_STRING),
            NewSystemScalar(NS_SYSTEM_CHAR, NS_SYSTEM_STRING, lowLimit: 1, upLimit:1),
            // ^\d{4}$
            NewSystemScalar(NS_SYSTEM_YEAR, baseType:NS_SYSTEM_INT, enableError:true, pattern:
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Digit, Min = 4, Max = 4 },
            ]),
            NewSystemScalar(NS_SYSTEM_YEARMONTH, baseType:NS_SYSTEM_DATE),
            // ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ (case-insensitive)
            NewSystemScalar(NS_SYSTEM_GUID, baseType:NS_SYSTEM_STRING, enableError:true, upLimit:36, pattern:
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, CaseIgnore = true, Min = 8, Max = 8 },
                new() { Type = PatternType.Literal, Text = "-" },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, CaseIgnore = true, Min = 4, Max = 4 },
                new() { Type = PatternType.Literal, Text = "-" },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, CaseIgnore = true, Min = 4, Max = 4 },
                new() { Type = PatternType.Literal, Text = "-" },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, CaseIgnore = true, Min = 4, Max = 4 },
                new() { Type = PatternType.Literal, Text = "-" },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Hex, CaseIgnore = true, Min = 12, Max = 12 },
            ]),
            // ^[a-z]{2}-?[A-Z]{2}$ (case-insensitive via CaseIgnore on Group)
            NewSystemScalar(NS_SYSTEM_LANGUAGE, baseType:NS_SYSTEM_STRING, upLimit:8, pattern:
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Lower, CaseIgnore = true, Min = 2, Max = 2 },
                new() { Type = PatternType.Literal, Text = "-", Min = 0 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.Lower, CaseIgnore = true, Min = 2, Max = 2 },
            ]),
            // ^[a-zA-Z]\w*$
            NewSystemScalar(NS_SYSTEM_IDENTIFIER, NS_SYSTEM_STRING, upLimit:32, pattern:
            [
                new() { Type = PatternType.CharSet, Ranges = CharRange.Alpha, Min = 1, Max = 1 },
                new() { Type = PatternType.CharSet, Ranges = CharRange.AlphaDigit, Chars = "_", Min = 0, Max = 0 },
            ]),
            
            #endregion

            #region struct
            
            NewSystemStruct(NS_SYSTEM_RANGE_DATE, [("start", NS_SYSTEM_DATE, true), ("stop", NS_SYSTEM_DATE, true)]),
            NewSystemStruct(NS_SYSTEM_RANGE_FULL_DATE, [("start", NS_SYSTEM_FULL_DATE, true), ("stop", NS_SYSTEM_FULL_DATE, true)]),
            NewSystemStruct(NS_SYSTEM_RANGE_MONTH, [("start", NS_SYSTEM_YEARMONTH, true), ("stop", NS_SYSTEM_YEARMONTH, true)]),
            NewSystemStruct(NS_SYSTEM_RANGE_YEAR, [("start", NS_SYSTEM_YEAR, true), ("stop", NS_SYSTEM_YEAR, true)]),
            
            #endregion

            #region array
            
            NewSystemArray(NS_SYSTEM_STRINGS, NS_SYSTEM_STRING),
            NewSystemArray(NS_SYSTEM_NUMBERS, NS_SYSTEM_NUMBER),
            NewSystemArray(NS_SYSTEM_INTS, NS_SYSTEM_INT),
            
            #endregion

            #region System.Schema

            // place holder types
            NewSystemSchema(NS_SYSTEM_SCHEMA).With([
                NewSystemScalar(NS_SYSTEM_SCHEMA_PROPERTY, upLimit: ENTITY_PRIMARY_KEY_MAX_LEN),

                // type
                NewSystemSchema(NS_SYSTEM_SCHEMA_TYPE).With([
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_NAMESPACE, NS_SYSTEM_STRING, upLimit:ENTITY_PRIMARY_KEY_MAX_LEN),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_ANY, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_SCALAR, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_ENUM, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_STRUCT, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_ARRAY, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_FUNC,NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_EVENT, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_WORKFLOW, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_POLICY, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RECOGNIZER, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_PROPERTY, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),

                    // constraint
                    NewSystemSchema(NS_SYSTEM_SCHEMA_TYPE_RULE).With([
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_ARELE, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_VALUE, NS_SYSTEM_SCHEMA_TYPE_NAMESPACE),
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_VALID, NS_SYSTEM_SCHEMA_TYPE_FUNC),
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_UNIONVALID,  NS_SYSTEM_SCHEMA_TYPE_FUNC),
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_WHITELIST, NS_SYSTEM_SCHEMA_TYPE_FUNC),
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_PREDICATE, NS_SYSTEM_SCHEMA_TYPE_FUNC),
                        NewSystemScalar(NS_SYSTEM_SCHEMA_TYPE_RULE_EVALUATOR, NS_SYSTEM_SCHEMA_TYPE_FUNC),
                    ]),
                ]),

                NewSystemSchema(NS_SYSTEM_SCHEMA_DOMAIN).With([
                    NewSystemScalar(NS_SYSTEM_SCHEMA_DOMAIN_APP, NS_SYSTEM_STRING, upLimit:ENTITY_PRIMARY_KEY_MAX_LEN),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_DOMAIN_FIELD, NS_SYSTEM_IDENTIFIER),
                    NewSystemScalar(NS_SYSTEM_SCHEMA_DOMAIN_TARGET, NS_SYSTEM_STRING, upLimit:ENTITY_PRIMARY_KEY_MAX_LEN),
                ]),

                NewSystemSchema(NS_SYSTEM_SCHEMA_DEF).With([
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_SCALAR),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_ENUM),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_STRUCT),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_ARRAY),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_FUNC),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_POLICY),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_EVENT),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_WORKFLOW),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_RECOGNIZER),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_PROPERTY),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_APP),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_APP_FIELD),
                    NewSystemSchema(NS_SYSTEM_SCHEMA_DEF_APP_WORKFLOW)
                ])
            ]),
            #endregion

            #region System.Workflow

            NewSystemSchema(NS_SYSTEM_WORKFLOW).With([
                NewSystemScalar(NS_SYSTEM_WORKFLOW_ID, NS_SYSTEM_STRING, upLimit:128),
                NewSystemScalar(NS_SYSTEM_WORKFLOW_CRON, NS_SYSTEM_STRING, upLimit:128),
                NewSystemScalar(NS_SYSTEM_WORKFLOW_NODE, NS_SYSTEM_STRING, upLimit:32),

                NewSystemSchema(NS_SYSTEM_WORKFLOW_CONTROL),
                NewSystemSchema(NS_SYSTEM_WORKFLOW_EVENT),
                NewSystemSchema(NS_SYSTEM_WORKFLOW_FUNC),
                NewSystemSchema(NS_SYSTEM_WORKFLOW_INTERACTION),
            ]),

            #endregion

            #region System.Property

            NewSystemSchema(NS_SYSTEM_PROPERTY),

            #endregion
        ])
    ]);

    #endregion
}