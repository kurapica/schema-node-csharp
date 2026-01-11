using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

/// <summary>
/// Use application storage to store type schemas
/// </summary>
public class DynamicSchemaStorageProvider(SchemaContext context) : ISchemaStorageProvider
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
                
                // query schema
                if (string.IsNullOrEmpty(name))
                {
                    schema = new NodeSchema
                    {
                        Name = name,
                        Type = SchemaType.Namespace,
                        Display = name
                    };
                }
                else
                {
                    schema = await context.GetEntityAsync<NodeSchema>(Target, name);
                    if (schema == null)
                    {
                        schema = new NodeSchema
                        {
                            Name = name,
                            Type = SchemaType.Namespace,
                            Display = name
                        };
                        checkSubNs = true;
                    }
                }

                switch (schema.Type)
                {
                    case SchemaType.Namespace: 
                    {
                        // sub namespace
                        string ns = string.IsNullOrEmpty(schema.Name) ? Root : schema.Name;
                        List<NodeSchema> value = await context.GetEntitiesAsync<NodeSchema>(Target, s => s.Namespace == ns);
                        if (value.Count == 0 && checkSubNs) continue;

                        foreach (NodeSchema sub in value)
                        {
                            if (sub.Type == SchemaType.Namespace)
                            {
                                sub.HasSchemas = (await context.GetEntitiesAsync<NodeSchema>(Target, s => s.Namespace == sub.Name, take: 1)).total != 0;
                            }
                        }
                        schema.Schemas = value.ToArray();
                        break;
                    }
                    case SchemaType.Scalar:
                        schema.Scalar = await context.GetEntityAsync<ScalarSchema>(Target, name);
                        break;
                    case SchemaType.Enum:
                        schema.Enum = await context.GetEntityAsync<EnumSchema>(Target, name);
                        
                        if (schema is { Type: SchemaType.Enum, Enum.Cascade.Length: > 1 })
                        {
                            foreach (EnumValueInfo enumValueInfo in schema.Enum.Values)
                            {
                                enumValueInfo.HasSubList = (await context.GetEntitiesAsync<EnumValueInfo>(Target, e => e.Enum == schema.Name && e.Root == enumValueInfo.Value, take: 1)).total != 0;
                            }
                        }

                        break;
                    case SchemaType.Struct:
                        schema.Struct = await context.GetEntityAsync<StructSchema>(Target, name);
                        break;
                    case SchemaType.Array:
                        schema.Array = await context.GetEntityAsync<ArraySchema>(Target, name);
                        break;
                    case SchemaType.Json:
                    case SchemaType.Event:
                    case SchemaType.Workflow:
                        break;
                    case SchemaType.Func:
                        schema.Func = await context.GetEntityAsync<FunctionSchema>(Target, name);
                        break;
                    case SchemaType.Policy:
                        schema.Policy = await context.GetEntityAsync<PolicySchema>(Target, name);
                        break;
                }
                
                result.Add(schema);
            }

            return result.ToArray();
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to load schemas: {names}", string.Join(", ", names));
            return [];
        }
    }

    /// <inheritdoc />
    public Task<JsonNode?> CallFunctionAsync(string schemaName, JsonArray args, string? rType = null, string? target = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<bool> SaveSchemaAsync(NodeSchema schema)
    {
        if (string.IsNullOrEmpty(schema.Name)) return false;
        
        try
        {
            string root = string.Join('.', schema.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));
            if (string.IsNullOrEmpty(root)) root = Root;
            schema.Namespace = root; // add namespace
           
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync(Target, schema);

            switch (schema.Type)
            {
                case SchemaType.Scalar:
                    if (schema.Scalar != null)
                    {
                        schema.Scalar.Name = schema.Name;
                        await context.SaveEntityAsync(Target, schema.Scalar);
                    }
                    break;
                case SchemaType.Enum:
                    if (schema.Enum != null)
                    {
                        schema.Enum!.Name = schema.Name;
                        foreach (EnumValueInfo val in schema.Enum.Values)
                        {
                            val.Root = null;
                            val.SubList = null;
                        }
                        await context.SaveEntityAsync(Target, schema.Enum);
                    }
                    break;
                case SchemaType.Struct:
                    if (schema.Struct != null)
                    {
                        schema.Struct.Name = schema.Name;
                        await context.SaveEntityAsync(Target, schema.Struct);
                    }

                    break;
                case SchemaType.Array:
                    if (schema.Array != null)
                    {
                        schema.Array.Name = schema.Name;
                        await context.SaveEntityAsync(Target, schema.Array);
                    }

                    break;
                case SchemaType.Func:
                    if (schema.Func != null)
                    {
                        schema.Func.Name = schema.Name;
                        await context.SaveEntityAsync(Target, schema.Func);
                    }
                    break;
                case SchemaType.Policy:
                    if (schema.Policy != null)
                    {
                        schema.Policy.Name = schema.Name;
                        await context.SaveEntityAsync(Target, schema.Policy);
                    }
                    break;
            }
            
            await context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to save schema: {schema}", schema.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string schema)
    {
        if (string.IsNullOrEmpty(schema)) return false;
        
        try
        {
            AnySchemaType? delNode = await context.GetSchemaTypeAsync(schema);
            if (delNode == null) return false;
            NodeSchema nodeSchema = delNode!;

            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync(Target, nodeSchema);

            switch (nodeSchema.Type)
            {
                case SchemaType.Scalar:
                    if (nodeSchema.Scalar != null)
                    {
                        nodeSchema.Scalar.Name = nodeSchema.Name;
                        await context.DeleteEntityAsync(Target, nodeSchema.Scalar);
                    }
                    break;
                case SchemaType.Enum:
                    if (nodeSchema.Enum != null)
                    {
                        nodeSchema.Enum!.Name = nodeSchema.Name;
                        await context.DeleteEntitiesAsync<EnumValueInfo>(Target, e => e.Enum == schema);
                        await context.DeleteEntityAsync(Target, nodeSchema.Enum);
                    }
                    break;
                case SchemaType.Struct:
                    if (nodeSchema.Struct != null)
                    {
                        nodeSchema.Struct.Name = nodeSchema.Name;
                        await context.DeleteEntityAsync(Target, nodeSchema.Struct);
                    }

                    break;
                case SchemaType.Array:
                    if (nodeSchema.Array != null)
                    {
                        nodeSchema.Array.Name = nodeSchema.Name;
                        await context.DeleteEntityAsync(Target, nodeSchema.Array);
                    }

                    break;
                case SchemaType.Func:
                    if (nodeSchema.Func != null)
                    {
                        nodeSchema.Func.Name = nodeSchema.Name;
                        await context.DeleteEntityAsync(Target, nodeSchema.Func);
                    }
                    break;
                case SchemaType.Policy:
                    if (nodeSchema.Policy != null)
                    {
                        nodeSchema.Policy.Name = nodeSchema.Name;
                        await context.DeleteEntityAsync(Target, nodeSchema.Policy);
                    }
                    break;
            }
            await context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to delete schema: {schema}", schema);
            return false;
        }
    }

    #endregion
    
    #region Eunm
    
    /// <inheritdoc />
    public async Task<EnumValueInfo[]> LoadEnumSubListAsync(string schemaName, string? value, bool? fullList = null)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return [];

            // load enum values
            List<EnumValueInfo> enumValues = await context.GetEntitiesAsync<EnumValueInfo>(Target, e => e.Enum == schemaName && e.Root == value);
            enumValues.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));

            // sub enum list
            foreach (EnumValueInfo info in enumValues)
            {
                info.HasSubList = (await context.GetEntitiesAsync<EnumValueInfo>(Target, e => e.Enum == schemaName && e.Root == info.Value, take: 1)).total != 0;
            }
            return enumValues.ToArray();
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to load enum sub list: {schema} - {value}", schemaName, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string? value, bool? noSubList = null, bool? withSubList = null)
    {
        if (string.IsNullOrEmpty(value)) return [];
        try
        {
            List<EnumValueAccess> accesses = [];
            
            NodeSchema? schema = await context.GetEntityAsync<NodeSchema>(Target, schemaName);
            if (schema?.Type != SchemaType.Enum || schema.Enum == null) return [];
            
            string previous = "";
            while (!string.IsNullOrEmpty(value))
            {
                EnumValueInfo? info = schema.Enum.Values.FirstOrDefault(v => v.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                    ?? await context.GetEntityAsync<EnumValueInfo>(Target, schemaName, value);
                if (info == null) return [];
                
                if (withSubList == true)
                {
                    accesses.Insert(0, new EnumValueAccess
                    {
                        Value = previous,
                        SubList = !(noSubList ?? false) ? await LoadEnumSubListAsync(schemaName, info.Value) : null
                    });
                }
                withSubList = true;

                previous = value;
                value = info.Root;
            }
            accesses.Insert(0, new EnumValueAccess
            {
                Value = previous,
                SubList = !(noSubList ?? false) ? schema.Enum.Values : null
            });

            return accesses.ToArray();
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to load enum sub list: {schema} - {value}", schemaName, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<EnumValueInfo[]> SaveEnumSubListAsync(EnumType enumType, string? value, EnumValueInfo[] values, bool? append)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return values; // should be done in save schema
            
            EnumValueInfo? last = enumType.Root.GetEnumAccesses(value)?.Last()
                ?? await context.GetEntityAsync<EnumValueInfo>(Target, enumType.Name, value);

            // not existed
            if (last == null) return [];

            // load enum values
            List<EnumValueInfo> enumValues = await context.GetEntitiesAsync<EnumValueInfo>(Target, e => e.Enum == enumType.Name && e.Root == value);
            enumValues.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));

            List<EnumValueInfo> deletes = [];
            
            // merge values
            if (append == true)
            {
                enumValues.AddRange(values.Where(v => !enumValues.Any(e => e.Value.Equals(v.Value, StringComparison.OrdinalIgnoreCase))));
            }
            else
            {
                deletes = enumValues.Where(e => !values.Any(v => v.Value.Equals(e.Value, StringComparison.OrdinalIgnoreCase))).ToList();
                enumValues = values.ToList();
            }
            
            // Index settings
            for(int i = 0; i < enumValues.Count; i++)
            {
                enumValues[i].Enum = enumType.Name;
                enumValues[i].Root = value;
                enumValues[i].Seqno = i;
            }
            
            await context.BeginTransactionAsync();
            if (deletes.Count > 0)
                await context.DeleteEntitiesAsync(Target, deletes);

            if (enumValues.Count > 0)
                await context.SaveEntitiesAsync(Target, enumValues);
            await context.CommitTransactionAsync();

            // sub enum list
            foreach (EnumValueInfo info in enumValues)
            {
                info.HasSubList = (await context.GetEntitiesAsync<EnumValueInfo>(Target, e => e.Enum == enumType.Name && e.Root == info.Value, take: 1)).total != 0;
            }
            return enumValues.ToArray();
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to save enum sub list: {schema} - {value}", enumType.Name, value);
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
            if (string.IsNullOrWhiteSpace(app))
            {
                schema = new AppSchema
                {
                    Name = "",
                    Display = "Root",
                    HasApps = true
                };
                app = Root;
            }
            else
            {
                schema = await context.GetEntityAsync<AppSchema>(Target, app);
            }

            // load apps
            List<AppSchema> apps = await context.GetEntitiesAsync<AppSchema>(Target, e => e.Parent == app);
            foreach(AppSchema subApp in apps)
            {
                // check sub apps
                subApp.HasApps = (await context.GetEntitiesAsync<AppSchema>(Target, e => e.Parent == subApp.Name, take: 1)).total != 0;

                // check fields
                subApp.HasFields = (await context.GetEntitiesAsync<AppFieldSchema>(Target, e => e.App == subApp.Name, take: 1)).total != 0;
            }

            // provide container app
            if (schema == null)
            {
                if (apps.Count > 0) schema = new AppSchema { Name = app, Apps = apps.ToArray() };
                return schema;
            }
            
            schema.Apps = apps.Count > 0 ? apps.ToArray() : null;
            
            // load fields
            List<AppFieldSchema> fields = await context.GetEntitiesAsync<AppFieldSchema>(Target, e => e.App == app);
            if (fields.Count > 0)
            {
                fields.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
                schema.Fields = fields.ToArray();
                schema.HasFields = schema.Fields.Length > 0;
            }
            
            // load workflows
            List<AppWorkflowSchema> workflows = await context.GetEntitiesAsync<AppWorkflowSchema>(Target, e => e.App == app);
            if (workflows.Count > 0)
            {
                workflows.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
                schema.Workflows = workflows.ToArray();
            }

            return schema;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to load app schema: {app}", app);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppSchemaAsync(AppSchema app)
    {
        try
        {
            await context.BeginTransactionAsync();
            app.Parent = string.Join('.', app.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));
            if (string.IsNullOrEmpty(app.Parent)) app.Parent = Root;
            await context.SaveEntityAsync(Target, app);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to save app schema: {app}", app.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppSchemaAsync(string app)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(app)) return false;

            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync<AppSchema>(Target, app);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to delete app schema: {app}", app);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field)
    {
        try
        {            
            AppType? appNode = await context.GetAppTypeAsync(app);
            if (appNode == null) return false;
            
            AppFieldType? exist = appNode.GetField(field.Name);
            field.App = appNode.Name;
            if (exist == null)
            {
                // new
                field.Seqno = (appNode.Fields?.Count ?? 0);
            }
            else
            {
                field.Seqno = exist.Seqno;
            }
            
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync(Target, field);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to save app field schema: {app} - {field}", app, field.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppFieldSchemaAsync(string app, string field)
    {
        try
        {
            List<AppFieldSchema> fields = await context.GetEntitiesAsync<AppFieldSchema>(Target, e => e.App == app);
            int exist = fields.FindIndex(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (exist < 0) return false;

            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync(Target, fields[exist]);

            fields = fields.Skip(exist + 1).ToList();
            for(int i = 0; i < fields.Count; i++)
            {
                fields[i].Seqno = exist + i;
            }
            await context.SaveEntitiesAsync(Target, fields);

            await context.CommitTransactionAsync();

            return true;
        }
        catch(Exception ex)
        {
            context.Logger.LogError(ex, "Failed to delete app field schema: {app} - {field}", app, field);
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

            AppFieldSchema? first = await context.GetEntityAsync<AppFieldSchema>(Target, app, field1);
            AppFieldSchema? second = await context.GetEntityAsync<AppFieldSchema>(Target, app, field2);
            if (first == null || second == null) return false;

            (first.Seqno, second.Seqno) = (second.Seqno, first.Seqno);

            await context.BeginTransactionAsync();
            await context.SaveEntitiesAsync(Target, [ first, second ]);
            await context.CommitTransactionAsync();
            return true;
        }
        catch(Exception e)
        {
            context.Logger.LogError(e, "Failed to swap app field schema: {app} - {field1} <-> {field2}", app, field1, field2);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppWorkflowSchemaAsync(string app, AppWorkflowSchema workflow)
    {
        try
        {            
            AppType? appNode = await context.GetAppTypeAsync(app);
            if (appNode == null) return false;
            
            AppFieldType? exist = appNode.GetField(workflow.Name);
            workflow.App = appNode.Name;
            if (exist == null)
            {
                // new
                workflow.Seqno = (appNode.Workflows?.Count ?? 0);
            }
            else
            {
                workflow.Seqno = exist.Seqno;
            }
            
            await context.BeginTransactionAsync();
            await context.SaveEntityAsync(Target, workflow);
            await context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            context.Logger.LogError(e, "Failed to save app workflow schema: {app} - {field}", app, workflow.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppWorkflowSchemaAsync(string app, string workflow)
    {
        try
        {
            List<AppWorkflowSchema> fields = await context.GetEntitiesAsync<AppWorkflowSchema>(Target, e => e.App == app);
            int exist = fields.FindIndex(f => f.Name.Equals(workflow, StringComparison.OrdinalIgnoreCase));
            if (exist < 0) return false;

            await context.BeginTransactionAsync();
            await context.DeleteEntityAsync(Target, fields[exist]);

            fields = fields.Skip(exist + 1).ToList();
            for(int i = 0; i < fields.Count; i++)
            {
                fields[i].Seqno = exist + i;
            }
            await context.SaveEntitiesAsync(Target, fields);

            await context.CommitTransactionAsync();

            return true;
        }
        catch(Exception ex)
        {
            context.Logger.LogError(ex, "Failed to delete app workflow schema: {app} - {field}", app, workflow);
            return false;
        }
    }

    #endregion

    #region Property
    
    /// <inheritdoc />
    public SchemaLoadState? DefaultLoadState { get; } = SchemaLoadState.Server;

    #endregion

    #region Utility

    static readonly string Target = Guid.Empty.ToString("D");

    private const string Root = "__root";

    #endregion
}