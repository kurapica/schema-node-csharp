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
    public AppFieldType Field { get; set; } = null!;
    
    /// <summary>
    /// The join data field
    /// </summary>
    Dictionary<string, string> Matches { get; set; } = null!;
}