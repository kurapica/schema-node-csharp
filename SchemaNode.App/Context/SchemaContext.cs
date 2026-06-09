using System.Collections.Concurrent;
using SchemaNode.Schema;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Service;
using SchemaNode.Utility;
using AppType = SchemaNode.Runtime.AppType;

namespace SchemaNode.Context;

public static class AppSchemaContextExtension
{
    extension(SchemaContext context)
    {
        /// <summary>
        /// Gets the application node
        /// </summary>
        public async Task<AppType?> GetAppTypeAsync(string fullName, bool reload = false)
        {
            AppSchemaRuntime runtime = context.Runtime as AppSchemaRuntime ?? throw new InvalidOperationException();
            SpanReader spans = fullName;
            AppType? app = await LoadAppTypeAsync(runtime.RootAppType, spans);
            while (app != null && spans.NextPath())
                app = await LoadAppTypeAsync(app, spans);
            return app;

            async Task<AppType?> LoadAppTypeAsync(AppType root, SpanReader span)
            {
                ReadOnlySpan<char> next = span.Current;
                AppType? result = root;
                if (!next.IsEmpty)
                    result = root.GetAppType(next);
                
                // loading
                if (result is not { Loaded: true } || reload && span.IsEnd)
                {
                    string nextVal = next.IsEmpty ? "" : next.ToString();
                    AppSchema? schema = await LoadAppSchemaAsync(root != result ? root : null, nextVal);
                    if (schema == null) return null;

                    result ??= new AppType();
                
                    // cache by segment name (next), because result.Name is empty until LoadTypeAsync sets Schema
                    if (root != result)
                        root.SaveAppType(nextVal, result);

                    // Load the schema
                    context.LogDebug("[Runtime]App Type {schemaName} loading", schema.FullName);

                    await result.LoadAsync(context, schema);
                
                    // Namespace
                    if (schema.Apps is { Length: > 0 })
                        foreach (AppSchema s in schema.Apps)
                            result.SaveAppSchema(s);
                
                    context.LogDebug("[Runtime]App Type {schemaName} working", schema.FullName);
                }
                
                return result;
            }

            async Task<AppSchema?> LoadAppSchemaAsync(AppType? root, string name)
            {
                AppSchema? schema = root?.GetAppSchema(name);
                if (schema != null) return schema;

                string schemaName = $"{root?.Name}.{name}".Trim('.');
                schema = SetSchemaState(runtime.GetSystemAppSchema(schemaName), SchemaLoadState.System);
                if (context.SystemMode) return schema;

                foreach (var provider in context.GetServices<IAppSchemaProvider>())
                {
                    try
                    {
                        AppSchema[] loadSchemas = await provider.LoadAppSchemaAsync([schemaName]);
                        if (loadSchemas.Length == 0) continue;
                        AppSchema loadSchema = SetSchemaState(loadSchemas[0], SchemaLoadState.Service, provider.GetType())!;

                        // check && combine
                        if (schema == null)
                        {
                            schema = loadSchema;
                            continue;
                        }

                        // Combine
                        schema.CombineExtensions(loadSchema, runtime);

                        if (schema.Apps == null || schema.Apps.Length == 0)
                        {
                            schema.Apps = loadSchema.Apps;
                            continue;
                        }
                        
                        if (loadSchema.Apps == null || schema.Apps.Length == 0) continue;

                        // combine
                        List<AppSchema>? otherSchemas = null;
                        foreach (var otherSchema in loadSchema.Apps)
                        {
                            int index = Array.FindIndex(schema.Apps,
                                s => s.Name.Equals(otherSchema.Name, StringComparison.OrdinalIgnoreCase));
                            if (index >= 0)
                            {
                                schema.Apps[index].CombineExtensions(otherSchema, runtime);
                            }
                            else
                            {
                                otherSchemas ??= [];
                                otherSchemas.Add(otherSchema);
                            }
                        }

                        if (otherSchemas != null)
                            schema.Apps = schema.Apps.Concat(otherSchemas).ToArray();
                    }
                    catch (Exception e)
                    {
                        context.LogError(e, $"Failed to load schema '{schemaName}' from schema provider '{provider.GetType().FullName}'.");
                    }
                }

                if (schema != null) root?.SaveAppSchema(schema);
                return schema;
            }
            
            AppSchema? SetSchemaState(AppSchema? schema, SchemaLoadState loadState, Type? provider = null)
            {
                schema?.Provider = provider;
                schema?.LoadState = loadState;
                if (schema?.Apps == null) return schema;
                foreach (var s in schema.Apps)
                    SetSchemaState(s, loadState, provider);
                return schema;
            }
        }
    }
    
    #region Context Item

    /// <summary>
    /// Gets the context item by field name, like @user.name
    /// </summary>
    public static DataNode? GetSchemaContextItem(this SchemaContext context, string field)
    {
        string[] paths = field.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (paths.Length == 0) return null;

        if (!ItemProvider.TryGetValue(paths[0].ToLower(), out (string schemaType, Type providerType) set)) return null;

        // Gets the field type
        var type = context.GetNodeTypeAsync<Runtime.ValueType>(set.schemaType).GetAwaiter().GetResult()!;

        // Check context item first
        if (context.TryGetContextItem(set.providerType, out object? setItem))
        {
            DataNode? node = setItem is DataNode n 
                ? n
                : type.From(setItem!);
            if (paths.Length > 1)
            {
                return node is StructNode @struct
                    ? @struct.GetAccessValue(paths.Skip(1))
                    : null;
            }
            return node;
        }
        
        // Gets the item provider
        if (context.GetService(set.providerType) is ISchemaContextItemProvider { HasItem: true } providerInstance
            && providerInstance.TryGetItem(out object? item))
        {
            DataNode? node = type.CreateNode(item);
            if (paths.Length > 1)
            {
                return node is StructNode @struct
                    ? @struct.GetValueByPaths(paths.Skip(1))
                    : null;
            }
            return node;
        }
        return null;
    }

    /// <summary>
    /// Gets the schema context item by type
    /// </summary>
    public static T? GetSchemaContextItem<T>(this SchemaContext context) where T : class
    {
        if (!TypeFieldMap.TryGetValue(typeof(T), out string? field)) return null;
        if (!ItemProvider.TryGetValue(field, out (string schemaType, Type providerType) set)) return null;

        // Check context item first
        if (context.TryGetContextItem(set.providerType, out object? setItem))
        {
            return setItem is DataNode n
                ? n.GetValue<T>()
                : setItem as T;
        }

        // Gets the item provider
        if (context.GetService(set.providerType) is ISchemaContextItemProvider { HasItem: true } provider
            && provider.TryGetItem(out object? item))
            return item as T;
        
        return null;
    }

    /// <summary>
    /// Copys the schema context item from source to target
    /// </summary>
    public static void CopySchemaContextItem(this SchemaContext context,SchemaContext source)
    {
        foreach (var (key, (_, providerType)) in ItemProvider)
        {
            var node = source.GetSchemaContextItem(key);
            if (node == null) continue;
            context.SetContextItem(providerType, node);
        }
    }
    
    internal static void BindSchemaContextItemProvider(string field, string schemaType, Type providerType, Type itemType)
    {
        field = field.ToLower();
        ItemProvider[field] = (schemaType, providerType);
        TypeFieldMap[itemType] = field;
    }

    /// <summary>
    /// The context item providers
    /// </summary>
    static readonly ConcurrentDictionary<string, (string schemaType, Type providerType)> ItemProvider = new();
    static readonly ConcurrentDictionary<Type, string> TypeFieldMap = new();
    
    #endregion
}