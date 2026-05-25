using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using NodeType = SchemaNode.Runtime.NodeType;

namespace SchemaNode.Utility;

/// <summary>
/// The type details of the given C# type
/// </summary>
internal class TypeDetail
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
        GenericDefinition = 1 << 9,
        Params = 1 << 10, // params T[]
        Complex = 1 << 11, // Dict<TK, TV> || JsonNode || other complex type
    }

    /// <summary>
    /// The original type
    /// </summary>
    public Type Type { get; set; } = null!;
    
    /// <summary>
    /// The core type
    /// </summary>
    public Type? CoreType { get; set; }
    
    /// <summary>
    /// The generic parameter
    /// </summary>
    [Obsolete("Use CoreType instead")]
    public Type? GenericParameter { get; set; }
    
    /// <summary>
    /// The generic type definition
    /// </summary>
    public TypeDetail? GenericDefine { get; set; }

    /// <summary>
    /// The generic arguments
    /// </summary>
    public TypeDetail[]? GenericArguments { get; set; }

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
    /// Is generic type
    /// </summary>
    public bool IsGenericType => (Kind & ParameterTypeKind.GenericType) > 0;
    
    /// <summary>
    /// Is generic parameter
    /// </summary>
    public bool IsGenericParameter => (Kind & ParameterTypeKind.GenericParameter) > 0;
    
    /// <summary>
    /// Is generic definition
    /// </summary>
    public bool IsGenericDefinition => (Kind & ParameterTypeKind.GenericDefinition) > 0;
    
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
        if (GenericParameter != null)
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
                        MethodInfo method = _convToEnum.GetOrAdd(generic, t => typeof(TypeDetail).GetMethod(nameof(ConvertToEnumerable), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
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
                        MethodInfo method = _arrConv.GetOrAdd(generic, t => typeof(TypeDetail).GetMethod(nameof(ConvertToArray), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
                        return (method.Invoke(null, [arr]), arrType, generic);
                    }
                    else
                    {
                        MethodInfo method = _lstConv.GetOrAdd(generic, t => typeof(TypeDetail).GetMethod(nameof(ConvertToList), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(t));
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
    
    static IEnumerable<T?> ConvertToEnumerable<T>(JsonArray arr) => arr.Select(a => a.TryConvertTo<T>(out var r) ? r : default(T?));
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
    internal static TypeDetail GetTypeDetail(this Type input)
    {
        TypeDetail? result = null;

        if (input.IsGenericTypeDefinition) // Entry<T>
        {
            result = new TypeDetail
            {
                CoreType = input,
                GenericArguments = input.GetGenericArguments().Select(GetTypeDetail).ToArray(),
                Kind = TypeDetail.ParameterTypeKind.GenericDefinition,
            };
        }
        else if (input.IsGenericParameter) // T where T: INumber<T>
        {
            // Only check INumber<T>, IFloatPoint<T>, don't cover full constraints
            var kind = TypeDetail.ParameterTypeKind.GenericParameter;

            foreach (Type constraint in input.GetGenericParameterConstraints())
            {
                if (!constraint.IsGenericType) continue;
                if (constraint.GetGenericTypeDefinition() == typeof(INumber<>))
                    kind |= TypeDetail.ParameterTypeKind.Number;
                else if (constraint.GetGenericTypeDefinition() == typeof(IFloatingPoint<>))
                    kind |= TypeDetail.ParameterTypeKind.Float;
            }
            result = new TypeDetail
            {
                CoreType = input,
                Kind = kind
            };
        }
        else if (input.IsGenericType) // IList<string>, IList<int>, Entry<string>
        {
            TypeDetail[] args = input.GetGenericArguments().Select(GetTypeDetail).ToArray();
            Type genType = input.GetGenericTypeDefinition();

            result = new TypeDetail
            {
                CoreType = input,
                GenericDefine = genType.GetTypeDetail(),
                GenericArguments = args,
                Kind = TypeDetail.ParameterTypeKind.GenericType
            };
            
            // common generic like nullable, list will be solved here
            if (args.Length == 1)
            {
                // T?
                if (genType == typeof(Nullable<>))
                {
                    result = args[0];
                    result.Kind |= TypeDetail.ParameterTypeKind.Nullable;
                }
            
                // IList<T>, List<T>
                else if (genType == typeof(IList<>) || genType == typeof(List<>))
                {
                    result = args[0];
                    result.Kind |= TypeDetail.ParameterTypeKind.List;
                }
            
                // IEnumerable<T>
                else if (genType == typeof(IEnumerable<>))
                {
                    result = args[0];
                    result.Kind |= TypeDetail.ParameterTypeKind.Enumerable;
                }

                // Task<T>
                else if (genType == typeof(Task<>))
                {
                    result = args[0];
                    result.Kind |= TypeDetail.ParameterTypeKind.Task;
                }
            }
        }
        // int[]
        else if (input.IsArray && input != typeof(string))
        {
            // only allow one-level array
            result = input.IsSZArray 
                ? input.GetElementType()?.GetTypeDetail()
                : null;
            result?.Kind |= TypeDetail.ParameterTypeKind.Array;
        }
        result ??= new TypeDetail
        {
            CoreType = input,
        };

        // Always keep the origin type
        result.Type = input;
        return result;
    }

    /// <summary>
    /// Gets the node type info
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    internal static TypeDetail GetNodeTypeDetails(this NodeType input)
        => new TypeDetail { Type = input.GetCsharpType() ?? typeof(object) };
}