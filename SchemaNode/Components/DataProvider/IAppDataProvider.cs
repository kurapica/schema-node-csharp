using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using System.Text.Json.Nodes;

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
    Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(DynamicTableSchema schema, string target, 
        JsonNode? filter = null, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, 
        bool forUpdate = false, bool onlyCount = false);

    /// <summary>
    /// Query dynamic table data with the filter and paging
    /// </summary>
    Task<(AnySchemaNode? result, int total)> QueryDynamicTableAsync(SchemaContext context, DynamicTableSchema schema, string target,
        AppSchemaDataResult type, AppSchemaDataFilter? filter, int skip = 0, int take = 0, bool desc = false, AppSchemaDataOrder[]? orderBy = null, string? dataField = null);

    /// <summary>
    /// Save the dynamic table data
    /// </summary>
    Task<(bool result, AnySchemaNode? update, AnySchemaNode? origin)> SaveDynamicTableDataAsync(DynamicTableSchema schema, string target, AnySchemaNode? data = null, bool canAdd = true, bool onlyAdd = false, string[]? overrides = null);
    
    /// <summary>
    /// Delete the dynamic table data with the filter
    /// </summary>
    Task<(bool result, AnySchemaNode? origin)> DeleteDynamicTableDataAsync(DynamicTableSchema schema, string target, JsonNode? filter = null);
    
    /// <summary>
    /// Drop the dynamic table
    /// </summary>
    Task DropDynamicTableAsync(string dynamicTableName);
    
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