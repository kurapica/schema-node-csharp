using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;

namespace SchemaNode.Components;

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

    public async Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, AppSchemaDataResult type,
        AppSchemaDataFilter? filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null,
        string? dataField = null, bool forUpdate = false)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema, target);
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.AppFieldType.DynamicTableName, _ => []);
        List<JsonNode> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            List<JsonNode> origins = [];
            
            if (filter == null)
            {
                origins = list;
            }
            else
            {
                foreach (JsonNode t in list)
                {
                    if (t is JsonObject record && Match(filter, record))
                        origins.Add(t);
                }
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

            return type switch
            {
                AppSchemaDataResult.Count => (SchemaContext.SystemInt.CreateNode(total), total),
                AppSchemaDataResult.Exist => (SchemaContext.SystemBool.CreateNode(total > 0), total),
                AppSchemaDataResult.First => (origins.Count > 0 ? schema.SchemaType.CreateNode(origins[0]) : null, total),
                AppSchemaDataResult.Last => (origins.Count > 0 ? schema.SchemaType.CreateNode(origins[^1]) : null, total),
                AppSchemaDataResult.Field => (new ArrayTypeNode(((schema.SchemaType as ArrayType)!.ElementSchemaType as StructType)!.GetField(dataField!)!.SchemeType!, 
                    origins.Select(o => ((JsonObject)o)[dataField!]).Where(x => x != null).Select(x => x!.DeepClone()).ToArray()), total),
                _ => (new ArrayTypeNode(schema.SchemaType, origins), total)
            };
        }
        else
        {
            JsonNode? origin = list.FirstOrDefault();
            return (schema.SchemaType.CreateNode(origin), origin == null ? 0 : 1);
        }
    }

    public async Task<(bool result, AnySchemaNode? update, AnySchemaNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, string target, AnySchemaNode? data = null, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema, target);
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.AppFieldType.DynamicTableName, _ => []);
        List<JsonNode> list = table.GetOrAdd(compositeKey, _ => []);

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
                    List<AnySchemaNode> updates = [];
                    foreach (AnySchemaNode item in arrayTypeNode)
                    {
                        string? key = schema.GetPrimaryKey((StructTypeNode)item);
                        if (string.IsNullOrEmpty(key)) continue;
                        if (dict.TryGetValue(key, out int index))
                        {
                            if (onlyAdd) continue; // no update
                            JsonNode origin = list[index];
                            list[index] = item.ToJson()!;
                            origins.Add(origin);
                            updates.Add(item);
                        }
                        else if (canAdd)
                        {
                            list.Add(item.ToJson()!);
                            updates.Add(item);
                        }
                        else
                        {
                            throw new UnauthorizedAccessException();
                        }
                    }
                    return (true, new ArrayTypeNode(schema.SchemaType, updates), new ArrayTypeNode(schema.SchemaType, origins));
                }
                case StructTypeNode structTypeNode:
                {
                    string? key = schema.GetPrimaryKey(structTypeNode);
                    if (string.IsNullOrEmpty(key)) return (false, null, null);
                    if (dict.TryGetValue(key, out int index))
                    {
                        if (onlyAdd) return (false, null, null); // no update
                        JsonNode origin = list[index];
                        list[index] = structTypeNode.ToJson()!;
                        return (true, new ArrayTypeNode(schema.SchemaType, structTypeNode), new ArrayTypeNode(schema.SchemaType, origin));
                    }
                    else if (canAdd)
                    {
                        list.Add(structTypeNode.ToJson()!);
                        return (true, new ArrayTypeNode(schema.SchemaType, structTypeNode), null);
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
            JsonNode? origin = list.FirstOrDefault();
            list.Clear();
            if (data != null) list.Add(data.ToJson()!);
            return (true, data, schema.SchemaType.CreateNode(origin));
        }
    }

    /// <summary>
    /// Clear all dynamic table data
    /// </summary>
    public async Task<(bool result, AnySchemaNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema, string target)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema, target);
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.AppFieldType.DynamicTableName, _ => []);
        if (table.TryRemove(compositeKey, out List<JsonNode>? list))
        {
            return (true, new ArrayTypeNode(schema.SchemaType, list));
        }

        return (false, null);
    }
    
    public async Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, string target, AppSchemaDataFilter filter)
    {
        await Task.Yield();
        string compositeKey = PrepareKey(schema, target);
        ConcurrentDictionary<string, List<JsonNode>> table = _dynamicTables.GetOrAdd(schema.AppFieldType.DynamicTableName, _ => []);
        List<JsonNode> list = table.GetOrAdd(compositeKey, _ => []);

        if (!schema.Single)
        {
            List<JsonNode> origins = [];
            List<JsonNode> remains = [];

            foreach (JsonNode t in list)
            {
                if (t is JsonObject record && Match(filter, record))
                    origins.Add(t);
                else
                    remains.Add(t);
            }

            table[compositeKey] = remains;
            return (true, new ArrayTypeNode(schema.SchemaType, origins));
        }
        else
        {
            JsonNode? origin = list.FirstOrDefault();
            list.Clear();
            return (true, schema.SchemaType.CreateNode(origin));
        }
    }
    
    public async Task DropDynamicTableAsync(DynamicTableSchema schema)
    {
        await Task.Yield();
        _dynamicTables.TryRemove(schema.AppFieldType.DynamicTableName, out _);
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

    static ConcurrentDictionary<string, ConcurrentDictionary<string, List<JsonNode>>> _dynamicTables = [];

    /// <summary>
    /// Prepare the composite key from scope and target fields
    /// </summary>
    private string PrepareKey(DynamicTableSchema schema, string target)
    {
        List<string> keyParts = [];
        
        // Add scope items
        if (schema.Fields.Any(f => f.Scope))
        {
            foreach ((string item, AnySchemaNode? value) in schema.GetScopeItems(serviceProvider))
            {
                if (value == null || value.IsEmpty)
                    throw new InvalidOperationException($"The scope field {item} is required for querying dynamic table data.");
                keyParts.Add($"{item}:{value}");
            }
        }
        
        // Add target field
        var tarField = schema.Fields.FirstOrDefault(f => f.Target);
        if (tarField != null)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException($"The target field {tarField.Name} is required for querying dynamic table data.");
            keyParts.Add($"{tarField.Name}:{target}");
        }
        
        // If no scope and no target, use a default key
        return keyParts.Count > 0 ? string.Join("|", keyParts) : "_default";
    }

    private bool Match(AppSchemaDataFilter filter, JsonObject record)
    {
        switch (filter)
        {
            case AppSchemaDataFilterValue v:
                // Filter value should not appear alone as partial evaluation result, but if it does, check its truthiness? 
                // In binary ops it's used as operand. 
                // If it appears here, it might be a constant true/false filter.
                return v.Value switch
                {
                    bool boolVal => boolVal,
                    AnySchemaNode n => !n.IsEmpty,
                    _ => true
                };

            case AppSchemaDataFilterField f:
                // Check if field exists and is "true" (not null/empty/false)
                var fieldVal = record[f.Field];
                if (fieldVal == null) return false;
                if (fieldVal is JsonValue jv && jv.TryGetValue(out bool boolVal2)) return boolVal2;
                return true;

            case AppSchemaDataFilterUnary u:
                return MatchUnary(u, record);

            case AppSchemaDataFilterBinary binaryVal:
                return MatchBinary(binaryVal, record);
                
            default:
                return false;
        }
    }

    private bool MatchUnary(AppSchemaDataFilterUnary unary, JsonObject record)
    {
        // For unary, we typically check operands. But wait, IsNull/IsEmpty applies to a field usually represented by Operand.
        // If operand is a field, we get its value. If it's a value, we check it directly.
        
        JsonNode? val = GetValue(unary.Operand, record);
        
        switch (unary.Type)
        {
            case LogicType.Not:
                return !Match(unary.Operand, record);
            case LogicType.IsNull:
                return val == null;
            case LogicType.NotNull:
                return val != null;
            case LogicType.IsEmpty:
                return val == null || val.ToString() == string.Empty; // Simplified empty check
            case LogicType.NotEmpty:
                return val != null && val.ToString() != string.Empty;
            default:
                return false;
        }
    }

    private bool MatchBinary(AppSchemaDataFilterBinary binary, JsonObject record)
    {
        if (binary.Type == LogicType.AndAlso)
        {
            if (binary.Left is AppSchemaDataFilterValue { Value: bool leftBool })
                return leftBool && Match(binary.Right, record);
            if (binary.Right is AppSchemaDataFilterValue { Value: bool rightBool })
                return Match(binary.Left, record) && rightBool;
                
            return Match(binary.Left, record) && Match(binary.Right, record);
        }

        if (binary.Type == LogicType.OrElse)
        {
            if (binary.Left is AppSchemaDataFilterValue { Value: bool leftBool })
                return leftBool || Match(binary.Right, record);
            if (binary.Right is AppSchemaDataFilterValue { Value: bool rightBool })
                return Match(binary.Left, record) || rightBool;

            return Match(binary.Left, record) || Match(binary.Right, record);
        }

        JsonNode? left = GetValue(binary.Left, record);
        
        // optimize for contains
        if (binary.Type == LogicType.Contains)
        {
             // Left should be the list (value), Right is the field (or value)
             // But wait, standard is: Collection.Contains(Item). 
             // In Filter: Left is Collection, Right is Item.
             // Usually Left is AppSchemaDataFilterValue (the list of ids), Right is AppSchemaDataFilterField (the record's id).
             
             if (binary.Left is AppSchemaDataFilterValue leftVal && leftVal.Value is ArrayTypeNode)
             {
                 // If left was Value, GetValue returns it. If left is field, it returns record data.
                 // Actually GetValue(binary.Left, record) already resolved Left.
                 
                 // If binary.Left is the collection value:
                 // GetValue will return the collection as JsonNode (or AnySchemaNode wrapped). 
                 // But GetValue implementation below returns JsonNode.
                 
                 // Let's rely on binary members.
                 
                 // Check if it is "List Contains Field" pattern
                 IEnumerable<JsonNode?>? list = null;
                 if (left is JsonArray arr) list = arr;
                 else if (binary.Left is AppSchemaDataFilterValue v && v.Value is ArrayTypeNode atn) 
                     list = atn.Select(x => x.ToJson());
                 
                 if (list != null)
                 {
                     string? itemVal = GetValue(binary.Right, record)?.ToString();
                     return list.Any(x => x?.ToString() == itemVal);
                 }
             }
        }
        
        // Resolve right value for other ops
        JsonNode? right = GetValue(binary.Right, record);
        
        string? lStr = left?.ToString();
        string? rStr = right?.ToString();

        switch (binary.Type)
        {
            case LogicType.Equal:
                return lStr == rStr;
            case LogicType.NotEqual:
                return lStr != rStr;
            case LogicType.GreaterThan:
                return string.CompareOrdinal(lStr, rStr) > 0; // Simplified string compare
            case LogicType.GreaterEqual:
                return string.CompareOrdinal(lStr, rStr) >= 0;
            case LogicType.LessThan:
                return string.CompareOrdinal(lStr, rStr) < 0;
            case LogicType.LessEqual:
                return string.CompareOrdinal(lStr, rStr) <= 0;
            case LogicType.Contains: // String contains
                return lStr != null && rStr != null && lStr.Contains(rStr);
            case LogicType.NotContains:
                return lStr != null && rStr != null && !lStr.Contains(rStr);
            case LogicType.StartsWith:
                return lStr != null && rStr != null && lStr.StartsWith(rStr);
            case LogicType.EndsWith:
                return lStr != null && rStr != null && lStr.EndsWith(rStr);
            case LogicType.Match: // Text match
                return lStr != null && rStr != null && lStr.Contains(rStr);
            default:
                return false;
        }
    }

    private JsonNode? GetValue(AppSchemaDataFilter filter, JsonObject record)
    {
        return filter switch
        {
            AppSchemaDataFilterValue v => v.Value switch
            {
                AnySchemaNode n => n.ToJson(),
                JsonValue j => JsonValue.Create(j.ToValue<string>()),
                string s => JsonValue.Create(s),
                _ => JsonValue.Create(v.Value)
            },
            AppSchemaDataFilterField f => record[f.Field],
            _ => null // Complex expr as value? Not supported in simple evaluator
        };
    }
    
    #endregion
}