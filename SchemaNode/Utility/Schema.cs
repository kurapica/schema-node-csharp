using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// Provide the system schema
/// </summary>
public static class Schema
{
    #region Static Methods

    /// <summary>
    /// Gets the system node schema
    /// </summary>
    public static NodeSchema? GetSystemNodeSchema(string schemaName)
    {
        schemaName = schemaName.ToLowerInvariant();
        NodeSchema[] schemas = _nodes;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+").SkipLast(1))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            NodeSchema? node = schemas.FirstOrDefault(x => x.Name == fullPath);
            if (node?.Schemas == null) return null;
            schemas = node.Schemas;
        }
        return schemas.FirstOrDefault(x => x.Name == schemaName);
    }

    /// <summary>
    /// Save the node schema as system, should only be used to save all system define functions
    /// When server init, so no lock will be used for simple
    /// </summary>
    public static void SaveSystemNodeSchema(NodeSchema schema, Type? type = null)
    {
        schema.LoadState = SchemaLoadState.System;
        
        string schemaName = schema.Name.ToLowerInvariant();
        NodeSchema[] schemas = _nodes;
        NodeSchema? root = null;
        string fullPath = "";
        foreach (string path in Regex.Split(schemaName, @"\W+").SkipLast(1))
        {
            fullPath = !string.IsNullOrWhiteSpace(fullPath) ? $"{fullPath}.{path}" : path;
            NodeSchema? node = schemas.FirstOrDefault(x => x.Name == fullPath);
            if (node == null)
            {
                node = new NodeSchema
                {
                    Name = fullPath,
                    Type = SchemaType.Namespace,
                    LoadState = SchemaLoadState.System,
                    Schemas = []
                };

                if (root == null)
                {
                    _nodes = _nodes.Concat([node]).ToArray();
                }
                else
                {
                    root.Schemas = root.Schemas != null ? root.Schemas.Concat([node]).ToArray() : [node];
                }
            }
            else
            {
                root = node;
                root.Schemas ??= [];
                schemas = root.Schemas;
            }
        }
        
        if (schemas.Any(x => x.Name == schemaName)) return;
        if (root == null)
        {
            _nodes = _nodes.Concat([schema]).ToArray();
        }
        else
        {
            root.Schemas = root.Schemas != null ? root.Schemas.Concat([schema]).ToArray() : [schema];
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
    /// Try register assembly type as schema and return its name
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="autoConv">Whether auto convert the type no matter the attribute existed</param>
    /// <returns>The schema name be registered</returns>
    public static string? GetSchemaType(this Type type, bool autoConv = false)
    {
        // array & list check
        bool isArray = false;
        string? typeName;
        
        // Generic check
        while (type.IsGenericType || type.IsArray)
        {
            if (type.IsArray)
            {
                if (isArray || !type.IsSZArray) return null; // not support multi dimension array
                Type? elementType = type.GetElementType();
                if (elementType == null) return null;
                type = elementType;
                isArray = true;
            }
            else if (type.IsSubclassOfGenericType(typeof(List<>)))
            {
                if (isArray) return null;
                type = type.GetGenericArguments()[0];
                isArray = true;
            }
            else if (type.IsSubclassOfGenericType(typeof(Nullable<>)))
            {
                type = type.GetGenericArguments()[0];
            }
            else
            {
                // not support other complex generic types
                return null;
            }
        }

        // Already registered
        if (isArray ? _typeArrNames.TryGetValue(type, out typeName) : _typeNames.TryGetValue(type, out typeName)) return typeName;
        
        // Basic value check
        if (!type.IsEnum)
        {
            if (type == typeof(Guid))
            {
                typeName = NS_SYSTEM_GUID;
            }
            else if (type == typeof(DateTimeOffset))
            {
                typeName = NS_SYSTEM_DATE;
            }
            else
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
        }

        if (string.IsNullOrWhiteSpace(typeName) && !_typeNames.TryGetValue(type, out typeName))
        {
            // try generate
            NodeSchema[]? schemas = null;
            if (type.IsClass)
            {
                if (type.IsAbstract)
                {
                    if (type.IsSealed)
                    {
                        // static class as method container
                        SchemaNameSpaceAttribute? funcNsAttr = type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>();
                        if (funcNsAttr != null)
                        {
                            List<NodeSchema>? funcNs = null;
                            foreach (MethodInfo info in type.GetMethods().Where(m => m.IsStatic && m.GetCustomAttribute<SchemaFuncAttribute>() != null))
                            {
                                NodeSchema? func = FunctionNode.GenerateSystemFunction(info, funcNsAttr?.Name);
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
                    if (autoConv || type.GetCustomAttribute<SchemaStructAttribute>() != null) 
                        schemas = StructNode.GenerateSystemStruct(type, type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                {
                    if (autoConv || type.GetCustomAttribute<SchemaEnumAttribute>() != null) 
                        schemas = EnumNode.GenerateSystemEnum(type, type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
                }
                else if (!type.IsPrimitiveLike())
                {
                    // struct
                    if (autoConv || type.GetCustomAttribute<SchemaStructAttribute>() != null) 
                        schemas = StructNode.GenerateSystemStruct(type, type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
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
    /// Gets the C# type by schema name
    /// </summary>
    public static Type ToCSharpType(this NamespaceNode node, bool? nullable = false)
    {
        bool isArray = false;
        Type? type = null;
        if (node is ArrayNode array)
        {
            if (array.ElementNode == null) return typeof(JsonArray);
            isArray = true;
            node = array.ElementNode;
        }
        if (!_systemTypes.TryGetValue(node.Name.ToLowerInvariant(), out type))
        {
            if (node is EnumNode enumNode)
            {
                type = enumNode.ValueType == EnumValueType.String ? typeof(string) : typeof(int);
            }
            else if (node is ScalarNode scalar)
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
                    return isArray ? typeof(JsonArray) : typeof(JsonValue);
                }
            }
            else if(node is StructNode)
            {
                return isArray ? typeof(JsonArray) : typeof(JsonObject);
            }
        }

        if (type == null) return isArray ? typeof(JsonArray) : typeof(JsonValue);
        if (isArray)
        {
            type = typeof(List<>).MakeGenericType(type);
        }
        if (nullable ?? false)
        {
            type = typeof(Nullable<>).MakeGenericType(type);
        }
        return type;
    }
        
    /// <summary>
    /// Try parse bool value from string
    /// </summary>
    public static bool TryParseBoolValue(string value, out bool ret)
    {
        ret = false;
        if (string.IsNullOrEmpty(value))
            return false;
        value = value.ToLower();
        switch (value)
        {
            case "true":
                ret = true;
                return true;
            case "false":
                ret = false;
                return true;
            default:
            {
                if (!int.TryParse(value, out int val) || val is < 0 or > 1)
                    return false;
                ret = val == 1;
                return true;
            }
        }
    }
    
    #endregion
    
    #region System
    
    // System type maps
    private static ConcurrentDictionary<string, Type> _systemTypes { get; } = new();
    private static ConcurrentDictionary<Type, string> _typeNames { get; } = new();
    private static ConcurrentDictionary<Type, string> _typeArrNames { get; } = new();
    
    // System Nodes
    private static NodeSchema[] _nodes = [
        new NodeSchema
        {
            Name = NS_SYSTEM,
            Type = SchemaType.Namespace,
            LoadState = SchemaLoadState.System,
            Display = NS_SYSTEM,
            Schemas = [
                // base type
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
                
                // scalar
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
                    Scalar = new ScalarSchema {
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
                    Scalar = new ScalarSchema {
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
                    Scalar = new ScalarSchema {
                        Base = NS_SYSTEM_FLOAT,
                        Error = "system.percent.error",
                        Regex = @"^\\d+(\\.\\d+)?$",
                        UpLimit = 100,
                        LowLimit = 0
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_FULLDATE,
                    Type = SchemaType.Scalar,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_FULLDATE,
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
                    Scalar = new ScalarSchema {
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
                
                // struct
                new NodeSchema
                {
                Name = NS_SYSTEM_RANGEDATE,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEDATE,
                    Struct = new StructSchema {
                        Fields = [
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
                    Name = NS_SYSTEM_RANGEFULLDATE,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEFULLDATE,
                    Struct = new StructSchema {
                        Fields = [
                            new StructFieldConfig
                            {
                                Name = "start",
                                Require = true,
                                Type = NS_SYSTEM_FULLDATE,
                                Display = "system.rangedate.start",
                            },
                            new StructFieldConfig
                            {
                                Name = "stop",
                                Require = true,
                                Type = NS_SYSTEM_FULLDATE,
                                Display = "system.rangedate.stop",
                            }
                        ],
                    },
                },
                new NodeSchema
                {
                    Name = NS_SYSTEM_RANGEMONTH,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEMONTH,
                    Struct = new StructSchema
                    {
                        Fields = [
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
                    Name = NS_SYSTEM_RANGEYEAR,
                    Type = SchemaType.Struct,
                    LoadState = SchemaLoadState.System,
                    Display = NS_SYSTEM_RANGEYEAR,
                    Struct = new StructSchema
                    {
                        Fields = [
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
                
                // array
                new NodeSchema{
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
            ]
        }
    ];
    
    #endregion
}