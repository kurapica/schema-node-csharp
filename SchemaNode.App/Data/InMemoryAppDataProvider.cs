using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Data;

/// <summary>
/// The in memory app schema data provider, for unit test only
/// </summary>
public class InMemoryAppDataProvider(ISchemaContext context): IAppDataProvider
{
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        return true;
    }

    public async Task<(IValueAccess? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, AppSchemaDataResult type,
        AppSchemaDataFilter? filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null,
        string? dataField = null, bool forUpdate = false)
    {
        SchemaContext schemaContext = context as  SchemaContext ?? throw new ArgumentNullException(nameof(context));
        
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<IValueAccess>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        List<IValueAccess> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            List<IValueAccess> origins = [];

            if (filter == null)
            {
                origins = list;
            }
            else
            {
                foreach (StructNode t in list.OfType<StructNode>())
                {
                    if (filter.Test(schemaContext, t).GetValue<bool>())
                        origins.Add(t);
                }
            }

            int total = origins.Count;

            if (take > 0)
            {
                if (skip > 0) origins = origins.Skip(skip).ToList();
                origins = origins.Take(take).ToList();
            }
            if (orderBy != null)
            {
                foreach (AppSchemaDataOrder t in orderBy)
                {
                    origins.Sort((a, b) =>
                    {
                        string aKey = (a is StructNode sa ? sa.GetAccessValue(t.Field)?.ToString() : null) ?? string.Empty;
                        string bKey = (b is StructNode sb ? sb.GetAccessValue(t.Field)?.ToString() : null) ?? string.Empty;
                        int result = String.Compare(aKey, bKey, StringComparison.Ordinal);
                        return t.Desc ? -1 * result : result;
                    });
                }
            }

            return type switch
            {
                AppSchemaDataResult.Count => (schemaContext.System.Int.From(total), total),
                AppSchemaDataResult.Exist => (schemaContext.System.Bool.From(total > 0), total),
                AppSchemaDataResult.First => (origins.Count > 0 ? origins[0].Clone() : null, total),
                AppSchemaDataResult.Last => (origins.Count > 0 ? origins[^1].Clone() : null, total),
                AppSchemaDataResult.Field => (new ArrayNode(((schema.ValueType as ArrayType)!.Element as StructType)!.GetField(dataField!)!.Type!, 
                    origins.Select(o => o is StructNode sn ? sn.GetAccessValue(dataField!) : null).Where(x => x is { IsEmpty: false }).Select(x => x!).ToArray()), total),
                _ => (new ArrayNode(schema.ValueType, origins.Select(o => o.Clone()).ToArray()), total)
            };
        }
        else
        {
            IValueAccess? origin = list.FirstOrDefault();
            return (origin?.Clone(), origin == null ? 0 : 1);
        }
    }

    public async Task<(bool result, IValueAccess? update, IValueAccess? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, IValueAccess? data = null, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<IValueAccess>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        List<IValueAccess> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            Dictionary<string, int> dict = [];
            for(int i = 0; i < list.Count; i++)
                if (list[i] is StructNode sni && schema.GetPrimaryKey(sni) is { } k) dict[k] = i;
            
            switch (data)
            {
                case ArrayNode arrayNode:
                {
                    List<IValueAccess> origins = [];
                    List<IValueAccess> updates = [];
                    foreach (IValueAccess item in arrayNode)
                    {
                        string? key = schema.GetPrimaryKey((StructNode)item);
                        if (string.IsNullOrEmpty(key)) continue;
                        if (dict.TryGetValue(key, out int index))
                        {
                            if (onlyAdd) continue; // no update
                            IValueAccess origin = list[index];
                            list[index] = item;
                            origins.Add(origin);
                            updates.Add(item);
                        }
                        else if (canAdd)
                        {
                            list.Add(item);
                            updates.Add(item);
                        }
                        else
                        {
                            throw new UnauthorizedAccessException();
                        }
                    }
                    return (true, new ArrayNode(schema.ValueType, updates), new ArrayNode(schema.ValueType, origins));
                }
                case StructNode structNode:
                {
                    string? key = schema.GetPrimaryKey(structNode);
                    if (string.IsNullOrEmpty(key)) return (false, null, null);
                    if (dict.TryGetValue(key, out int index))
                    {
                        if (onlyAdd) return (false, null, null); // no update
                        IValueAccess origin = list[index];
                        list[index] = structNode;
                        return (true, new ArrayNode(schema.ValueType, structNode), new ArrayNode(schema.ValueType, origin));
                    }
                    else if (canAdd)
                    {
                        list.Add(structNode);
                        return (true, new ArrayNode(schema.ValueType, structNode), null);
                    }
                    else
                    {
                        throw new UnauthorizedAccessException();
                    }
                }
                default:
                    return (false, null, null);
            }
        }
        else
        {
            IValueAccess? origin = list.FirstOrDefault();
            list.Clear();
            if (data != null) list.Add(data);
            return (true, data, origin);
        }
    }

    /// <summary>
    /// Clear all dynamic table data
    /// </summary>
    public async Task<(bool result, IValueAccess? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<IValueAccess>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        if (table.TryRemove(compositeKey, out List<IValueAccess>? list))
        {
            return (true, new ArrayNode(schema.ValueType, list));
        }

        return (false, null);
    }
    
    public async Task<(bool result, IValueAccess? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, AppSchemaDataFilter? filter)
    {
        SchemaContext schemaContext = context as  SchemaContext ?? throw new ArgumentNullException(nameof(context));
        
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<IValueAccess>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        List<IValueAccess> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            List<IValueAccess> origins = [];
            List<IValueAccess> remains = [];

            foreach (StructNode t in list.OfType<StructNode>())
            {
                if (filter == null || filter.Test(schemaContext, t).GetValue<bool>())
                    origins.Add(t);
                else
                    remains.Add(t);
            }

            table[compositeKey] = remains;
            return (true, new ArrayNode(schema.ValueType, origins));
        }
        else
        {
            IValueAccess? origin = list.FirstOrDefault();
            list.Clear();
            return (true, origin);
        }
    }
    
    public async Task DropDynamicTableAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        _dynamicTables.TryRemove(schema.AppField.DynamicTableName, out _);
    }

    public Task BeginTransactionAsync()
    {
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync()
    {
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync()
    {
        return Task.CompletedTask;
    }

    #region Utility

    static ConcurrentDictionary<string, ConcurrentDictionary<string, List<IValueAccess>>> _dynamicTables = [];

    /// <summary>
    /// Clears all in-memory data; call between unit tests to ensure isolation.
    /// </summary>
    public static void Reset() => _dynamicTables.Clear();

    /// <summary>
    /// Prepare the composite key from scope and target fields
    /// </summary>
    private string PrepareKey(DynamicTableSchema schema)
    {
        List<string> keyParts = [];
        
        // Add scope items
        foreach ((string item, IValueAccess? value) in schema.GetScopeItems(context))
        {
            if (value == null || value.IsEmpty)
                throw new InvalidOperationException($"The scope field {item} is required for querying dynamic table data.");
            keyParts.Add($"{item}:{value}");
        }
        
        // If no scope and no target, use a default key
        return keyParts.Count > 0 ? string.Join("|", keyParts) : "_default";
    }
        
    #endregion
}