using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Components;

public static class SchemaStorageProviderExtension
{
    /// <summary>
    /// Save the schema to the storage
    /// </summary>
    public static async Task<bool> SaveSchemaAsync(this SchemaContext context, NodeSchema schema)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(schema.Name);

        // authorize
        if (node == null)
        {
            string[] paths = schema.Name.SplitTypeName().SkipLast(1).ToArray();
            AnySchemaType? parentNode = null;
            for (int i = paths.Length - 1; i >= 0; i--)
            {
                string path = string.Join('.', paths.Take(i + 1));
                parentNode = await context.GetSchemaTypeAsync(path);
                if (parentNode != null) break;
            }

            // check parent if creation
            parentNode ??= SchemaContext.RootNamespace;
            await context.AuthorizeAsync(parentNode, PolicyScope.SchemaCreate);
        }
        else
        {
            await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);
        }

        // Gets storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;

        // save schema
        if (!await provider.SaveSchemaAsync(schema)) return false;

        // save runtime
        if (node == null)
        {
            AnySchemaType? parentNode = await context.GetSchemaTypeAsync(string.Join('.', schema.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
            if (parentNode is TypeNamespace ns)
                ns.Schemas = ns.Schemas.Where(p => !p.Name.Equals(schema.Name, StringComparison.OrdinalIgnoreCase)).Concat([schema]).ToArray();
        }
        await context.GetSchemaTypeAsync(schema.Name, reload: true); // force reload
        
        // check sub schemas
        if (schema is { Type: SchemaType.Namespace, Schemas.Length: > 0 })
        {
            foreach (var subSchema in schema.Schemas)
            {
                await context.SaveSchemaAsync(subSchema);
            }
        }
        
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
        AnySchemaType? node = await context.GetSchemaTypeAsync(name);
        if (node == null || node.IsUsed) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaDelete);

        // get storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;

        // delete the schema
        if (!await provider.DeleteSchemaAsync(name)) return false;

        // runtime remove
        context.RemoveSchemaType(name);

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
    /// <returns>true if saved</returns>
    public static async Task<bool> SaveEnumSubListAsync(this SchemaContext context, string name, string? value, EnumValueInfo[] values, bool? append)
    {
        AnySchemaType? node = await context.GetSchemaTypeAsync(name);
        if (node is not EnumType @enum) return false;

        // authorize
        await context.AuthorizeAsync(@enum, PolicyScope.SchemaUpdate);

        // gets storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;

        // save the sub list
        @enum.SaveEnumSubListAsync(value, await provider.SaveEnumSubListAsync(@enum, value, values, append));

        // event
        context.RaiseEvent<SchemaChangeEvent>(node.Name);
        return true;
    }

    /// <summary>
    /// Save the app schema
    /// </summary>
    /// <param name="context">The schema context</param>
    /// <param name="app"></param>
    /// <returns></returns>
    public static async Task<bool> SaveAppSchemaAsync(this SchemaContext context, AppSchema app)
    {
        AppType? node = await context.GetAppTypeAsync(app.Name);

        // authorize
        if (node == null)
        {
            string[] paths = app.Name.SplitTypeName().SkipLast(1).ToArray();
            AppType? appParent = null;
            for (int i = paths.Length - 1; i >= 0; i--)
            {
                string path = string.Join('.', paths.Take(i + 1));
                appParent = await context.GetAppTypeAsync(path);
                if (appParent != null) break;
            }

            appParent ??= SchemaContext.RootAppType;
            await context.AuthorizeAsync(appParent, PolicyScope.SchemaCreate);
        }
        else
        {
            await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);
        }

        // Ges the storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;

        // save the schema
        if (!await provider.SaveAppSchemaAsync(app)) return false;

        // runtime save
        if (node == null)
        {
            AppType? parentNode = await context.GetAppTypeAsync(string.Join('.', app.Name.Split(".").Where(s => !string.IsNullOrEmpty(s)).SkipLast(1)));
            if (parentNode != null)
            {
                parentNode.Apps = parentNode.Apps == null ? [app] : parentNode.Apps.Where(p => !p.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)).Concat([app]).ToArray();
            }
        }
        await context.GetAppTypeAsync(app.Name, reload: true); // force reload

        // event
        context.RaiseEvent<AppSchemaChangeEvent>(app.Name);
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
        AppType? node = await context.GetAppTypeAsync(app);
        if (node == null || node.IsUsed) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaDelete);

        // delete the schema
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
        if (provider == null) return false;
        if (!await provider.DeleteAppSchemaAsync(app)) return false;
        context.RemoveAppType(app);

        // event
        context.RaiseEvent<AppSchemaDeleteEvent>(app);
        return true;
    }

    /// <summary>
    /// Save app field schema
    /// </summary>
    public static async Task<bool> SaveAppFieldSchemaAsync(this SchemaContext context, string app, AppFieldSchema field)
    {
        AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        // Gets the storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
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
        AppType? node = await context.GetAppTypeAsync(app);
        var appField = node?.GetField(field);
        if (appField == null) return false;

        // authorize
        await context.AuthorizeAsync(node!, PolicyScope.SchemaUpdate);

        // Gets the storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
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
            if (dataProvider != null) await dataProvider.DropDynamicTableAsync(appField.Schema!);
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
        AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        // Gets the storage provider
        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
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
        AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        AppWorkflowType? appWorkflowType = node.Workflows?.FirstOrDefault(w => w.Name.Equals(workflow.Name, StringComparison.OrdinalIgnoreCase));
        if (!forActive && appWorkflowType is { Activated: true }) return false;

        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
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
        AppType? node = await context.GetAppTypeAsync(app);
        if (node == null) return false;

        // authorize
        await context.AuthorizeAsync(node, PolicyScope.SchemaUpdate);

        AppWorkflowType? appWorkflowType = node.Workflows?.FirstOrDefault(w => w.Name.Equals(workflow, StringComparison.OrdinalIgnoreCase));
        if (appWorkflowType is { Activated: true }) return false;

        ISchemaStorageProvider? provider = context.GetService<ISchemaStorageProvider>();
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
        AppType? node = await context.GetAppTypeAsync(app);
        AppWorkflowType? appWorkflowType = node?.Workflows?.FirstOrDefault(w => w.Name.Equals(workflow, StringComparison.OrdinalIgnoreCase));
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
