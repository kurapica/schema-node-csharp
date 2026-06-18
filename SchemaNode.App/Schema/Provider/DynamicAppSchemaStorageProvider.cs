using SchemaNode.Context;
using SchemaNode.Data;
using SchemaNode.Data.Entity;
using SchemaNode.Runtime;
using SchemaNode.Property;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Schema.Provider;

/// <summary>
/// Use application storage to store type schemas
/// </summary>
public class DynamicAppSchemaStorageProvider(SchemaContext context) : IAppSchemaStorageProvider
{
    #region Schema

    /// <inheritdoc />
    public async Task<NodeSchema[]> LoadSchemaAsync(string[] names)
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
                    schema = await context.GetEntityAsync<NodeEntity>(Target, string.IsNullOrWhiteSpace(namespaceName) ? Root : namespaceName, schemaName);
                    
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

                // clear the root namespace
                if (Root.Equals(schema.Namespace, StringComparison.InvariantCultureIgnoreCase))
                    schema.Namespace = null;

                switch (schema.Kind)
                {
                    case SCHEMA_KIND_NAMESPACE: 
                    {
                        // sub namespace
                        string ns = string.IsNullOrEmpty(schema.Name) ? Root : schema.FullName;
                        NodeSchema[] value = (await context.GetEntitiesAsync<NodeEntity>(Target, s => s.Namespace == ns))
                            .Select(s => (NodeSchema)s!).ToArray();
                        if (value.Length == 0 && checkSubNs) continue;
                        schema.Schemas = value;
                        break;
                    }
                    case SCHEMA_KIND_ENUM:
                        EnumSchema? @enum = schema.GetProperty<EnumProperty>()?.Value;
                        if (@enum is { Cascade.Length: > 0 })
                        {
                            foreach (EnumValueSchema value in @enum.Values)
                            {
                                value.IsFullyLoaded = false;
                                value.SubList = null; // sub list will be loaded on demand, set to null to indicate not loaded
                                value.HasSubList = (await context.GetEntitiesAsync<EnumValueEntity>(Target,e => e.Enum == schema.Name && e.Root == value.Value, take: 1)).total != 0;
                            }
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
            if (string.IsNullOrEmpty(schema.Namespace)) schema.Namespace = Root;
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync(Target, (NodeEntity)schema!);
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
            await context.DeleteEntityAsync(Target, (NodeEntity)nodeSchema!);

            switch (nodeSchema.Kind)
            {
                case SCHEMA_KIND_ENUM:
                    if (delNode is Runtime.EnumType { Cascade.Length: > 0 })
                        await context.DeleteEntitiesAsync<EnumValueEntity>(Target, e => e.Enum == schema);
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
    public async Task<EnumValueSchema[]> LoadEnumSubListAsync(string schemaName, string? value)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return [];

            // load enum values
            List<EnumValueEntity> enumValues = await context.GetEntitiesAsync<EnumValueEntity>(Target, e => e.Enum == schemaName && e.Root == value);
            enumValues.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
            return enumValues.Select(e => (EnumValueSchema)e!).ToArray();
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to load enum sub list: {schema} - {value}", schemaName, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(string name, string? value, bool? noSubList = null, bool? withSubList = null)
    {
        if (string.IsNullOrEmpty(value)) return [];
        try
        {
            string namespaceName = name.GetNamespace();
            string schemaName = name.GetSchemaName();
            NodeSchema? schema = await context.GetEntityAsync<NodeEntity>(Target, string.IsNullOrWhiteSpace(namespaceName) ? Root : namespaceName, schemaName);
            if (schema?.Kind != SCHEMA_KIND_ENUM) return [];
            EnumSchema? enumSchema = schema.GetProperty<EnumProperty>()?.Value;
            if (enumSchema == null) return [];
            
            // current value is in the root list, just return the root list
            if (enumSchema.Values.FirstOrDefault(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase)) is {} exist)
            {
                if (withSubList == true && enumSchema.Cascade is { Length: > 1 })
                {
                    EnumValueSchema[] subList = await LoadEnumSubListAsync(name, value);
                    if (subList.Length > 0)
                        return
                        [
                            new EnumValueAccess { Value = value, Schema =  noSubList == true ? exist.Clone() : null, SubList = !(noSubList ?? false) ? enumSchema.Values : null },
                            new EnumValueAccess { Value = "", SubList = subList }
                        ];
                }
                return
                [
                    new EnumValueAccess { Value = value, SubList = !(noSubList ?? false) ? enumSchema.Values : null }
                ];
            }

            // no root value match
            if (enumSchema is not { Cascade.Length: > 0 }) return [];
            
            // check the entity
            EnumValueEntity? info = await context.GetEntityAsync<EnumValueEntity>(Target, name, value);
            
            List<EnumValueAccess>? accesses = null;
            while (!string.IsNullOrWhiteSpace(info?.Root))
            {
                accesses ??= [];
                accesses.Insert(0, new EnumValueAccess
                {
                    Value = info.Value,
                    Schema = noSubList == true ? info : null,
                    SubList = noSubList != true ? await  LoadEnumSubListAsync(name, info.Root) : null
                });
                
                // check root value is in the enum list, if exist just break, otherwise continue to find the parent value
                if (enumSchema.Values.FirstOrDefault(v => v.Value.Equals(info.Root, StringComparison.OrdinalIgnoreCase)) is {} existRoot)
                {
                    accesses.Insert(0, new EnumValueAccess
                    {
                        Value = info.Root,
                        Schema = noSubList == true ? existRoot : null,
                        SubList = noSubList != true ? enumSchema.Values : null
                    });
                    break;
                }
                info = await context.GetEntityAsync<EnumValueEntity>(Target, name, info.Root);
            }

            // access path broken
            if (string.IsNullOrWhiteSpace(info?.Root) || accesses == null) return [];

            if (withSubList == true && enumSchema.Cascade.Length > accesses.Count)
            {
                accesses.Add(new EnumValueAccess
                {
                    Value = "",
                    SubList = await LoadEnumSubListAsync(name, value)
                });
            }

            return accesses.ToArray();
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to load enum sub list: {schema} - {value}", name, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<EnumValueSchema[]> SaveEnumSubListAsync(string name, string? value, EnumValueSchema[] values, bool? append)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return values; // should be done in save schema
            
            EnumValueAccess[] accessList = await LoadEnumAccessListAsync(name, value, noSubList: true, withSubList: true);
            if (accessList.Length == 0 || !string.IsNullOrWhiteSpace(accessList.Last().Value)) return [];

            // no sub list allowed
            if (!string.IsNullOrWhiteSpace(accessList.Last().Value))
                return [];
            
            List<EnumValueSchema> enumValues = accessList.Last().SubList?.ToList() ?? [];
            EnumValueSchema? enumSchema = accessList.SkipLast(1).LastOrDefault()?.Schema;
            if (enumSchema == null) return [];

            List<EnumValueEntity>? deletes = null;
            
            // merge values
            if (append == true)
            {
                enumValues.AddRange(values.Where(v => !enumValues.Any(e => e.Value.Equals(v.Value, StringComparison.OrdinalIgnoreCase))));
            }
            else
            {
                deletes = enumValues
                    .Where(e => !values.Any(v => v.Value.Equals(e.Value, StringComparison.OrdinalIgnoreCase)))
                    .Select(e => (EnumValueEntity)e!)
                    .ToList();
                enumValues = values.Select(e =>
                {
                    EnumValueSchema? exist = enumValues.FirstOrDefault(v => v.Value.Equals(e.Value, StringComparison.OrdinalIgnoreCase));
                    if (exist != null) e.HasSubList = exist.HasSubList; // keep the sub list info if exist
                    return e;
                }).ToList();
            }
            
            // Index settings
            List<EnumValueEntity> entities  = enumValues.Select(e => (EnumValueEntity)e!).ToList();
            
            await context.BeginTransactionAsync();
            if (deletes is { Count: > 0 })
            {
                foreach (var delete in deletes)
                    delete.Enum = name;

                await context.DeleteEntitiesAsync(Target, deletes);
            }

            if (enumValues.Count > 0)
            {
                for(int i = 0; i < enumValues.Count; i++)
                {
                    entities[i].Enum = name;
                    entities[i].Root = value;
                    entities[i].Seqno = i;
                }
                await context.SaveEntitiesAsync(Target, entities);
            }

            // Update the sub list settings
            if (enumSchema.HasSubList == true ? enumValues.Count == 0 : enumValues.Count > 0)
            {
                enumSchema.HasSubList = enumValues.Count > 0;
                await context.SaveEntityAsync(Target, (EnumValueEntity)enumSchema!);
            }
            
            await context.CommitTransactionAsync();
            return enumValues.ToArray();
        }
        catch (Exception e)
        {
            context.LogError(e, "Failed to save enum sub list: {schema} - {value}", name, value);
            return [];
        }
    }

    #endregion
    
    #region App
    
    /// <inheritdoc />
    public async Task<AppSchema?> LoadAppSchemaAsync(string app)
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
                app = Root;
            }
            else
            {
                schema = await context.GetEntityAsync<AppEntity>(Target, string.IsNullOrWhiteSpace(container) ? Root : container, name);
            }

            // load apps
            List<AppSchema> apps = (await context.GetEntitiesAsync<AppEntity>(Target, e => e.Container == app))
                .Select(a => (AppSchema)a!).ToList();
            foreach(AppSchema subApp in apps)
            {
                // check sub apps
                subApp.HasApps = (await context.GetEntitiesAsync<AppEntity>(Target, e => e.Container == subApp.FullName, take: 1)).total != 0;

                // check fields
                subApp.HasFields = (await context.GetEntitiesAsync<AppFieldEntity>(Target, e => e.App == subApp.FullName, take: 1)).total != 0;
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
            app.Container = string.Join('.', app.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));
            if (string.IsNullOrEmpty(app.Container)) app.Container = Root;
            await context.SaveEntityAsync(Target, app);
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
            await context.DeleteEntityAsync<AppSchema>(Target, string.IsNullOrWhiteSpace(container) ? Root : container, name);
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
            await context.SaveEntityAsync(Target, (AppFieldEntity)field!);
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
            await context.SaveEntityAsync(Target, (AppWorkflowEntity)workflow!);
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
    private const string Root = "__root";

    #endregion
}