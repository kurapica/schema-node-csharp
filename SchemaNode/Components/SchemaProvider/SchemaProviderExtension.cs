using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.App;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.Schema;

namespace SchemaNode.Components;

public static class SchemaProviderExtension
{
    /// <summary>
    /// Load the schema information
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schemaName">The schema name</param>
    /// <returns>The schema</returns>
    public static async Task<NodeSchema?> LoadSchemaAsync(this SchemaContext context, string schemaName)
    {
        NodeSchema? schema = GetSystemNodeSchema(schemaName);
        if (schema != null)
        {
            if (schema.Type != SchemaType.Namespace) return schema;
        }

        foreach (ISchemaProvider provider in context.GetServices<ISchemaProvider>())
        {
            try
            {
                NodeSchema[] loadSchemas = await provider.LoadSchemaAsync([schemaName]);
                if (loadSchemas.Length == 0) continue;
                NodeSchema loadSchema = loadSchemas[0];

                // load provider & state
                loadSchema.SchemaProvider = provider.GetType();
                if (loadSchema.LoadState == null && provider.DefaultLoadState != null)
                    loadSchema.LoadState = provider.DefaultLoadState;

                // check && combine
                if (schema == null)
                {
                    schema = loadSchema;
                }
                else if (loadSchema is { Type: SchemaType.Namespace })
                {
                    // combine
                    loadSchema.Schemas ??= [];
                    loadSchema.Schemas = schema.Schemas == null || schema.Schemas?.Length == 0
                        ? loadSchema.Schemas
                        : schema.Schemas!.Concat(loadSchema.Schemas.Where(s => !schema.Schemas!.Any(v => s.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
                    
                    // display
                    if (loadSchema.Display == null || string.IsNullOrEmpty(loadSchema.Display.Key) || (loadSchema.Display.Key == schema.Name))
                        loadSchema.Display = schema.Display;
                    
                    // auth
                    if (string.IsNullOrEmpty(loadSchema.Auth)) loadSchema.Auth = schema.Auth;
                    
                    schema = loadSchema;
                }
                if (schema.Type != SchemaType.Namespace) return schema;
            }
            catch
            {
                //pass
            }
        }
        return schema;
    }

    /// <summary>
    /// Load the app schema information
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schemaName">The app schema name</param>
    /// <returns>The app schema</returns>
    public static async Task<AppSchema?> LoadAppSchemaAsync(this SchemaContext context, string schemaName)
    {
        AppSchema? schema = GetSystemApp(schemaName);
        if (schema?.Fields is { Length: > 0 }) return schema;

        foreach (ISchemaProvider provider in context.GetServices<ISchemaProvider>())
        {
            try
            {
                AppSchema? loadSchema = await provider.LoadAppSchemaAsync(schemaName);
                if (loadSchema == null) continue;

                // check && combine
                if (schema == null)
                {
                    schema = loadSchema;
                }
                else if (schema.Fields == null || schema.Fields.Length == 0)
                {
                    // combine
                    loadSchema.Apps ??= [];
                    schema.Apps = schema.Apps == null || schema.Apps?.Length == 0
                        ? loadSchema.Apps
                        : schema.Apps!.Concat(loadSchema.Apps.Where(s => !schema.Apps!.Any(v => s.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))).ToArray()).ToArray();
                    
                    // display
                    if (schema.Display == null || string.IsNullOrEmpty(schema.Display.Key))
                        schema.Display = loadSchema.Display;
                    
                    // desc
                    if (schema.Desc == null || string.IsNullOrEmpty(schema.Desc.Key))
                        schema.Desc = loadSchema.Desc;
                    
                    // auth
                    if (string.IsNullOrEmpty(schema.Auth)) schema.Auth = loadSchema.Auth;
                    
                    // auths
                    if (schema.Auths == null || schema.Auths.Length == 0)
                        schema.Auths = loadSchema.Auths;
                }
            }
            catch
            {
                //pass
            }
        }
        return schema;
    }

    /// <summary>
    /// Load the enum value sub list
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="node">The enum schema node</param>
    /// <param name="value">The root enum value, optional</param>
    /// <param name="fullList">Whether load the full list</param>
    /// <returns></returns>
    public static async Task<EnumValueInfo[]> LoadEnumSubListAsync(this SchemaContext context, EnumType node, string? value, bool? fullList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await ((ISchemaProvider)context.GetRequiredService(node.SchemaProvider)).LoadEnumSubListAsync(node.Name, value, fullList);
        }
        foreach (ISchemaProvider provider in context.GetServices<ISchemaProvider>())
        {
            try
            {
                EnumValueInfo[] result = await provider.LoadEnumSubListAsync(node.Name, value, fullList);
                node.SchemaProvider = provider.GetType();
                return result;
            }
            catch
            {
                //pass
            }
        }
        return [];
    }

    /// <summary>
    /// Load the enum value access list from the server
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="node">The enum schema node</param>
    /// <param name="value">The enum value for access</param>
    /// <param name="noSubList">no sub list should be loaded</param>
    /// <param name="withSubList">with the value's sub list if existed</param>
    /// <returns></returns>
    public static async Task<EnumValueAccess[]> LoadEnumAccessListAsync(this SchemaContext context, EnumType node, string value, bool? noSubList = null, bool? withSubList = null)
    {
        if (node.SchemaProvider != null)
        {
            return await ((ISchemaProvider)context.GetRequiredService(node.SchemaProvider)).LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
        }
        foreach (ISchemaProvider provider in context.GetServices<ISchemaProvider>())
        {
            try
            {
                EnumValueAccess[] result = await provider.LoadEnumAccessListAsync(node.Name, value, noSubList, withSubList);
                node.SchemaProvider = provider.GetType();
                return result;
            }
            catch
            {
                // pass
            }
        }
        return [];
    }

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="node">The function schema node</param>
    /// <param name="args">The arguments</param>
    /// <param name="generic">The generic types</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    public static async Task<JsonNode?> CallFunctionAsync(this SchemaContext context, FunctionType node, JsonArray args, string[]? generic = null, string? target = null)
    {
        if (node.IsRemoteCall)
        {
            return node.SchemaProvider != null
                ? await ((ISchemaProvider)context.GetRequiredService(node.SchemaProvider)).CallFunctionAsync(node.Name, args, generic, target)
                : null;
        }

        // Argument validation
        SchemaFuncInfo funcInfo = node.GetSchemaFuncInfo(context) ?? throw new Exception($"Function {node.Name} can't be complied");

        // fill generic if provided
        Type?[] generics = new Type?[funcInfo.Generics.Length];
        if (generic != null)
        {
            for (int i = 0; i < Math.Min(funcInfo.Generics.Length, generic.Length); i++)
            {
                if (string.IsNullOrEmpty(generic[i])) continue;
                AnySchemaType? ns = await context.GetSchemaTypeAsync(generic[i]);
                if (ns is { IsValueType: true }) generics[i] = ns.ToCSharpType();
            }
        }

        // parse parameters
        object?[] callArgs = new object[funcInfo.Args.Length];
        for (int i = 0; i < funcInfo.Args.Length; i++)
        {
            SchemaParamTypeInfo arg = funcInfo.Args[i];
            Type? eleType;

            if (args.Count <= i || args[i] == null)
            {
                if (arg.Nullable) continue;
                if (arg.Params)
                {
                    if (arg.Generic != null)
                    {
                        int idx = Array.FindIndex(funcInfo.Generics, f => f.Generic == arg.Generic);
                        if (idx < 0) throw new Exception("The function not valid");
                        eleType = generics[idx] ?? throw new Exception($"The generic type must be provided");
                        callArgs[i] = Array.CreateInstance(eleType, 0);
                        continue;
                    }
                    else if (arg.Type != null)
                    {
                        callArgs[i] = Array.CreateInstance(arg.Type.GetElementType() ?? arg.Type, 0);
                        continue;
                    }
                }
                throw new Exception($"The {i + 1} argument must be provided");
            }

            // generic type
            if (arg.Generic != null)
            {
                int idx = Array.FindIndex(funcInfo.Generics, f => f.Generic == arg.Generic);
                if (idx < 0) throw new Exception("The function not valid");

                (object? o, Type? _, Type? gen) = arg.ParseValue(args[i], generics[idx]);
                callArgs[i] = o ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                if (generics[idx] is null && gen is not null) generics[idx] = gen; // scan for generic
                eleType = gen ?? o.GetType();
            }
            else if (arg.Type != null && arg.Type.IsAssignableTo(typeof(AnySchemaNode)) && arg.SchemaType != null)
            {
                callArgs[i] = (await context.GetSchemaTypeAsync(arg.SchemaType))
                              ?.CreateNode(args[i]) ??
                              throw new Exception($"The {i + 1} argument must be provided and valid");
                eleType = typeof(AnySchemaNode);
            }
            else if (arg.Type != null)
            {
                (object? o, eleType, Type? _) = arg.ParseValue(args[i]);
                callArgs[i] = o ?? throw new Exception($"The {i + 1} argument must be provided and valid");
            }
            else
            {
                throw new Exception("The function not valid");
            }

            // params check
            if (arg.Params)
            {
                eleType ??= (arg.Type?.GetElementType() ?? arg.Type) ?? typeof(object);
                var array = Array.CreateInstance(eleType, args.Count - funcInfo.Args.Length + 1);
                array.SetValue(callArgs[i], 0);
                int count = 1;
                for (int j = funcInfo.Args.Length; j < args.Count; j++)
                {
                    if (args[j] == null || args[j].IsEmpty()) continue;
                    if (eleType.IsAssignableTo(typeof(AnySchemaNode)) && arg.SchemaType != null)
                    {
                        var nodeArg = (await context.GetSchemaTypeAsync(arg.SchemaType))?.CreateNode(args[j]) ??
                                      throw new Exception($"The {j + 1} argument must be provided and valid");
                        array.SetValue(nodeArg, j - funcInfo.Args.Length + 1);
                        continue;
                    }

                    (object? o, Type? _, Type? _) = arg.ParseValue(args[j], eleType);
                    array.SetValue(o ?? throw new Exception($"The {j + 1} argument must be provided and valid"), count++);
                }
                callArgs[i] = array.Length == count ? array : array.SliceArray(count);
            }
        }

        if ((funcInfo.Sign & FUNC_SIGN_CONTEXT) > 0)
            callArgs = callArgs.Prepend(context).ToArray();

        // Call the method
        object? result;
        if ((funcInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            MethodInfo callMethod = funcInfo.Method!;

            // Gets the generic method instance
            if ((funcInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
            {
                for (int i = 0; i < generics.Length; i++)
                {
                    generics[i] ??= typeof(JsonNode);
                }

                if (generics.Any(g => g is null)) throw new Exception($"The generic types must be provided");

                string genSign = string.Join('|', generics.Select(p => p!.Name));
                callMethod = funcInfo.GenericMethods.GetOrAdd(genSign, _ => funcInfo.Method!.MakeGenericMethod(generics!));
            }

            // Call the method
            result = (funcInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC
                ?  GetCallAsyncFunc(callMethod.ReturnType.GetGenericArguments()[0])
                    .Invoke(null, [callMethod, callArgs])
                : callMethod.Invoke(null, callArgs);
        }
        else
        {
            // Invoke the dynamic method
            try
            {
                result = funcInfo.DynamicMethod!.DynamicInvoke(callArgs);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                // ReSharper disable once PossibleIntendedRethrow
                throw ex;
            }
        }

        if (result != null)
        {
            return result switch
            {
                AnySchemaNode n => n.ToJson(),
                JsonObject obj => obj,
                JsonArray arr => arr,
                JsonValue val => val,
                _ => result.ToJsonNode()
            };
        }

        return null;
    }

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="generic">The generic types</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    public static async Task<JsonNode?> CallFunctionAsync(this SchemaContext context, string name, JsonArray args, string[]? generic = null, string? target = null)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(name);
        if (node is not FunctionType funcNode) throw new Exception($"Function {name} not found");
        return await CallFunctionAsync(context, funcNode, args, generic, target);
    }

    /// <summary>
    /// Call the function with schema node arguments
    /// </summary>
    public static async Task<AnySchemaNode?> CallFunctionAsync(this SchemaContext context, FunctionType node, AnySchemaNode?[] args, string? target = null)
    {
        // Argument validation
        SchemaFuncInfo funcInfo = node.GetSchemaFuncInfo(context) ?? throw new Exception($"Function {node.Name} can't be complied");

        // fill generic if provided
        Type?[] generics = new Type?[funcInfo.Generics.Length];

        // parse parameters
        object?[] callArgs = new object[funcInfo.Args.Length];
        for (int i = 0; i < funcInfo.Args.Length; i++)
        {
            SchemaParamTypeInfo arg = funcInfo.Args[i];
            Type? eleType;

            if (args.Length <= i || args[i] == null)
            {
                if (arg.Nullable) continue;
                if (arg.Params)
                {
                    if (arg.Generic != null)
                    {
                        int idx = Array.FindIndex(funcInfo.Generics, f => f.Generic == arg.Generic);
                        if (idx < 0) throw new Exception("The function not valid");
                        eleType = generics[idx] ?? throw new Exception($"The generic type must be provided");
                        callArgs[i] = Array.CreateInstance(eleType, 0);
                        continue;
                    }
                    else if (arg.Type != null)
                    {
                        callArgs[i] = Array.CreateInstance(arg.Type, 0);
                        continue;
                    }
                }
                throw new Exception($"The {i + 1} argument must be provided");
            }

            // generic type
            if (arg.Generic != null)
            {
                int idx = Array.FindIndex(funcInfo.Generics, f => f.Generic == arg.Generic);
                if (idx < 0) throw new Exception("The function not valid");

                callArgs[i] = args[i]!.Value ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                generics[idx] ??= args[i]!.CsharpType;
                eleType = generics[idx] ?? throw new Exception($"The generic type {i + 1} must be provided");
            }
            else if (arg.Type != null && arg.Type.IsAssignableTo(typeof(AnySchemaNode)) && arg.SchemaType != null)
            {
                callArgs[i] = args[i];
                eleType = typeof(AnySchemaNode);
            }
            else if (arg.Type != null)
            {
                callArgs[i] = args[i]?.ToTypeValue(arg.Type) ?? throw new Exception($"The {i + 1} argument must be provided and valid");
                eleType = arg.Type;
            }
            else
            {
                throw new Exception("The function not valid");
            }

            // params check
            if (arg.Params)
            {
                var array = Array.CreateInstance(eleType, args.Length - funcInfo.Args.Length + 1);
                array.SetValue(callArgs[i], 0);
                int count = 1;
                for (int j = funcInfo.Args.Length; j < args.Length; j++)
                {
                    if (args[j] == null || args[j]!.IsEmpty) continue;
                    if (eleType.IsAssignableTo(typeof(AnySchemaNode)) && arg.SchemaType != null)
                    {
                        array.SetValue(args[j], j - funcInfo.Args.Length + 1);
                        continue;
                    }

                    array.SetValue(args[i]?.ToTypeValue(eleType) ?? throw new Exception($"The {i + 1} argument must be provided and valid")
                        ?? throw new Exception($"The {j + 1} argument must be provided and valid"), count++);
                }
                callArgs[i] = array.Length == count ? array : array.SliceArray(count);
            }
        }

        if ((funcInfo.Sign & FUNC_SIGN_CONTEXT) > 0)
            callArgs = callArgs.Prepend(context).ToArray();

        // Gets the return type
        AnySchemaType? retType;
        if (funcInfo.Return.Generic != null)
        {
            int gIdx = Array.FindIndex(funcInfo.Generics, g => g.Generic == funcInfo.Return.Generic);
            if (gIdx >= 0 && generics[gIdx] != null)
            {
                string? type = generics[gIdx]!.GetSchemaType();
                retType = !string.IsNullOrEmpty(type)
                    ? await context.GetSchemaTypeAsync(type)
                    : throw new Exception("The return type can't be resolved");
            }
            else
            {
                throw new Exception("The return type can't be resolved");
            }
        }
        else
        {
            retType = await context.GetSchemaTypeAsync(funcInfo.Return.SchemaType!);
        }

        if (node.IsRemoteCall)
        {
            if (generics.Any(g => g == null))
                throw new Exception($"The generic types can't be resolved for remote call");

            JsonArray cargs = new JsonArray();
            foreach (AnySchemaNode? arg in args)
                cargs.Add(arg.ToJsonNode()!);

            JsonNode? res = node.SchemaProvider != null
                ? await ((ISchemaProvider)context.GetRequiredService(node.SchemaProvider))
                    .CallFunctionAsync(node.Name, cargs, generics.Select(g => g!.GetSchemaType()!).ToArray())
                : null;
            return retType?.CreateNode(res);
        }

        // Call the method
        object? result;
        if ((funcInfo.Sign & FUNC_SIGN_IMMUTABLE) == FUNC_SIGN_IMMUTABLE)
        {
            MethodInfo callMethod = funcInfo.Method!;

            // Gets the generic method instance
            if ((funcInfo.Sign & FUNC_SIGN_GENERIC) == FUNC_SIGN_GENERIC)
            {
                for (int i = 0; i < generics.Length; i++)
                    generics[i] ??= typeof(JsonNode);

                if (generics.Any(g => g is null)) throw new Exception($"The generic types must be provided");

                string genSign = string.Join('|', generics.Select(p => p!.Name));
                callMethod =
                    funcInfo.GenericMethods.GetOrAdd(genSign, _ => funcInfo.Method!.MakeGenericMethod(generics!));
            }

            // Call the method
            result = (funcInfo.Sign & FUNC_SIGN_ASYNC) == FUNC_SIGN_ASYNC
                ? GetCallAsyncFunc(callMethod.ReturnType.GetGenericArguments()[0])
                    .Invoke(null, [callMethod, callArgs])
                : callMethod.Invoke(null, callArgs);
        }
        else
        {
            // Invoke the dynamic method
            try
            {
                result = funcInfo.DynamicMethod!.DynamicInvoke(callArgs);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                // ReSharper disable once PossibleIntendedRethrow
                throw ex;
            }
        }

        return result != null ? retType?.CreateNode(result) : null;
    }

    /// <summary>
    /// Call the function with arguments by name
    /// </summary>
    public static async Task<AnySchemaNode?> CallFunctionAsync(this SchemaContext context, string name, AnySchemaNode[] args, string? target = null)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(name);
        if (node is not FunctionType funcNode) throw new Exception($"Function {name} not found");
        return await CallFunctionAsync(context, funcNode, args, target);
    }

    #region Utility

    // Call async function
    static T? CallAsyncFunc<T>(MethodBase asyncCall, params object[] callArgs)
    {
        Task<T>? task = (Task<T>?)asyncCall.Invoke(null, callArgs);
        return task == null ? default : task.GetAwaiter().GetResult();
    }

    // Gets the call async method
    static MethodInfo GetCallAsyncFunc(Type t) => CallAsyncMethodMap.GetOrAdd(t, p => typeof(SchemaProviderExtension).GetMethod(nameof(CallAsyncFunc), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(p));
    static readonly ConcurrentDictionary<Type, MethodInfo> CallAsyncMethodMap = new();

    #endregion
}
