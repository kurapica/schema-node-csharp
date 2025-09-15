using System.Text.Json.Nodes;

namespace SchemaNode.Config;

/// <summary>
/// The array config
/// </summary>
public interface IArrayConfig: ISchemaConfig
{
    /// <summary>
    /// The array data is increase update, only usable within application
    /// </summary>
    public bool? IncrUpdate { get; set; }

    /// <summary>
    /// The page count
    /// </summary>
    public int? Count { get; set; }
    
    /// <summary>
    /// The query offset
    /// </summary>
    public int? Offset { get; set; }

    /// <summary>
    /// The data total count
    /// </summary>
    public int? Total { get; set; }

    /// <summary>
    /// Use descend order
    /// </summary>
    public bool? Descend { get; set; }
}