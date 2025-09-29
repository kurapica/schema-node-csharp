using System.Collections.Concurrent;
using System.Numerics;
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
        // ReSharper disable once InconsistentlySynchronizedField
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
    public static void SaveSystemNodeSchema(NodeSchema schema, Type? type = null)
    {
        lock (_root)
        {
            schema.LoadState = SchemaLoadState.System;
            Console.WriteLine("save {0}", schema.Name);

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
    }

    /// <summary>
    /// Try get the schema name of a assembly type, with auto register
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="autoConv">Whether auto convert the type no matter the attribute existed</param>
    /// <returns>The schema name be registered</returns>
    public static string? GetSchemaType(this Type type, bool autoConv = false)
    {
        return type.GetSchemaTypeInfo()?.GetSchemaType(autoConv);
    }

    /// <summary>
    /// Try get the schema name of a assembly type, with auto register
    /// </summary>
    /// <param name="typeInfo">The type info</param>
    /// <param name="autoConv">Whether auto convert the type</param>
    /// <returns></returns>
    public static string? GetSchemaType(this SchemaParamTypeInfo typeInfo, bool autoConv = false)
    {
        if (typeInfo.BaseType == null) return null;
        
        // array & list check
        bool isArray = (typeInfo.Kind & (ParameterTypeKind.Array | ParameterTypeKind.List | ParameterTypeKind.Enumerable)) > 0;
        Type type = typeInfo.BaseType;

        // Already registered
        if (isArray ? _typeArrNames.TryGetValue(type, out var typeName) : _typeNames.TryGetValue(type, out typeName)) return typeName;
        
        // Basic value check
        if (!type.IsEnum)
        {
            if (type == typeof(JsonArray))
            {
                return NS_SYSTEM_ARRAY;
            }
            else if (type == typeof(JsonObject))
            {
                return NS_SYSTEM_STRUCT;
            }
            
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
                // static class as api container
                if (type.IsAbstract)
                {
                    if (type.IsSealed)
                    {
                        // static class as method container
                        SchemaNameSpaceAttribute? funcNsAttr = type.GetCustomAttribute<SchemaNameSpaceAttribute>();
                        if (funcNsAttr != null)
                        {
                            List<NodeSchema>? funcNs = null;
                            foreach (MethodInfo info in type.GetMethods().Where(m => m.IsStatic && m.GetCustomAttribute<SchemaFuncAttribute>() != null))
                            {
                                NodeSchema? func = FunctionNode.GenerateSystemFunction(info, funcNsAttr.Name);
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
                        schemas = StructNode.GenerateSystemStruct(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                {
                    if (autoConv || type.GetCustomAttribute<SchemaEnumAttribute>() != null) 
                        schemas = EnumNode.GenerateSystemEnum(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
                }
                else if (!type.IsPrimitiveLike())
                {
                    // struct
                    if (autoConv || type.GetCustomAttribute<SchemaStructAttribute>() != null) 
                        schemas = StructNode.GenerateSystemStruct(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
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
    public static SchemaParamTypeInfo? GetSchemaTypeInfo(this Type? input, bool autoConv = false)
    {
        if (input == null) return null;

        SchemaParamTypeInfo? result;
        
        if (input.IsGenericType) // IList<T>, IList<int>
        {
            Type[] args = input.GetGenericArguments();
            if (args.Length != 1) return null; // not support complex generic types like Dict<TK, TV>
            
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
            
            // Don't support other generic type
            else
            {
                return null;
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
    public static SchemaParamTypeInfo? GetSchemaTypeInfo(this AnySchemaNode node)
    {
        return node switch
        {
            ScalarNode or EnumNode or StructNode or ArrayNode => new SchemaParamTypeInfo
            {
                BaseType = node.ToCSharpType(), 
                SchemaType = node.Name
            },
            _ => null
        };
    }
    
    /// <summary>
    /// Gets the C# type by schema name
    /// </summary>
    public static Type ToCSharpType(this AnySchemaNode node, bool? nullable = false)
    {
        bool isArray = false;
        Type? type = null;
        if (node is ArrayNode array)
        {
            if (array.ElementNode == null)
            {
                type = typeof(JsonArray);
            }
            else
            {
                isArray = true;
                node = array.ElementNode;
            }
        }
        if (type is null && !_systemTypes.TryGetValue(node.Name.ToLowerInvariant(), out type))
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
                    type = isArray ? typeof(JsonArray) : typeof(JsonValue);
                    isArray = false;
                }
            }
            else if(node is StructNode)
            {
                type = isArray ? typeof(JsonArray) : typeof(JsonObject);
                isArray = false;
            }
        }

        // cover all
        if (type == null)
        {
            type ??= isArray ? typeof(JsonArray) : typeof(JsonValue);
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
        
        public bool List => (Kind & (ParameterTypeKind.List | ParameterTypeKind.Enumerable)) > 0;
        
        public bool Array => (Kind & ParameterTypeKind.Array) > 0;
        
        public bool Task => (Kind & ParameterTypeKind.Task) > 0;
        
        public bool Number => (Kind & (ParameterTypeKind.Number | ParameterTypeKind.Float)) > 0;
        
        public bool OnlyFloat => (Kind & ParameterTypeKind.Float) > 0;
        
        #endregion
        
        #region Method

        (object? value, Type? type) ParseJsonValue(JsonValue val)
        {
            object raw = val.GetValue<object>();
            return raw switch
            {
                bool or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                    => (raw, raw.GetType()),
                string s => DateTime.TryParse(s, out _) ? (raw, typeof(DateTime)) : (raw, typeof(string)),
                _ => ((object? value, Type? type))(null, null)
            };
        }

        /// <summary>
        /// Parse the value to get the real value, type and generic type
        /// </summary>
        public (object? value, Type? type, Type? generic) ParseValue(JsonNode? node, Type? generic = null)
        {
            if (Generic != null)
            {
                if (node == null || node.IsEmpty()) return (null, Type, generic);
                if (node is JsonArray arr)
                {
                    
                }
                
                // single
                if (List || Array) return (null, Type, generic);
                if (node is JsonObject obj)
                {
                    
                }
                else if (node is JsonValue val)
                {
                    
                    object raw = val.GetValue<object>();

                    switch (raw)
                    {
                        case bool: 
                            return (raw, NS_SYSTEM_BOOL);
                        case sbyte or byte or short or ushort or int or uint or long or ulong:
                            return (raw, NS_SYSTEM_INT);
                        case float: 
                            return (raw, NS_SYSTEM_FLOAT);
                        case double:
                            return (raw, NS_SYSTEM_DOUBLE);
                        case decimal:
                            return (raw, NS_SYSTEM_NUMBER);
                        case string s:
                            return DateTime.TryParse(s, out _) ? (raw, NS_SYSTEM_DATE) : (raw, NS_SYSTEM_STRING);
                        default:
                            return null;
                    }
                }
            }
            else
            {
                // not generic
                try
                {
                    return (node?.FromJson(Type!), Type, null);
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
    }
    
    #endregion
    
    #region Utility
    
    // System type maps
    private static ConcurrentDictionary<string, Type> _systemTypes { get; } = new();
    private static ConcurrentDictionary<Type, string> _typeNames { get; } = new();
    private static ConcurrentDictionary<Type, string> _typeArrNames { get; } = new();

    #endregion
    
    #region System

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

                    // struct
                    new NodeSchema
                    {
                        Name = NS_SYSTEM_RANGEDATE,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGEDATE,
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
                        Name = NS_SYSTEM_RANGEFULLDATE,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGEFULLDATE,
                        Struct = new StructSchema
                        {
                            Fields =
                            [
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
                        Name = NS_SYSTEM_RANGEYEAR,
                        Type = SchemaType.Struct,
                        LoadState = SchemaLoadState.System,
                        Display = NS_SYSTEM_RANGEYEAR,
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

                    // array
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
                ]
            }
        ]
    };

    #endregion
}