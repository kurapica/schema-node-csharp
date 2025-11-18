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
    SchemaRead = 2,
    
    /// <summary>
    /// Update Schema
    /// </summary>
    SchemaUpdate = 3,
    
    /// <summary>
    /// Delete Schema
    /// </summary>
    SchemaDelete = 4,
    
    /// <summary>
    /// Create App Data
    /// </summary>
    DataCreate = 5,
    
    /// <summary>
    /// Read App Data
    /// </summary>
    DataRead = 6,
    
    /// <summary>
    /// Update App Data
    /// </summary>
    DataUpdate = 7,
    
    /// <summary>
    /// Delete App Data
    /// </summary>
    DataDelete = 8,
    
    /// <summary>
    /// Row access filter
    /// </summary>
    RowAccess = 9,
    
    /// <summary>
    /// Column access filter
    /// </summary>
    ColumnAccess = 10,
}