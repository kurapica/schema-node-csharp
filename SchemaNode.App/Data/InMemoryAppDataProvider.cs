using System.Collections.Concurrent;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;

namespace SchemaNode.Data;

/// <summary>
/// The in memory app schema data provider, for unit test only
/// </summary>
public class InMemoryAppDataProvider(IServiceProvider serviceProvider): IAppDataProvider
{
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        return true;
    }

    public async Task<(DataNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, AppSchemaDataResult type,
        AppSchemaDataFilter? filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null,
        string? dataField = null, bool forUpdate = false)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<DataNode>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        List<DataNode> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            List<DataNode> origins = [];

            if (filter == null)
            {
                origins = list;
            }
            else
            {
                foreach (StructNode t in list.Cast<StructNode>())
                {
                    if (filter.Test(t).ToValue<bool>())
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
                        string aKey = (a is StructNode sa ? sa.GetField(t.Field)?.ToString() : null) ?? string.Empty;
                        string bKey = (b is StructNode sb ? sb.GetField(t.Field)?.ToString() : null) ?? string.Empty;
                        int result = String.Compare(aKey, bKey, StringComparison.Ordinal);
                        return t.Desc ? -1 * result : result;
                    });
                }
            }

            return type switch
            {
                AppSchemaDataResult.Count => (SchemaContext.SystemInt.CreateNode(total), total),
                AppSchemaDataResult.Exist => (SchemaContext.SystemBool.CreateNode(total > 0), total),
                AppSchemaDataResult.First => (origins.Count > 0 ? origins[0] : null, total),
                AppSchemaDataResult.Last => (origins.Count > 0 ? origins[^1] : null, total),
                AppSchemaDataResult.Field => (new ArrayTypeNode(((schema.ValueType as ArrayType)!.ElementSchemaType as StructType)!.GetField(dataField!)!.SchemaType!, 
                    origins.Select(o => o is StructNode sn ? sn.GetField(dataField!) : null).Where(x => x != null && !x.IsEmpty).Select(x => x!).ToArray()), total),
                _ => (new ArrayTypeNode(schema.ValueType, origins), total)
            };
        }
        else
        {
            DataNode? origin = list.FirstOrDefault();
            return (origin, origin == null ? 0 : 1);
        }
    }

    public async Task<(bool result, DataNode? update, DataNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, DataNode? data = null, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<DataNode>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        List<DataNode> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            Dictionary<string, int> dict = [];
            for(int i = 0; i < list.Count; i++)
                if (list[i] is StructNode sni && schema.GetPrimaryKey(sni) is { } k) dict[k] = i;
            
            switch (data)
            {
                case ArrayTypeNode arrayTypeNode:
                {
                    List<DataNode> origins = [];
                    List<DataNode> updates = [];
                    foreach (DataNode item in arrayTypeNode)
                    {
                        string? key = schema.GetPrimaryKey((StructNode)item);
                        if (string.IsNullOrEmpty(key)) continue;
                        if (dict.TryGetValue(key, out int index))
                        {
                            if (onlyAdd) continue; // no update
                            DataNode origin = list[index];
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
                    return (true, new ArrayTypeNode(schema.ValueType, updates), new ArrayTypeNode(schema.ValueType, origins));
                }
                case StructNode StructNode:
                {
                    string? key = schema.GetPrimaryKey(StructNode);
                    if (string.IsNullOrEmpty(key)) return (false, null, null);
                    if (dict.TryGetValue(key, out int index))
                    {
                        if (onlyAdd) return (false, null, null); // no update
                        DataNode origin = list[index];
                        list[index] = StructNode;
                        return (true, new ArrayTypeNode(schema.ValueType, StructNode), new ArrayTypeNode(schema.ValueType, origin));
                    }
                    else if (canAdd)
                    {
                        list.Add(StructNode);
                        return (true, new ArrayTypeNode(schema.ValueType, StructNode), null);
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
            DataNode? origin = list.FirstOrDefault();
            list.Clear();
            if (data != null) list.Add(data);
            return (true, data, origin);
        }
    }

    /// <summary>
    /// Clear all dynamic table data
    /// </summary>
    public async Task<(bool result, DataNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<DataNode>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        if (table.TryRemove(compositeKey, out List<DataNode>? list))
        {
            return (true, new ArrayTypeNode(schema.ValueType, list));
        }

        return (false, null);
    }
    
    public async Task<(bool result, DataNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, AppSchemaDataFilter? filter)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema);
        ConcurrentDictionary<string, List<DataNode>> table = _dynamicTables.GetOrAdd(schema.AppField.DynamicTableName, _ => []);
        List<DataNode> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            List<DataNode> origins = [];
            List<DataNode> remains = [];

            foreach (StructNode t in list.Cast<StructNode>())
            {
                if (filter == null || filter.Test(t).ToValue<bool>())
                    origins.Add(t);
                else
                    remains.Add(t);
            }

            table[compositeKey] = remains;
            return (true, new ArrayTypeNode(schema.ValueType, origins));
        }
        else
        {
            DataNode? origin = list.FirstOrDefault();
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

    static ConcurrentDictionary<string, ConcurrentDictionary<string, List<DataNode>>> _dynamicTables = [];

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
        foreach ((string item, DataNode? value) in schema.GetScopeItems(serviceProvider))
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