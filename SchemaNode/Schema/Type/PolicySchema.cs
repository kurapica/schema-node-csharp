using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Runtime;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The permission policy schema
/// </summary>
[SchemaApp]
public class PolicySchema
{
    /// <summary>
    /// The policy name
    /// </summary>
    [Index]
    [JsonIgnore]
    [StringLength(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string? Name { get; set; }
    
    /// <summary>
    /// The policy items
    /// </summary>
    public PolicyItem[] Items { get; set; } = [];
}

/// <summary>
/// The policy item schema
/// </summary>
public class PolicyItem
{
    /// <summary>
    /// The policy scope
    /// </summary>
    public required PolicyScope Scope { get; set; }
    
    /// <summary>
    /// The policy evaluator
    /// </summary>
    public required string Evaluator { get; set; }

    /// <summary>
    /// The policy combine method
    /// </summary>
    public required PolicyCombine Combine { get; set; }
    
    /// <summary>
    /// The function type of the evaluator
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public FunctionType? Function { get; set; }
    
    /// <summary>
    /// The status
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public SchemaNodeStatus? Status { get; set; }
}