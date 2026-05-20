using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using NodeType = SchemaNode.Runtime.NodeType;

namespace SchemaNode.Utility;

/// <summary>
/// The type details of the given C# type
/// </summary>
internal class TypeDetails
{
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
        Params = 1 << 9, // params T[]
        Complex = 1 << 10, // Dict<TK, TV> || JsonNode || other complex type
    }
    
    /// <summary>
    /// The generic type
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
    /// The generic type kind
    /// </summary>
    internal ParameterTypeKind Kind { get; set; } = ParameterTypeKind.Normal;

    #region State

    /// <summary>
    /// Is nullable
    /// </summary>
    public bool Nullable => (Kind & ParameterTypeKind.Nullable) > 0;

    /// <summary>
    /// Is params
    /// </summary>
    public bool Params => (Kind & ParameterTypeKind.Params) > 0;

    /// <summary>
    /// Is List
    /// </summary>
    public bool List => (Kind & (ParameterTypeKind.List)) > 0;

    /// <summary>
    /// Is Enumerable
    /// </summary>
    public bool Enumerable => (Kind & (ParameterTypeKind.Enumerable)) > 0;

    /// <summary>
    /// Is SzArray
    /// </summary>
    public bool Array => (Kind & ParameterTypeKind.Array) > 0;

    /// <summary>
    /// Array
    /// </summary>
    public bool AnyArray => (Kind & (ParameterTypeKind.List | ParameterTypeKind.Array | ParameterTypeKind.Enumerable)) > 0 && !Params;
    
    /// <summary>
    /// Is Task
    /// </summary>
    public bool Task => (Kind & ParameterTypeKind.Task) > 0;
    
    /// <summary>
    /// Is INumber
    /// </summary>
    public bool Number => (Kind & (ParameterTypeKind.Number | ParameterTypeKind.Float)) > 0;
    
    /// <summary>
    /// Only float
    /// </summary>
    public bool OnlyFloat => (Kind & ParameterTypeKind.Float) > 0;
    
    /// <summary>
    /// Use complex type like Dict
    /// </summary>
    public bool Complex => (Kind & ParameterTypeKind.Complex) > 0;
    
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Parse the value to get the real value, type and generic type
    /// </summary>
    public (object? value, Type? type, Type? generic) ParseValue(JsonNode? node, Type? generic = null)
    {
        Type? valueType = Type;
        if (Params) valueType = valueType?.GetElementType() ?? valueType;
        
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
                        MethodInfo method = _convToEnum.GetOrAdd(generic, t => typeof(TypeDetails).GetMethod(nameof(ConvertToEnumerable), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
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
                        MethodInfo method = _arrConv.GetOrAdd(generic, t => typeof(TypeDetails).GetMethod(nameof(ConvertToArray), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
                        return (method.Invoke(null, [arr]), arrType, generic);
                    }
                    else
                    {
                        MethodInfo method = _lstConv.GetOrAdd(generic, t => typeof(TypeDetails).GetMethod(nameof(ConvertToList), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
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
                        value = generic.TryConvert(value, out var r) ? r : null;
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
    
    static IEnumerable<T?> ConvertToEnumerable<T>(JsonArray arr)
    {
        return arr.Select(a => a.TryConvertTo<T>(out var r) ? r : default(T?));
    }

    static T?[] ConvertToArray<T>(JsonArray arr) => ConvertToEnumerable<T>(arr).ToArray();

    static List<T?> ConvertToList<T>(JsonArray arr)=> ConvertToEnumerable<T>(arr).ToList();

    private static readonly ConcurrentDictionary<Type, MethodInfo> _convToEnum = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> _arrConv = [];
    private static readonly ConcurrentDictionary<Type, MethodInfo> _lstConv = [];
    
    #endregion
}

internal static class TypeInfoExtensions
{
    /// <summary>
    /// Gets the parameter type info in the schema system
    /// </summary>
    internal static TypeDetails? GetTypeDetails(this Type? input)
    {
        if (input == null) return null;
        TypeDetails? result;
        
        if (input.IsGenericType) // IList<T>, IList<int>
        {
            Type[] args = input.GetGenericArguments();
            if (args.Length != 1)
            {
                // Normally types like Dict<TK, TV> should be treated as complex type, which is not supported in schema system
                // so we just return a fake TypeInfo with JsonNode type to cover it, it'll be converted to system.any
                return new TypeDetails
                {
                    Kind = TypeDetails.ParameterTypeKind.GenericType | TypeDetails.ParameterTypeKind.Complex,
                    Type = typeof(JsonNode),
                    BaseType = typeof(JsonNode),
                };
            }
            
            result = GetTypeDetails(args[0]);
            if (result == null) return null;

            Type genType = input.GetGenericTypeDefinition();
            
            // T?
            if (genType == typeof(Nullable<>))
            {
                result.Kind |= TypeDetails.ParameterTypeKind.Nullable;
            }
            
            // IList<T>, List<T>
            else if (genType == typeof(IList<>) || genType == typeof(List<>))
            {
                result.Kind |= TypeDetails.ParameterTypeKind.List;
            }
            
            // IEnumerable<T>
            else if (genType == typeof(IEnumerable<>))
            {
                result.Kind |= TypeDetails.ParameterTypeKind.Enumerable;
            }

            // Task<T>
            else if (genType == typeof(Task<>))
            {
                result.Kind |= TypeDetails.ParameterTypeKind.Task;
            }
            
            // Like Dict<TK, TV>
            else
            {
                return new TypeDetails
                {
                    Kind = TypeDetails.ParameterTypeKind.GenericType | TypeDetails.ParameterTypeKind.Complex,
                    Type = typeof(JsonNode),
                    BaseType = typeof(JsonNode),
                };
            }

            result.Kind |= TypeDetails.ParameterTypeKind.GenericType;
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
                    isNumber = true;
                else if (constraint.GetGenericTypeDefinition() == typeof(IFloatingPoint<>))
                    isFloat = true;
            }
            return new TypeDetails
            {
                Generic = input,
                Kind = TypeDetails.ParameterTypeKind.GenericParameter | 
                       (isNumber ? TypeDetails.ParameterTypeKind.Number : isFloat ? TypeDetails.ParameterTypeKind.Float : TypeDetails.ParameterTypeKind.Normal)
            };
        }
        else if (input.IsArray && input != typeof(string))
        {
            // only allow one-level array
            if (!input.IsSZArray) return null;
            result = GetTypeDetails(input.GetElementType());
            
            if (result == null) return null;
            result.Kind |= TypeDetails.ParameterTypeKind.Array;
        }
        else
        {
            result = new TypeDetails
            {
                BaseType = input,
            };
        }

        result.Type = input;
        return result;
    }

    /// <summary>
    /// Gets the node type info
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    internal static TypeDetails GetNodeTypeDetails(this NodeType input)
        => new TypeDetails { Type = input.GetCsharpType() ?? typeof(object) };
}