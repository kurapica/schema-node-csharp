namespace SchemaNode.Config;

/// <summary>
/// The config of the schema node.
/// </summary>
public interface ISchemaConfig
{
    /// <summary>
    /// The type name of the node.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// The label of the node.
    /// </summary>
    public string? Display { get; set; }

    /// <summary>
    /// The description of the node.
    /// </summary>
    public string? Desc { get; set; }
    
    /// <summary>
    /// The error message if validation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The node data is required.
    /// </summary>
    public bool? Require { get; set; }

    /// <summary>
    /// The node data is immutable, unchangable if inited.
    /// </summary>
    public bool? Immutable { get; set; }

    /// <summary>
    /// The node data is readonly.
    /// </summary>
    public bool? Readonly { get; set; }

    /// <summary>
    /// The node should be invisible.
    /// </summary>
    public bool? Invisible { get; set; }

    /// <summary>
    /// The node should be display only, won't be submitted.
    /// </summary>
    public bool? DisplayOnly { get; set; }

    /// <summary>
    /// The unit of the node data like 'm/s', '%', '°C'.
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// The default value of the node.
    /// </summary>
    public string? Default { get; set; }
}