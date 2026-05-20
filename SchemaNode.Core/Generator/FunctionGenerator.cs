using System.Collections.Concurrent;
using System.Reflection;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Schema;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using GenericParameter = SchemaNode.Property.Schema.GenericParameter;
using SchemaType = SchemaNode.Property.Schema.SchemaType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Service;

/// <summary>
/// Generates FunctionSchema for each public static method found in a static class
/// annotated with [Meta&lt;SchemaType&gt;]. The class maps to a namespace; each method
/// becomes a separate function schema whose full name is either the method's own
/// [Meta&lt;SchemaType&gt;] value or "&lt;classNamespace&gt;.&lt;methodName&gt;".
/// </summary>
internal sealed class FunctionGenerator : INodeSchemaGenerator
{
    /// <summary>
    /// The system func infos
    /// </summary>
    static readonly ConcurrentDictionary<string, SchemaFuncInfo> SystemFuncInfos = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Gets the system func info
    /// </summary>
    public static SchemaFuncInfo? GetSystemFuncInfo(string name) => SystemFuncInfos.GetValueOrDefault(name);

    /// <inheritdoc />
    public IEnumerable<NodeSchema> GenerateSchema(SchemaRuntime runtime, Type type, string @namespace, string name, Func<Type, string, string?> typeResolver)
    {
        // Only process static classes with namespace type related
        if (!type.IsAbstract || !type.IsSealed || type.GetMetaProperty<SchemaType>() is not {} schemaType) yield break;
        
        // Save the namespace
        NodeSchema nsSchema = NodeSchema.Create(SCHEMA_KIND_NAMESPACE,  schemaType.Value ?? $"{@namespace}.{name}".Trim('.'), type);
        yield return nsSchema;

        foreach (MethodInfo method in type
                     .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                     .Where(m => m is { IsSpecialName: false, IsAbstract: false, IsConstructor: false }))
        {
            if (method.GetCustomAttribute<SchemaIgnoreAttribute>()  != null) continue;
            
            // Determine schema name: explicit [Meta<ValueType>] wins, otherwise "<classNs>.<methodName>"
            schemaType = method.GetMetaProperty<SchemaType>();

            NodeSchema? funcSchema = BuildFunctionSchema(method, 
                schemaType?.Value?.GetNamespace() ?? nsSchema.FullName, 
                schemaType?.Value?.GetSchemaName() ?? method.Name.ToLowerInvariant(), 
                typeResolver);
            if (funcSchema != null)
                yield return funcSchema;
        }
    }
    
    private static NodeSchema? BuildFunctionSchema(MethodInfo method, string @namespace, string name, Func<Type, string, string?> typeResolver)
    {
        // node schema
        NodeSchema schema = NodeSchema.Create(SCHEMA_KIND_FUNCTION, @namespace, name, null, method.GetSummaryFromXmlDoc());
        
        // function info
        FunctionFlags sign = FunctionFlags.Immutable; // The system method won't be changed and already compiled
        if (method.IsGenericMethodDefinition) sign |= FunctionFlags.Generic;
        
        // Generate the arguments and result type
        ParameterInfo[] parameters = method.GetParameters();
        TypeDetails[] genInfos = method.GetGenericArguments()
            .Select(g => g.GetTypeDetails() ?? throw new Exception($"The {g.FullName} used by method {method.Name} can't be resolved"))
            .ToArray(); // The generic type infos

        // The schema context must be the first if used
        if (parameters.Length > 0 && parameters[0].ParameterType.IsAssignableTo(typeof(ISchemaContext)))
        {
            sign |= FunctionFlags.Context;
            parameters = parameters.Skip(1).ToArray();
        }
        
        // function schema
        FunctionSchema funcSchema = new ()
        {
            Return = string.Empty,
            Args = new FuncArg[parameters.Length],
            Exps = [],
        };
        foreach (IProperty prop in method.GetMetaPropertiesForSchema<IProperty>(SCHEMA_KIND_FUNCTION))
            funcSchema.SetProperty(prop);

        // Generics
        if (genInfos.Length > 0)
            funcSchema.SetProperty<Generics, GenericParameter[]>(genInfos.Select((g, i) => 
                new GenericParameter
                {
                    Name = genInfos.Length > 1 ? $"T{i + 1}" : "T",
                    Compatibles = g is { AnyArray: false, Number: true } ? [NS_SYSTEM_NUMBER] : null
                }
            ).ToArray());

        // Parameter types
        TypeDetails?[] paramInfos = parameters.Select(p => p.ParameterType.GetTypeDetails()).ToArray();
        for (int i = 0; i < parameters.Length; i++)
        {
            ParameterInfo p = parameters[i];
            TypeDetails? pt = paramInfos[i];
            if (pt == null) return null;

            var defaultProp = p.GetMetaProperty<Default>();
            
            FuncArg arg = new ()
            {
                Name = p.Name ?? $"arg{i}",
                Nullable = pt.Nullable || p.HasDefaultValue || 
                    p.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute") != null ||
                    defaultProp != null,
                Display = method.GetSummaryFromXmlDoc(p) ?? null,
                Default = defaultProp?.Value, // not the default value of the parameter
            };
            funcSchema.Args[i] = arg;
            if ((arg.Nullable ?? false) || new NullabilityInfoContext().Create(p).ReadState == NullabilityState.Nullable)
                pt.Kind |= TypeDetails.ParameterTypeKind.Nullable;

            // Params
            if (p.IsDefined(typeof(ParamArrayAttribute), false))
            {
                arg.Params = true;
                arg.Nullable = true;
                pt.Kind |= TypeDetails.ParameterTypeKind.Params;
            }

            // Check dynamic type
            if (p.GetMetaProperty<SchemaType>() is { HasValue: true} schemaTypeAttr)
            {
                arg.Type = schemaTypeAttr.GetValue<string>()!;
            }
            else if (pt.Generic != null)
            {
                if (pt.AnyArray && !(arg.Params ?? false))
                {
                    arg.Type = NS_SYSTEM_ARRAY;
                }
                else
                {
                    int gIdx = Array.FindIndex(genInfos, (g) => g.Generic == pt.Generic);
                    if (gIdx >= 0)
                    {
                        // generic type
                        arg.Type = genInfos.Length > 1 ? $"T{gIdx + 1}" : "T";
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            else
            {
                string? paramType = typeResolver(
                    ((arg.Params == true) ? pt.BaseType : pt.Type) 
                    ?? throw new Exception($"Can't resolve parameter type for method {method.Name} in {@namespace}"), 
                    @namespace);
                if (paramType == null) return null;
                arg.Type = paramType;
            }
        }

        // Return type
        TypeDetails? retInfo = method.ReturnType.GetTypeDetails();
        if (retInfo == null) return null;
        if (retInfo.Task) sign |= FunctionFlags.Async;
        if (retInfo.Nullable) sign |= FunctionFlags.NullableRet;
        else if (new NullabilityInfoContext().Create(method.ReturnParameter).ReadState == NullabilityState.Nullable)
            sign |= FunctionFlags.NullableRet;

        if (retInfo.Generic != null)
        {
            // IList<T>, use system.array instead
            if (retInfo.AnyArray)
            {
                funcSchema.Return = NS_SYSTEM_ARRAY;
            }
            else
            {
                // single
                int gIdx = Array.FindIndex(genInfos, g => g.Generic == retInfo.Generic);
                if (gIdx >= 0)
                    funcSchema.Return = genInfos.Length > 1 ? $"T{gIdx + 1}" : "T";
                else
                    return null;
            }
        }
        else
        {
            string? retType = typeResolver(method.ReturnType, @namespace);
            if (retType == null) return null;
            funcSchema.Return = retType;
        }

        // Save the method info to cache
        SystemFuncInfos.TryAdd(schema.FullName, new SchemaFuncInfo
        {
            Name = schema.FullName,
            Method = method,
            Sign = sign,
            Generics = genInfos,
            Args = paramInfos!,
            Return = retInfo
        });

        schema.SetProperty<FuncProperty, FunctionSchema>(funcSchema);
        return schema;
    }
}

/// <summary>
/// The system schema func info
/// </summary>
internal sealed class SchemaFuncInfo
{
    /// <summary>
    /// The method name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The method info
    /// </summary>
    public MethodInfo? Method { get; internal set; }

    /// <summary>
    /// The dynamic method generated by expression
    /// </summary>
    public Delegate? DynamicMethod { get; internal set; }

    /// <summary>
    ///  The sign of the function
    /// </summary>
    public FunctionFlags Sign { get; internal set; } = FunctionFlags.None;

    /// <summary>
    /// The generic info
    /// </summary>
    public TypeDetails[] Generics { get; init; } = [];
    
    /// <summary>
    /// The argument info
    /// </summary>
    public TypeDetails[] Args { get; init; } = [];
    
    /// <summary>
    /// The return info
    /// </summary>
    public required TypeDetails Return { get; init; }

    /// <summary>
    /// The generic instances
    /// </summary>
    public ConcurrentDictionary<string, MethodInfo> GenericMethods { get; } = new();
}

