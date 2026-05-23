using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.App;
using static SchemaNode.Utility.Schema;

namespace SchemaNode.Components;

public static class SchemaProviderExtension
{
    /// <summary>
    /// Load the schema information
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schemaName">The schema name</param>
    /// <param name="onlySystem">Only load system schema</param>
    /// <returns>The schema</returns>
    public static async Task<NodeSchema?> LoadSchemaAsync(this SchemaContext context, string schemaName, bool onlySystem = false)
    {
        NodeSchema? schema = GetSystemNodeSchema(schemaName);
        if (onlySystem) return schema;

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
                // Combine custom schemas
                else
                {
                    schema.CombineCustomSchema(loadSchema);
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
    /// Load the app schema information
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="schemaName">The app schema name</param>
    /// <param name="onlySystem">Only load system apps</param>
    /// <returns>The app schema</returns>
    public static async Task<AppSchema?> LoadAppSchemaAsync(this SchemaContext context, string schemaName, bool onlySystem = false)
    {
        AppSchema? schema = GetSystemApp(schemaName);
        if (onlySystem) return schema;

        foreach (ISchemaProvider provider in context.GetServices<ISchemaProvider>())
        {
            try
            {
                AppSchema? loadSchema = await provider.LoadAppSchemaAsync(schemaName);
                if (loadSchema == null) continue;

                // check && combine
                if (schema == null)
                    schema = loadSchema;
                else
                    schema.CombineCustomSchema(loadSchema);
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
    /// <param name="rType">The return type</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    public static Task<JsonNode?> CallFunctionAsync(this SchemaContext context, FunctionType node, JsonArray args, string? rType = null, string? target = null)
        => node.CallAsync<JsonNode>(context, args.Select(object? (p) => p).ToArray(), rType, target);

    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return types</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    public static Task<JsonNode?> CallFunctionAsync(this SchemaContext context, string name, JsonArray args, string? rType = null, string? target = null)
        => CallFunctionAsync<JsonNode>(context, name,  args.Select(object? (p) => p).ToArray(), rType, target);
    
    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return type</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    public static Task<T?> CallFunctionAsync<T>(this SchemaContext context, string name, object?[] args, string? rType = null, string? target = null) 
        => CallFunctionAsync<T, CompileContext>(context, name, args, rType, target);
    
    /// <summary>
    /// Call the function with arguments and given generic type
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The function schema name</param>
    /// <param name="args">The arguments</param>
    /// <param name="rType">The return type</param>
    /// <param name="target">The related target</param>
    /// <returns>The result</returns>
    public static async Task<T?> CallFunctionAsync<T, TC>(this SchemaContext context, string name, object?[] args, string? rType = null, string? target = null) 
        where TC: CompileContext
    {
        FunctionType node = await context.GetSchemaTypeAsync<FunctionType>(name) ?? throw new Exception($"Function {name} not found");
        return await node.CallAsync<T, TC>(context, args.Select(object? (p) => p).ToArray(), rType, target);
    }
}
