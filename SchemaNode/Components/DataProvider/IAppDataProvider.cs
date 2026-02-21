using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Components;

/// <summary>
/// The application data storage provider
/// </summary>
public interface IAppDataProvider
{
    /// <summary>
    /// Add or update the data table with the dynamic table schema
    /// </summary>
    Task<bool> EnsureDynamicTableAsync(DynamicTableSchema schema);
    
    /// <summary>
    /// Query dynamic table data with the filter and paging
    /// </summary>
    Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, 
        AppSchemaDataResult type, AppSchemaDataFilter? filter = null, int skip = 0, int take = 0, 
        bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null, bool forUpdate = false);
    
    /// <summary>
    /// Save the dynamic table data
    /// </summary>
    Task<(bool result, AnySchemaNode? update, AnySchemaNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, 
        AnySchemaNode? data = null, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null);

    /// <summary>
    /// Delete the dynamic table data with the filter
    /// </summary>
    Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, AppSchemaDataFilter filter);
    
    /// <summary>
    /// Clear all dynamic table data
    /// </summary>
    Task<(bool result, AnySchemaNode? origin)> ClearDynamicTableDataAsync(DynamicTableSchema schema);
    
    /// <summary>
    /// Drop the dynamic table
    /// </summary>
    Task DropDynamicTableAsync(DynamicTableSchema schema);
    
    /// <summary>
    /// Begin a transaction
    /// </summary>
    Task BeginTransactionAsync();
    
    /// <summary>
    /// Commit the current transaction
    /// </summary>
    /// <returns></returns>
    Task CommitTransactionAsync();
    
    /// <summary>
    /// Roll back the current transaction
    /// </summary>
    Task RollbackTransactionAsync();
}

/// <summary>
/// The extension methods for IAppDataProvider to support batch querying and deleting with combined filters
/// </summary>
public static class AppDataProviderExtension
{
    /// <summary>
    /// Gets the data with the given nodes
    /// </summary>
    public static async Task<AnySchemaNode?> QueryOriginNodesAsync(this IAppDataProvider dataProvider, DynamicTableSchema schema, IEnumerable<StructTypeNode> nodes, bool forUpdate = false)
    {
        if (schema.Single) return null; // not supported for single record tables
        
        StructTypeNode[] queryNodes = nodes.ToArray();
        ArrayType arrType = schema.AppFieldType.SchemaType as ArrayType ?? throw new InvalidOperationException("Invalid array type in schema");
        StructType structType = ((schema.AppFieldType.SchemaType as ArrayType)!.ElementSchemaType as StructType)
                                ?? throw new InvalidOperationException("Invalid struct type in array type");

        if (queryNodes.Length > MAX_COMBINE_CASE_COUNT)
        {
            // 1. Collect keys and prepare IN lists
            HashSet<string> keys = [];
            Dictionary<string, ArrayTypeNode> keyData = arrType.Primary!.ToDictionary(k => k, k => new ArrayTypeNode(structType.GetField(k)!.SchemeType!));

            foreach (StructTypeNode node in queryNodes)
            {
                string? key = arrType.GetPrimaryKey(node);
                if (string.IsNullOrEmpty(key) || !keys.Add(key)) continue;

                foreach (string pk in arrType.Primary!)
                    keyData[pk].Add(node.GetField(pk)!);
            }
            if (keys.Count == 0) return null;

            // 2. Build filter (PK1 in (...) AND PK2 in (...))
            AppSchemaDataFilter? filter = null;
            foreach ((string pk, ArrayTypeNode list) in keyData)
            {
                var subFilter = new AppSchemaDataFilterBinary(LogicType.Contains,
                    new AppSchemaDataFilterValue(list),
                    new AppSchemaDataFilterField(pk));
                filter = filter == null ? subFilter : filter.AndAlso(subFilter);
            }

            // 3. Query
            (AnySchemaNode? res, _) = await dataProvider.QueryDynamicTableAsync(schema,AppSchemaDataResult.List, filter, forUpdate: forUpdate);

            // 4. Filter results by combined PKs
            if (res is ArrayTypeNode resultArray)
            {
                // Filter in memory
                var filtered = new ArrayTypeNode(structType);
                foreach (StructTypeNode node in resultArray.Cast<StructTypeNode>())
                {
                    string? key = arrType.GetPrimaryKey(node);
                    if (!string.IsNullOrEmpty(key) && keys.Contains(key))
                    {
                        filtered.Add(node);
                    }
                }
                return filtered;
            }
            return null;
        }
        else
        {
            HashSet<string> keys = [];
            AppSchemaDataFilter? filter = null;
            foreach (StructTypeNode node in queryNodes)
            {
                string? key = arrType.GetPrimaryKey(node);
                if (string.IsNullOrEmpty(key) || !keys.Add(key)) continue;
                var caseFilter = node.GetQueryFilter(arrType);
                if (caseFilter != null)
                    filter = filter == null ? caseFilter : filter.OrElse(caseFilter);
            }
            
            (AnySchemaNode? res, _) = await dataProvider.QueryDynamicTableAsync(schema,AppSchemaDataResult.List, filter, forUpdate: forUpdate);
            return res;
        }
    }
    
    /// <summary>
    /// Delete the dynamic table data with the filter
    /// </summary>
    public static async Task<(bool result, AnySchemaNode? origin)> DeleteSchemaNodeAsync(this IAppDataProvider dataProvider, DynamicTableSchema schema, string target, AnySchemaNode node)
    {
        if (schema.Single)
            return await dataProvider.ClearDynamicTableDataAsync(schema);

        var arrayType = schema.AppFieldType.SchemaType as ArrayType ?? throw new InvalidOperationException("Invalid array schema");
        
        if (node is StructTypeNode structNode)
        {
            var filter = structNode.GetQueryFilter(arrayType);
            if (filter == null) return (false, null);
            return await dataProvider.DeleteDynamicTableDataAsync(schema, filter);
        }
        else if (node is ArrayTypeNode arrNode)
        {
            if (arrNode.ElementType != arrayType.ElementSchemaType)
                throw new InvalidOperationException("Invalid element type in array node for deletion");

            var count = 0;
            var origin = new ArrayTypeNode(arrNode.ElementType as StructType ?? throw new InvalidOperationException("Invalid element type"));
            for (int i = 0; i < arrNode.Count; i += MAX_COMBINE_CASE_COUNT)
            {
                var batch = arrNode.Skip(i).Take(MAX_COMBINE_CASE_COUNT).Cast<StructTypeNode>().ToArray();
                AppSchemaDataFilter? filter = null;
                foreach (var item in batch)
                {
                    var itemFilter = item.GetQueryFilter(arrayType);
                    if (itemFilter == null) continue;
                    filter = filter == null ? itemFilter : filter.OrElse(itemFilter);
                }

                if (filter != null)
                {
                    var (result, deleted) = await dataProvider.DeleteDynamicTableDataAsync(schema, filter);
                    if (result && deleted is ArrayTypeNode deletedArray)
                    {
                         count += deletedArray.Count;
                         origin.AddRange(deletedArray);
                    }
                }
            }
            return (count > 0, origin);
        }
        else
        {
            return (false, null);
        }
    }
    
}