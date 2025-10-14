using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? field = node?.GetField(nameof(NodeSchema));
            if (field == null) return [];
            
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
                    (AnySchemaNode? value, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                    {
                        { nameof(NodeSchema.Name).ToLower(), name }
                    });
                    
                    if (value is ArrayNode { Count: > 0 } arr && arr[0] is StructNode structNode)
                        schema = structNode.ToValue<NodeSchema>();
                }
                
                if (schema == null) continue;

                if (schema.Type == SchemaType.Namespace)
                {
                    // sub namespace
                    (AnySchemaNode? value, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                    {
                        { nameof(NodeSchema.Namespace).ToLower(), string.IsNullOrEmpty(schema.Name) ? ROOT : schema.Name }
                    });
                    
                    if (value is ArrayNode { Count: > 0 } arr)
                    {
                        List<NodeSchema> subs = new();
                        foreach (AnySchemaNode subNode in arr)
                        {
                            NodeSchema? subSchema = subNode.ToValue<NodeSchema>();
                            if (subSchema == null) continue;
                            
                            subs.Add(subSchema);
                            if (subSchema.Type == SchemaType.Namespace)
                            {
                                (_, int total) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                                {
                                    { nameof(NodeSchema.Namespace).ToLower(), subSchema.Name }
                                }, take: 1);
                                if (total > 0) subSchema.HasSchemas = true;
                            }
                        }
                        schema.Schemas = subs.ToArray();
                    }
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? field = node?.GetField(nameof(NodeSchema));
            if (field == null) return false;

            string root = string.Join('.', schema.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));
            if (string.IsNullOrEmpty(root)) root = ROOT;
            schema.Namespace = root; // add namespace
            
            await _context.BeginTransactionAsync();
            await _context.SaveFieldDataAsync(field, TARGET, field.TypeNode!.CreateNode(schema) ?? throw new NotSupportedException());
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? field = node?.GetField(nameof(NodeSchema));
            if (field == null) return false;

            AnySchemeType? delNode = await _context.GetSchemaNodeAsync(schema);

            await _context.BeginTransactionAsync();
            await _context.DeleteFieldListDataAsync(field, TARGET, new JsonArray([
                new JsonObject
                {
                    { nameof(NodeSchema.Name).ToLower(), schema }
                }
            ]));
            if (delNode?.Type == SchemaType.Enum)
            {
                AppFieldNode? enumField = node?.GetField(nameof(EnumValueInfo));
                if (enumField != null)
                {
                    await _context.DeleteFieldListDataAsync(enumField, TARGET, new JsonArray([
                        new JsonObject
                        {
                            { nameof(EnumValueInfo.Enum).ToLower(), schema }
                        }
                    ]));
                }
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? field = node?.GetField(nameof(EnumValueInfo));
            if (field == null) return [];

            AnySchemeType? eNode = await _context.GetSchemaNodeAsync(schemaName);
            if (eNode is not EnumType enumType) return [];
            int maxCascade = enumType.Cascade?.Length ?? 1;

            EnumValueInfo? last = enumType.Root.GetEnumAccesses(value)?.Last();

            if (last == null)
            {
                (AnySchemaNode? query, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                {
                    { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                    { nameof(EnumValueInfo.Value).ToLower(), value }
                }, take: 1);
                if (query is ArrayNode { Count: > 0 } a && a[0] is StructNode structNode)
                    last = structNode.ToValue<EnumValueInfo>();
            }

            // not existed
            if (last == null || (last.Level + 1) >= maxCascade) return [];
            if (string.IsNullOrEmpty(value)) value = ROOT;

            // load enum values
            (AnySchemaNode? values, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
            {
                { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                { nameof(EnumValueInfo.Root).ToLower(), value }
            });

            List<EnumValueInfo> enumValues = [];
            if (values is ArrayNode { Count: > 0 } arr)
            {
                foreach (AnySchemaNode valNode in arr)
                {
                    EnumValueInfo? info = valNode.ToValue<EnumValueInfo>();
                    if (info != null)
                    {
                        enumValues.Add(info);
                        info.Level = last.Level + 1;
                    }
                }
                enumValues.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
            }

            if ((last.Level + 1) < maxCascade)
            {
                // sub enum list
                foreach (EnumValueInfo info in enumValues)
                {
                    (_, int total) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                    {
                        { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                        { nameof(EnumValueInfo.Root).ToLower(), info.Value }
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? field = node?.GetField(nameof(EnumValueInfo));
            if (field == null) return [];

            AnySchemeType? eNode = await _context.GetSchemaNodeAsync(schemaName);
            if (eNode is not EnumType enumType) return [];

            List<EnumValueAccess> accesses = [];
            string previous = "";
            while (!string.IsNullOrEmpty(value))
            {
                (AnySchemaNode? result, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                {
                    { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                    { nameof(EnumValueInfo.Value).ToLower(), value }
                }, take: 1);
                if (result is ArrayNode { Count: > 0 } arr && arr[0] is StructNode structNode)
                {
                    EnumValueInfo? info = structNode.ToValue<EnumValueInfo>();
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
                else
                    return [];
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? field = node?.GetField(nameof(EnumValueInfo));
            if (field == null) return [];

            int maxCascade = enumType.Cascade?.Length ?? 1;

            EnumValueInfo? last = enumType.Root.GetEnumAccesses(value)?.Last();

            if (last == null)
            {
                (AnySchemaNode? query, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                {
                    { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                    { nameof(EnumValueInfo.Value).ToLower(), value }
                }, take: 1);
                if (query is ArrayNode { Count: > 0 } a && a[0] is StructNode structNode)
                    last = structNode.ToValue<EnumValueInfo>();
            }

            // not existed
            if (last == null || (last.Level + 1) >= maxCascade) return [];
            if (string.IsNullOrEmpty(value)) value = ROOT;

            // load enum values
            (AnySchemaNode? existed, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
            {
                { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                { nameof(EnumValueInfo.Root).ToLower(), value }
            });

            List<EnumValueInfo> enumValues = [];
            if (existed is ArrayNode { Count: > 0 } arr)
            {
                foreach (AnySchemaNode valNode in arr)
                {
                    EnumValueInfo? info = valNode.ToValue<EnumValueInfo>();
                    if (info != null)
                    {
                        enumValues.Add(info);
                        info.Level = last.Level + 1;
                    }
                }
                enumValues.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
            }

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
            {
                JsonArray query = [];
                foreach (EnumValueInfo info in deletes)
                {
                    query.Add(new JsonObject
                    {
                        { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                        { nameof(EnumValueInfo.Value).ToLower(), info.Value },
                    });
                }
                await _context.DeleteFieldListDataAsync(field, TARGET, query);
            }

            if (enumValues.Count > 0)
                await _context.SaveFieldDataAsync(field, TARGET, field.TypeNode!.CreateNode(enumValues) ?? throw new NotSupportedException());
            await _context.CommitTransactionAsync();

            if ((last.Level + 1) < maxCascade)
            {
                // sub enum list
                foreach (EnumValueInfo info in enumValues)
                {
                    (_, int total) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                    {
                        { nameof(EnumValueInfo.Enum).ToLower(), enumType.Name },
                        { nameof(EnumValueInfo.Root).ToLower(), info.Value }
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? appField = node?.GetField(nameof(AppSchema));
            AppFieldNode? field = node?.GetField(nameof(AppFieldSchema));
            if (appField == null || field == null) return null;

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
                (AnySchemaNode? value, _) = await _context.GetFieldDataAsync(appField, TARGET, new JsonObject
                {
                    { nameof(AppSchema.Name).ToLower(), app }
                }, take: 1);

                schema = (value is ArrayNode { Count: > 0 } arr && arr[0] is StructNode structNode)
                    ? structNode.ToValue<AppSchema>()
                    : null;
            }
            if (schema == null) return null;
            
            // load apps
            (AnySchemaNode? apps, _) = await _context.GetFieldDataAsync(appField, TARGET, new JsonObject
            {
                { nameof(AppSchema.Parent).ToLower(), app }
            });
            if (apps is ArrayNode { Count: > 0 } arrApps)
            {
                foreach (AnySchemaNode n in arrApps)
                {
                    AppSchema? a = n.ToValue<AppSchema>();
                    if (a != null)
                    {
                        schema.Apps ??= [];
                        schema.Apps = schema.Apps.Append(a).ToArray();
                        
                        // check sub apps
                        (_, int total) = await _context.GetFieldDataAsync(appField, TARGET, new JsonObject
                        {
                            { nameof(AppSchema.Parent).ToLower(), a.Name }
                        }, take: 1);
                        if (total > 0) a.HasApps = true;
                        
                        // check fields
                        (_, total) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
                        {
                            { nameof(AppFieldSchema.App).ToLower(), a.Name }
                        }, take: 1);
                        if (total > 0) a.HasFields = true;
                    }
                }
            }
            
            // load fields
            (AnySchemaNode? fields, _) = await _context.GetFieldDataAsync(field, TARGET, new JsonObject
            {
                { nameof(AppFieldSchema.App).ToLower(), app }
            });
            if (fields is ArrayNode { Count: > 0 } arrFields)
            {
                List<AppFieldSchema> fieldList = [];
                foreach (AnySchemaNode n in arrFields)
                {
                    AppFieldSchema? f = n.ToValue<AppFieldSchema>();
                    if (f != null) fieldList.Add(f);
                }
                fieldList.Sort((a, b) => a.Seqno.CompareTo(b.Seqno));
                schema.Fields = fieldList.ToArray();
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? appField = node?.GetField(nameof(AppSchema));
            if (appField == null) return false;

            await _context.BeginTransactionAsync();
            app.Parent = string.Join('.', app.Name.Split('.', StringSplitOptions.RemoveEmptyEntries).SkipLast(1));
            if (string.IsNullOrEmpty(app.Parent)) app.Parent = ROOT;
            await _context.SaveFieldDataAsync(appField, TARGET, appField.TypeNode!.CreateNode(app) ?? throw new NotSupportedException());
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
            
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? appField = node?.GetField(nameof(AppSchema));
            if (appField == null) return false;

            await _context.BeginTransactionAsync();
            await _context.DeleteFieldListDataAsync(appField, TARGET, [
                new JsonObject
                {
                    { nameof(AppSchema.Name).ToLower(), app }
                }
            ]);
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
            AppNode? node = await _context.GetAppNodeAsync(NS_SYSTEM_SCHEMA);
            AppFieldNode? appField = node?.GetField(nameof(AppFieldSchema));
            if (appField == null) return false;
            
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
            await _context.SaveFieldDataAsync(appField, TARGET, appField.TypeNode!.CreateNode(field) ?? throw new NotSupportedException());
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
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<bool> SwapAppFieldSchemaAsync(string app, string field1, string field2)
    {
        throw new NotImplementedException();
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