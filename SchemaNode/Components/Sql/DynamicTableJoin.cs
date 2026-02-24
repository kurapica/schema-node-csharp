using SchemaNode.Runtime;

namespace SchemaNode.Components;

/// <summary>
/// The dynamic table join info
/// </summary>
public class DynamicTableJoin
{
    /// <summary>
    /// The join field
    /// </summary>
    public string Field { get; set; } = null!;

    /// <summary>
    /// The join data field
    /// </summary>
    public Dictionary<string, AppSchemaDataFilter> Matches { get; set; } = null;
}