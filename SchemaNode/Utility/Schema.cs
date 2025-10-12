using System.Collections;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// Provide the system schema
/// </summary>
internal static class Schema
{
    #region Static Methods

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
        if (typeInfo.BaseType == null) return null; // Generic, no schema type
        
        // array & list check
        bool isArray = typeInfo.AnyArray;
        Type type = typeInfo.BaseType;

        // Already registered
        if (isArray ? _typeArrNames.TryGetValue(type, out var typeName) : _typeNames.TryGetValue(type, out typeName)) return typeName;
        
        // Common
        if (type == typeof(JsonArray) || type == typeof(ArrayNode) || type.IsAssignableTo(typeof(IEnumerable)))
        {
            return NS_SYSTEM_ARRAY;
        }
        else if (type == typeof(JsonObject) || type == typeof(StructNode))
        {
            return NS_SYSTEM_STRUCT;
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
                    if (autoConv || type.GetCustomAttribute<SchemaStructAttribute>() != null) 
                        schemas = StructType.GenerateSystemStruct(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                {
                    if (autoConv || type.GetCustomAttribute<SchemaEnumAttribute>() != null) 
                        schemas = EnumType.GenerateSystemEnum(type, ((type.DeclaringType?.IsClass ?? false) 
                            ? type.DeclaringType.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name 
                            : null) ?? type.Assembly.GetCustomAttribute<SchemaNameSpaceAttribute>()?.Name);
                }
                else if (!type.IsPrimitiveLike())
                {
                    // struct
                    if (autoConv || type.GetCustomAttribute<SchemaStructAttribute>() != null) 
                        schemas = StructType.GenerateSystemStruct(type, ((type.DeclaringType?.IsClass ?? false) 
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
    internal static SchemaParamTypeInfo? GetSchemaTypeInfo(this Type? input, bool autoConv = false)
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
    internal static SchemaParamTypeInfo? GetSchemaTypeInfo(this AnySchemeType node)
    {
        return node switch
        {
            ScalarType or EnumType or StructType or ArrayType => new SchemaParamTypeInfo
            {
                Type = node.ToCSharpType(), 
                SchemaType = node.Name
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
        if (node is ArrayType array)
        {
            if (array.ElementNode == null)
            {
                return typeof(ArrayNode);
            }
            else
            {
                isArray = true;
                node = array.ElementNode;
            }
        }
        if (type is null && !_systemTypes.TryGetValue(node.Name.ToLowerInvariant(), out type))
        {
            if (node is EnumType enumNode)
            {
                type = enumNode.ValueType == EnumValueType.String ? typeof(string) : typeof(int);
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
                    type = isArray ? typeof(ArrayNode) : typeof(ScalarNode);
                    isArray = false;
                }
            }
            else if(node is StructType)
            {
                type = isArray ? typeof(ArrayNode) : typeof(StructNode);
                isArray = false;
            }
        }

        // cover all
        if (type == null)
        {
            type ??= isArray ? typeof(ArrayNode) : typeof(StructNode);
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