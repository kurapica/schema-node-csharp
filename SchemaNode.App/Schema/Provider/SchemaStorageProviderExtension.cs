using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Event;
using SchemaNode.Property;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema.Provider;

public static class SchemaStorageProviderExtension
{
    /// <summary>
    /// Save the schema to the storage
    /// </summary>
    public static async Task<bool> SaveSchemaAsync(this SchemaContext context, NodeSchema schema, CancellationToken token = default)
    {
        ClearPolicyFlags(schema);
        NodeType? node = await context.GetNodeTypeAsync(schema.FullName, token: token).ConfigureAwait(false);

        // authorize
        NodeType? parent = null;
        if (node == null)
        {
            string name = schema.FullName.GetNamespace();
            while (!string.IsNullOrEmpty(name) && (parent = await context.GetNodeTypeAsync(name, token: token).ConfigureAwait(false)) is null)
                name = name.GetNamespace();
            
            // parent if creation
            parent ??= (context.Runtime as SchemaRuntime)?.RootNamespace ?? throw new Exception("runtime not available");
            await context.AuthorizeAsync(parent, PolicyScope.SchemaCreate, token: token).ConfigureAwait(false);
        }
        else
        {
            await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);
        }

        // Gets storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;
        
        // enum check
        Queue<(string, Entry<string>[])>? enumValues = null;
        if (schema.Kind == NODE_KIND_ENUM)
        {
            EnumSchema? enumSchema = schema.GetProperty<EnumProperty>()?.Value;
            if (enumSchema != null)
            {
                void ScanChildren(Entry<string>[] children, int cascade)
                {
                    foreach (Entry<string> child in children)
                    {
                        Entry<string>[] childs = (child.Children is {  Length: > 0 } 
                            ? child.Children 
                            : (child.Extensions != null && child.Extensions.TryGetValue("children", out JsonNode? a) && a is JsonArray arr ? arr.ToValue<Entry<string>[]>() : [])) ?? [];

                        if (cascade > 1 && childs is { Length: > 0 })
                        {
                            enumValues ??= [];
                            enumValues.Enqueue((child.Value, childs));
                            ScanChildren(childs, cascade - 1);
                        }
                        child.Children = null;
                        child.Extensions?.Remove("children");
                    }
                }
                ScanChildren(enumSchema.Values, enumSchema.Cascade?.Length ?? 1);
                schema.SetProperty<EnumProperty, EnumSchema>(enumSchema);
            }
        }

        // save schema
        if (!await provider.SaveSchemaAsync(schema, token).ConfigureAwait(false)) return false;
        
        // enum sub entries
        if (enumValues is { Count: > 0 })
            foreach ((string, Entry<string>[]) value in enumValues)
                await provider.SaveEnumEntriesAsync(schema.FullName, value.Item1, value.Item2, false, token).ConfigureAwait(false);

        // save runtime
        if (node == null)
        {
            parent = await context.GetNodeTypeAsync(string.Join('.', schema.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)), token: token).ConfigureAwait(false);
            if (parent is Runtime.NamespaceType ns)
                ns.SaveNodeSchema(schema);
        }
        await context.GetNodeTypeAsync(schema.FullName, reload: true, token: token).ConfigureAwait(false); // force reload
        
        // check sub schemas
        if (schema is { Kind: NODE_KIND_NAMESPACE, Schemas.Length: > 0 })
            foreach (var subSchema in schema.Schemas)
                await context.SaveSchemaAsync(subSchema, token).ConfigureAwait(false);
        
        // event
        context.RaiseEvent<SchemaChangeEvent, string>(schema.Name);

        return true;
    }

    /// <summary>
    /// Delete the schema from the storage
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The schema</param>
    /// <returns>true if deleted</returns>
    public static async Task<bool> DeleteSchemaAsync(this SchemaContext context, string name, CancellationToken token = default)
    {
        NodeType? node = await context.GetNodeTypeAsync(name, token: token).ConfigureAwait(false);
        if (node == null || node.IsUsed) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaDelete, token: token).ConfigureAwait(false);

        // get storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;

        // delete the schema
        if (!await provider.DeleteSchemaAsync(name, token).ConfigureAwait(false)) return false;

        // runtime remove
        node.Namespace?.RemoveNodeSchema(name.GetSchemaName());

        // event
        context.RaiseEvent<SchemaDeleteEvent, string>(name);
        return true;
    }

    /// <summary>
    /// Save the sub list for an enum value
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The schema name</param>
    /// <param name="value">The enum value</param>
    /// <param name="values">The enum sub list</param>
    /// <param name="append">Whether append the sub list not replace</param>
    /// <param name="noEvent">no event raised</param>
    /// <returns>true if saved</returns>
    public static async Task<bool> SaveEnumEntriesAsync(this SchemaContext context, string name, string value, Entry<string>[] values, bool append = false, bool noEvent = false, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(value)) return false; // for root level, please use SaveSchemaAsync to save the whole enum schema with sub list

        NodeType? node = await context.GetNodeTypeAsync(name, token: token).ConfigureAwait(false);
        if (node is not Runtime.EnumType { Cascade.Length: > 1 } @enum) return false;

        EntryAccess<string>[] access = await @enum.GetEnumEntryAccessAsync(context, value, token: token).ConfigureAwait(false);
        if (access.Length == 0 || string.IsNullOrWhiteSpace(access.Last().Entry?.Value)) return false;
        
        // authorize
        await context.AuthorizeAsync(@enum, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        // gets storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;

        foreach (var entry in values)
            entry.Children = null;
        await provider.SaveEnumEntriesAsync(name, value, values, append, token).ConfigureAwait(false);

        // Reload to avoid strange errors
        await context.GetNodeTypeAsync(@enum.Name, reload: true, token: token).ConfigureAwait(false);

        // event
        if (!noEvent) context.RaiseEvent<SchemaChangeEvent, string>(node.Name);
        return true;
    }

    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task<bool> SaveAppSchemaAsync(this SchemaContext context, AppSchema app, CancellationToken token = default)
    {
        ClearPolicyFlags(app);
        Runtime.AppType? node = await context.GetAppTypeAsync(app.FullName, token: token).ConfigureAwait(false);

        // authorize
        Runtime.AppType? parent = null;
        if (node == null)
        {
            string name = app.FullName.GetNamespace();
            while (!string.IsNullOrEmpty(name) && (parent = await context.GetAppTypeAsync(name, token: token).ConfigureAwait(false)) is not {})
                name = name.GetNamespace();
            
            // parent if creation
            parent ??= (context.Runtime as AppSchemaRuntime)?.RootAppType ?? throw new Exception("runtime not available");
            await context.AuthorizeAsync(parent, PolicyScope.SchemaCreate, token: token).ConfigureAwait(false);
        }
        else
        {
            await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

            // Check app scope policy, allow change when DEBUG
            #if !DEBUG
            if ((node.ScopePolicy != null || app.GetProperty<ScopePolicy>()?.Value is {} policy && 
                (node.Apps is { Length: > 0 } || node.GetFields().Any()) &&
                (node.ScopePolicy == null || !node.ScopePolicy.Equals(policy))))
            {
                throw new Exception(AppErrorCodes.APP_TARGET_POLICY_CANT_CHANGE);
            }
            #endif

            if (node.ScopePolicy?.Type == AppScopeType.IsolationContext)
            {
                if (node.ScopePolicy.ContextMaps == null || node.ScopePolicy.ContextMaps.Length == 0)
                    throw new Exception(AppErrorCodes.APP_ISOLATION_CONTEXT_POLICY_MISSING_MAP);
                
                Array.Sort(node.ScopePolicy.ContextMaps, (a, _) =>
                {
                    // put Access.Target last 
                    if (a.ContextItem.Equals($"{nameof(Access)}.{nameof(Access.Target)}", StringComparison.OrdinalIgnoreCase))
                        return 1;
                    return -1;
                });
            }
        }

        // Ges the storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;

        // Save node schemas first (field types may depend on them)
        if (app.NodeSchemas is { Length: > 0 })
        {
            foreach (var nodeSchema in app.NodeSchemas)
                await context.SaveSchemaAsync(nodeSchema, token).ConfigureAwait(false);
        }

        // save the schema
        if (!await provider.SaveAppSchemaAsync(app, token).ConfigureAwait(false)) return false;

        // runtime save
        if (node == null)
        {
            parent = await context.GetAppTypeAsync(app.FullName.GetNamespace(), token: token).ConfigureAwait(false);
            parent?.SaveAppSchema(app);
        }
        await context.GetAppTypeAsync(app.FullName, reload: true, token: token).ConfigureAwait(false); // force reload

        // Save fields in order if provided
        if (app.Fields is { Length: > 0 })
        {
            foreach (var field in app.Fields)
                await context.SaveAppFieldSchemaAsync(app.FullName, field, token).ConfigureAwait(false);
        }

        // Save workflows if provided
        if (app.Workflows is { Length: > 0 })
        {
            foreach (var workflow in app.Workflows)
                await context.SaveAppWorkflowSchemaAsync(app.FullName, workflow, token: token).ConfigureAwait(false);
        }

        // event
        context.RaiseEvent<AppSchemaChangeEvent, string>(app.FullName);
        return true;
    }

    /// <summary>
    /// Delete an app schema
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task<bool> DeleteAppSchemaAsync(this SchemaContext context, string app, CancellationToken token = default)
    {
        var node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        if (node == null || node.IsUsed) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaDelete, token: token).ConfigureAwait(false);

        // delete the schema
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppSchemaAsync(app, token).ConfigureAwait(false)) return false;
        node.Container?.RemoveAppSchema(app);

        // event
        context.RaiseEvent<AppSchemaDeleteEvent, string>(app);
        return true;
    }

    /// <summary>
    /// Save app field schema
    /// </summary>
    public static async Task<bool> SaveAppFieldSchemaAsync(this SchemaContext context, string app, AppFieldSchema field, CancellationToken token = default)
    {
        ClearPolicyFlags(field);
        var node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        if (node == null) return false;
        
        // validate by app scope policy
        NodeType fieldType = await context.GetNodeTypeAsync(field.Type, token: token).ConfigureAwait(false) ?? throw new Exception(AppErrorCodes.APP_FIELD_TYPE_NOT_VALID);
        if (fieldType is Runtime.ArrayType arrType) fieldType = arrType.Element ?? throw new Exception(AppErrorCodes.APP_FIELD_TYPE_NOT_VALID);
        if (fieldType is Runtime.StructType structType)
        {
            if (!structType.GetFields().Any()) throw new Exception(AppErrorCodes.APP_FIELD_TYPE_NOT_VALID);
            if (node.ScopePolicy?.ContextMaps is { Length: > 0 })
            {
                if (structType.GetFields().Any(f => node.ScopePolicy.ContextMaps.Any(m => f.Name.Equals(m.MapKey, StringComparison.OrdinalIgnoreCase))))
                    throw new Exception(AppErrorCodes.APP_FIELD_TYPE_NOT_VALID);
            }
        }

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        // Gets the storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;

        // save the field schema
        if (!await provider.SaveAppFieldSchemaAsync(app, field, token).ConfigureAwait(false)) return false;

        await context.GetAppTypeAsync(app, reload: true, token: token).ConfigureAwait(false);

        // event
        context.RaiseEvent<AppSchemaChangeEvent, string>(app);
        return true;
    }

    /// <summary>
    /// Delete app field schema
    /// </summary>
    public static async Task<bool> DeleteAppFieldSchemaAsync(this SchemaContext context, string app, string field, CancellationToken token = default)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        var appField = node?.GetField(field);
        if (appField == null) return false;

        // authorize
        await context.AuthorizeAsync(node!, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        // Gets the storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;

        // delete the field schema
        if (!await provider.DeleteAppFieldSchemaAsync(app, field, token).ConfigureAwait(false)) return false;

        await context.GetAppTypeAsync(app, reload: true, token: token).ConfigureAwait(false);

        // event
        context.RaiseEvent<AppSchemaChangeEvent, string>(app);
        
        // try drop the app field table
        if (appField.EnableDynamicTable)
        {
            var dataProvider = context.GetService<IAppDataProvider>();
            if (dataProvider != null) await dataProvider.DropDynamicTableAsync(appField.GetDynamicTableSchema(context), token).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Swap the field order
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app"></param>
    /// <param name="field1"></param>
    /// <param name="field2"></param>
    /// <returns></returns>
    public static async Task<bool> SwapAppFieldSchemaAsync(this SchemaContext context, string app, string field1, string field2, CancellationToken token = default)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        // Gets the storage provider
        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;

        // swap the field schema
        if (!await provider.SwapAppFieldSchemaAsync(app, field1, field2, token).ConfigureAwait(false)) return false;

        await context.GetAppTypeAsync(app, reload: true, token: token).ConfigureAwait(false);

        // event
        context.RaiseEvent<AppSchemaChangeEvent, string>(app);
        return true;
    }

    /// <summary>
    /// Save app workflow schema
    /// </summary>
    public static async Task<bool> SaveAppWorkflowSchemaAsync(this SchemaContext context, string app, AppWorkflowSchema workflow, bool forActive = false, CancellationToken token = default)
    {
        ClearPolicyFlags(workflow);
        Runtime.AppType? node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        AppWorkflowType? appWorkflowType = node.GetWorkflow(workflow.Name);
        if (!forActive && appWorkflowType is { Activated: true }) return false;

        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveAppWorkflowSchemaAsync(app, workflow, token).ConfigureAwait(false)) return false;

        if (forActive) return true;

        await context.GetAppTypeAsync(app, reload: true, token: token).ConfigureAwait(false);

        // event
        context.RaiseEvent<AppSchemaChangeEvent, string>(app);
        return true;
    }

    /// <summary>
    /// Delete app workflow schema
    /// </summary>
    public static async Task<bool> DeleteAppWorkflowSchemaAsync(this SchemaContext context, string app, string workflow, CancellationToken token = default)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        AppWorkflowType? appWorkflowType = node.GetWorkflow(workflow);
        if (appWorkflowType is { Activated: true }) return false;

        IAppEntryStorageProvider? provider = context.GetService<IAppEntryStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppWorkflowSchemaAsync(app, workflow, token).ConfigureAwait(false)) return false;

        await context.GetAppTypeAsync(app, reload: true, token: token).ConfigureAwait(false);

        // event
        context.RaiseEvent<AppSchemaChangeEvent, string>(app);
        return true;
    }

    /// <summary>
    /// Toggle app workflow schema active state
    /// </summary>
    public static async Task<bool> ToggleAppWorkflowSchemaAsync(this SchemaContext context, string app, string workflow, bool active, CancellationToken token = default)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app, token: token).ConfigureAwait(false);
        AppWorkflowType? appWorkflowType = node?.GetWorkflow(workflow);
        if (appWorkflowType == null) return false;

        // authorize
        await context.AuthorizeAsync(node!, PolicyScope.SchemaUpdate, token: token).ConfigureAwait(false);

        if (active)
        {
            if (appWorkflowType.Activated) return true;
            try
            {
                await appWorkflowType.ActiveAsync(context).ConfigureAwait(false);
                if (appWorkflowType.Activated)
                {
                    appWorkflowType.Active = true;
                    AppWorkflowSchema schema = appWorkflowType.Schema;
                    schema.Active = true;
                    await context.SaveAppWorkflowSchemaAsync(app, schema, true, token).ConfigureAwait(false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        else
        {
            if (!appWorkflowType.Activated) return true;
            try
            {
                await appWorkflowType.DeactivateAsync().ConfigureAwait(false);
                if (!appWorkflowType.Activated)
                {
                    appWorkflowType.Active = false;
                    AppWorkflowSchema schema = appWorkflowType.Schema;
                    schema.Active = false;
                    await context.SaveAppWorkflowSchemaAsync(app, schema, true, token).ConfigureAwait(false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    static void ClearPolicyFlags(PropertyOwner schema)
    {
        schema.ClearProperty<SchemaCreate>();
        schema.ClearProperty<SchemaRead>();
        schema.ClearProperty<SchemaUpdate>();
        schema.ClearProperty<SchemaDelete>();
        schema.ClearProperty<DataCreate>();
        schema.ClearProperty<DataRead>();
        schema.ClearProperty<DataUpdate>();
        schema.ClearProperty<DataDelete>();
    }
}
