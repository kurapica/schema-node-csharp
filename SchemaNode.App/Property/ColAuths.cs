namespace SchemaNode.Property.Common;

public class ColAuths
{
    
}


/// <summary>
/// The column policy item
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA_DEF_POLICY}.col")]
public sealed class ColPolicy
{
    /// <summary>
    /// The struct field name
    /// </summary>
    public required string Name { get; set; } = string.Empty;

    /// <summary>
    /// The column access evaluators
    /// </summary>
    public string[] Evaluators { get; set; } = [];

    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public FunctionType[] Functions { get; set; } = [];
}
