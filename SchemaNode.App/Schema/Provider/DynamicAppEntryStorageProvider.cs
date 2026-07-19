using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Data.Entity;
using SchemaNode.Runtime;
using SchemaNode.Property;
using SchemaNode.Struct;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Schema.Provider;

/// <summary>
/// Use application storage to store type schemas
/// </summary>
public class DynamicAppEntryStorageProvider(SchemaContext context) : IAppEntryStorageProvider
{
    #region Schema

    /// <inheritdoc />
    public async Task<NodeSchema[]> GetSchemaAsync(string[] names)
    {
        try
        {
            List<NodeSchema> result = new();
            foreach (string name in names)
            {
                NodeSchema? schema;
                bool checkSubNs = false;
                
                // root namespace not exist in the storage, just check the sub namespaces
                if (string.IsNullOrWhiteSpace(name))
                {
                    schema = new NodeSchema
                    {
                        Name = name,
                        Kind = SCHEMA_KIND_NAMESPACE,
                    };
                }
                else
                {
                    string namespaceName = name.GetNamespace();
                    string schemaName = name.GetSchemaName();
                    schema = await context.GetEntityAsync<NodeEntity>(Target, string.IsNullOrWhiteSpace(namespaceName) ? ROOT : namespaceName, schemaName);
                    
                    // namespace may not save when they are system generated, just try
                    if (schema == null)
                    {
                        schema = new NodeSchema
                        {
                            Namespace = namespaceName,
                            Name = schemaName,
                            Kind = SCHEMA_KIND_NAMESPACE,
                        };
                        checkSubNs = true;
                    }
                }

                switch (schema.Kind)
                {
                    case SCHEMA_KIND_NAMESPACE: 
                    {
                        // sub namespace
                        string ns = string.IsNullOrEmpty(schema.Name) ? ROOT : schema.FullName;
                        NodeSchema[] value = (await context.GetEntitiesAsync<NodeEntity>(Target, s => s.Namespace == ns)).Select(s => (NodeSchema)s!).ToArray();
                        if (value.Length == 0 && checkSubNs) continue;
                        schema.Schemas = value;
                        break;
                    }
                    case SCHEMA_KIND_ENUM:
                        EnumSchema? @enum = schema.GetProperty<EnumProperty>()?.Value;
                        if (@enum is { Cascade.Length: > 0 })
                        {
                            foreach (var value in @enum.Values)
                            {
                                value.HasChildren = (await context.GetEntitiesAsync<EnumValueEntity>(Target,e => e.Enum == schema.FullName && e.Root == value.Value, take: 1)).Count != 0;
                            }
                            schema.SetProperty<EnumProperty, EnumSchema>(@enum);
                        }
                        break;
                }

                result.Add(schema);
            }

            return result.ToArray();
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to load schemas: {names}", string.Join(", ", names));
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveSchemaAsync(NodeSchema schema)
    {
        if (string.IsNullOrEmpty(schema.Name)) return false;
        
        try
        {
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync<NodeEntity>(Target, schema!);
            await context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save schema: {schema}", schema.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string schema)
    {
        if (string.IsNullOrEmpty(schema)) return false;
        
        try
        {
            NodeType? delNode = await context.GetNodeTypeAsync(schema);
            NodeSchema? nodeSchema = delNode?.GetNodeSchema();
            if (nodeSchema == null) return false;
            
            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync<NodeEntity>(Target, nodeSchema!);

            switch (nodeSchema.Kind)
            {
                case SCHEMA_KIND_ENUM:
                    if (delNode is Runtime.EnumType { Cascade.Length: > 0 })
                        await context.DeleteEntitiesAsync<EnumValueEntity>(Target, e => e.Enum == nodeSchema.FullName);
                    break;
            }
            await context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to delete schema: {schema}", schema);
            return false;
        }
    }

    #endregion
    
    #region Eunm
    
    /// <inheritdoc />
    public async Task<EntryAccess<string>[]> GetEnumEntryAccessAsync(string name, string? value, string? start = null)
    {
        if (string.IsNullOrEmpty(value) && string.IsNullOrWhiteSpace(start)) return [];
        try
        {
            var enumType = await context.GetNodeTypeAsync<Runtime.EnumType>(name);
            if (enumType is not { Cascade.Length: > 0 }) return [];
            AppSchemaDataOrder[] orderBy = [new AppSchemaDataOrder(nameof(EnumValueEntity.Seqno), false)];

            // Only load the children of the start value if value not provided
            if (string.IsNullOrWhiteSpace(value))
            {
                Entry<string>? startEntry = await context.GetEntityAsync<EnumValueEntity>(Target, name, start!);
                if (startEntry is not { HasChildren: true }) return []; // not existed
                
                var children = await context.GetEntitiesAsync<EnumValueEntity>(Target, 
                    e => e.Enum == name && e.Root == start, orderBy: orderBy);
                return
                [
                    new EntryAccess<string>
                    {
                        Entry    =  startEntry,
                        Children = children.Select(e => (Entry<string>)e!).ToArray(),
                    }
                ];
            }
            
            // check the entity
            List<EntryAccess<string>> accesses = [];
            bool matchBranch = false;
            while (value is not null)
            {
                EnumValueEntity? info = await context.GetEntityAsync<EnumValueEntity>(Target, name, value);
                if (info == null) return [];
                accesses.Add(new EntryAccess<string>
                {
                    Entry =  info,
                    Children = (await context.GetEntitiesAsync<EnumValueEntity>(Target, 
                        e => e.Enum == name && e.Root == value, orderBy: orderBy))
                        .Select(e => (Entry<string>)e!).ToArray()
                });
                if (start is not null && value.Equals(start))
                {
                    matchBranch = true;
                    break;
                }
                value = info.Root;
            }
            if (start is not null && !matchBranch) return [];
            
            // return
            accesses.Reverse();
            return accesses.ToArray();
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to load enum sub list: {schema} - {value}", name, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveEnumEntriesAsync(string name, string value, Entry<string>[] values, bool? append)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return false; // should be done in save schema
            
            var enumType = await context.GetNodeTypeAsync<Runtime.EnumType>(name);
            if (enumType is not { Cascade.Length: > 0 }) return false;
            
            var accessList = await GetEnumEntryAccessAsync(name, value);
            if (accessList.Length == 0 || accessList.Length >= enumType.Cascade.Length) return false;

            EnumValueEntity? valueEntity = null;
            if (accessList.Length > 1)
            {
                valueEntity = await context.GetEntityAsync<EnumValueEntity>(Target, name, value);
                if (valueEntity == null) return false;
            }
            
            // Gets the exist entries
            List<EnumValueEntity> existEntries = await context.GetEntitiesAsync<EnumValueEntity>(
                Target, e => e.Enum == name && e.Root == value, orderBy:[new  AppSchemaDataOrder(nameof(EnumValueEntity.Seqno), false)]);
            
            List<EnumValueEntity>? deletes = null;
            
            // merge values
            if (append == true)
            {
                existEntries.AddRange(values.Where(v => 
                    !existEntries.Any(e => e.Value.Equals(v.Value, StringComparison.OrdinalIgnoreCase)))
                    .Select(v =>
                    {
                        EnumValueEntity entity = v!;
                        entity.Enum = name;
                        entity.Root = value;
                        entity.HasChildren = false;
                        return entity;
                    }));
            }
            else
            {
                deletes = existEntries
                    .Where(e => !values.Any(v => v.Value.Equals(e.Value, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (deletes.Any(d => d.HasChildren)) return false; // don't delete entry that has children
                existEntries = values.Select(e => (EnumValueEntity)e!).Select((e, i) =>
                {
                    e.Enum = name;
                    e.Root = value;
                    e.HasChildren = false;
                    e.Seqno = i;
                    
                    var exist = existEntries.FirstOrDefault(v => v.Value.Equals(e.Value, StringComparison.OrdinalIgnoreCase));
                    if (exist != null) e.HasChildren = exist.HasChildren; // keep the sub list info if exist
                    return e;
                }).ToList();
            }
            
            await context.BeginTransactionAsync();
            if (deletes is { Count: > 0 })
                await context.DeleteEntitiesAsync(Target, deletes);

            if (existEntries.Count > 0)
                await context.SaveEntitiesAsync(Target, existEntries);

            // Update the children flag
            if (valueEntity is not null)
            {
                valueEntity.HasChildren = existEntries.Count > 0;
                await context.SaveEntityAsync(Target, valueEntity);
            }
            
            await context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save enum sub list: {schema} - {value}", name, value);
            return false;
        }
    }

    #endregion
    
    #region App
    
    /// <inheritdoc />
    public async Task<AppSchema?> GetAppSchemaAsync(string app)
    {
        try
        {
            AppSchema? schema;
            string container = app.GetNamespace();
            string name = app.GetSchemaName();
            if (string.IsNullOrWhiteSpace(name))
            {
                schema = new AppSchema
                {
                    Name = "",
                    HasApps = true
                };
                app = ROOT;
            }
            else
            {
                schema = await context.GetEntityAsync<AppEntity>(Target, string.IsNullOrWhiteSpace(container) ? ROOT : container, name);
                if (schema != null) app = schema.FullName;
            }

            // load apps
            List<AppSchema> apps = (await context.GetEntitiesAsync<AppEntity>(Target, e => e.Container == app)).Select(a => (AppSchema)a!).ToList();
            foreach(AppSchema subApp in apps)
            {
                // check sub apps
                subApp.HasApps = (await context.GetEntitiesAsync<AppEntity>(Target, e => e.Container == subApp.FullName, take: 1)).Count != 0;

                // check fields
                subApp.HasFields = (await context.GetEntitiesAsync<AppFieldEntity>(Target, e => e.App == subApp.FullName, take: 1)).Count != 0;
            }

            // provide container app
            if (schema == null)
            {
                if (apps.Count > 0) schema = new AppSchema { Container = container, Name = name, Apps = apps.ToArray() };
                return schema;
            }
            
            schema.Apps = apps.Count > 0 ? apps.ToArray() : null;
            
            // load fields
            List<AppFieldEntity> fields = await context.GetEntitiesAsync<AppFieldEntity>(Target, e => e.App == app);
            if (fields.Count > 0)
            {
                fields.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
                schema.Fields = fields.Select(f => (AppFieldSchema)f!).ToArray();
                schema.HasFields = schema.Fields.Length > 0;
            }
            
            // load workflows
            List<AppWorkflowEntity> workflows = await context.GetEntitiesAsync<AppWorkflowEntity>(Target, e => e.App == app);
            if (workflows.Count > 0)
            {
                workflows.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
                schema.Workflows = workflows.Select(w => (AppWorkflowSchema)w!).ToArray();
            }

            return schema;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to load app schema: {app}", app);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppSchemaAsync(AppSchema app)
    {
        try
        {
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync<AppEntity>(Target, app!);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save app schema: {app}", app.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppSchemaAsync(string app)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(app)) return false;

            string container = app.GetNamespace();
            string name = app.GetSchemaName();
            
            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync<AppEntity>(Target, string.IsNullOrWhiteSpace(container) ? ROOT : container, name);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to delete app schema: {app}", app);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field)
    {
        try
        {            
            var appNode = await context.GetAppTypeAsync(app);
            if (appNode == null) return false;
            
            AppFieldType? exist = appNode.GetField(field.Name);
            field.App = appNode.Name;
            if (exist == null)
            {
                // new
                field.Seqno = appNode.GetFields().Count() + 1;
            }
            else
            {
                field.Seqno = exist.Seqno;
            }
            
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync<AppFieldEntity>(Target, field!);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save app field schema: {app} - {field}", app, field.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppFieldSchemaAsync(string app, string field)
    {
        try
        {
            List<AppFieldEntity> fields = await context.GetEntitiesAsync<AppFieldEntity>(Target, e => e.App == app);
            int exist = fields.FindIndex(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (exist < 0) return false;

            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync(Target, fields[exist]);

            fields = fields.Skip(exist + 1).ToList();
            for(int i = 0; i < fields.Count; i++)
                fields[i].Seqno = exist + i;
            await context.SaveEntitiesAsync(Target, fields);
            await context.CommitTransactionAsync();

            return true;
        }
        catch(Exception ex)
        {
            context.LogError(ex, "Failed to delete app field schema: {app} - {field}", app, field);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2)
    {
        try
        {
            if (string.IsNullOrEmpty(field1) || string.IsNullOrEmpty(field2)) return false;
            if (field1 == field2) return true;

            AppFieldEntity? first = await context.GetEntityAsync<AppFieldEntity>(Target, app, field1);
            AppFieldEntity? second = await context.GetEntityAsync<AppFieldEntity>(Target, app, field2);
            if (first == null || second == null) return false;

            (first.Seqno, second.Seqno) = (second.Seqno, first.Seqno);

            await context.BeginTransactionAsync();
            await context.SaveEntitiesAsync(Target, [ first, second ]);
            await context.CommitTransactionAsync();
            return true;
        }
        catch(Exception e)
        {
            context.LogError(e, "Failed to swap app field schema: {app} - {field1} <-> {field2}", app, field1, field2);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow)
    {
        try
        {            
            var appNode = await context.GetAppTypeAsync(app);
            if (appNode == null) return false;
            
            AppWorkflowType? exist = appNode.GetWorkflow(workflow.Name);
            workflow.App = appNode.Name;
            if (exist == null)
            {
                // new
                workflow.Seqno = appNode.GetWorkflows().Count() + 1;
            }
            else
            {
                workflow.Seqno = exist.Seqno;
            }
            
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync<AppWorkflowEntity>(Target, workflow!);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save app workflow schema: {app} - {field}", app, workflow.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow)
    {
        try
        {
            List<AppWorkflowEntity> fields = await context.GetEntitiesAsync<AppWorkflowEntity>(Target, e => e.App == app);
            int exist = fields.FindIndex(f => f.Name.Equals(workflow, StringComparison.OrdinalIgnoreCase));
            if (exist < 0) return false;

            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync(Target, fields[exist]);

            fields = fields.Skip(exist + 1).ToList();
            for(int i = 0; i < fields.Count; i++)
                fields[i].Seqno = exist + i;
            await context.SaveEntitiesAsync(Target, fields);

            await context.CommitTransactionAsync();

            return true;
        }
        catch(Exception ex)
        {
            context.LogError(ex, "Failed to delete app workflow schema: {app} - {field}", app, workflow);
            return false;
        }
    }

    #endregion

    #region Utility

    static readonly string Target = Guid.Empty.ToString("D");

    #endregion
}