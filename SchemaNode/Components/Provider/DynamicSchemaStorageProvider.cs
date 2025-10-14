using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;

namespace SchemaNode.Components.Provider;

/// <summary>
/// Use application storage to store type schemas
/// </summary>
public class DynamicSchemaStorageProvider: ISchemaStorageProvider
{
    #region Constructor

    public DynamicSchemaStorageProvider(SchemaContext context)
    {
        _context = context;
    }

    #endregion
    
    #region Schema

    /// <inheritdoc />
    public async Task<NodeSchema[]> LoadSchemaAsync(string[] names)
    {
        try
        {            
            List<NodeSchema> result = new();
            foreach (string name in names)
            {
                NodeSchema? schema = null;
                
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
                    schema = await _context.GetEntityAsync<NodeSchema>(TARGET, name);
                }
                
                if (schema == null) continue;

                if (schema.Type == SchemaType.Namespace)
                {
                    // sub namespace
                    List<NodeSchema> value = await _context.GetEntitysAsync<NodeSchema>(TARGET, (nameof(NodeSchema.Namespace), string.IsNullOrEmpty(schema.Name) ? ROOT : schema.Name));

                    foreach (NodeSchema sub in value)
                    {
                        if (sub.Type == SchemaType.Namespace)
                        {
                            (List<NodeSchema> _, int total) = await _context.GetFieldDataAsync<NodeSchema>(TARGET, new JsonObject
                            {
                                { nameof(NodeSchema.Namespace).ToCamelCase(), sub.Name }
                            }, take: 1);
                            if (total > 0) sub.HasSchemas = true;
                        }
                    }
                    schema.Schemas = value.ToArray();
                }
                else if (schema.Type == SchemaType.Enum)
                {
                    schema.Enum!.Values = await LoadEnumSubListAsync(schema.Name, null);
                }
                
                result.Add(schema);
            }

            return result.ToArray();
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to load schemas: {names}", string.Join(", ", names));
            return [];
        }
    }

    /// <inheritdoc />
    public Task<JsonNode?> CallFunctionAsync(string schemaName, JsonArray args, string[]? generic = null)
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
            if (string.IsNullOrEmpty(root)) root = ROOT;
            schema.Namespace = root; // add namespace
            
            await _context.BeginTransactionAsync();
            await _context.SaveEntityAsync(TARGET, schema);
            await _context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to save schema: {schema}", schema.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSchemaAsync(string schema)
    {
        if (string.IsNullOrEmpty(schema)) return false;
        
        try
        {
            AnySchemeType? delNode = await _context.GetSchemaNodeAsync(schema);
            if (delNode == null) return false;

            await _context.BeginTransactionAsync();
            await _context.DeleteEntityAsync<NodeSchema>(TARGET, delNode!);
            if (delNode?.Type == SchemaType.Enum)
            {
                await _context.DeleteEntitysAsync<EnumValueInfo>(TARGET, (nameof(EnumValueInfo.Enum), schema));
            }
            await _context.CommitTransactionAsync();
            return true;
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to delete schema: {schema}", schema);
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
            AnySchemeType? eNode = await _context.GetSchemaNodeAsync(schemaName);
            if (eNode is not EnumType enumType) return [];
            int maxCascade = enumType.Cascade?.Length ?? 1;

            EnumValueInfo? last = enumType.Root.GetEnumAccesses(value)?.Last() 
                ?? await _context.GetEntityAsync<EnumValueInfo>(TARGET, enumType.Name, value!);

            // not existed
            if (last == null || (last.Level + 1) >= maxCascade) return [];
            if (string.IsNullOrEmpty(value)) value = ROOT;

            // load enum values
            List<EnumValueInfo> enumValues = await _context.GetEntitysAsync<EnumValueInfo>(TARGET, 
                (nameof(EnumValueInfo.Enum), enumType.Name),
                (nameof(EnumValueInfo.Root), value));
            enumValues.ForEach(v => v.Level = last.Level + 1);
            enumValues.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));

            if ((last.Level + 1) < maxCascade)
            {
                // sub enum list
                foreach (EnumValueInfo info in enumValues)
                {
                    (_, int total) = await _context.GetFieldDataAsync<EnumValueInfo>(TARGET, new JsonObject
                    {
                        { nameof(EnumValueInfo.Enum).ToCamelCase(), enumType.Name },
                        { nameof(EnumValueInfo.Root).ToCamelCase(), info.Value }
                    }, take: 1);
                    if (total > 0) info.HasSubList = true;
                }
            }
            return enumValues.ToArray();
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to load enum sub list: {schema} - {value}", schemaName, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<EnumValueAccess[]> LoadEnumAccessListAsync(string schemaName, string value, bool? noSubList = null, bool? withSubList = null)
    {
        if (string.IsNullOrEmpty(value)) return [];
        try
        {
            AnySchemeType? eNode = await _context.GetSchemaNodeAsync(schemaName);
            if (eNode is not EnumType enumType) return [];

            List<EnumValueAccess> accesses = [];
            string previous = "";
            while (!string.IsNullOrEmpty(value))
            {
                EnumValueInfo? info = await _context.GetEntityAsync<EnumValueInfo>(TARGET, enumType.Name, value);
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
                value = value == ROOT ? "" : (info.Root ?? ROOT);
            }

            return accesses.ToArray();
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to load enum sub list: {schema} - {value}", schemaName, value);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<EnumValueInfo[]> SaveEnumSubListAsync(EnumType enumType, string? value, EnumValueInfo[] values, bool? append)
    {
        try
        {
            int maxCascade = enumType.Cascade?.Length ?? 1;

            EnumValueInfo? last = enumType.Root.GetEnumAccesses(value)?.Last()
                ?? await _context.GetEntityAsync<EnumValueInfo>(TARGET, enumType.Name, value!);

            // not existed
            if (last == null || (last.Level + 1) >= maxCascade) return [];
            if (string.IsNullOrEmpty(value)) value = ROOT;

            // load enum values
            List<EnumValueInfo> enumValues = await _context.GetEntitysAsync<EnumValueInfo>(TARGET,
                (nameof(EnumValueInfo.Enum), enumType.Name),
                (nameof(EnumValueInfo.Root), value)
            );
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
            
            for(int i = 0; i < enumValues.Count; i++)
            {
                enumValues[i].Level = last.Level + 1;
                enumValues[i].Enum = enumType.Name;
                enumValues[i].Root = value;
                enumValues[i].Seqno = i;
            }
            
            await _context.BeginTransactionAsync();
            if (deletes.Count > 0)
                await _context.DeleteEntitysAsync(TARGET, deletes);

            if (enumValues.Count > 0)
                await _context.SaveEntitysAsync(TARGET, enumValues);
            await _context.CommitTransactionAsync();

            if ((last.Level + 1) < maxCascade)
            {
                // sub enum list
                foreach (EnumValueInfo info in enumValues)
                {
                    (_, int total) = await _context.GetFieldDataAsync<EnumValueInfo>(TARGET, new JsonObject
                    {
                        { nameof(EnumValueInfo.Enum).ToCamelCase(), enumType.Name },
                        { nameof(EnumValueInfo.Root).ToCamelCase(), info.Value }
                    }, take: 1);
                    if (total > 0) info.HasSubList = true;
                }
            }
            return enumValues.ToArray();
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to save enum sub list: {schema} - {value}", enumType.Name, value);
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
                app = ROOT;
            }
            else
            {
                schema = await _context.GetEntityAsync<AppSchema>(TARGET, app);
            }
            if (schema == null) return null;

            // load apps
            List<AppSchema> apps = await _context.GetEntitysAsync<AppSchema>(TARGET, (nameof(AppSchema.Parent), app));
            foreach(AppSchema subApp in apps)
            {
                // check sub apps
                (_, int total) = await _context.GetFieldDataAsync<AppSchema>(TARGET, new JsonObject
                    {
                        { nameof(AppSchema.Parent).ToCamelCase(), subApp.Name }
                    }, take: 1);
                if (total > 0) subApp.HasApps = true;

                // check fields
                (_, total) = await _context.GetFieldDataAsync<AppFieldSchema>(TARGET, new JsonObject
                    {
                        { nameof(AppFieldSchema.App).ToCamelCase(), subApp.Name }
                    }, take: 1);
                if (total > 0) subApp.HasFields = true;
            }
            schema.Apps = apps.Count > 0 ? apps.ToArray() : null;
            
            // load fields
            List<AppFieldSchema> fields = await _context.GetEntitysAsync<AppFieldSchema>(TARGET, (nameof(AppFieldSchema.App), app));
            if (fields.Count > 0)
            {
                fields.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
                schema.Fields = fields.ToArray();
                schema.HasFields = schema.Fields.Length > 0;
            }
            return schema;
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to load app schema: {app}", app);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppSchemaAsync(AppSchema app)
    {
        try
        {
            await _context.BeginTransactionAsync();
            app.Parent = string.Join('.', app.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));
            if (string.IsNullOrEmpty(app.Parent)) app.Parent = ROOT;
            await _context.SaveEntityAsync(TARGET, app);
            await _context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to save app schema: {app}", app.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppSchemaAsync(string app)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(app)) return false;

            await _context.BeginTransactionAsync();
            await _context.DeleteEntityAsync<AppSchema>(TARGET, app);
            await _context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to delete app schema: {app}", app);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAppFieldSchemaAsync(string app, AppFieldSchema field)
    {
        try
        {            
            AppNode? appNode = await _context.GetAppNodeAsync(app);
            if (appNode == null) return false;
            
            AppFieldNode? exist = appNode.GetField(field.Name);
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
            
            await _context.BeginTransactionAsync();
            await _context.SaveEntityAsync(TARGET, field);
            await _context.CommitTransactionAsync();
            
            return true;
        }
        catch (Exception e)
        {
            _context.Logger.LogError(e, "Failed to save app field schema: {app} - {field}", app, field.Name);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAppFieldSchemaAsync(string app, string field)
    {
        try
        {
            List<AppFieldSchema> fields = await _context.GetEntitysAsync<AppFieldSchema>(TARGET, (nameof(AppFieldSchema.App), app));
            int exist = fields.FindIndex(f => f.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
            if (exist < 0) return false;

            await _context.BeginTransactionAsync();
            await _context.DeleteEntityAsync(TARGET, fields[exist]);

            fields = fields.Skip(exist + 1).ToList();
            for(int i = 0; i < fields.Count; i++)
            {
                fields[i].Seqno = exist + i;
            }
            await _context.SaveEntitysAsync(TARGET, fields);

            await _context.CommitTransactionAsync();

            return true;
        }
        catch(Exception ex)
        {
            _context.Logger.LogError(ex, "Failed to delete app field schema: {app} - {field}", app, field);
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

            AppFieldSchema? first = await _context.GetEntityAsync<AppFieldSchema>(TARGET, app, field1);
            AppFieldSchema? second = await _context.GetEntityAsync<AppFieldSchema>(TARGET, app, field2);
            if (first == null || second == null) return false;

            int seq = first.Seqno;
            first.Seqno = second.Seqno;
            second.Seqno = seq;

            await _context.BeginTransactionAsync();
            await _context.SaveEntitysAsync(TARGET, [ first, second ]);
            await _context.CommitTransactionAsync();
            return true;
        }
        catch(Exception e)
        {
            _context.Logger.LogError(e, "Failed to swap app field schema: {app} - {field1} <-> {field2}", app, field1, field2);
            return false;
        }
    }
    
    #endregion

    #region Property
    
    /// <inheritdoc />
    public SchemaLoadState? DefaultLoadState { get; } = SchemaLoadState.Server;

    #endregion

    #region Utility

    readonly SchemaContext _context;
    static readonly string TARGET = Guid.Empty.ToString("D");

    private const string ROOT = "$root";

    #endregion
}