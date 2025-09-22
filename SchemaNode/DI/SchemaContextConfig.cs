namespace SchemaNode;

/// <summary>
/// The schema context global config
/// </summary>
public class SchemaContextConfig
{
    /// <summary>
    /// Preload all schemas
    /// </summary>
    public bool PreLoad { get; set; }
}

/// <summary>
/// The schema api config
/// </summary>
public class SchemaApiConfig
{
    /// <summary>
    /// Enable the schema edit
    /// </summary>
    public bool EnableSchemaEdit { get; set; } = false;
    
    /// <summary>
    /// Enable the api schema edit
    /// </summary>
    public bool EnableAppSchemaEdit { get; set; } = false;
}