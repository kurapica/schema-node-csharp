using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Enum;
using SchemaNode.Event;
using SchemaNode.Property.App;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema.Provider;

public static class SchemaStorageProviderExtension
{
    /// <summary>
    /// Save the schema to the storage
    /// </summary>
    public static async Task<bool> SaveSchemaAsync(this SchemaContext context, NodeSchema schema)
    {
        NodeType? node = await context.GetNodeTypeAsync(schema.FullName);

        // authorize
        NodeType? parent = null;
        if (node == null)
        {
            string name = schema.FullName.GetNamespace();
            while (!string.IsNullOrEmpty(name) && (parent = await context.GetNodeTypeAsync(name)) is not {})
                name = name.GetNamespace();
            
            // parent if creation
            parent ??= (context.Runtime as SchemaRuntime)?.RootNamespace ?? throw new Exception("runtime not available");
            await context.AuthorizeAsync(parent, PolicyScope.SchemaCreate);
        }
        else
        {
            await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);
        }

        // Gets storage provider
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;

        // save schema
        if (!await provider.SaveSchemaAsync(schema)) return false;

        // save runtime
        if (node == null)
        {
            parent = await context.GetNodeTypeAsync(string.Join('.', schema.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
            if (parent is Runtime.NamespaceType ns)
                ns.SaveNodeSchema(schema);
        }
        await context.GetNodeTypeAsync(schema.Name, reload: true); // force reload
        
        // check sub schemas
        if (schema is { Kind: SCHEMA_KIND_NAMESPACE, Schemas.Length: > 0 })
            foreach (var subSchema in schema.Schemas)
                await context.SaveSchemaAsync(subSchema);
        
        // event
        context.RaiseEvent<SchemaChangeEvent>(schema.Name);

        return true;
    }

    /// <summary>
    /// Delete the schema from the storage
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="name">The schema</param>
    /// <returns>true if deleted</returns>
    public static async Task<bool> DeleteSchemaAsync(this SchemaContext context, string name)
    {
        NodeType? node = await context.GetNodeTypeAsync(name);
        if (node == null || node.IsUsed) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaDelete);

        // get storage provider
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;

        // delete the schema
        if (!await provider.DeleteSchemaAsync(name)) return false;

        // runtime remove
        node.Namespace?.RemoveNodeSchema(name);

        // event
        context.RaiseEvent<SchemaDeleteEvent>(name);
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
    public static async Task<bool> SaveEnumSubListAsync(this SchemaContext context, string name, string value, EnumValueSchema[] values, bool append = false, bool noEvent = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return false; // for root level, please use SaveSchemaAsync to save the whole enum schema with sub list

        NodeType? node = await context.GetNodeTypeAsync(name);
        if (node is not Runtime.EnumType @enum || @enum.Cascade is not { Length: > 1 }) return false;

        // authorize
        await context.AuthorizeAsync(@enum, PolicyScope.SchemaUpdate);

        // gets storage provider
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;
                
        // Need check the delete case when it has sub list, only delete the leaf node
        await context.SaveSubEnumListWithoutNonLeafNodesDeleted(provider, @enum, value, values, append);

        // Reload to avoid strange errors
        await context.GetNodeTypeAsync(@enum.Name, reload: true);

        // event
        if (!noEvent)
            context.RaiseEvent<SchemaChangeEvent>(node.Name);
        return true;
    }

    static async Task SaveSubEnumListWithoutNonLeafNodesDeleted(this SchemaContext context, IAppSchemaStorageProvider provider, Runtime.EnumType @enum, string value, EnumValueSchema[] values, bool append = false)
    {
        EnumValueAccess[] access = await @enum.LoadEnumAccessListAsync(context, value, noSubList: true, withSubList: true);
        if (access.Length == 0 || !string.IsNullOrWhiteSpace(access.Last().Value)) return;
        
        if (!append)
        {
            EnumValueSchema[] existSubList = access.Last().SubList ?? [];
            EnumValueSchema[] appends = existSubList.Where(e => e.HasSubList == true && values.All(v => !v.Value.Equals(e.Value, StringComparison.OrdinalIgnoreCase))).ToArray();
            values = appends.Length > 0 ? values.Concat(appends).ToArray() : values; // keep it simple
        }

        // Save
        await provider.SaveEnumSubListAsync(@enum.Name, value, values.Select(v => v.Clone()).ToArray(), append);

        // Save sub list for the nodes with sub list recursively
        foreach (var node in values.Where(v => v.SubList is { Length: > 0 }))
            await context.SaveSubEnumListWithoutNonLeafNodesDeleted(provider, @enum, node.Value, node.SubList!, append);
    }


    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task<bool> SaveAppSchemaAsync(this SchemaContext context, AppSchema app)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app.FullName);

        // authorize
        Runtime.AppType? parent = null;
        if (node == null)
        {
            string name = app.FullName.GetNamespace();
            while (!string.IsNullOrEmpty(name) && (parent = await context.GetAppTypeAsync(name)) is not {})
                name = name.GetNamespace();
            
            // parent if creation
            parent ??= (context.Runtime as AppSchemaRuntime)?.RootAppType ?? throw new Exception("runtime not available");
            await context.AuthorizeAsync(parent, PolicyScope.SchemaCreate);
        }
        else
        {
            await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

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
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;

        // Save node schemas first (field types may depend on them)
        if (app.NodeSchemas is { Length: > 0 })
        {
            foreach (var nodeSchema in app.NodeSchemas)
                await context.SaveSchemaAsync(nodeSchema);
        }

        // save the schema
        if (!await provider.SaveAppSchemaAsync(app)) return false;

        // runtime save
        if (node == null)
        {
            parent = await context.GetAppTypeAsync(app.FullName.GetNamespace());
            parent?.SaveAppSchema(app);
        }
        await context.GetAppTypeAsync(app.Name, reload: true); // force reload

        // Save fields in order if provided
        if (app.Fields is { Length: > 0 })
        {
            foreach (var field in app.Fields)
                await context.SaveAppFieldSchemaAsync(app.FullName, field);
        }

        // Save workflows if provided
        if (app.Workflows is { Length: > 0 })
        {
            foreach (var workflow in app.Workflows)
                await context.SaveAppWorkflowSchemaAsync(app.FullName, workflow);
        }

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app.FullName);
        return true;
    }

    /// <summary>
    /// Delete an app schema
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task<bool> DeleteAppSchemaAsync(this SchemaContext context, string app)
    {
        var node = await context.GetAppTypeAsync(app);
        if (node == null || node.IsUsed) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaDelete);

        // delete the schema
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppSchemaAsync(app)) return false;
        node.Container?.RemoveAppSchema(app);

        // event
        context.RaiseEvent<AppSchemaDeleteEvent>(app);
        return true;
    }

    /// <summary>
    /// Save app field schema
    /// </summary>
    public static async Task<bool> SaveAppFieldSchemaAsync(this SchemaContext context, string app, AppFieldSchema field)
    {
        var node = await context.GetAppTypeAsync(app);
        if (node == null) return false;
        
        // validate by app scope policy
        NodeType fieldType = await context.GetNodeTypeAsync(field.Type) ?? throw new Exception(AppErrorCodes.APP_FIELD_TYPE_NOT_VALID);
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
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        // Gets the storage provider
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;

        // save the field schema
        if (!await provider.SaveAppFieldSchemaAsync(app, field)) return false;

        await context.GetAppTypeAsync(app, reload: true);

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app);
        return true;
    }

    /// <summary>
    /// Delete app field schema
    /// </summary>
    public static async Task<bool> DeleteAppFieldSchemaAsync(this SchemaContext context, string app, string field)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app);
        var appField = node?.GetField(field);
        if (appField == null) return false;

        // authorize
        await context.AuthorizeAsync(node!, PolicyScope.SchemaUpdate);

        // Gets the storage provider
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;

        // delete the field schema
        if (!await provider.DeleteAppFieldSchemaAsync(app, field)) return false;

        await context.GetAppTypeAsync(app, reload: true);

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app);
        
        // try drop the app field table
        if (appField.EnableDynamicTable)
        {
            var dataProvider = context.GetService<IAppDataProvider>();
            if (dataProvider != null) await dataProvider.DropDynamicTableAsync(appField.GetDynamicTableSchema(context));
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
    public static async Task<bool> SwapAppFieldSchemaAsync(this SchemaContext context, string app, string field1, string field2)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        // Gets the storage provider
        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;

        // swap the field schema
        if (!await provider.SwapAppFieldSchemaAsync(app, field1, field2)) return false;

        await context.GetAppTypeAsync(app, reload: true);

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app);
        return true;
    }

    /// <summary>
    /// Save app workflow schema
    /// </summary>
    public static async Task<bool> SaveAppWorkflowSchemaAsync(this SchemaContext context, string app, AppWorkflowSchema workflow, bool forActive = false)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        AppWorkflowType? appWorkflowType = node.GetWorkflow(workflow.Name);
        if (!forActive && appWorkflowType is { Activated: true }) return false;

        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.SaveAppWorkflowSchemaAsync(app, workflow)) return false;

        if (forActive) return true;

        await context.GetAppTypeAsync(app, reload: true);

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app);
        return true;
    }

    /// <summary>
    /// Delete app workflow schema
    /// </summary>
    public static async Task<bool> DeleteAppWorkflowSchemaAsync(this SchemaContext context, string app, string workflow)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        AppWorkflowType? appWorkflowType = node.GetWorkflow(workflow);
        if (appWorkflowType is { Activated: true }) return false;

        IAppSchemaStorageProvider? provider = context.GetService<IAppSchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppWorkflowSchemaAsync(app, workflow)) return false;

        await context.GetAppTypeAsync(app, reload: true);

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app);
        return true;
    }

    /// <summary>
    /// Toggle app workflow schema active state
    /// </summary>
    public static async Task<bool> ToggleAppWorkflowSchemaAsync(this SchemaContext context, string app, string workflow, bool active)
    {
        Runtime.AppType? node = await context.GetAppTypeAsync(app);
        AppWorkflowType? appWorkflowType = node?.GetWorkflow(workflow);
        if (appWorkflowType == null) return false;

        // authorize
        await context.AuthorizeAsync(node!, PolicyScope.SchemaUpdate);

        if (active)
        {
            if (appWorkflowType.Activated) return true;
            try
            {
                await appWorkflowType.ActiveAsync(context);
                if (appWorkflowType.Activated)
                {
                    appWorkflowType.Active = true;
                    await context.SaveAppWorkflowSchemaAsync(app, appWorkflowType, true);
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
                await appWorkflowType.DeactivateAsync();
                if (!appWorkflowType.Activated)
                {
                    appWorkflowType.Active = false;
                    await context.SaveAppWorkflowSchemaAsync(app, appWorkflowType, true);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
