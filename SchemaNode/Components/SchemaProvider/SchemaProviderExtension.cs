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
    public static Task<JsonNode?> CallFunctionAsync(this SchemaContext context, FunctionType node, JsonArray args, string[]? generic = null, string? target = null)
    {
        return node.CallAsync<JsonNode>(context, args.ToArray(), generic, target);
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
        FunctionType node = await context.GetSchemaTypeAsync<FunctionType>(name) ?? throw new Exception($"Function {name} not found");
        return await node.CallAsync<JsonNode>(context, args.ToArray(), generic, target);
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
