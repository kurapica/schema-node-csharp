using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Node;

namespace SchemaNode.Components;

/// <summary>
/// The in memory app schema data provider, for unit test only
/// </summary>
public class InMemoryAppDataProvider: IAppDataProvider
{
    public async Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        return true;
    }

    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, JsonNode? filter = null, int skip = 0,
        int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        await Task.Yield();
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.Name, _ => []);
        List<JsonNode> list = table.GetOrAdd(target, _ => []);
        
        if (!schema.Single)
        {
            List<JsonNode> origins = [];
            switch (filter)
            {
                case JsonArray arrayTypeNode:
                {
                    HashSet<string> query = [];
                    foreach (var item in arrayTypeNode)
                    {
                        if (item is not JsonObject jsonObject) continue;
                        string? key = schema.GetPrimaryKey(jsonObject);
                        if (string.IsNullOrEmpty(key)) continue;
                        query.Add(key);
                    }
                    if (query.Count == 0) return (null, 0);

                    foreach (JsonNode t in list)
                    {
                        string key =  schema.GetPrimaryKey((JsonObject)t)!;
                        if (query.Contains(key))
                            origins.Add(t);
                    }
                    break;
                }
                case JsonObject jsonObject:
                {
                    Dictionary<string, string> query = [];
                    foreach ((string field, AnySchemaNode? value) in schema.GetFieldValues(jsonObject))
                    {
                        if (value != null && !value.IsEmpty)
                            query[field] = value.ToString();
                    }
                    
                    // clear by filter
                    foreach (JsonNode t in list)
                    {
                        bool match = true;
                        foreach ((string p, string v) in query)
                        {
                            if (((JsonObject)t)[p]!.ToString() != v)
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match) origins.Add(t);
                    }

                    break;
                }
                default:
                    return (null, 0);
            }

            int total = origins.Count;
            if (skip > 0) origins = origins.Skip(skip).ToList();
            if (take > 0) origins = origins.Take(take).ToList();
            if (orderBy != null)
            {
                foreach (AppSchemaDataOrder t in orderBy)
                {
                    origins.Sort((a, b) =>
                    {
                        string aKey = ((JsonObject)a)[t.Field]!.GetValue<string>();
                        string bKey = ((JsonObject)b)[t.Field]!.GetValue<string>();
                        int result = String.Compare(aKey, bKey, StringComparison.Ordinal);
                        return t.Desc ? -1 * result : result;
                    });
                }
            }
            return (new ArrayTypeNode(schema.SchemaType, origins), total);
        }
        else
        {
            JsonNode? origin = list.FirstOrDefault();
            return (schema.SchemaType.CreateNode(origin),origin == null ? 0 : 1);
        }
    }

    public Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, ExpNode filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, bool forUpdate = false)
    {
        throw new NotImplementedException();
    }

    public async Task<(bool result, AnySchemaNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, string target, AnySchemaNode? data = null)
    {
        await Task.Yield();
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.Name, _ => []);
        List<JsonNode> list = table.GetOrAdd(target, _ => []);

        if (!schema.Single)
        {
            Dictionary<string, int> dict = [];
            for(int i = 0; i < list.Count; i++)
                dict[schema.GetPrimaryKey((JsonObject)list[i])!] = i;
            
            switch (data)
            {
                case ArrayTypeNode arrayTypeNode:
                {
                    List<JsonNode> origins = [];
                    foreach (AnySchemaNode item in arrayTypeNode)
                    {
                        string? key = schema.GetPrimaryKey((StructTypeNode)item);
                        if (string.IsNullOrEmpty(key)) continue;
                        if (dict.TryGetValue(key, out int index))
                        {
                            JsonNode origin = list[index];
                            list[index] = item.ToJson()!;
                            origins.Add(origin);
                        }
                        else
                        {
                            list.Add(item.ToJson()!);
                        }
                    }
                    return (true, new ArrayTypeNode(schema.SchemaType, origins));
                }
                case StructTypeNode structTypeNode:
                {
                    string? key = schema.GetPrimaryKey(structTypeNode);
                    if (string.IsNullOrEmpty(key)) return (false, null);
                    if (dict.TryGetValue(key, out int index))
                    {
                        JsonNode origin = list[index];
                        list[index] = structTypeNode.ToJson()!;
                        return (true, new ArrayTypeNode(schema.SchemaType, origin));
                    }
                    else
                    {
                        list.Add(structTypeNode.ToJson()!);
                        return (true, null);
                    }
                }
                default:
                    return (false, null);
            }
        }
        else
        {
            JsonNode? origin = list.FirstOrDefault();
            list.Clear();
            if (data != null) list.Add(data.ToJson()!);
            return (true, schema.SchemaType.CreateNode(origin));
        }
    }

    public async Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, string target, JsonNode? filter = null)
    {
        await Task.Yield();
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.Name, _ => []);
        List<JsonNode> list = table.GetOrAdd(target, _ => []);

        if (!schema.Single)
        {
            switch (filter)
            {
                case JsonArray arrayTypeNode:
                {
                    List<JsonNode> origins = [];
                    HashSet<string> deletes = [];
                    foreach (var item in arrayTypeNode)
                    {
                        if (item is not JsonObject jsonObject) continue;
                        string? key = schema.GetPrimaryKey(jsonObject);
                        if (string.IsNullOrEmpty(key)) continue;
                        deletes.Add(key);
                    }

                    if (deletes.Count == 0) return (false, null);

                    List<JsonNode> remain = [];
                    foreach (JsonNode t in list)
                    {
                        string key =  schema.GetPrimaryKey((JsonObject)t)!;
                        if (deletes.Contains(key))
                        {
                            origins.Add(t);
                        }
                        else
                        {
                            remain.Add(t);
                        }
                    }
                    
                    table[target] = remain;
                    
                    return (true, new ArrayTypeNode(schema.SchemaType, origins));
                }
                case JsonObject jsonObject:
                {
                    Dictionary<string, string> query = [];
                    foreach ((string field, AnySchemaNode? value) in schema.GetFieldValues(jsonObject, true))
                    {
                        if (value != null && !value.IsEmpty)
                            query[field] = value.ToString();
                    }

                    // all clear
                    if (query.Count == 0)
                    {
                        if (schema.IncrUpdate) return (false, null);
                        
                        table[target] = [];
                        return (true,  new ArrayTypeNode(schema.SchemaType, list));
                    }
                    
                    // clear by filter
                    List<JsonNode> remains = [];
                    List<JsonNode> origins = [];
                    foreach (JsonNode t in list)
                    {
                        bool match = true;
                        foreach ((string p, string v) in query)
                        {
                            if (((JsonObject)t[p]!).ToString() != v)
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            origins.Add(t);
                        }
                        else
                        {
                            remains.Add(t);
                        }
                    }

                    table[target] = remains;
                    return (true, new ArrayTypeNode(schema.SchemaType, origins));
                }
                default:
                    return (false, null);
            }
        }
        else
        {
            JsonNode? origin = list.FirstOrDefault();
            list.Clear();
            return (true, schema.SchemaType.CreateNode(origin));
        }
    }
    
    public async Task DropDynamicTableAsync(string dynamicTableName)
    {
        await Task.Yield();
        _dynamicTables.TryRemove(dynamicTableName, out _);
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
        throw new NotImplementedException();
    }

    #region Utility

    static ConcurrentDictionary<string, ConcurrentDictionary<string, List<JsonNode>>> _dynamicTables = [];

    #endregion
}