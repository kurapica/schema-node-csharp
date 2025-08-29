namespace SchemaNode.Config;

/// <summary>
/// The array config
/// </summary>
public interface IArrayConfig: ISchemaConfig
{
    /// <summary>
    /// The array data is increase update, only usable within application
    /// </summary>
    public bool? IsIncrUpdate { get; set; }

    /// <summary>
    /// The page count
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// The data total count
    /// </summary>
    public int? Total { get; set; }

    /// <summary>
    /// Use descend order
    /// </summary>
    public bool? Descend { get; set; }
}