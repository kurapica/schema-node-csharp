namespace SchemaNode.Enum;

/// <summary>
/// The policy scope
/// </summary>
public enum PolicyScope
{
    /// <summary>
    /// Create Schema
    /// </summary>
    SchemaCreate = 1,
    
    /// <summary>
    /// Read Schema
    /// </summary>
    SchemaRead,
    
    /// <summary>
    /// Update Schema
    /// </summary>
    SchemaUpdate,
    
    /// <summary>
    /// Delete Schema
    /// </summary>
    SchemaDelete,
    
    /// <summary>
    /// Create App Data
    /// </summary>
    DataCreate,
    
    /// <summary>
    /// Read App Data
    /// </summary>
    DataRead,
    
    /// <summary>
    /// Update App Data
    /// </summary>
    DataUpdate,
    
    /// <summary>
    /// Delete App Data
    /// </summary>
    DataDelete,

    /// <summary>
    /// Execute Function
    /// </summary>
    FuncExecute,
    
    /// <summary>
    /// Row access filter
    /// </summary>
    RowAccess,
    
    /// <summary>
    /// Column access filter
    /// </summary>
    ColumnAccess,
}